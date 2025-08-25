using UnityEngine;
using HyperVoxel;

namespace HyperVoxel
{
    /// <summary>
    /// Helper component to validate proper setup of the voxel engine components
    /// </summary>
    [System.Serializable]
    public class VoxelEngineSetupValidator : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private WorldStreamer worldStreamer;
        [SerializeField] private BlockDatabaseIntegration blockDatabaseIntegration;
        [SerializeField] private BlockDefinitionDatabase blockDatabase;
        
        [Header("Auto-Find Components")]
        [SerializeField] private bool autoFindComponents = true;
        
        private void Start()
        {
            if (autoFindComponents)
            {
                FindComponents();
            }
            ValidateSetup();
        }
        
        private void FindComponents()
        {
            if (worldStreamer == null)
                worldStreamer = FindObjectOfType<WorldStreamer>();
                
            if (blockDatabaseIntegration == null)
                blockDatabaseIntegration = FindObjectOfType<BlockDatabaseIntegration>();
                
            if (blockDatabase == null && blockDatabaseIntegration != null)
                blockDatabase = blockDatabaseIntegration.blockDatabase;
        }
        
        [ContextMenu("Validate Setup")]
        public void ValidateSetup()
        {
            Debug.Log("=== Voxel Engine Setup Validation ===", this);
            
            // Check WorldStreamer
            if (worldStreamer == null)
            {
                Debug.LogError("❌ WorldStreamer not found! Add a WorldStreamer component to your scene.", this);
            }
            else
            {
                Debug.Log("✅ WorldStreamer found", this);
                
                if (worldStreamer.blockDatabaseIntegration == null)
                {
                    Debug.LogError("❌ WorldStreamer.blockDatabaseIntegration is not assigned!", this);
                }
                else
                {
                    Debug.Log("✅ WorldStreamer has BlockDatabaseIntegration assigned", this);
                }
            }
            
            // Check BlockDatabaseIntegration
            if (blockDatabaseIntegration == null)
            {
                Debug.LogError("❌ BlockDatabaseIntegration not found! Add a BlockDatabaseIntegration component to your scene.", this);
            }
            else
            {
                Debug.Log("✅ BlockDatabaseIntegration found", this);
                blockDatabaseIntegration.CheckSetup();
            }
            
            // Check BlockDefinitionDatabase
            if (blockDatabase == null)
            {
                Debug.LogError("❌ BlockDefinitionDatabase not found! Create a BlockDefinitionDatabase asset.", this);
            }
            else
            {
                Debug.Log($"✅ BlockDefinitionDatabase found: {blockDatabase.name}", this);
                
                if (blockDatabase.textureArray == null)
                {
                    Debug.LogError("❌ BlockDefinitionDatabase.textureArray is not assigned!", this);
                }
                else
                {
                    Debug.Log($"✅ Texture array assigned: {blockDatabase.textureArray.name} ({blockDatabase.textureArray.depth} slices)", this);
                }
            }
            
            Debug.Log("=== Validation Complete ===", this);
        }
        
        [ContextMenu("Auto-Setup")]
        public void AutoSetup()
        {
            Debug.Log("=== Auto-Setup Starting ===", this);
            
            FindComponents();
            
            // Connect WorldStreamer to BlockDatabaseIntegration
            if (worldStreamer != null && blockDatabaseIntegration != null)
            {
                if (worldStreamer.blockDatabaseIntegration == null)
                {
                    worldStreamer.blockDatabaseIntegration = blockDatabaseIntegration;
                    Debug.Log("✅ Connected WorldStreamer to BlockDatabaseIntegration", this);
                }
            }
            
            ValidateSetup();
        }
    }
}
