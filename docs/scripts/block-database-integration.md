# BlockDatabaseIntegration (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BlockDatabaseIntegration.cs`

Summary
- Bridges `BlockDefinitionDatabase` to `GpuMesher`
- Uploads per-face texture indices to the face texture buffer
- Provides refresh/setup helpers

Inspector
- Block Database
	- blockDatabase (BlockDefinitionDatabase): ScriptableObject asset reference
- GPU Mesher Integration
	- gpuMesher (GpuMesher): assigned by WorldStreamer on Awake

Editor/Context Menu
- Refresh Block Data: re-pulls face indices and re-uploads to GPU
- Check Setup: logs assignment status and texture array info

Related
- [BlockDefinitionDatabase](block-definition-database.md), [GpuMesher](gpu-mesher.md), [WorldStreamer](world-streamer.md)

[Back to overview](../overview.md)
