using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Auto-Generates Scene, Tag, Layer enums in Assets/Imported Assets/UnityEnums/UnityEnums.cs
/// </summary>
public static class UnityEnumsGenerator
{
    private const string SCENE_LABEL = "Scene";
    private const string OUTPUT_DIRECTORY = "Assets/Imported Assets/UnityEnums";
    private const string OUTPUT_FILE_NAME = "UnityEnums.cs";

    [MenuItem("Tools/Generate/Unity Enums")]
    public static void Generate()
    {
        if (!Directory.Exists(OUTPUT_DIRECTORY))
        {
            Directory.CreateDirectory(OUTPUT_DIRECTORY);
        }

        string outputPath = Path.Combine(OUTPUT_DIRECTORY, OUTPUT_FILE_NAME);

        var sb = new StringBuilder();

        sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT");
        sb.AppendLine("// Generated via Tools/Generate/Unity Enums");
        sb.AppendLine();
        // sb.AppendLine("namespace Project");
        // sb.AppendLine("{");

        GenerateScenes(sb);
        GenerateTags(sb);
        GenerateLayers(sb);

        // sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[UnityEnumsGenerator] Successfully generated enums at: {outputPath}");
    }

    // -----------------------------------------------------

    private static void GenerateScenes(StringBuilder sb)
    {
        // Finds all scene assets with the specified label (case-insensitive in AssetDatabase search)
        var guids = AssetDatabase.FindAssets($"t:SceneAsset l:{SCENE_LABEL}");

        var sceneNames = new HashSet<string>();
        var validNames = new List<string>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var rawName = Path.GetFileNameWithoutExtension(path);
            var sanitizedName = SanitizeIdentifier(rawName);

            if (string.IsNullOrEmpty(sanitizedName))
            {
                Debug.LogWarning($"[UnityEnumsGenerator] Scene '{rawName}' at '{path}' has an invalid identifier name. Skipping.");
                continue;
            }

            if (sceneNames.Add(sanitizedName))
            {
                validNames.Add(sanitizedName);
            }
            else
            {
                Debug.LogWarning($"[UnityEnumsGenerator] Duplicate scene enum name '{sanitizedName}' found from '{path}'. Skipping duplicate.");
            }
        }

        if (validNames.Count == 0)
        {
            Debug.LogWarning($"[UnityEnumsGenerator] No scenes found with label '{SCENE_LABEL}'. Tag your scene assets with the label '{SCENE_LABEL}' in the Inspector.");
        }

        sb.AppendLine("public enum SceneId");
        sb.AppendLine("{");

        for (int i = 0; i < validNames.Count; i++)
        {
            sb.Append("    ").Append(validNames[i]);
            if (i < validNames.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void GenerateTags(StringBuilder sb)
    {
        var tags = InternalEditorUtility.tags;
        var validTags = new List<string>();

        foreach (var tag in tags)
        {
            var sanitized = SanitizeIdentifier(tag);
            if (!string.IsNullOrEmpty(sanitized))
            {
                validTags.Add(sanitized);
            }
        }

        sb.AppendLine("public enum Tag");
        sb.AppendLine("{");

        for (int i = 0; i < validTags.Count; i++)
        {
            sb.Append("    ").Append(validTags[i]);
            if (i < validTags.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void GenerateLayers(StringBuilder sb)
    {
        var layers = InternalEditorUtility.layers;

        sb.AppendLine("[System.Flags]");
        sb.AppendLine("public enum LayerMaskId");
        sb.AppendLine("{");

        for (int i = 0; i < layers.Length; i++)
        {
            int index = LayerMask.NameToLayer(layers[i]);
            if (index < 0) continue;

            string enumName = SanitizeIdentifier(layers[i]);
            if (string.IsNullOrEmpty(enumName)) continue;

            sb.Append("    ").Append(enumName)
              .Append(" = 1 << ").Append(index).Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    // -----------------------------------------------------

    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sb = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c == '-' || c == '.')
            {
                sb.Append('_');
            }
        }

        string result = sb.ToString();
        if (string.IsNullOrEmpty(result)) return string.Empty;

        // C# identifiers cannot start with a digit
        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    // Optional helper context menu to quickly label selected scene assets
    [MenuItem("Assets/UnityEnums/Add Scene Label", true)]
    private static bool ValidateAddSceneLabel()
    {
        return Selection.activeObject is SceneAsset;
    }

    [MenuItem("Assets/UnityEnums/Add Scene Label")]
    private static void AddSceneLabel()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is SceneAsset)
            {
                var labels = new HashSet<string>(AssetDatabase.GetLabels(obj));
                if (labels.Add(SCENE_LABEL))
                {
                    AssetDatabase.SetLabels(obj, new List<string>(labels).ToArray());
                    Debug.Log($"[UnityEnumsGenerator] Added '{SCENE_LABEL}' label to: {AssetDatabase.GetAssetPath(obj)}");
                }
            }
        }
    }
}
