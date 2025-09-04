using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoxelEngine.Data;

namespace VoxelEngine.Data
{
    /// <summary>
    /// ScriptableObject database for storing and managing block definitions
    /// </summary>
    [CreateAssetMenu(fileName = "BlockDefinitionDatabase", menuName = "Voxel Engine/Block Definition Database")]
    public class BlockDefinitionDatabase : ScriptableObject
    {
        [Header("Texture Array")]
        public Texture2DArray textureArray;
        public int textureArraySliceCount = 0;
        
        [Header("Block Definitions")]
        [SerializeField]
        public List<BlockDefinition> blocks = new List<BlockDefinition>();
        
        // Cached lookup for performance
        private Dictionary<BlockId, BlockDefinition> _blockLookup;
        private bool _isInitialized = false;
        
        public int BlockCount => blocks.Count;
        public IReadOnlyList<BlockDefinition> Blocks => blocks;
        
        private void OnEnable()
        {
            InitializeLookup();
        }
        
        private void InitializeLookup()
        {
            _blockLookup = new Dictionary<BlockId, BlockDefinition>();
            
            foreach (var block in blocks)
            {
                if (block != null)
                {
                    _blockLookup[block.blockId] = block;
                }
            }
            
            _isInitialized = true;
            
            // Update texture array slice count if needed
            if (textureArray != null && textureArraySliceCount != textureArray.depth)
            {
                textureArraySliceCount = textureArray.depth;
                Debug.Log($"Updated texture array slice count to {textureArraySliceCount}");
            }
            
            // Initialize defaults if empty
            if (blocks.Count == 0)
            {
                InitializeDefaults();
            }
        }
        
        /// <summary>
        /// Get block definition by ID
        /// </summary>
        public BlockDefinition GetBlock(BlockId id)
        {
            if (!_isInitialized)
            {
                InitializeLookup();
            }
            
            _blockLookup.TryGetValue(id, out BlockDefinition block);
            return block;
        }
        
        /// <summary>
        /// Get texture index for a specific block face
        /// </summary>
        public int GetFaceTextureIndex(BlockId blockId, BlockFace face)
        {
            var block = GetBlock(blockId);
            return block?.GetFaceTextureIndex(face) ?? 0;
        }
        
        /// <summary>
        /// Add a new block definition
        /// </summary>
        public void AddBlock(BlockDefinition block)
        {
            if (block == null) return;
            
            // Check if block ID already exists
            var existing = blocks.FirstOrDefault(b => b != null && b.blockId == block.blockId);
            if (existing != null)
            {
                Debug.LogWarning($"Block with ID {block.blockId} already exists. Use UpdateBlock instead.");
                return;
            }
            
            blocks.Add(block);
            InitializeLookup();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Remove a block definition
        /// </summary>
        public void RemoveBlock(BlockId blockId)
        {
            var block = blocks.FirstOrDefault(b => b != null && b.blockId == blockId);
            if (block != null)
            {
                blocks.Remove(block);
                InitializeLookup();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }
        
        /// <summary>
        /// Update an existing block definition
        /// </summary>
        public void UpdateBlock(BlockDefinition updatedBlock)
        {
            if (updatedBlock == null) return;
            
            var index = blocks.FindIndex(b => b != null && b.blockId == updatedBlock.blockId);
            if (index >= 0)
            {
                blocks[index] = updatedBlock;
                InitializeLookup();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }
        
        /// <summary>
        /// Initialize with default block types
        /// </summary>
        public void InitializeDefaults()
        {
            blocks.Clear();
            
            // Air block
            blocks.Add(new BlockDefinition("Air", BlockId.Air, 0)
            {
                isTransparent = true,
                isSolid = false
            });
            
            // Grass block - different textures for top/bottom/sides
            var grassBlock = new BlockDefinition("Grass", BlockId.Grass, 0);
            grassBlock.faceTextures.topFace = 0;    // Grass top
            grassBlock.faceTextures.bottomFace = 2; // Dirt bottom
            grassBlock.faceTextures.northFace = 1;  // Grass side
            grassBlock.faceTextures.southFace = 1;  // Grass side
            grassBlock.faceTextures.eastFace = 1;   // Grass side
            grassBlock.faceTextures.westFace = 1;   // Grass side
            grassBlock.requiredTool = ToolType.Shovel;
            blocks.Add(grassBlock);
            
            // Dirt block
            blocks.Add(new BlockDefinition("Dirt", BlockId.Dirt, 2)
            {
                requiredTool = ToolType.Shovel
            });
            
            // Stone block
            blocks.Add(new BlockDefinition("Stone", BlockId.Stone, 3)
            {
                requiredTool = ToolType.Pickaxe,
                hardness = 3
            });
            
            // Sand block
            blocks.Add(new BlockDefinition("Sand", BlockId.Sand, 4)
            {
                requiredTool = ToolType.Shovel
            });
            
            // Water block
            blocks.Add(new BlockDefinition("Water", BlockId.Water, 5)
            {
                isTransparent = true,
                isSolid = false
            });
            
            InitializeLookup();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log($"Initialized {blocks.Count} default block definitions");
        }
        
        /// <summary>
        /// Get texture indices for compute shader (legacy compatibility)
        /// </summary>
        public uint[] GetTextureIndicesForComputeShader()
        {
            var indices = new uint[blocks.Count * 3]; // top, side, bottom for each block
            
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block != null)
                {
                    indices[i * 3 + 0] = (uint)block.faceTextures.topFace;
                    indices[i * 3 + 1] = (uint)block.faceTextures.northFace; // Use north as representative side
                    indices[i * 3 + 2] = (uint)block.faceTextures.bottomFace;
                }
            }
            
            return indices;
        }
        
        /// <summary>
        /// Get face texture indices for the new per-face system
        /// </summary>
        public int[] GetFaceTextureIndices()
        {
            // 6 faces per block: top, bottom, north, south, east, west
            var indices = new int[blocks.Count * 6];
            
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block != null)
                {
                    indices[i * 6 + 0] = block.faceTextures.topFace;
                    indices[i * 6 + 1] = block.faceTextures.bottomFace;
                    indices[i * 6 + 2] = block.faceTextures.northFace;
                    indices[i * 6 + 3] = block.faceTextures.southFace;
                    indices[i * 6 + 4] = block.faceTextures.eastFace;
                    indices[i * 6 + 5] = block.faceTextures.westFace;
                }
            }
            
            return indices;
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Validate the database in the editor
        /// </summary>
        private void OnValidate()
        {
            if (textureArray != null)
            {
                textureArraySliceCount = textureArray.depth;
            }
            
            // Ensure we have basic blocks
            if (Application.isPlaying && (blocks == null || blocks.Count == 0))
            {
                InitializeDefaults();
            }
        }
        #endif
    }
}
