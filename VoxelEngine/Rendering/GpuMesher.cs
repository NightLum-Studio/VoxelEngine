using System;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using VoxelEngine.Core;
using VoxelEngine.Data;

namespace VoxelEngine.Rendering
{
    public class GpuMesher : IDisposable
    {
        public ComputeShader shader;

        public ComputeBuffer voxelBuffer;        // uint per voxel (BlockId)
        public ComputeBuffer countersBuffer;     // [vertexCount, indexCount]
        public ComputeBuffer vertexBuffer;       // packed vertices
        public ComputeBuffer indexBuffer;        // packed indices
        public ComputeBuffer blockUvTriplets;    // per block uv tile indices
        public ComputeBuffer faceTextureBuffer;  // 6 face texture indices per block

    // Reusable CPU-side buffer to avoid managed allocations during voxel upload
    private Unity.Collections.NativeArray<uint> _voxelUintTemp;
    private static readonly uint[] s_CounterZeros = new uint[] { 0u, 0u };

        private int kernelBuildFaces;
        private int kGreedyPosY, kGreedyNegY, kGreedyPosX, kGreedyNegX, kGreedyPosZ, kGreedyNegZ;
    private int[] _kernels;
    private readonly int[] _chunkCoordInts = new int[3];
    private readonly float[] _sunDirFloats = new float[3];

        private const int MaxFacesPerChunk = ChunkDefs.VoxelsPerChunk * ChunkDefs.MaxFacesPerVoxel;
        public const int MaxVerticesPerChunk = MaxFacesPerChunk * ChunkDefs.MaxVerticesPerFace;
        public const int MaxIndicesPerChunk = MaxFacesPerChunk * ChunkDefs.MaxIndicesPerFace;

        public struct PackedVertex
        {
            public float3 position;
            public float3 normal;
            public float2 uv;
            public float ao;
            public float shadow;
            public float4 tangent;
        }

