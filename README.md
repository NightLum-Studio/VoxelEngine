# Luxelith – Hyper‑Performant GPU Voxel Rendering (Unity)

Luxelith is a Unity project showcasing a hyper‑performant voxel world renderer powered by Compute Shaders and greedy meshing on the GPU. It streams chunks around the player, generates terrain with Burst/Jobs, and builds meshes via a shared GPU mesher with async readback.

Key modules
- GPU meshing (compute + greedy) with per‑face textures
- Streaming and terrain generation (Burst/Jobs)
- Block database (per‑face Texture2DArray indices) and editor tooling
- Fly camera and diagnostics overlays

For detailed documentation, see the docs folder:
- [docs/overview.md](docs/overview.md)
- Per‑script reference stubs in [docs/scripts/](docs/scripts/)

## Quick start

- Unity version: see `ProjectSettings/ProjectVersion.txt` (tested with 6000.0.24f1)
- Open the project at `HyperPerformantGPURendering/`
- In a scene, add:
  - WorldStreamer (assign ComputeShader/Material or let it auto‑load from Resources)
  - BlockDatabaseIntegration (assign a `BlockDefinitionDatabase` asset)
  - VoxelEngineSetupValidator (optional: Validate/Auto‑setup)
  - PlayerFlyController on the Camera rig
- Create/assign `Assets/BlockDefinitionDatabase.asset` with a Texture2DArray atlas; open Tools > Voxel Engine > Block Editor to author per‑face indices.
- Play. The world streams around the player in a deterministic spiral.

## Features

- ComputeShader greedy mesher with per‑face texture indices
- Async GPU readback + MeshData API to reduce GC and main‑thread stalls
- Deterministic streaming order, distance‑based unload
- Burst IJob terrain generation (FBM sample included)
- Editor Block Editor with live per‑face preview from Texture2DArray
- Build diagnostics overlay (FPS, frame time, memory, compute support)

## Project layout

- Assets/ActualStuff/Scripts/Voxels
  - ChunkDefs, Chunk, WorldStreamer, GpuMesher, TerrainGeneration (GenerateChunkVoxelsJob), BuildDiagnostic
  - BlockDefinition, BlockDefinitionDatabase, BlockDatabaseIntegration, BlockTypes (ids & UV triplets)
- Assets/ActualStuff/EditorStuff
  - Editor: BlockEditor, TextureArrayShaderGUI
  - Scripts/Voxels: BlockTypes (BlockId, UV triplets, static BlockDatabase used by GPU mesher)

See `docs/scripts/` for per‑file notes.

## How it works (high‑level)

- WorldStreamer
  - Computes a streaming center (player/camera), keeps chunks within `viewDistanceChunks`
  - Schedules `GenerateChunkVoxelsJob` to fill `NativeArray<byte>` voxels
  - Queues ready chunks, feeds them to a shared `GpuMesher`
  - Reads back counters then vertex/index streams; applies via MeshData; draws with `Graphics.DrawMesh`

- GpuMesher
  - Manages ComputeBuffers (voxels, counters, vertices, indices, UV triplets, per‑face textures)
  - Dispatches greedy kernels for ±X/±Y/±Z planes
  - Supports per‑face texture mapping via `[blockCount*6]` buffer uploaded by `BlockDatabaseIntegration`

- Blocks & textures
  - `BlockDefinitionDatabase` stores `BlockDefinition`s (id, per‑face texture indices, gameplay flags)
  - `BlockDatabaseIntegration` uploads per‑face indices to the mesher
  - `BlockEditor` (EditorWindow) previews/edits faces and reads slices from `Texture2DArray`

## Build and run notes

- Ensure target platform supports compute shaders and URP (or the target shader)
- Provide a `Texture2DArray` with one tile per material slice; set array slice count on the material/shader if required
- Add a Directional Light for sun direction (optional). Defaults to Vector3.down if missing

## Troubleshooting

- No meshes: assign/locate `Resources/VoxelMesher.compute` or set `voxelMesher` in WorldStreamer
- All faces same texture: ensure `BlockDatabaseIntegration` is in scene and database has a valid `Texture2DArray`; use its Refresh action
- Editor preview shows color tiles: extraction fallback when array isn’t readable; verify Texture2DArray asset
- Performance: enable `BuildDiagnostic`, reduce `viewDistanceChunks`/`maxGeneratePerFrame`

## License

See LICENSE.
