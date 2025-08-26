using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperVoxel
{
    public class WorldStreamer : MonoBehaviour
    {
        [Header("Streaming")]
        [SerializeField]
        public int viewDistanceChunks = 8;
          [SerializeField]
        public int maxGeneratePerFrame = 10;
          [SerializeField]
        public int seed = 1337;

        [Header("Rendering")]
        public ComputeShader voxelMesher;
        public Material voxelMaterial;
    // Atlas tiles per row is now driven by HyperVoxel.BlockDatabase.AtlasTilesPerRow
    [Min(1)] public int gpuWorkerCount = 2;
    [Tooltip("Max greedy passes we dispatch per frame across all chunks (6 passes to complete a chunk). Lower to reduce Gfx.WaitForPresent spikes.")]
    public int maxGreedyPassesPerFrame = 6;
    [Tooltip("Max simultaneous GPU readbacks (counters + mesh data). Limits in-flight GPU memory/bandwidth.")]
    public int maxConcurrentReadbacks = 4;
    [Tooltip("Max chunks to stage for meshing per frame (upload voxels + first pass).")]
    public int maxStageBeginsPerFrame = 2;
        
    [Header("Runtime")]
    [Tooltip("Continue world streaming/meshing when the window loses focus (alt-tab).")]
    public bool runInBackground = true;
        
        [Header("Block Database")]
        public BlockDatabaseIntegration blockDatabaseIntegration;

        private readonly Dictionary<long, Chunk> _chunks = new();
        private class GpuWorker
        {
            public GpuMesher mesher;
            public Chunk chunk;                // currently staged chunk for greedy passes
            public bool reservedForReadback;   // true after counters requested until mesh data request issued
        }
        private List<GpuWorker> _workers = new List<GpuWorker>();
        // Staged chunk currently receiving greedy passes on any worker
        private Chunk _stagedGreedyChunk; // kept for backward diagnostic compatibility (first worker)
    // Upload/readback pipeline list
    private readonly List<Chunk> _inFlightReadbacks = new List<Chunk>(32);

    // Deterministic load/mesh ordering
    private List<int2> _spiralOffsets = new List<int2>();
    private Dictionary<long, int> _offsetToIndex = new Dictionary<long, int>();
    private int _spiralRadiusCached = -1;
    private readonly List<Chunk> _readyQueue = new List<Chunk>();
    private int3 _currentCenter;
    // Reusable temp containers to reduce GC
    private readonly List<long> _toRemoveKeys = new List<long>(256);
    // removed unused UV reuse list

        [Header("Driver")]
        public Transform player; // Preferred driver for streaming center
        private Transform _cam;

        private void Awake()
        {
            // Keep running even when window is unfocused
            if (runInBackground) Application.runInBackground = true;
            _cam = Camera.main != null ? Camera.main.transform : transform;

            if (voxelMesher == null)
            {
                voxelMesher = Resources.Load<ComputeShader>("VoxelMesher");
            }
            if (voxelMaterial == null)
            {
                var mat = Resources.Load<Material>("VoxelAtlasMat");
                if (mat != null) voxelMaterial = mat;
                else
                {
                    var sh = Shader.Find("HyperVoxel/UnlitVoxelAtlas");
                    if (sh != null) voxelMaterial = new Material(sh);
                }
            }

            // Atlas tiles per row is taken from HyperVoxel.BlockDatabase.AtlasTilesPerRow

            if (voxelMesher == null)
            {
                Debug.LogError("VoxelMesher compute shader not found. Place it at Resources/VoxelMesher.compute or assign in inspector.");
                enabled = false;
                return;
            }
            // Create GPU workers pool
            _workers.Clear();
            int workerCount = Mathf.Max(1, gpuWorkerCount);
            for (int i = 0; i < workerCount; i++)
            {
                _workers.Add(new GpuWorker { mesher = new GpuMesher(voxelMesher), chunk = null, reservedForReadback = false });
            }
            // Build initial spiral cache
            EnsureSpiralCache(viewDistanceChunks);
            
            // Initialize block database integration
            if (blockDatabaseIntegration != null)
            {
                if (_workers.Count > 0)
                {
                    blockDatabaseIntegration.gpuMesher = _workers[0].mesher;
                    blockDatabaseIntegration.InitializeBlockData();
                    // Propagate face texture data to other workers
                    if (_workers.Count > 1 && blockDatabaseIntegration.blockDatabase != null)
                    {
                        var faceData = blockDatabaseIntegration.blockDatabase.GetFaceTextureIndices();
                        for (int i = 1; i < _workers.Count; i++)
                        {
                            _workers[i].mesher.SetFaceTextureData(faceData);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("BlockDatabaseIntegration not assigned. Face textures will not be available for meshing.");
            }
        }

        private void Start()
        {
            // Prewarm an initial streaming pass so chunks spawn even if window isn't focused on start
            Transform centerT = player;
            if (centerT == null)
            {
                if (_cam == null)
                {
                    var cam = Camera.main;
                    if (cam != null) _cam = cam.transform;
                }
                centerT = _cam != null ? _cam : transform;
            }
            int3 camChunk = WorldToChunkCoord(centerT.position);
            _currentCenter = camChunk;
            EnsureSpiralCache(viewDistanceChunks);
            StreamAround(camChunk);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Reassert background running when focus changes
            if (runInBackground && !hasFocus)
                Application.runInBackground = true;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Reassert background running when application is paused in background
            if (runInBackground && pauseStatus)
                Application.runInBackground = true;
        }

        private void OnDestroy()
        {
            foreach (var kv in _chunks)
            {
                kv.Value.Dispose();
            }
            for (int i = 0; i < _workers.Count; i++)
            {
                _workers[i].mesher?.Dispose();
            }
        }

        private const int KeyBits = 21; // supports coords in [-2^20, 2^20-1]
        private const long KeyMask = (1L << KeyBits) - 1L;
        private const int KeyBias = 1 << 20;
        private static long Key(int3 c)
        {
            long x = ((long)(c.x + KeyBias)) & KeyMask;
            long y = ((long)(c.y + KeyBias)) & KeyMask;
            long z = ((long)(c.z + KeyBias)) & KeyMask;
            return (x << (KeyBits * 2)) | (y << KeyBits) | z;
        }

        private static long OffsetKey(int dx, int dz)
        {
            // Pack dx,dz (y is always 0 for offsets) into a single long using the same biasing idea
            long x = ((long)(dx + KeyBias)) & KeyMask;
            long z = ((long)(dz + KeyBias)) & KeyMask;
            return (x << KeyBits) | z;
        }

        private void EnsureSpiralCache(int radius)
        {
            if (radius == _spiralRadiusCached && _spiralOffsets.Count > 0) return;

            _spiralOffsets.Clear();
            _offsetToIndex.Clear();

            // Center
            _spiralOffsets.Add(new int2(0, 0));
            _offsetToIndex[OffsetKey(0, 0)] = 0;

            for (int r = 1; r <= radius; r++)
            {
                // Start exactly at (r, 0)
                void AddOffset(int x, int z)
                {
                    int idx = _spiralOffsets.Count;
                    _spiralOffsets.Add(new int2(x, z));
                    _offsetToIndex[OffsetKey(x, z)] = idx;
                }

                AddOffset(r, 0);
                // Up along right edge (exclude z=0 which we already added)
                for (int z = 1; z <= r; z++) AddOffset(r, z);
                // Left along top edge
                for (int x = r - 1; x >= -r; x--) AddOffset(x, r);
                // Down along left edge
                for (int z = r - 1; z >= -r; z--) AddOffset(-r, z);
                // Right along bottom edge
                for (int x = -r + 1; x <= r; x++) AddOffset(x, -r);
                // Up along right edge (avoid duplicating (r,0) and (r,>=0) we already covered)
                for (int z = -r + 1; z <= -1; z++) AddOffset(r, z);
            }

            _spiralRadiusCached = radius;
        }

        private int3 WorldToChunkCoord(float3 pos)
        {
            int cx = Mathf.FloorToInt(pos.x / (float)ChunkDefs.ChunkSizeXZ);
            int cz = Mathf.FloorToInt(pos.z / (float)ChunkDefs.ChunkSizeXZ);
            return new int3(cx, 0, cz);
        }

        private void Update()
        {
            // Determine streaming center: prefer player, fallback to camera, then self
            Transform centerT = player;
            if (centerT == null)
            {
                if (_cam == null)
                {
                    var cam = Camera.main;
                    if (cam != null) _cam = cam.transform;
                }
                centerT = _cam != null ? _cam : transform;
            }
            int3 camChunk = WorldToChunkCoord(centerT.position);
            _currentCenter = camChunk;
            EnsureSpiralCache(viewDistanceChunks);
            StreamAround(camChunk);
            ProcessMeshing();
            RenderChunks();
        }

        private void StreamAround(int3 center)
        {
            int loadedThisFrame = 0;
            // Generate using a precomputed deterministic spiral order around center
            for (int i = 0; i < _spiralOffsets.Count && loadedThisFrame < maxGeneratePerFrame; i++)
            {
                var off = _spiralOffsets[i];
                int3 c = new int3(center.x + off.x, 0, center.z + off.y);
                long k = Key(c);
                if (_chunks.ContainsKey(k)) continue;
                var chunk = new Chunk(c);
                _chunks.Add(k, chunk);
                ScheduleGeneration(chunk);
                loadedThisFrame++;
            }
            // Unload far chunks
        _toRemoveKeys.Clear();
        foreach (var kv in _chunks)
            {
                var c = kv.Value.coord;
                int dx = math.abs(c.x - center.x);
                int dz = math.abs(c.z - center.z);
                if (dx > viewDistanceChunks + 1 || dz > viewDistanceChunks + 1)
                {
            _toRemoveKeys.Add(kv.Key);
                }
            }
        foreach (var k in _toRemoveKeys)
            {
                var c = _chunks[k];
                // If chunk is in flight on GPU, mark for removal after mesh upload completes
                if (c.state == ChunkState.MeshingGPU || c.state == ChunkState.Uploading)
                {
                    c.pendingRemoval = true;
                }
                else
                {
                    // If generating on CPU, ensure job completes before disposing
                    if (c.hasJob)
                    {
                        try { c.genHandle.Complete(); }
                        catch { /* swallow to ensure cleanup proceeds */ }
                        c.hasJob = false;
                    }
                    // Remove from ready queue if present (any state)
                    for (int i = _readyQueue.Count - 1; i >= 0; i--)
                    {
                        if (_readyQueue[i] == c) _readyQueue.RemoveAt(i);
                    }
                    c.Dispose();
                    _chunks.Remove(k);
                }
            }
        }

        private void ScheduleGeneration(Chunk chunk)
        {
            var job = new GenerateChunkVoxelsJob
            {
                voxels = chunk.voxels,
                chunkCoord = chunk.coord,
                seed = seed
            };
            chunk.state = ChunkState.Generating;
            chunk.genHandle = job.Schedule();
            chunk.hasJob = true;
        }

    private const float GpuReadbackTimeout = 2.5f; // seconds before considering a GPU readback stuck
    private const int MaxGpuRetries = 2;            // how many times we retry a stuck/failed readback

    private void ProcessMeshing()
        {
            int greedyBudget = Mathf.Max(1, maxGreedyPassesPerFrame);

            // Update readiness and fill queue
            foreach (var kv in _chunks)
            {
                var chunk = kv.Value;
                if (chunk.state == ChunkState.Generating && chunk.hasJob && chunk.genHandle.IsCompleted)
                {
                    chunk.genHandle.Complete();
                    chunk.hasJob = false;
                    chunk.state = ChunkState.ReadyForMesh;
                    _readyQueue.Add(chunk);
                }
            }

            // Optional: detect and log if nothing is progressing for a while (Editor only)
#if UNITY_EDITOR
            if (_stagedGreedyChunk == null && _readyQueue.Count == 0)
            {
                // Check if we have any generating chunks that might be stuck for a long time
                // Unity jobs rarely hang, but this helps visibility during debugging
                // We don't cancel jobs here; we only warn.
                int generating = 0;
                foreach (var kv in _chunks)
                {
                    if (kv.Value.state == ChunkState.Generating) generating++;
                }
                if (generating > 0)
                {
                    // This log is throttled implicitly by Editor console collapsing
                    // Debug.Log("WorldStreamer: Waiting for CPU generation jobs... (" + generating + ")");
                }
            }
#endif

            // No active job; pick the best next chunk (nearest in spiral order) to mesh
            // 1) Advance staged chunks on workers with available greedy budget (round-robin)
            for (int wi = 0; wi < _workers.Count && greedyBudget > 0; wi++)
            {
                var w = _workers[wi];
                if (w.chunk == null || w.reservedForReadback) continue;
                while (greedyBudget > 0 && w.chunk.gpuStage < GpuMesher.GreedyStageCount)
                {
                    w.mesher.DispatchGreedyStage(w.chunk.gpuStage);
                    w.chunk.gpuStage++;
                    greedyBudget--;
                }
                if (w.chunk.gpuStage >= GpuMesher.GreedyStageCount)
                {
                    // Request counters on the same worker and reserve it until mesh data request
                    w.mesher.RequestCounters(out w.chunk.countersReq);
                    w.chunk.gpuStartTime = Time.realtimeSinceStartup;
                    w.reservedForReadback = true;
                    _inFlightReadbacks.Add(w.chunk);
                    if (_stagedGreedyChunk == w.chunk) _stagedGreedyChunk = null;
                }
            }

            // 2) Stage new chunks if we have budget and nothing staged
            int stagedBegins = 0;
            while (greedyBudget > 0 && stagedBegins < maxStageBeginsPerFrame && _readyQueue.Count > 0)
            {
                // Find a free worker (not reserved and with no chunk)
                int freeWi = -1;
                for (int wi = 0; wi < _workers.Count; wi++)
                {
                    if (_workers[wi].chunk == null && !_workers[wi].reservedForReadback) { freeWi = wi; break; }
                }
                if (freeWi < 0) break;

                // Choose best next chunk by spiral priority
                int bestIdx = -1; int bestPriority = int.MaxValue;
                for (int i = 0; i < _readyQueue.Count; i++)
                {
                    var ch = _readyQueue[i];
                    int dx = ch.coord.x - _currentCenter.x;
                    int dz = ch.coord.z - _currentCenter.z;
                    int pri = GetSpiralPriority(dx, dz);
                    if (pri < bestPriority) { bestPriority = pri; bestIdx = i; }
                }
                if (bestIdx < 0) break;
                var chunk = _readyQueue[bestIdx];
                _readyQueue.RemoveAt(bestIdx);

                // Upload voxels and prepare for staged passes
                var worker = _workers[freeWi];
                worker.mesher.UploadVoxels(chunk.voxels);
                Vector3 sunDir = Vector3.down; var sun = RenderSettings.sun; if (sun != null) sunDir = -sun.transform.forward;
                worker.mesher.PrepareForChunk(chunk.coord, new Unity.Mathematics.float3(sunDir.x, sunDir.y, sunDir.z), 0, 0.75f);
                chunk.gpuStage = 0; chunk.gpuRetryCount = 0; chunk.meshDataRequested = false; chunk.uploadStartTime = 0f;
                chunk.state = ChunkState.MeshingGPU;
                worker.chunk = chunk; worker.reservedForReadback = false; _stagedGreedyChunk ??= chunk; // remember first staged for diagnostics
                stagedBegins++;

                // Immediately dispatch at least one stage this frame respecting budget
                if (greedyBudget > 0)
                {
                    worker.mesher.DispatchGreedyStage(worker.chunk.gpuStage);
                    worker.chunk.gpuStage++;
                    greedyBudget--;
                }

                // If we still have budget, try to advance more in the same loop iteration
                while (greedyBudget > 0 && worker.chunk != null && worker.chunk.gpuStage < GpuMesher.GreedyStageCount)
                {
                    worker.mesher.DispatchGreedyStage(worker.chunk.gpuStage);
                    worker.chunk.gpuStage++;
                    greedyBudget--;
                }
                if (worker.chunk != null && worker.chunk.gpuStage >= GpuMesher.GreedyStageCount)
                {
                    worker.mesher.RequestCounters(out worker.chunk.countersReq);
                    worker.chunk.gpuStartTime = Time.realtimeSinceStartup;
                    worker.reservedForReadback = true;
                    _inFlightReadbacks.Add(worker.chunk);
                    if (_stagedGreedyChunk == worker.chunk) _stagedGreedyChunk = null;
                }
            }

            // 3) Service in-flight readbacks (counters -> mesh data -> apply mesh)
            for (int i = _inFlightReadbacks.Count - 1; i >= 0; i--)
            {
                var ch = _inFlightReadbacks[i];
                if (!ch.meshDataRequested)
                {
                    // Handle counters readiness/timeouts
                    bool timeout = !ch.countersReq.done && (Time.realtimeSinceStartup - ch.gpuStartTime) > GpuReadbackTimeout;
                    if (ch.countersReq.hasError || timeout)
                    {
                        if (ch.gpuRetryCount < MaxGpuRetries)
                        {
                            ch.gpuRetryCount++;
                            // Use the worker reserved for this chunk to retry counters
                            var w = FindWorkerForChunk(ch);
                            if (w != null) w.mesher.RequestCounters(out ch.countersReq);
                            ch.gpuStartTime = Time.realtimeSinceStartup;
                            continue;
                        }
                        // Give up: empty mesh and finish
                        ApplyMesh(ch, System.Array.Empty<GpuMesher.PackedVertex>(), System.Array.Empty<uint>());
                        ch.state = ChunkState.Active;
                        _inFlightReadbacks.RemoveAt(i);
                        ReleaseWorkerForChunk(ch);
                        if (ch.pendingRemoval) { long key = Key(ch.coord); if (_chunks.ContainsKey(key)) _chunks.Remove(key); ch.Dispose(); }
                        continue;
                    }
                    if (ch.countersReq.done && !ch.countersReq.hasError)
                    {
                        var counts = ch.countersReq.GetData<uint>();
                        int vCount = (int)counts[0];
                        int iCount = (int)counts[1];
                        vCount = Mathf.Min(vCount, GpuMesher.MaxVerticesPerChunk);
                        iCount = Mathf.Min(iCount, GpuMesher.MaxIndicesPerChunk);
                        int quadCount = Mathf.Min(vCount / 4, iCount / 6);
                        vCount = quadCount * 4;
                        iCount = quadCount * 6;
                        if (vCount == 0 || iCount == 0)
                        {
                            ApplyMesh(ch, System.Array.Empty<GpuMesher.PackedVertex>(), System.Array.Empty<uint>());
                            ch.state = ChunkState.Active;
                            _inFlightReadbacks.RemoveAt(i);
                            ReleaseWorkerForChunk(ch);
                            if (ch.pendingRemoval) { long key = Key(ch.coord); if (_chunks.ContainsKey(key)) _chunks.Remove(key); ch.Dispose(); }
                            continue;
                        }
                        // Throttle number of simultaneous mesh data readbacks (bounded by worker count)
                        int activeMeshReads = 0; for (int j = 0; j < _inFlightReadbacks.Count; j++) if (_inFlightReadbacks[j].meshDataRequested && _inFlightReadbacks[j].state == ChunkState.Uploading) activeMeshReads++;
                        int maxReads = Mathf.Min(maxConcurrentReadbacks, _workers.Count);
                        if (activeMeshReads >= maxReads)
                        {
                            // Defer to next frame to avoid overloading
                            continue;
                        }
                        // Use the reserved worker to request mesh data (snapshot of its buffers)
                        var w = FindWorkerForChunk(ch);
                        if (w != null)
                        {
                            w.mesher.RequestMeshData(vCount, iCount, out ch.vReq, out ch.iReq);
                            // Keep worker reserved until mesh data readbacks complete
                        }
                        ch.pendingVertexCount = vCount; ch.pendingIndexCount = iCount;
                        ch.uploadStartTime = Time.realtimeSinceStartup;
                        ch.meshDataRequested = true;
                        ch.state = ChunkState.Uploading;
                    }
                }
                else
                {
                    bool timedOut = (!ch.vReq.done || !ch.iReq.done) && (Time.realtimeSinceStartup - ch.uploadStartTime) > GpuReadbackTimeout;
                    bool hasError = ch.vReq.hasError || ch.iReq.hasError;
                    if (hasError || timedOut)
                    {
                        if (ch.gpuRetryCount < MaxGpuRetries)
                        {
                            ch.gpuRetryCount++;
                            // Retry mesh data readback on the same worker that holds the buffers
                            var w = FindWorkerForChunk(ch);
                            if (w != null)
                            {
                                w.mesher.RequestMeshData(ch.pendingVertexCount, ch.pendingIndexCount, out ch.vReq, out ch.iReq);
                            }
                            ch.uploadStartTime = Time.realtimeSinceStartup;
                            continue;
                        }
                        ApplyMesh(ch, System.Array.Empty<GpuMesher.PackedVertex>(), System.Array.Empty<uint>());
                        ch.state = ChunkState.Active;
                        _inFlightReadbacks.RemoveAt(i);
                        ReleaseWorkerForChunk(ch);
                        if (ch.pendingRemoval) { long key = Key(ch.coord); if (_chunks.ContainsKey(key)) _chunks.Remove(key); ch.Dispose(); }
                        continue;
                    }
                    if (ch.vReq.done && ch.iReq.done && !ch.vReq.hasError && !ch.iReq.hasError)
                    {
                        if (ch.mesh != null)
                        {
                            var vFloats = ch.vReq.GetData<float>();
                            var iData = ch.iReq.GetData<uint>();
                            ApplyMeshNative(ch, vFloats, iData);
                            ch.state = ChunkState.Active;
                        }
                        _inFlightReadbacks.RemoveAt(i);
                        ReleaseWorkerForChunk(ch);
                        if (ch.pendingRemoval) { long key = Key(ch.coord); if (_chunks.ContainsKey(key)) _chunks.Remove(key); ch.Dispose(); }
                    }
                }
            }
        }

        private int GetSpiralPriority(int dx, int dz)
        {
            // Return index into spiral if available; otherwise fallback to a large priority with distance weighting
            if (math.abs(dx) <= _spiralRadiusCached && math.abs(dz) <= _spiralRadiusCached)
            {
                long ok = OffsetKey(dx, dz);
                if (_offsetToIndex.TryGetValue(ok, out int idx)) return idx;
            }
            // Outside cache: prioritize by Chebyshev distance with a large base to ensure farther chunks come after cached radius
            int r = math.max(math.abs(dx), math.abs(dz));
            return (_spiralRadiusCached + r) * 100000; // lump far distances
        }

        private void ApplyMesh(Chunk chunk, GpuMesher.PackedVertex[] verts, uint[] inds)
        {
            if (verts.Length == 0 || inds.Length == 0)
            {
                chunk.mesh.Clear();
                return;
            }
              var uverts = new Vector3[verts.Length];
            var unorms = new Vector3[verts.Length];
            var utangents = new Vector4[verts.Length];
            var uuvs = new Vector2[verts.Length];
            var uuv2s = new Vector2[verts.Length];
            var ucolors = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                uverts[i] = new Vector3(verts[i].position.x, verts[i].position.y, verts[i].position.z);
                unorms[i] = new Vector3(verts[i].normal.x, verts[i].normal.y, verts[i].normal.z);
                utangents[i] = new Vector4(verts[i].tangent.x, verts[i].tangent.y, verts[i].tangent.z, verts[i].tangent.w);
                uuvs[i] = new Vector2(verts[i].uv.x, verts[i].uv.y);
                uuv2s[i] = new Vector2(Mathf.Clamp01(verts[i].ao), 0f);
                ucolors[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(verts[i].shadow));
            }
            chunk.mesh.Clear();
            chunk.mesh.SetVertices(uverts);
            chunk.mesh.SetNormals(unorms);
            chunk.mesh.SetTangents(utangents);
            chunk.mesh.SetUVs(0, uuvs);
            chunk.mesh.SetUVs(1, uuv2s);
            chunk.mesh.colors = ucolors;
            // Convert to int[] as Mesh API expects int indices array
            var iarr = new int[inds.Length];
            for (int i = 0; i < inds.Length; i++) iarr[i] = (int)inds[i];
            chunk.mesh.SetIndices(iarr, MeshTopology.Triangles, 0);
        }

        private void ApplyMeshNative(Chunk chunk, NativeArray<float> vFloats, NativeArray<uint> inds)
        {
            if (!vFloats.IsCreated || !inds.IsCreated || vFloats.Length == 0 || inds.Length == 0)
            {
                chunk.mesh.Clear();
                return;
            }
            int strideFloats = 16; // pos3, norm3, uv2, ao1, shadow1, tan4, tileIndex2
            int vertCount = vFloats.Length / strideFloats;

            // Use MeshData API to avoid CopyChannels spikes
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var md = meshDataArray[0];

            var layout = new VertexAttributeDescriptor[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0), // 0..2
                new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3, 0), // 3..5
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2, 0), // 6..7
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32, 2, 0), // 8..9 (ao, shadow)
                new VertexAttributeDescriptor(VertexAttribute.Tangent,  VertexAttributeFormat.Float32, 4, 0), // 10..13
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32, 2, 0), // 14..15 (tile index)
            };

            md.SetVertexBufferParams(vertCount, layout);
            var dstV = md.GetVertexData<float>();
            dstV.CopyFrom(vFloats);

            int indexCount = inds.Length;
            md.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            var dstI = md.GetIndexData<uint>();
            dstI.CopyFrom(inds);

            md.subMeshCount = 1;
            var sub = new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles);
            md.SetSubMesh(0, sub, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            chunk.mesh.Clear();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, chunk.mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            // Conservative bounds to avoid culling spikes
            var size = new Vector3(ChunkDefs.ChunkSizeXZ, ChunkDefs.ChunkSizeY, ChunkDefs.ChunkSizeXZ);
            var center = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            chunk.mesh.bounds = new Bounds(center, size);

            // Ensure UV0 channel is correctly bound (workaround for drivers/materials ignoring interleaved UVs)
            EnsureUVCache(vertCount);
            for (int i = 0; i < vertCount; i++)
            {
                int b = i * strideFloats;
                _uvCache[i] = new Vector2(vFloats[b + 6], vFloats[b + 7]);
            }
            chunk.mesh.SetUVs(0, _uvCache, 0, vertCount);
        }

        // Cached arrays to minimize GC
        private Vector3[] _uverts = System.Array.Empty<Vector3>();
        private Vector3[] _unorms = System.Array.Empty<Vector3>();
        private Vector4[] _utangents = System.Array.Empty<Vector4>();
        private Vector2[] _uuvs = System.Array.Empty<Vector2>();
        private Vector2[] _uuv2s = System.Array.Empty<Vector2>();
        private Color[] _ucolors = System.Array.Empty<Color>();
        private int[] _iarr = System.Array.Empty<int>();
        private Vector2[] _uvCache = System.Array.Empty<Vector2>();

        private void EnsureVertexCaches(int vCount)
        {
            if (_uverts.Length < vCount)
            {
                int cap = Mathf.NextPowerOfTwo(Mathf.Max(64, vCount));
                _uverts = new Vector3[cap];
                _unorms = new Vector3[cap];
                _utangents = new Vector4[cap];
                _uuvs = new Vector2[cap];
                _uuv2s = new Vector2[cap];
                _ucolors = new Color[cap];
            }
        }

        private void EnsureIndexCache(int iCount)
        {
            if (_iarr.Length < iCount)
            {
                int cap = Mathf.NextPowerOfTwo(Mathf.Max(64, iCount));
                _iarr = new int[cap];
            }
        }

        private void EnsureUVCache(int vCount)
        {
            if (_uvCache.Length < vCount)
            {
                int cap = Mathf.NextPowerOfTwo(Mathf.Max(64, vCount));
                _uvCache = new Vector2[cap];
            }
        }

        private void RenderChunks()
        {
            if (voxelMaterial == null) return;
            // Update material params each frame in case edited at runtime
            voxelMaterial.SetFloat("_AtlasTilesPerRow", HyperVoxel.BlockDatabase.AtlasTilesPerRow);
            foreach (var kv in _chunks)
            {
                var chunk = kv.Value;
                if (chunk.state == ChunkState.Active)
                {
                    var origin = ChunkDefs.ChunkToVoxelOrigin(chunk.coord);
                    var matrix = Matrix4x4.TRS(new Vector3(origin.x, origin.y, origin.z), Quaternion.identity, Vector3.one);
                    Graphics.DrawMesh(chunk.mesh, matrix, voxelMaterial, 0);
                }
            }
        }

        // --- Diagnostics API ---
        public void GetChunkStateCounts(out int requested, out int generating, out int ready, out int meshing, out int uploading, out int active)
        {
            requested = generating = ready = meshing = uploading = active = 0;
            foreach (var kv in _chunks)
            {
                switch (kv.Value.state)
                {
                    case ChunkState.Requested: requested++; break;
                    case ChunkState.Generating: generating++; break;
                    case ChunkState.ReadyForMesh: ready++; break;
                    case ChunkState.MeshingGPU: meshing++; break;
                    case ChunkState.Uploading: uploading++; break;
                    case ChunkState.Active: active++; break;
                }
            }
        }

        public int GetReadyQueueLength() => _readyQueue.Count;
        public bool HasActiveGpuChunk => _stagedGreedyChunk != null || _inFlightReadbacks.Count > 0;

        // --- Worker helpers ---
        private GpuWorker FindWorkerForChunk(Chunk ch)
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].chunk == ch) return _workers[i];
            }
            return null;
        }

        private void ReleaseWorkerForChunk(Chunk ch)
        {
            for (int i = 0; i < _workers.Count; i++)
            {
                if (_workers[i].chunk == ch)
                {
                    _workers[i].chunk = null;
                    _workers[i].reservedForReadback = false;
                    if (_stagedGreedyChunk == ch) _stagedGreedyChunk = null;
                    return;
                }
            }
        }
        public int3 GetCurrentCenter() => _currentCenter;
    }
}


