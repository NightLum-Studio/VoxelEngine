# VoxelEngineSetupValidator (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/VoxelEngineSetupValidator.cs`

Summary
- Validates scene components and wiring for the voxel engine
- Provides context menu actions for Validation and Auto-setup

Inspector
- Required Components
	- worldStreamer (WorldStreamer)
	- blockDatabaseIntegration (BlockDatabaseIntegration)
	- blockDatabase (BlockDefinitionDatabase)
- Auto-Find Components
	- autoFindComponents (bool)

Context Menu
- Validate Setup(): logs components and checks assignments and texture array
- Auto-Setup(): attempts to wire WorldStreamer to BlockDatabaseIntegration

Related
- [WorldStreamer](world-streamer.md), [BlockDatabaseIntegration](block-database-integration.md)

[Back to overview](../overview.md)
