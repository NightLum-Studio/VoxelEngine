# Documentation Overview

Start here for a high-level map of Luxelith’s voxel engine and links to per-script references.

- Streaming and meshing pipeline
  - WorldStreamer orchestrates chunk lifecycle and rendering
  - GpuMesher builds meshes on the GPU via greedy compute kernels
  - Chunk and ChunkDefs define data layout, limits, and helpers
  - TerrainGeneration schedules a Burst IJob (FBM sample) to fill voxel data
- Blocks and textures
  - BlockDefinition and BlockDefinitionDatabase define blocks and per-face texture indices
  - BlockDatabaseIntegration uploads per-face indices to the mesher
  - BlockDatabase and BlockTypes provide runtime ids and legacy UV triplets
- Tooling and utilities
  - BlockEditor editor window for per-face textures with live preview
  - TextureArrayShaderGUI material inspector for texture array shaders
  - VoxelEngineSetupValidator validates scene setup and can auto-wire required refs
  - BuildDiagnostic runtime overlay for performance/support checks
  - PlayerFlyController free-fly controller for testing

Per-script reference information
- [WorldStreamer](scripts/world-streamer.md)
- [GpuMesher](scripts/gpu-mesher.md)
- [Chunk](scripts/chunk.md)
- [ChunkDefs](scripts/chunk-defs.md)
- [TerrainGeneration](scripts/terrain-generation.md)
- [BlockDefinition](scripts/block-definition.md)
- [BlockDefinitionDatabase](scripts/block-definition-database.md)
- [BlockDatabaseIntegration](scripts/block-database-integration.md)
- [BlockDatabase](scripts/block-database.md)
- [BlockTypes](scripts/block-types.md)
- [BuildDiagnostic](scripts/build-diagnostic.md)
- [PlayerFlyController](scripts/player-fly-controller.md)
- [BlockEditor](scripts/block-editor.md)
- [TextureArrayShaderGUI](scripts/texture-array-shader-gui.md)

[Back to README](../README.md)
