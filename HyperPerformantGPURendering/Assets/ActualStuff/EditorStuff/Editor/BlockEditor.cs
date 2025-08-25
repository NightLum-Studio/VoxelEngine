using UnityEngine;
using UnityEditor;
using HyperVoxel;
using System.Linq;

namespace HyperVoxel.Editor
{
    public class BlockEditor : EditorWindow
    {
        private BlockDefinitionDatabase _database;
        private BlockDefinition _currentBlock;
        private BlockDefinition _editingBlock; // Copy for editing
        
        // 3D Preview
        private PreviewRenderUtility _previewRenderUtility;
        private GameObject _previewCube;
        private GameObject[] _faceMeshes = new GameObject[6]; // One for each face
        private Material[] _faceMaterials = new Material[6]; // One material per face
        private Vector2 _previewDir = new Vector2(120f, -20f);
        private float _previewZoom = 3f;
        private bool _isDragging = false;
        private Vector2 _lastMousePosition;
        
        // UI State
        private Vector2 _scrollPosition;
        private int _selectedBlockIndex = 0;
        private bool _showAdvancedProperties = false;
        
        // Texture Array Preview
        private Texture2D[] _textureSlices;
        private bool _textureSlicesLoaded = false;
        
        [MenuItem("Tools/Voxel Engine/Block Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<BlockEditor>("Block Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            InitializePreview();
            LoadDatabase();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void InitializePreview()
        {
            _previewRenderUtility = new PreviewRenderUtility();
            _previewRenderUtility.camera.transform.position = Vector3.zero;
            _previewRenderUtility.camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _previewRenderUtility.camera.nearClipPlane = 0.3f;
            _previewRenderUtility.camera.farClipPlane = 1000f;

            // Create parent object for organization
            _previewCube = new GameObject("PreviewCube");
            _previewCube.hideFlags = HideFlags.HideAndDontSave;
            
            // Create individual face meshes
            CreateFaceMeshes();
            
            // Setup lighting
            _previewRenderUtility.lights[0].intensity = 1.4f;
            _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _previewRenderUtility.lights[1].intensity = 1.4f;
        }

        private void CreateFaceMeshes()
        {
            // Find appropriate shader
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            // Create each face with specific mesh and positioning
            CreateTopFace(shader);     // 0: Top
            CreateBottomFace(shader);  // 1: Bottom
            CreateNorthFace(shader);   // 2: North (+Z)
            CreateSouthFace(shader);   // 3: South (-Z)
            CreateEastFace(shader);    // 4: East (+X)
            CreateWestFace(shader);    // 5: West (-X)
        }

        private void CreateTopFace(Shader shader)
        {
            _faceMeshes[0] = new GameObject("Face_Top");
            _faceMeshes[0].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[0].transform.SetParent(_previewCube.transform);
            _faceMeshes[0].transform.localPosition = new Vector3(0, 0.5f, 0);
            _faceMeshes[0].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[0].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[0].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.up);
            
            _faceMaterials[0] = new Material(shader);
            _faceMaterials[0].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[0].name = "Face_Top_Material";
            _faceMaterials[0].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[0];
        }

        private void CreateBottomFace(Shader shader)
        {
            _faceMeshes[1] = new GameObject("Face_Bottom");
            _faceMeshes[1].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[1].transform.SetParent(_previewCube.transform);
            _faceMeshes[1].transform.localPosition = new Vector3(0, -0.5f, 0);
            _faceMeshes[1].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[1].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[1].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.down);
            
            _faceMaterials[1] = new Material(shader);
            _faceMaterials[1].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[1].name = "Face_Bottom_Material";
            _faceMaterials[1].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[1];
        }

        private void CreateNorthFace(Shader shader)
        {
            _faceMeshes[2] = new GameObject("Face_North");
            _faceMeshes[2].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[2].transform.SetParent(_previewCube.transform);
            _faceMeshes[2].transform.localPosition = new Vector3(0, 0, 0.5f);
            _faceMeshes[2].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[2].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[2].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.forward);
            
            _faceMaterials[2] = new Material(shader);
            _faceMaterials[2].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[2].name = "Face_North_Material";
            _faceMaterials[2].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[2];
        }

        private void CreateSouthFace(Shader shader)
        {
            _faceMeshes[3] = new GameObject("Face_South");
            _faceMeshes[3].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[3].transform.SetParent(_previewCube.transform);
            _faceMeshes[3].transform.localPosition = new Vector3(0, 0, -0.5f);
            _faceMeshes[3].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[3].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[3].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.back);
            
