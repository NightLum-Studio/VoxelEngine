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
        
    [Header("Runtime")]
    [Tooltip("Continue world streaming/meshing when the window loses focus (alt-tab).")]
    public bool runInBackground = true;
        
        [Header("Block Database")]
        public BlockDatabaseIntegration blockDatabaseIntegration;

        private readonly Dictionary<long, Chunk> _chunks = new();
        private GpuMesher _gpuMesher;
        private Chunk _activeGpuChunk;

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
            _gpuMesher = new GpuMesher(voxelMesher);
            // Build initial spiral cache
            EnsureSpiralCache(viewDistanceChunks);
            
            // Initialize block database integration
            if (blockDatabaseIntegration != null)
            {
                blockDatabaseIntegration.gpuMesher = _gpuMesher;
                blockDatabaseIntegration.InitializeBlockData();
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
            _gpuMesher?.Dispose();
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
                    // Also remove from ready queue if present
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

    private void ProcessMeshing()
        {
            // If there's an active chunk using the shared GPU mesher, drive it to completion first
            if (_activeGpuChunk != null)
            {
                var chunk = _activeGpuChunk;
                if (chunk.state == ChunkState.MeshingGPU)
                {
                    if (chunk.countersReq.done && !chunk.countersReq.hasError)
                    {
                        var counts = chunk.countersReq.GetData<uint>();
                        int vCount = (int)counts[0];
                        int iCount = (int)counts[1];
                        // Capacity clamp
                        vCount = Mathf.Min(vCount, GpuMesher.MaxVerticesPerChunk);
                        iCount = Mathf.Min(iCount, GpuMesher.MaxIndicesPerChunk);
                        // Clamp to whole quads to avoid partial writes
                        int quadCount = Mathf.Min(vCount / 4, iCount / 6);
                        vCount = quadCount * 4;
                        iCount = quadCount * 6;
                        // Debug counts once
                        // Debug.Log($"Chunk {chunk.coord} counts v={vCount} i={iCount}");
                        if (vCount == 0 || iCount == 0)
                        {
                            ApplyMesh(chunk, System.Array.Empty<GpuMesher.PackedVertex>(), System.Array.Empty<uint>());
                            chunk.state = ChunkState.Active;
                            _activeGpuChunk = null;
                        }
                        else
                        {
                            _gpuMesher.RequestMeshData(vCount, iCount, out chunk.vReq, out chunk.iReq);
                            chunk.state = ChunkState.Uploading;
                        }
                    }
                    return;
                }
                if (chunk.state == ChunkState.Uploading)
                {
                    if (chunk.vReq.done && chunk.iReq.done && !chunk.vReq.hasError && !chunk.iReq.hasError)
                    {
                        if (chunk.mesh == null)
                        {
                            // Mesh was destroyed (chunk being removed), skip applying
                            _activeGpuChunk = null;
                        }
                        else
                        {
                            // Use native arrays to avoid GC allocations
                            var vFloats = chunk.vReq.GetData<float>();
                            var iData = chunk.iReq.GetData<uint>();
                            ApplyMeshNative(chunk, vFloats, iData);
                            chunk.state = ChunkState.Active;
                            _activeGpuChunk = null;
                        }
                        if (chunk.pendingRemoval)
                        {
                            long key = Key(chunk.coord);
                            if (_chunks.ContainsKey(key)) _chunks.Remove(key);
                            chunk.Dispose();
                        }
                    }
                    return;
                }
                // If state is anything else, release
                _activeGpuChunk = null;
            }

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

            // No active job; pick the best next chunk (nearest in spiral order) to mesh
            if (_readyQueue.Count > 0)
            {
                int bestIdx = -1;
                int bestPriority = int.MaxValue;
                for (int i = 0; i < _readyQueue.Count; i++)
                {
                    var ch = _readyQueue[i];
                    int dx = ch.coord.x - _currentCenter.x;
                    int dz = ch.coord.z - _currentCenter.z;
                    int pri = GetSpiralPriority(dx, dz);
                    if (pri < bestPriority)
                    {
                        bestPriority = pri;
                        bestIdx = i;
                    }
                }
                if (bestIdx >= 0)
                {
                    var chunk = _readyQueue[bestIdx];
                    _readyQueue.RemoveAt(bestIdx);

                    _gpuMesher.UploadVoxels(chunk.voxels);
                    // Provide sun direction hint; if directional light exists use it, else default
                    Vector3 sunDir = Vector3.down;
                    var sun = RenderSettings.sun;
                    if (sun != null) sunDir = -sun.transform.forward;
                    _gpuMesher.BuildMesh(chunk.coord, new Unity.Mathematics.float3(sunDir.x, sunDir.y, sunDir.z), 0, 0.75f);
                    _gpuMesher.RequestCounters(out chunk.countersReq);
                    chunk.state = ChunkState.MeshingGPU;
                    _activeGpuChunk = chunk;
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
    }
}


