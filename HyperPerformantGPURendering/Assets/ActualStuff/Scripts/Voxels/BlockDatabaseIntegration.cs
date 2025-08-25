using UnityEngine;
using Unity.Collections;
using HyperVoxel;

namespace HyperVoxel
{
    /// <summary>
    /// Integrates the BlockDefinitionDatabase with the GPU meshing system
    /// </summary>
    public class BlockDatabaseIntegration : MonoBehaviour
    {
        [Header("Block Database")]
        public BlockDefinitionDatabase blockDatabase;
        
        [Header("GPU Mesher Integration")]
        public GpuMesher gpuMesher;
        
        private int[] _faceTextureData;
        
        private void Start()
        {
            InitializeBlockData();
        }
        
        // GpuMesher manages its own buffers, so no cleanup needed here
        
        /// <summary>
        /// Initialize block data for GPU mesher
        /// </summary>
        public void InitializeBlockData()
        {
            if (blockDatabase == null)
            {
                Debug.LogError("BlockDefinitionDatabase is not assigned!", this);
                return;
            }
            
            if (gpuMesher == null)
            {
                Debug.LogWarning("GpuMesher is not assigned yet. This will be initialized when WorldStreamer starts.", this);
                return;
            }
            
            // Get face texture indices from database
            _faceTextureData = blockDatabase.GetFaceTextureIndices();
            
            // Set face texture data to GPU mesher
            gpuMesher.SetFaceTextureData(_faceTextureData);
            
            Debug.Log($"Initialized block database with {blockDatabase.BlockCount} blocks and {_faceTextureData.Length} face texture indices", this);
        }
        
        /// <summary>
        /// Refresh block data when database changes
        /// </summary>
        [ContextMenu("Refresh Block Data")]
        public void RefreshBlockData()
        {
            InitializeBlockData();
        }
        
        /// <summary>
        /// Check if the integration is properly set up
        /// </summary>
        [ContextMenu("Check Setup")]
        public void CheckSetup()
        {
            Debug.Log("=== BlockDatabaseIntegration Setup Check ===", this);
            Debug.Log($"BlockDefinitionDatabase: {(blockDatabase != null ? "✅ Assigned" : "❌ Missing")}", this);
            Debug.Log($"GpuMesher: {(gpuMesher != null ? "✅ Assigned" : "⚠️ Not assigned (will be set by WorldStreamer)")}", this);
            
            if (blockDatabase != null)
            {
                Debug.Log($"Block Count: {blockDatabase.BlockCount}", this);
                Debug.Log($"Texture Array: {(blockDatabase.textureArray != null ? $"✅ {blockDatabase.textureArray.name} ({blockDatabase.textureArray.depth} slices)" : "❌ Missing")}", this);
            }
        }
        
        /// <summary>
        /// Get block definition by ID
        /// </summary>
        public BlockDefinition GetBlock(BlockId id)
        {
            return blockDatabase?.GetBlock(id);
        }
        
        /// <summary>
        /// Get texture index for a specific face
        /// </summary>
        public int GetFaceTextureIndex(BlockId blockId, BlockFace face)
        {
            return blockDatabase?.GetFaceTextureIndex(blockId, face) ?? 0;
        }
        
        // No buffer cleanup needed - GpuMesher handles its own buffers
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Refresh in editor when database changes
            if (Application.isPlaying && blockDatabase != null && gpuMesher != null)
            {
                InitializeBlockData();
            }
        }
        #endif
    }
}
