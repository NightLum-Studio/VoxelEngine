using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperVoxel
{
    public class Chunk : IDisposable
    {
        public int3 coord;
        public ChunkState state;

        public NativeArray<byte> voxels; // length = VoxelsPerChunk

        public Mesh mesh;
        public MeshCollider collider; // optional later

        public AsyncGPUReadbackRequest countersReq;
        public AsyncGPUReadbackRequest vReq;
        public AsyncGPUReadbackRequest iReq;

        public JobHandle genHandle;
        public bool hasJob;
        public bool pendingRemoval;

    // Diagnostics and retry state for GPU meshing/readbacks
    public float gpuStartTime;       // when counters readback was requested
    public float uploadStartTime;    // when mesh data readback was requested
    public int gpuRetryCount;        // retry attempts for counters/mesh data
    public int pendingVertexCount;   // last requested vertex count for readback
    public int pendingIndexCount;    // last requested index count for readback
    public int gpuStage;             // 0..6 greedy passes completed
    public bool meshDataRequested;   // whether vertex/index readback has been requested

        public Chunk(int3 coord)
        {
            this.coord = coord;
            state = ChunkState.Requested;
            voxels = new NativeArray<byte>(ChunkDefs.VoxelsPerChunk, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
        }

        public void Dispose()
        {
            if (voxels.IsCreated) voxels.Dispose();
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }
    }
}


