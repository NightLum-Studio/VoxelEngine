# BlockDefinitionDatabase (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BlockDefinitionDatabase.cs`

Summary
- ScriptableObject database for blocks and per-face texture indices
- Provides lookup tables and `[blockCount * 6]` face index arrays
- Initializes default blocks; manages add/remove/update

Inspector
- Texture Array
	- textureArray (Texture2DArray)
	- textureArraySliceCount (int, info)
- Block Definitions
	- blocks (List<BlockDefinition>)

Key APIs
- GetBlock(BlockId id)
- GetFaceTextureIndex(BlockId, BlockFace)
- AddBlock(BlockDefinition), RemoveBlock(BlockId), UpdateBlock(BlockDefinition)
- InitializeDefaults()
- GetTextureIndicesForComputeShader() [legacy]
- GetFaceTextureIndices() → int[blocks*6]

Related
- [BlockDefinition](block-definition.md), [BlockDatabaseIntegration](block-database-integration.md), [BlockEditor](block-editor.md)

Back to overview: ../overview.md
