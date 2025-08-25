# ChunkDefs (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/ChunkDefs.cs`

Summary
- Defines chunk dimensions, counts, face/vertex/index constants, and threadgroup size
- Provides coordinate helpers for voxel indexing

Constants
- ChunkSizeXZ = 32, ChunkSizeY = 128
- VoxelsPerChunk = XZ*XZ*Y
- MaxFacesPerVoxel = 6, MaxVerticesPerFace = 4, MaxIndicesPerFace = 6
- ThreadsPerGroup = 8

Helpers
- ChunkToVoxelOrigin(int3 chunkCoord)

Related
- [Chunk](chunk.md), [WorldStreamer](world-streamer.md), [GpuMesher](gpu-mesher.md)

Back to overview: ../overview.md
