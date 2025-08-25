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
- scripts/world-streamer.md
- scripts/gpu-mesher.md
- scripts/chunk.md
- scripts/chunk-defs.md
- scripts/terrain-generation.md
- scripts/block-definition.md
- scripts/block-definition-database.md
- scripts/block-database-integration.md
- scripts/block-database.md
- scripts/block-types.md
- scripts/voxel-engine-setup-validator.md
- scripts/build-diagnostic.md
- scripts/player-fly-controller.md
- scripts/block-editor.md
- scripts/texture-array-shader-gui.md

Back to README: [README.md]../README.md
