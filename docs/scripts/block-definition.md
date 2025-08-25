# BlockDefinition (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BlockDefinition.cs`

Summary
- Describes a block: id, name, per-face texture indices, and gameplay/physics/audio flags
- Helpers to set/get per-face texture indices

Inspector
- Basic Properties
	- blockName (string)
	- blockId (enum BlockId)
- Rendering
	- faceTextures: top, bottom, north(+Z), south(-Z), east(+X), west(-X)
- Gameplay
	- requiredTool (ToolType)
	- hardness (int)
	- isTransparent (bool)
	- isSolid (bool)
- Physics
	- friction (float)
	- bounciness (float)
- Audio
	- breakSoundName, placeSoundName, stepSoundName

Key APIs
- SetAllFaces(int textureIndex)
- GetFaceTextureIndex(BlockFace face)
- SetFaceTextureIndex(BlockFace face, int index)

Related
- [BlockDefinitionDatabase](block-definition-database.md), [BlockEditor](block-editor.md)

Back to overview: ../overview.md