        public GpuMesher(ComputeShader cs)
        {
            shader = cs;
            kernelBuildFaces = shader.FindKernel("CSBuildFaces");
            kGreedyPosY = shader.FindKernel("CSGreedyPosY");
            kGreedyNegY = shader.FindKernel("CSGreedyNegY");
            kGreedyPosX = shader.FindKernel("CSGreedyPosX");
            kGreedyNegX = shader.FindKernel("CSGreedyNegX");
            kGreedyPosZ = shader.FindKernel("CSGreedyPosZ");
            kGreedyNegZ = shader.FindKernel("CSGreedyNegZ");
            _kernels = new int[] { kernelBuildFaces, kGreedyPosY, kGreedyNegY, kGreedyPosX, kGreedyNegX, kGreedyPosZ, kGreedyNegZ };
            
            voxelBuffer = new ComputeBuffer(ChunkDefs.VoxelsPerChunk, sizeof(uint));
            countersBuffer = new ComputeBuffer(2, sizeof(uint));
            // Store vertices as a flat float stream: pos(3), normal(3), uv(2), ao(1), shadow(1), tangent(4), tileIndex(2) = 16 floats
            vertexBuffer = new ComputeBuffer(MaxVerticesPerChunk * 16, sizeof(float));
            indexBuffer = new ComputeBuffer(MaxIndicesPerChunk, sizeof(uint));
            blockUvTriplets = new ComputeBuffer(BlockDatabase.BlockCount, sizeof(uint) * 3);
            blockUvTriplets.SetData(BlockDatabase.GetAll());
            
            // Initialize face texture buffer - will be populated by BlockDatabaseIntegration
            faceTextureBuffer = new ComputeBuffer(256 * 6, sizeof(int)); // Support up to 256 block types * 6 faces each
            
            // Set default face texture data (all faces use texture index 0)
            var defaultFaceData = new int[256 * 6];
            for (int i = 0; i < defaultFaceData.Length; i++) defaultFaceData[i] = 0;
            faceTextureBuffer.SetData(defaultFaceData);

            // Allocate reusable temp buffer for voxel uploads (uint per voxel)
            _voxelUintTemp = new Unity.Collections.NativeArray<uint>(ChunkDefs.VoxelsPerChunk, Unity.Collections.Allocator.Persistent, Unity.Collections.NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose()
        {
            voxelBuffer?.Dispose();
            countersBuffer?.Dispose();
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            blockUvTriplets?.Dispose();
            faceTextureBuffer?.Dispose();
            if (_voxelUintTemp.IsCreated) _voxelUintTemp.Dispose();
        }

        public void UploadVoxels(NativeArray<byte> voxels)
        {
            // Expand bytes to uints using reusable NativeArray to avoid GC
            int len = voxels.Length;
            // Safety: ensure temp buffer fits (should equal VoxelsPerChunk)
            if (!_voxelUintTemp.IsCreated || _voxelUintTemp.Length < len)
            {
                if (_voxelUintTemp.IsCreated) _voxelUintTemp.Dispose();
                _voxelUintTemp = new Unity.Collections.NativeArray<uint>(len, Unity.Collections.Allocator.Persistent, Unity.Collections.NativeArrayOptions.UninitializedMemory);
            }
            for (int i = 0; i < len; i++) _voxelUintTemp[i] = voxels[i];
            voxelBuffer.SetData(_voxelUintTemp);
        }

        public void SetFaceTextureData(int[] faceTextureData)
        {
            if (faceTextureData != null && faceTextureData.Length > 0)
            {
                faceTextureBuffer.SetData(faceTextureData);
            }
        }

        public void BuildMesh(int3 chunkCoord, float3 sunDir, int sunShadowSteps, float sunShadowStepLen)
        {
            _chunkCoordInts[0] = chunkCoord.x; _chunkCoordInts[1] = chunkCoord.y; _chunkCoordInts[2] = chunkCoord.z;
            shader.SetInts("_ChunkCoord", _chunkCoordInts);
            shader.SetInt("_ChunkSizeXZ", ChunkDefs.ChunkSizeXZ);
            shader.SetInt("_ChunkSizeY", ChunkDefs.ChunkSizeY);
            // Note: WorldStreamer updates BlockDatabase.AtlasTilesPerRow
            shader.SetInt("_AtlasTilesPerRow", BlockDatabase.AtlasTilesPerRow);
            _sunDirFloats[0] = sunDir.x; _sunDirFloats[1] = sunDir.y; _sunDirFloats[2] = sunDir.z;
            shader.SetFloats("_SunDir", _sunDirFloats);
            shader.SetInt("_SunShadowSteps", sunShadowSteps);
            shader.SetFloat("_SunShadowStepLen", sunShadowStepLen);

            // Bind buffers for all kernels used
            for (int i = 0; i < _kernels.Length; i++)
            {
                int k = _kernels[i];
                shader.SetBuffer(k, "_Voxels", voxelBuffer);
                shader.SetBuffer(k, "_Counters", countersBuffer);
                shader.SetBuffer(k, "_Vertices", vertexBuffer);
                shader.SetBuffer(k, "_Indices", indexBuffer);
                shader.SetBuffer(k, "_BlockUvTriplets", blockUvTriplets);
                shader.SetBuffer(k, "_BlockFaceTextures", faceTextureBuffer); // Add face texture buffer
            }

            // zero counters using static buffer to avoid per-call allocation
            countersBuffer.SetData(s_CounterZeros);

            // Greedy passes by face planes
            shader.Dispatch(kGreedyPosY, ChunkDefs.ChunkSizeY, 1, 1);
            shader.Dispatch(kGreedyNegY, ChunkDefs.ChunkSizeY, 1, 1);
            shader.Dispatch(kGreedyPosX, ChunkDefs.ChunkSizeXZ, 1, 1);
            shader.Dispatch(kGreedyNegX, ChunkDefs.ChunkSizeXZ, 1, 1);
            shader.Dispatch(kGreedyPosZ, ChunkDefs.ChunkSizeXZ, 1, 1);
            shader.Dispatch(kGreedyNegZ, ChunkDefs.ChunkSizeXZ, 1, 1);
        }

        public void ReadbackMesh(AsyncGPUReadbackRequest verticesReq, AsyncGPUReadbackRequest indicesReq,
            out PackedVertex[] vertices, out uint[] indices)
        {
            if (verticesReq.done && !verticesReq.hasError)
            {
                var floats = verticesReq.GetData<float>();
                int vertCount = floats.Length / 16;
                var result = new PackedVertex[vertCount];
                for (int i = 0; i < vertCount; i++)
                {
                    int b = i * 16;
                    result[i].position = new float3(floats[b + 0], floats[b + 1], floats[b + 2]);
                    result[i].normal = new float3(floats[b + 3], floats[b + 4], floats[b + 5]);
                    result[i].uv = new float2(floats[b + 6], floats[b + 7]);
                    result[i].ao = floats[b + 8];
                    result[i].shadow = floats[b + 9];
                    result[i].tangent = new float4(floats[b + 10], floats[b + 11], floats[b + 12], floats[b + 13]);
                }
                vertices = result;
            }
            else
            {
                vertices = Array.Empty<PackedVertex>();
            }
            indices = indicesReq.done && !indicesReq.hasError
                ? indicesReq.GetData<uint>().ToArray()
                : Array.Empty<uint>();
        }

        public void ReadbackMeshNative(AsyncGPUReadbackRequest verticesReq, AsyncGPUReadbackRequest indicesReq,
            out NativeArray<float> vertexFloats, out NativeArray<uint> indexData)
        {
            if (verticesReq.done && !verticesReq.hasError)
            {
                vertexFloats = verticesReq.GetData<float>();
            }
            else
            {
                vertexFloats = default;
            }
            if (indicesReq.done && !indicesReq.hasError)
            {
                indexData = indicesReq.GetData<uint>();
            }
            else
            {
                indexData = default;
            }
        }

        public void RequestCounters(out AsyncGPUReadbackRequest cReq)
        {
            cReq = AsyncGPUReadback.Request(countersBuffer);
        }

        public void RequestMeshData(int vertexCount, int indexCount, out AsyncGPUReadbackRequest vReq, out AsyncGPUReadbackRequest iReq)
        {
            int vSize = vertexCount * (sizeof(float) * (3 + 3 + 2 + 2 + 4 + 2));
            int iSize = indexCount * sizeof(uint);
            vReq = AsyncGPUReadback.Request(vertexBuffer, vSize, 0);
            iReq = AsyncGPUReadback.Request(indexBuffer, iSize, 0);
        }
    }
}

