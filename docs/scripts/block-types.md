# BlockTypes (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BlockTypes.cs`

Summary
- Defines `BlockId` enum and UV triplet structs used by the mesher
- Editor mirror exists under `Assets/ActualStuff/EditorStuff/Scripts/Voxels/BlockTypes.cs`

Notes
- In this snapshot, the runtime `BlockTypes.cs` file is empty; the actual runtime identifiers and UV triplets are provided by `BlockDatabase.cs`.
- The GPU mesher uses `HyperVoxel.BlockDatabase.AtlasTilesPerRow` and `GetAll()` for UV triplets.

Related
- [BlockDatabase](block-database.md), [GpuMesher](gpu-mesher.md)

[Back to overview](../overview.md)
