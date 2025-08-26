# TerrainGeneration (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/TerrainGeneration.cs`

Summary
- Burst-compiled `GenerateChunkVoxelsJob` fills voxels using FBM noise
- Assigns Air above surface, Grass at surface, Dirt a few below, Stone deeper

Usage
- Scheduled by `WorldStreamer` during chunk generation

Job fields
- voxels (NativeArray<byte>)
- chunkCoord (int3)
- seed (int)

Related
- [WorldStreamer](world-streamer.md), [BlockTypes](block-types.md)

[Back to overview](../overview.md)
