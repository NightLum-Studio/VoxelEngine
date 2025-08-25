# TextureArrayShaderGUI (editor)

Location: `Assets/ActualStuff/EditorStuff/Editor/TextureArrayShaderGUI.cs`

Summary
- Custom ShaderGUI for materials using Texture2DArray inputs
- Exposes slots and toggles shader keywords based on assignments

Properties handled
- _BaseMapArray, _BaseColor
- Ambient: _AmbientStrength, _AmbientColor
- Normal: _BumpMapArray, _BumpScale (enables _NORMALMAP when assigned)
- Metallic/Smoothness: _MetallicGlossMapArray, _Metallic, _Smoothness (enables _METALLICSPECGLOSSMAP when assigned)
- Emission: _EmissionMapArray, _EmissionColor
- Texture Array: _ArraySliceCount (info)
- Alpha testing: _Cutoff

Related
- [BlockEditor](block-editor.md)

Back to overview: ../overview.md
