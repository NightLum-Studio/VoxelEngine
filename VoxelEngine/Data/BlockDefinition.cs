using System;
using UnityEngine;
using VoxelEngine.Data;

namespace VoxelEngine.Data
{
    [Serializable]
    public struct BlockFaceTextures
    {
        public int topFace;
        public int bottomFace;
        public int northFace;   // +Z
        public int southFace;   // -Z
        public int eastFace;    // +X
        public int westFace;    // -X

        public int GetFaceIndex(BlockFace face)
        {
            return face switch
            {
                BlockFace.Top => topFace,
                BlockFace.Bottom => bottomFace,
                BlockFace.North => northFace,
                BlockFace.South => southFace,
                BlockFace.East => eastFace,
                BlockFace.West => westFace,
                _ => 0
            };
        }

        public void SetFaceIndex(BlockFace face, int textureIndex)
        {
            switch (face)
            {
                case BlockFace.Top: topFace = textureIndex; break;
                case BlockFace.Bottom: bottomFace = textureIndex; break;
                case BlockFace.North: northFace = textureIndex; break;
                case BlockFace.South: southFace = textureIndex; break;
                case BlockFace.East: eastFace = textureIndex; break;
                case BlockFace.West: westFace = textureIndex; break;
            }
        }
    }

    public enum BlockFace
    {
        Top = 0,    // +Y
        Bottom = 1, // -Y
        North = 2,  // +Z
        South = 3,  // -Z
        East = 4,   // +X
        West = 5    // -X
    }

    public enum ToolType
    {
        None = 0,
        Pickaxe = 1,
        Shovel = 2,
        Axe = 3,
        Shears = 4,
        Hoe = 5
    }

    [Serializable]
    public class BlockDefinition
    {
        [Header("Basic Properties")]
        public string blockName = "New Block";
        public BlockId blockId = BlockId.Air;
        
        [Header("Rendering")]
        public BlockFaceTextures faceTextures = new BlockFaceTextures();
        
        [Header("Gameplay")]
        public ToolType requiredTool = ToolType.None;
        public int hardness = 1;
        public bool isTransparent = false;
        public bool isSolid = true;
        
        [Header("Physics")]
        public float friction = 0.6f;
        public float bounciness = 0.0f;
        
        [Header("Audio")]
        public string breakSoundName = "stone_break";
        public string placeSoundName = "stone_place";
        public string stepSoundName = "stone_step";

        public BlockDefinition()
        {
            // Default to all faces using texture index 0
            faceTextures = new BlockFaceTextures
            {
                topFace = 0,
                bottomFace = 0,
                northFace = 0,
                southFace = 0,
                eastFace = 0,
                westFace = 0
            };
        }

        public BlockDefinition(string name, BlockId id, int defaultTextureIndex = 0)
        {
            blockName = name;
            blockId = id;
            faceTextures = new BlockFaceTextures
            {
                topFace = defaultTextureIndex,
                bottomFace = defaultTextureIndex,
                northFace = defaultTextureIndex,
                southFace = defaultTextureIndex,
                eastFace = defaultTextureIndex,
                westFace = defaultTextureIndex
            };
        }

        /// <summary>
        /// Set all faces to use the same texture index
        /// </summary>
        public void SetAllFaces(int textureIndex)
        {
            faceTextures.topFace = textureIndex;
            faceTextures.bottomFace = textureIndex;
            faceTextures.northFace = textureIndex;
            faceTextures.southFace = textureIndex;
            faceTextures.eastFace = textureIndex;
            faceTextures.westFace = textureIndex;
        }

        /// <summary>
        /// Get texture index for a specific face
        /// </summary>
        public int GetFaceTextureIndex(BlockFace face)
        {
            return faceTextures.GetFaceIndex(face);
        }

        /// <summary>
        /// Set texture index for a specific face
        /// </summary>
        public void SetFaceTextureIndex(BlockFace face, int textureIndex)
        {
            faceTextures.SetFaceIndex(face, textureIndex);
        }
    }
}