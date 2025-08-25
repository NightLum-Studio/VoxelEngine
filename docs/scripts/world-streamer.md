# WorldStreamer (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/WorldStreamer.cs`

Summary
- Manages chunk lifecycle around a center (camera/player)
- Schedules terrain jobs, invokes GPU mesher, applies MeshData, and draws
- Unloads distant chunks; deterministic spiral priority

Inspector
- Streaming
	- viewDistanceChunks (int): radius in chunks kept loaded around center
	- maxGeneratePerFrame (int): cap on new chunk generations per frame
	- seed (int): terrain noise seed
- Rendering
	- voxelMesher (ComputeShader): auto-loads Resources/VoxelMesher if null
	- voxelMaterial (Material): auto-loads Resources/VoxelAtlasMat or creates fallback shader
- Runtime
	- runInBackground (bool): keeps streaming/meshing when unfocused
- Block Database
	- blockDatabaseIntegration (BlockDatabaseIntegration): uploads per-face textures
- Driver
	- player (Transform): preferred streaming center; falls back to Camera.main or self

Key concepts
- viewDistanceChunks, maxGeneratePerFrame
- Single active GPU meshing task; counters readback, then vertex/index readback
- Material param `_AtlasTilesPerRow`, sun direction default if missing light

Usage
- Add to a scene and assign ComputeShader/Material (or place in Resources for auto-load)
- Ensure `BlockDatabaseIntegration` is present and linked to the mesher
- Optional: assign `player`; otherwise main camera is used as the center
- Material `_AtlasTilesPerRow` is updated from `HyperVoxel.BlockDatabase.AtlasTilesPerRow`

Related
- [GpuMesher](gpu-mesher.md), [Chunk](chunk.md), [ChunkDefs](chunk-defs.md), [TerrainGeneration](terrain-generation.md)

Back to overview: ../overview.md
