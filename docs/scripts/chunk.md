# Chunk (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/Chunk.cs`

Summary
- Container for a streamed chunk: coord, state, voxel data, mesh refs, readbacks, job handle
- Manages lifetime and disposal of Native arrays and GPU requests

Fields
- coord (int3)
- state (ChunkState)
- voxels (NativeArray<byte>) length = ChunkDefs.VoxelsPerChunk
- mesh (Mesh)
- countersReq, vReq, iReq (AsyncGPUReadbackRequest)
- genHandle (JobHandle), hasJob (bool), pendingRemoval (bool)

Key concepts
- ChunkState lifecycle; pendingRemoval
- MeshData apply path after readbacks complete

Related
- [ChunkDefs](chunk-defs.md), [WorldStreamer](world-streamer.md)

Back to overview: ../overview.md
