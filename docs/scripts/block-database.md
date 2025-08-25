# BlockDatabase (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BlockDatabase.cs`

Summary
- Provides runtime enums and legacy UV triplets mapping used by mesher/material
- Exposes `AtlasTilesPerRow` and helpers

Notes
- This file also contains a disabled legacy duplicate of BlockDefinitionDatabase guarded by `#if false` to avoid symbol conflicts.
- `GpuMesher` initializes `blockUvTriplets` from `HyperVoxel.BlockDatabase.GetAll()`.

Related
- [GpuMesher](gpu-mesher.md), [BlockTypes](block-types.md)

Back to overview: ../overview.md