            _faceMaterials[3] = new Material(shader);
            _faceMaterials[3].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[3].name = "Face_South_Material";
            _faceMaterials[3].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[3];
        }

        private void CreateEastFace(Shader shader)
        {
            _faceMeshes[4] = new GameObject("Face_East");
            _faceMeshes[4].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[4].transform.SetParent(_previewCube.transform);
            _faceMeshes[4].transform.localPosition = new Vector3(0.5f, 0, 0);
            _faceMeshes[4].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[4].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[4].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.right);
            
            _faceMaterials[4] = new Material(shader);
            _faceMaterials[4].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[4].name = "Face_East_Material";
            _faceMaterials[4].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[4];
        }

        private void CreateWestFace(Shader shader)
        {
            _faceMeshes[5] = new GameObject("Face_West");
            _faceMeshes[5].hideFlags = HideFlags.HideAndDontSave;
            _faceMeshes[5].transform.SetParent(_previewCube.transform);
            _faceMeshes[5].transform.localPosition = new Vector3(-0.5f, 0, 0);
            _faceMeshes[5].transform.localRotation = Quaternion.identity;
            
            var meshFilter = _faceMeshes[5].AddComponent<MeshFilter>();
            var meshRenderer = _faceMeshes[5].AddComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = CreateQuadMesh(Vector3.left);
            
            _faceMaterials[5] = new Material(shader);
            _faceMaterials[5].hideFlags = HideFlags.HideAndDontSave;
            _faceMaterials[5].name = "Face_West_Material";
            _faceMaterials[5].SetFloat("_Cull", 2); // Cull Back faces
            meshRenderer.material = _faceMaterials[5];
        }

        private Mesh CreateQuadMesh(Vector3 normal)
        {
            var mesh = new Mesh();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            
            Vector3[] vertices = new Vector3[4];
            Vector2[] uvs = new Vector2[4];
            Vector3[] normals = new Vector3[4];
            int[] triangles;
            
            // Generate vertices and triangles with correct winding order (counter-clockwise when viewed from outside)
            if (normal == Vector3.up) // Top face (looking down from above)
            {
                vertices[0] = new Vector3(-0.5f, 0f, 0.5f);  // Front left
                vertices[1] = new Vector3(0.5f, 0f, 0.5f);   // Front right
                vertices[2] = new Vector3(0.5f, 0f, -0.5f);  // Back right
                vertices[3] = new Vector3(-0.5f, 0f, -0.5f); // Back left
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from above
            }
            else if (normal == Vector3.down) // Bottom face (looking up from below)
            {
                vertices[0] = new Vector3(-0.5f, 0f, -0.5f); // Back left
                vertices[1] = new Vector3(0.5f, 0f, -0.5f);  // Back right
                vertices[2] = new Vector3(0.5f, 0f, 0.5f);   // Front right
                vertices[3] = new Vector3(-0.5f, 0f, 0.5f);  // Front left
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from below
            }
            else if (normal == Vector3.forward) // North face (+Z) (looking from front)
            {
                vertices[0] = new Vector3(-0.5f, -0.5f, 0f); // Bottom left
                vertices[1] = new Vector3(0.5f, -0.5f, 0f);  // Bottom right
                vertices[2] = new Vector3(0.5f, 0.5f, 0f);   // Top right
                vertices[3] = new Vector3(-0.5f, 0.5f, 0f);  // Top left
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from front
            }
            else if (normal == Vector3.back) // South face (-Z) (looking from back)
            {
                vertices[0] = new Vector3(0.5f, -0.5f, 0f);  // Bottom left (when looking from back)
                vertices[1] = new Vector3(-0.5f, -0.5f, 0f); // Bottom right (when looking from back)
                vertices[2] = new Vector3(-0.5f, 0.5f, 0f);  // Top right (when looking from back)
                vertices[3] = new Vector3(0.5f, 0.5f, 0f);   // Top left (when looking from back)
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from back
            }
            else if (normal == Vector3.right) // East face (+X) (looking from right)
            {
                vertices[0] = new Vector3(0f, -0.5f, 0.5f);  // Bottom left (when looking from right)
                vertices[1] = new Vector3(0f, -0.5f, -0.5f); // Bottom right (when looking from right)
                vertices[2] = new Vector3(0f, 0.5f, -0.5f);  // Top right (when looking from right)
                vertices[3] = new Vector3(0f, 0.5f, 0.5f);   // Top left (when looking from right)
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from right
            }
            else // Vector3.left - West face (-X) (looking from left)
            {
                vertices[0] = new Vector3(0f, -0.5f, -0.5f); // Bottom left (when looking from left)
                vertices[1] = new Vector3(0f, -0.5f, 0.5f);  // Bottom right (when looking from left)
                vertices[2] = new Vector3(0f, 0.5f, 0.5f);   // Top right (when looking from left)
                vertices[3] = new Vector3(0f, 0.5f, -0.5f);  // Top left (when looking from left)
                triangles = new[] { 0, 1, 2, 0, 2, 3 }; // Counter-clockwise from left
            }
            
            // Standard UV mapping for all faces
            uvs[0] = new Vector2(0f, 0f); // Bottom left
            uvs[1] = new Vector2(1f, 0f); // Bottom right
            uvs[2] = new Vector2(1f, 1f); // Top right
            uvs[3] = new Vector2(0f, 1f); // Top left
            
            // Set normals
            for (int i = 0; i < 4; i++)
            {
                normals[i] = normal;
            }
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.normals = normals;
            
            return mesh;
        }

        private void CleanupPreview()
        {
            if (_previewRenderUtility != null)
            {
                _previewRenderUtility.Cleanup();
                _previewRenderUtility = null;
            }

            // Clean up face meshes
            for (int i = 0; i < _faceMeshes.Length; i++)
            {
                if (_faceMeshes[i] != null)
                {
                    var meshFilter = _faceMeshes[i].GetComponent<MeshFilter>();
                    if (meshFilter?.sharedMesh != null)
                    {
                        DestroyImmediate(meshFilter.sharedMesh);
                    }
                    DestroyImmediate(_faceMeshes[i]);
                    _faceMeshes[i] = null;
                }
            }

            // Clean up face materials and their textures
            for (int i = 0; i < _faceMaterials.Length; i++)
            {
                if (_faceMaterials[i] != null)
                {
                    // Clean up extracted textures
                    if (_faceMaterials[i].mainTexture != null && _faceMaterials[i].mainTexture is Texture2D)
                    {
                        DestroyImmediate(_faceMaterials[i].mainTexture);
                    }
                    DestroyImmediate(_faceMaterials[i]);
                    _faceMaterials[i] = null;
                }
            }

            if (_previewCube != null)
            {
                DestroyImmediate(_previewCube);
                _previewCube = null;
            }
        }

        private void LoadDatabase()
        {
            // Try to find existing database
            var guids = AssetDatabase.FindAssets("t:BlockDefinitionDatabase");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _database = AssetDatabase.LoadAssetAtPath<BlockDefinitionDatabase>(path);
            }

            // Create new database if none exists
            if (_database == null)
            {
                _database = CreateInstance<BlockDefinitionDatabase>();
                _database.InitializeDefaults();
                
                AssetDatabase.CreateAsset(_database, "Assets/BlockDefinitionDatabase.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            LoadTextureSlices();
            
            if (_database.Blocks.Count > 0)
            {
                SelectBlock(0);
            }
        }

        private void LoadTextureSlices()
        {
            if (_database?.textureArray == null) return;

            _textureSlices = new Texture2D[_database.textureArray.depth];
            _textureSlicesLoaded = true;

            // Note: In practice, you'd need to extract slices from the texture array
            // This is a simplified approach - you might need custom code to properly extract slices
        }

        private void SelectBlock(int index)
        {
            if (_database == null || index < 0 || index >= _database.Blocks.Count) return;

            _selectedBlockIndex = index;
            _currentBlock = _database.Blocks[index];
            
            // Create a copy for editing
            _editingBlock = new BlockDefinition
            {
                blockName = _currentBlock.blockName,
                blockId = _currentBlock.blockId,
                faceTextures = _currentBlock.faceTextures,
                requiredTool = _currentBlock.requiredTool,
                hardness = _currentBlock.hardness,
                isTransparent = _currentBlock.isTransparent,
                isSolid = _currentBlock.isSolid,
                friction = _currentBlock.friction,
                bounciness = _currentBlock.bounciness,
                breakSoundName = _currentBlock.breakSoundName,
                placeSoundName = _currentBlock.placeSoundName,
                stepSoundName = _currentBlock.stepSoundName
            };

            UpdatePreviewMaterial();
        }

        private void UpdatePreviewMaterial()
        {
            if (_editingBlock == null) return;
            
            // Make sure we have all face materials
            if (_faceMaterials == null || _faceMaterials.Length != 6) return;
            
            // Handle case where texture array is not assigned
            if (_database?.textureArray == null)
            {
                // Show debug colors when no texture array is available
                var debugColors = new Color[]
                {
                    new Color(0.8f, 1f, 0.8f),    // Top: Light green
                    new Color(0.8f, 0.8f, 1f),    // Bottom: Light blue
                    new Color(1f, 0.8f, 0.9f),    // North: Light pink
                    new Color(0.9f, 1f, 0.8f),    // South: Light yellow-green
                    new Color(1f, 0.9f, 0.8f),    // East: Light orange
                    new Color(0.9f, 0.8f, 1f)     // West: Light purple
                };
                
                for (int i = 0; i < 6; i++)
                {
                    if (_faceMaterials[i] != null)
                    {
                        _faceMaterials[i].mainTexture = null;
                        _faceMaterials[i].color = debugColors[i];
                    }
                }
                return;
            }
            
            // Clamp texture indices to valid range
            int maxIndex;
            try
            {
                maxIndex = _database.textureArray.depth - 1;
            }
            catch (System.Exception)
            {
                // Fallback if texture array access fails
                maxIndex = 0;
            }
            
            // Get texture indices for each face
            var faceIndices = new int[]
            {
                Mathf.Clamp(_editingBlock.faceTextures.topFace, 0, maxIndex),      // 0: Top
                Mathf.Clamp(_editingBlock.faceTextures.bottomFace, 0, maxIndex),   // 1: Bottom
                Mathf.Clamp(_editingBlock.faceTextures.northFace, 0, maxIndex),    // 2: North
                Mathf.Clamp(_editingBlock.faceTextures.southFace, 0, maxIndex),    // 3: South
                Mathf.Clamp(_editingBlock.faceTextures.eastFace, 0, maxIndex),     // 4: East
                Mathf.Clamp(_editingBlock.faceTextures.westFace, 0, maxIndex)      // 5: West
            };
            
            // Extract individual textures from texture array and assign to face materials
            for (int i = 0; i < 6; i++)
            {
                if (_faceMaterials[i] != null)
                {
                    // Clean up old texture first
                    if (_faceMaterials[i].mainTexture != null && _faceMaterials[i].mainTexture is Texture2D)
                    {
                        DestroyImmediate(_faceMaterials[i].mainTexture);
                        _faceMaterials[i].mainTexture = null;
                    }
                    
                    var texture = ExtractTextureFromArray(_database.textureArray, faceIndices[i]);
                    if (texture != null)
                    {
                        _faceMaterials[i].mainTexture = texture;
                        _faceMaterials[i].color = Color.white; // Reset color when texture is used
                    }
                    else
                    {
                        // Fallback: use debug colors
                        var debugColors = new Color[]
                        {
                            new Color(0.8f, 1f, 0.8f),    // Top: Light green
                            new Color(0.8f, 0.8f, 1f),    // Bottom: Light blue
                            new Color(1f, 0.8f, 0.9f),    // North: Light pink
                            new Color(0.9f, 1f, 0.8f),    // South: Light yellow-green
                            new Color(1f, 0.9f, 0.8f),    // East: Light orange
                            new Color(0.9f, 0.8f, 1f)     // West: Light purple
                        };
                        _faceMaterials[i].color = debugColors[i];
                    }
                }
            }
            
            // Debug: Log the texture indices being set (only on first load or errors)
            if (Application.isPlaying == false) // Only log in editor, not during play
            {
                Debug.Log($"Preview updated - Array: {_database.textureArray.name}, " +
                         $"Faces: [{string.Join(", ", faceIndices)}]");
            }
        }

        private Texture2D ExtractTextureFromArray(Texture2DArray textureArray, int sliceIndex)
        {
            if (textureArray == null)
            {
                Debug.LogWarning("Texture array is null");
                return null;
            }
            
            if (sliceIndex < 0 || sliceIndex >= textureArray.depth)
            {
                Debug.LogWarning($"Slice index {sliceIndex} out of range (0-{textureArray.depth - 1})");
                return null;
            }

            try
            {
                // Method 1: Try using Graphics.CopyTexture with RenderTexture
                var tempRT = RenderTexture.GetTemporary(textureArray.width, textureArray.height, 0, RenderTextureFormat.ARGB32);
                
                if (tempRT != null)
                {
                    try
                    {
                        Graphics.CopyTexture(textureArray, sliceIndex, tempRT, 0);
                        
                        var texture2D = new Texture2D(textureArray.width, textureArray.height, TextureFormat.RGBA32, false);
                        texture2D.hideFlags = HideFlags.HideAndDontSave;
                        texture2D.name = $"ExtractedTexture_Slice{sliceIndex}";
                        
                        RenderTexture.active = tempRT;
                        texture2D.ReadPixels(new Rect(0, 0, textureArray.width, textureArray.height), 0, 0);
                        texture2D.Apply();
                        RenderTexture.active = null;
                        
                        RenderTexture.ReleaseTemporary(tempRT);
                        
                        // Check if we got valid data
                        var pixels = texture2D.GetPixels();
                        bool hasValidData = false;
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            if (pixels[i].a > 0.01f) // Check for alpha
                            {
                                hasValidData = true;
                                break;
                            }
                        }
                        
                        if (hasValidData)
                        {
                            Debug.Log($"Successfully extracted texture slice {sliceIndex} using Graphics.CopyTexture");
                            return texture2D;
                        }
                        else
                        {
                            Debug.LogWarning($"Graphics.CopyTexture returned empty data for slice {sliceIndex}");
                            DestroyImmediate(texture2D);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Graphics.CopyTexture failed for slice {sliceIndex}: {e.Message}");
                        RenderTexture.ReleaseTemporary(tempRT);
                    }
                }
                
                // Method 2: Create a simple colored texture as fallback
                Debug.Log($"Creating fallback colored texture for slice {sliceIndex}");
                return CreateFallbackTexture(sliceIndex, textureArray.width, textureArray.height);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to extract texture from array at index {sliceIndex}: {e.Message}");
                return CreateFallbackTexture(sliceIndex, 64, 64); // Smaller fallback
            }
        }

        private Texture2D CreateFallbackTexture(int sliceIndex, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.name = $"FallbackTexture_Slice{sliceIndex}";
            
            // Create a unique pattern for each slice
            var colors = new Color[width * height];
            var baseColor = GetSliceColor(sliceIndex);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    
                    // Create a simple checkerboard pattern with the slice-specific color
                    bool isCheckerSquare = ((x / 8) + (y / 8)) % 2 == 0;
                    if (isCheckerSquare)
                    {
                        colors[index] = baseColor;
                    }
                    else
                    {
                        colors[index] = baseColor * 0.7f; // Darker shade
                        colors[index].a = 1f;
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            return texture;
        }

        private Color GetSliceColor(int sliceIndex)
        {
            // Generate distinct colors for different slices
            switch (sliceIndex % 10)
            {
                case 0: return Color.red;
                case 1: return Color.green;
                case 2: return Color.blue;
                case 3: return Color.yellow;
                case 4: return Color.cyan;
                case 5: return Color.magenta;
                case 6: return new Color(1f, 0.5f, 0f); // Orange
                case 7: return new Color(0.5f, 0f, 1f); // Purple
                case 8: return new Color(0f, 0.5f, 0.5f); // Teal
                case 9: return new Color(0.5f, 0.5f, 0f); // Olive
                default: return Color.gray;
            }
        }

        private void OnGUI()
        {
            if (_database == null)
            {
                EditorGUILayout.HelpBox("No Block Definition Database found. Create one to begin.", MessageType.Warning);
                if (GUILayout.Button("Create Block Definition Database"))
                {
                    LoadDatabase();
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Left panel - Block list and properties
            DrawLeftPanel();

            // Right panel - 3D preview
            DrawRightPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(400));

            // Database reference
            EditorGUILayout.LabelField("Block Definition Database", EditorStyles.boldLabel);
            _database = (BlockDefinitionDatabase)EditorGUILayout.ObjectField(_database, typeof(BlockDefinitionDatabase), false);

            EditorGUILayout.Space();

            // Block list
            EditorGUILayout.LabelField("Blocks", EditorStyles.boldLabel);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            
            for (int i = 0; i < _database.Blocks.Count; i++)
            {
                var block = _database.Blocks[i];
                bool isSelected = i == _selectedBlockIndex;
                
                if (isSelected)
                {
                    GUI.backgroundColor = Color.cyan;
                }

                if (GUILayout.Button($"{block.blockId}: {block.blockName}", GUILayout.Height(25)))
                {
                    SelectBlock(i);
                }

                if (isSelected)
                {
                    GUI.backgroundColor = Color.white;
                }
            }
            
            EditorGUILayout.EndScrollView();

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Block"))
            {
                AddNewBlock();
            }
            if (GUILayout.Button("Remove Block") && _editingBlock != null)
            {
                RemoveCurrentBlock();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Block properties
            if (_editingBlock != null)
            {
                DrawBlockProperties();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockProperties()
        {
            EditorGUILayout.LabelField("Block Properties", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // Basic properties
            _editingBlock.blockName = EditorGUILayout.TextField("Name", _editingBlock.blockName);
            _editingBlock.blockId = (BlockId)EditorGUILayout.EnumPopup("Block ID", _editingBlock.blockId);

            EditorGUILayout.Space();

            // Face textures with color coding
            EditorGUILayout.LabelField("Face Textures", EditorStyles.boldLabel);
            
            // Use color boxes to help visualize faces
            GUI.color = new Color(0.8f, 1f, 0.8f); // Light green for top
            _editingBlock.faceTextures.topFace = EditorGUILayout.IntField("Top Face (↑)", _editingBlock.faceTextures.topFace);
            
            GUI.color = new Color(0.8f, 0.8f, 1f); // Light blue for bottom
            _editingBlock.faceTextures.bottomFace = EditorGUILayout.IntField("Bottom Face (↓)", _editingBlock.faceTextures.bottomFace);
            
            GUI.color = new Color(1f, 0.9f, 0.8f); // Light orange for north
            _editingBlock.faceTextures.northFace = EditorGUILayout.IntField("North Face (+Z)", _editingBlock.faceTextures.northFace);
            
            GUI.color = new Color(1f, 0.8f, 0.9f); // Light pink for south
            _editingBlock.faceTextures.southFace = EditorGUILayout.IntField("South Face (-Z)", _editingBlock.faceTextures.southFace);
            
            GUI.color = new Color(0.9f, 1f, 0.8f); // Light yellow-green for east
            _editingBlock.faceTextures.eastFace = EditorGUILayout.IntField("East Face (+X)", _editingBlock.faceTextures.eastFace);
            
            GUI.color = new Color(0.9f, 0.8f, 1f); // Light purple for west
            _editingBlock.faceTextures.westFace = EditorGUILayout.IntField("West Face (-X)", _editingBlock.faceTextures.westFace);
            
            GUI.color = Color.white; // Reset color

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set All to Top"))
            {
                int value = _editingBlock.faceTextures.topFace;
                _editingBlock.SetAllFaces(value);
            }
            if (GUILayout.Button("Copy Top to Sides"))
            {
                int topValue = _editingBlock.faceTextures.topFace;
                _editingBlock.faceTextures.northFace = topValue;
                _editingBlock.faceTextures.southFace = topValue;
                _editingBlock.faceTextures.eastFace = topValue;
                _editingBlock.faceTextures.westFace = topValue;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Sides Same"))
            {
                int sideValue = _editingBlock.faceTextures.northFace;
                _editingBlock.faceTextures.southFace = sideValue;
                _editingBlock.faceTextures.eastFace = sideValue;
                _editingBlock.faceTextures.westFace = sideValue;
            }
            if (GUILayout.Button("Reset to 0"))
            {
                _editingBlock.SetAllFaces(0);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Gameplay properties
            _editingBlock.requiredTool = (ToolType)EditorGUILayout.EnumPopup("Required Tool", _editingBlock.requiredTool);
            _editingBlock.hardness = EditorGUILayout.IntField("Hardness", _editingBlock.hardness);
            _editingBlock.isTransparent = EditorGUILayout.Toggle("Is Transparent", _editingBlock.isTransparent);
            _editingBlock.isSolid = EditorGUILayout.Toggle("Is Solid", _editingBlock.isSolid);

            // Advanced properties
            _showAdvancedProperties = EditorGUILayout.Foldout(_showAdvancedProperties, "Advanced Properties");
            if (_showAdvancedProperties)
            {
                _editingBlock.friction = EditorGUILayout.Slider("Friction", _editingBlock.friction, 0f, 1f);
                _editingBlock.bounciness = EditorGUILayout.Slider("Bounciness", _editingBlock.bounciness, 0f, 1f);
                
                EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
                _editingBlock.breakSoundName = EditorGUILayout.TextField("Break Sound", _editingBlock.breakSoundName);
                _editingBlock.placeSoundName = EditorGUILayout.TextField("Place Sound", _editingBlock.placeSoundName);
                _editingBlock.stepSoundName = EditorGUILayout.TextField("Step Sound", _editingBlock.stepSoundName);
            }

            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewMaterial();
            }

            EditorGUILayout.Space();

            // Save/Revert buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Changes"))
            {
                SaveCurrentBlock();
            }
            if (GUILayout.Button("Revert Changes"))
            {
                SelectBlock(_selectedBlockIndex); // Reload from database
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("3D Preview", EditorStyles.boldLabel);

            if (_previewRenderUtility != null && _editingBlock != null)
            {
                // Preview controls
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset View"))
                {
                    _previewDir = new Vector2(120f, -20f);
                    _previewZoom = 3f;
                    Repaint();
                }
                if (GUILayout.Button("Front View"))
                {
                    _previewDir = new Vector2(0f, 0f);
                    Repaint();
                }
                if (GUILayout.Button("Side View"))
                {
                    _previewDir = new Vector2(90f, 0f);
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
                
                // Zoom control
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
                float newZoom = EditorGUILayout.Slider(_previewZoom, 1f, 10f);
                if (newZoom != _previewZoom)
                {
                    _previewZoom = newZoom;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox("Click and drag to rotate the preview. Use the buttons above for preset views.", MessageType.Info);
                
                // Preview area
                Rect previewRect = GUILayoutUtility.GetRect(400, 400);
                
                HandlePreviewInput(previewRect);
                RenderPreview(previewRect);
                
                // Show current face texture indices in preview
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Current Block Face Layout:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Top: {_editingBlock.faceTextures.topFace}, Bottom: {_editingBlock.faceTextures.bottomFace}");
                EditorGUILayout.LabelField($"North: {_editingBlock.faceTextures.northFace}, South: {_editingBlock.faceTextures.southFace}");
                EditorGUILayout.LabelField($"East: {_editingBlock.faceTextures.eastFace}, West: {_editingBlock.faceTextures.westFace}");
                
                // Debug info
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Debug Info:", EditorStyles.boldLabel);
                if (_database?.textureArray != null)
                {
                    try
                    {
                        EditorGUILayout.LabelField($"Texture Array: {_database.textureArray.name}");
                        EditorGUILayout.LabelField($"Array Depth: {_database.textureArray.depth}");
                        EditorGUILayout.LabelField($"Array Format: {_database.textureArray.format}");
                        EditorGUILayout.LabelField($"Array Size: {_database.textureArray.width}x{_database.textureArray.height}");
                    }
                    catch (System.Exception e)
                    {
                        EditorGUILayout.HelpBox($"Error reading texture array: {e.Message}", MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No texture array assigned! Please assign one in the Block Definition Database.", MessageType.Warning);
                }
                
                // Test buttons
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Test Functions:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Different Faces"))
                {
                    // Set different texture indices to test each face
                    if (_editingBlock != null)
                    {
                        int maxIndex = 5; // Default fallback
                        try
                        {
                            if (_database?.textureArray != null)
                            {
                                maxIndex = _database.textureArray.depth - 1;
                            }
                        }
                        catch (System.Exception)
                        {
                            maxIndex = 5; // Safe fallback
                        }
                        
                        _editingBlock.faceTextures.topFace = Mathf.Min(0, maxIndex);
                        _editingBlock.faceTextures.bottomFace = Mathf.Min(1, maxIndex);
                        _editingBlock.faceTextures.northFace = Mathf.Min(2, maxIndex);
                        _editingBlock.faceTextures.southFace = Mathf.Min(3, maxIndex);
                        _editingBlock.faceTextures.eastFace = Mathf.Min(4, maxIndex);
                        _editingBlock.faceTextures.westFace = Mathf.Min(5, maxIndex);
                        UpdatePreviewMaterial();
                        Repaint();
                    }
                }
                if (GUILayout.Button("Reset to 0"))
                {
                    if (_editingBlock != null)
                    {
                        _editingBlock.SetAllFaces(0);
                        UpdatePreviewMaterial();
                        Repaint();
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                if (GUILayout.Button("Force Material Update"))
                {
                    UpdatePreviewMaterial();
                    Repaint();
                }
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Texture Extraction"))
                {
                    TestTextureExtraction();
                }
                if (GUILayout.Button("Use Fallback Textures"))
                {
                    UseFallbackTextures();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Show Wireframe"))
                {
                    SetWireframeMode(true);
                    Repaint();
                }
                if (GUILayout.Button("Show Solid"))
                {
                    SetWireframeMode(false);
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Show Double-Sided"))
                {
                    SetDoubleSided(true);
                    Repaint();
                }
                if (GUILayout.Button("Show Single-Sided"))
                {
                    SetDoubleSided(false);
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("3D Preview not available. Please assign a texture array to the Block Definition Database.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            Event current = Event.current;
            
            if (previewRect.Contains(current.mousePosition))
            {
                if (current.type == EventType.MouseDown && current.button == 0)
                {
                    _isDragging = true;
                    _lastMousePosition = current.mousePosition;
                    current.Use();
                }
                else if (current.type == EventType.MouseDrag && _isDragging)
                {
                    Vector2 delta = current.mousePosition - _lastMousePosition;
                    _previewDir += delta;
                    _lastMousePosition = current.mousePosition;
                    current.Use();
                    Repaint();
                }
                else if (current.type == EventType.MouseUp)
                {
                    _isDragging = false;
                    current.Use();
                }
                else if (current.type == EventType.ScrollWheel)
                {
                    // Zoom with scroll wheel
                    _previewZoom = Mathf.Clamp(_previewZoom + current.delta.y * 0.1f, 1f, 10f);
                    current.Use();
                    Repaint();
                }
            }
        }

        private void RenderPreview(Rect previewRect)
        {
            if (_previewCube == null || _faceMeshes == null) return;

            // Set camera position and rotation
            float distance = _previewZoom;
            _previewRenderUtility.camera.transform.position = Quaternion.Euler(-_previewDir.y, -_previewDir.x, 0) * Vector3.forward * distance;
            _previewRenderUtility.camera.transform.LookAt(Vector3.zero, Vector3.up);

            // Position the cube
            _previewCube.transform.position = Vector3.zero;
            _previewCube.transform.rotation = Quaternion.identity;

            // Render the preview
            _previewRenderUtility.BeginPreview(previewRect, GUIStyle.none);
            
            // Render each face mesh individually
            for (int i = 0; i < _faceMeshes.Length; i++)
            {
                if (_faceMeshes[i] != null && _faceMaterials[i] != null)
                {
                    var meshFilter = _faceMeshes[i].GetComponent<MeshFilter>();
                    if (meshFilter?.sharedMesh != null)
                    {
                        _previewRenderUtility.DrawMesh(
                            meshFilter.sharedMesh,
                            _faceMeshes[i].transform.localToWorldMatrix,
                            _faceMaterials[i],
                            0
                        );
                    }
                }
            }
            
            _previewRenderUtility.camera.Render();
            
            Texture resultTexture = _previewRenderUtility.EndPreview();
            GUI.DrawTexture(previewRect, resultTexture, ScaleMode.StretchToFill, false);
        }

        private void AddNewBlock()
        {
            var newBlockId = (BlockId)(_database.Blocks.Count);
            var newBlock = new BlockDefinition($"Block {newBlockId}", newBlockId);
            
            _database.AddBlock(newBlock);
            EditorUtility.SetDirty(_database);
            
            SelectBlock(_database.Blocks.Count - 1);
        }

        private void RemoveCurrentBlock()
        {
            if (_editingBlock == null) return;

            _database.RemoveBlock(_editingBlock.blockId);
            EditorUtility.SetDirty(_database);
            
            if (_selectedBlockIndex >= _database.Blocks.Count)
            {
                _selectedBlockIndex = _database.Blocks.Count - 1;
            }
            
            if (_selectedBlockIndex >= 0)
            {
                SelectBlock(_selectedBlockIndex);
            }
            else
            {
                _currentBlock = null;
                _editingBlock = null;
            }
        }

        private void SaveCurrentBlock()
        {
            if (_editingBlock == null) return;

            _database.UpdateBlock(_editingBlock);
            EditorUtility.SetDirty(_database);
            
            _currentBlock = _editingBlock;
            
            Debug.Log($"Saved block: {_editingBlock.blockName}");
        }

        private void SetWireframeMode(bool wireframe)
        {
            if (_faceMaterials == null) return;
            
            for (int i = 0; i < _faceMaterials.Length; i++)
            {
                if (_faceMaterials[i] != null)
                {
                    if (wireframe)
                    {
                        // Set materials to wireframe mode and make them double-sided
                        _faceMaterials[i].SetFloat("_Cull", 0); // Cull Off (double-sided)
                        _faceMaterials[i].SetFloat("_Mode", 1); // Set to transparent mode
                        _faceMaterials[i].SetFloat("_ZWrite", 0);
                        _faceMaterials[i].color = new Color(1, 1, 1, 0.5f); // Semi-transparent white
                        _faceMaterials[i].mainTexture = null; // Remove texture to see wireframe clearly
                    }
                    else
                    {
                        // Restore normal rendering
                        _faceMaterials[i].SetFloat("_Cull", 2); // Cull Back (normal)
                        _faceMaterials[i].SetFloat("_Mode", 0); // Set to opaque mode
                        _faceMaterials[i].SetFloat("_ZWrite", 1);
                        _faceMaterials[i].color = Color.white;
                    }
                }
            }
            
            // Update materials with current textures if not in wireframe mode
            if (!wireframe)
            {
                UpdatePreviewMaterial();
            }
        }

        private void SetDoubleSided(bool doubleSided)
        {
            if (_faceMaterials == null) return;
            
            for (int i = 0; i < _faceMaterials.Length; i++)
            {
                if (_faceMaterials[i] != null)
                {
                    if (doubleSided)
                    {
                        _faceMaterials[i].SetFloat("_Cull", 0); // Cull Off (double-sided)
                    }
                    else
                    {
                        _faceMaterials[i].SetFloat("_Cull", 2); // Cull Back (single-sided)
                    }
                }
            }
        }

        private void TestTextureExtraction()
        {
            if (_database?.textureArray == null)
            {
                Debug.LogWarning("No texture array assigned to test");
                return;
            }

            Debug.Log($"Testing texture extraction from array: {_database.textureArray.name}");
            Debug.Log($"Array format: {_database.textureArray.format}");
            Debug.Log($"Array size: {_database.textureArray.width}x{_database.textureArray.height}");
            Debug.Log($"Array depth: {_database.textureArray.depth}");
            Debug.Log($"Array mip count: {_database.textureArray.mipmapCount}");

            // Test extracting the first few slices
            for (int i = 0; i < Mathf.Min(3, _database.textureArray.depth); i++)
            {
                var extractedTexture = ExtractTextureFromArray(_database.textureArray, i);
                if (extractedTexture != null)
                {
                    Debug.Log($"Successfully extracted slice {i}: {extractedTexture.name} ({extractedTexture.width}x{extractedTexture.height})");
                    
                    // Test a few pixels
                    var pixels = extractedTexture.GetPixels(0, 0, 4, 4);
                    Debug.Log($"Sample pixels from slice {i}: {string.Join(", ", pixels.Take(4))}");
                    
                    DestroyImmediate(extractedTexture); // Clean up test texture
                }
                else
                {
                    Debug.LogError($"Failed to extract slice {i}");
                }
            }
        }

        private void UseFallbackTextures()
        {
            if (_faceMaterials == null || _editingBlock == null) return;

            Debug.Log("Using fallback textures for preview");

            var faceIndices = new int[]
            {
                _editingBlock.faceTextures.topFace,
                _editingBlock.faceTextures.bottomFace,
                _editingBlock.faceTextures.northFace,
                _editingBlock.faceTextures.southFace,
                _editingBlock.faceTextures.eastFace,
                _editingBlock.faceTextures.westFace
            };

            for (int i = 0; i < 6; i++)
            {
                if (_faceMaterials[i] != null)
                {
                    // Clean up old texture
                    if (_faceMaterials[i].mainTexture != null && _faceMaterials[i].mainTexture is Texture2D)
                    {
                        DestroyImmediate(_faceMaterials[i].mainTexture);
                    }

                    // Create fallback texture
                    var fallbackTexture = CreateFallbackTexture(faceIndices[i], 64, 64);
                    _faceMaterials[i].mainTexture = fallbackTexture;
                    _faceMaterials[i].color = Color.white;
                }
            }

            Repaint();
        }
    }
}
