# GpuMesher (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/GpuMesher.cs`

Summary
- Orchestrates ComputeShader-based greedy meshing
- Holds buffers for voxels, counters, vertices, indices, UV triplets, per-face textures
- Dispatches kernels for ±X/±Y/±Z planes; async readbacks for counters and geometry

Key concepts
- Vertex stream packing (e.g., 16 floats per vertex)
- Face texture buffer layout `[blockCount * 6]` uploaded by `BlockDatabaseIntegration`
- MaxVertices/MaxIndices guards

Usage
- Called by `WorldStreamer`; set face texture data via `BlockDatabaseIntegration`

Inspector/Fields
- shader (ComputeShader) via constructor
- Buffers created internally:
	- voxelBuffer: uint per voxel
	- countersBuffer: [vertexCount, indexCount]
	- vertexBuffer: float stream (16 floats per vertex)
	- indexBuffer: uint indices
	- blockUvTriplets: legacy triplets from `BlockDatabase.GetAll()`
	- faceTextureBuffer: `[256*6]` per-face indices (overwritten by integration)

Key APIs
- UploadVoxels(NativeArray<byte> voxels)
- SetFaceTextureData(int[] faceTextureData)
- BuildMesh(int3 chunkCoord, float3 sunDir, int sunShadowSteps, float sunShadowStepLen)
- RequestCounters(out AsyncGPUReadbackRequest)
- RequestMeshData(int vertexCount, int indexCount, out AsyncGPUReadbackRequest vReq, out AsyncGPUReadbackRequest iReq)
- ReadbackMesh/ReadbackMeshNative(...)

Related
- [WorldStreamer](world-streamer.md), [BlockDatabaseIntegration](block-database-integration.md), [BlockTypes](block-types.md)

[Back to overview](../overview.md)
