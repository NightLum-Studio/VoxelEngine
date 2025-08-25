using UnityEngine;
using UnityEditor;

public class TextureArrayShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Get the material
        Material material = materialEditor.target as Material;
        
        EditorGUILayout.LabelField("Texture Arrays", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Main texture array
        var baseMapArray = FindProperty("_BaseMapArray", properties, false);
        if (baseMapArray != null)
        {
            materialEditor.TextureProperty(baseMapArray, "Albedo Texture Array");
        }
        
        // Base color
        var baseColor = FindProperty("_BaseColor", properties, false);
        if (baseColor != null)
        {
            materialEditor.ColorProperty(baseColor, "Base Color");
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ambient Lighting", EditorStyles.boldLabel);
        
        // Ambient lighting controls
        var ambientStrength = FindProperty("_AmbientStrength", properties, false);
        if (ambientStrength != null)
        {
            materialEditor.FloatProperty(ambientStrength, "Ambient Light Strength");
        }
        
        var ambientColor = FindProperty("_AmbientColor", properties, false);
        if (ambientColor != null)
        {
            materialEditor.ColorProperty(ambientColor, "Ambient Light Color");
        }
        
        EditorGUILayout.Space();
        
        // Normal map array
        var bumpMapArray = FindProperty("_BumpMapArray", properties, false);
        if (bumpMapArray != null)
        {
            materialEditor.TextureProperty(bumpMapArray, "Normal Map Array");
            
            // Auto-enable/disable _NORMALMAP keyword based on texture assignment
            if (bumpMapArray.textureValue != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }
        }
        
        var bumpScale = FindProperty("_BumpScale", properties, false);
        if (bumpScale != null)
        {
            materialEditor.FloatProperty(bumpScale, "Normal Scale");
        }
        
        EditorGUILayout.Space();
        
        // Metallic/Smoothness
        var metallicGlossArray = FindProperty("_MetallicGlossMapArray", properties, false);
        if (metallicGlossArray != null)
        {
            materialEditor.TextureProperty(metallicGlossArray, "Metallic/Smoothness Array");
            
            // Auto-enable/disable _METALLICSPECGLOSSMAP keyword based on texture assignment
            if (metallicGlossArray.textureValue != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
            }
        }
        
        var metallic = FindProperty("_Metallic", properties, false);
        if (metallic != null)
        {
            materialEditor.FloatProperty(metallic, "Metallic");
        }
        
        var smoothness = FindProperty("_Smoothness", properties, false);
        if (smoothness != null)
        {
            materialEditor.FloatProperty(smoothness, "Smoothness");
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Note: Ambient Occlusion is handled automatically by Unity's lighting system", EditorStyles.helpBox);
        
        EditorGUILayout.Space();
        
        // Emission array
        var emissionArray = FindProperty("_EmissionMapArray", properties, false);
        if (emissionArray != null)
        {
            materialEditor.TextureProperty(emissionArray, "Emission Array");
        }
        
        var emissionColor = FindProperty("_EmissionColor", properties, false);
        if (emissionColor != null)
        {
            materialEditor.ColorProperty(emissionColor, "Emission Color");
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Texture Array Settings", EditorStyles.boldLabel);
        
        var arraySliceCount = FindProperty("_ArraySliceCount", properties, false);
        if (arraySliceCount != null)
        {
            materialEditor.FloatProperty(arraySliceCount, "Array Slice Count");
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Alpha Testing", EditorStyles.boldLabel);
        
        var cutoff = FindProperty("_Cutoff", properties, false);
        if (cutoff != null)
        {
            materialEditor.FloatProperty(cutoff, "Alpha Cutoff");
        }
        
        // Show remaining properties in default layout
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shader Keywords", EditorStyles.boldLabel);
        
        // Show active keywords for debugging
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Toggle("_NORMALMAP", material.IsKeywordEnabled("_NORMALMAP"));
        EditorGUILayout.Toggle("_METALLICSPECGLOSSMAP", material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Other Properties", EditorStyles.boldLabel);
        
        // Draw remaining properties
        foreach (var prop in properties)
        {
            if (!IsPropertyHandledAbove(prop.name))
            {
                materialEditor.DefaultShaderProperty(prop, prop.displayName);
            }
        }
    }
    
    private bool IsPropertyHandledAbove(string propName)
    {
        return propName == "_BaseMapArray" ||
               propName == "_BaseColor" ||
               propName == "_AmbientStrength" ||
               propName == "_AmbientColor" ||
               propName == "_BumpMapArray" ||
               propName == "_BumpScale" ||
               propName == "_MetallicGlossMapArray" ||
               propName == "_Metallic" ||
               propName == "_Smoothness" ||

               propName == "_EmissionMapArray" ||
               propName == "_EmissionColor" ||
               propName == "_ArraySliceCount" ||
               propName == "_Cutoff";
    }
}
