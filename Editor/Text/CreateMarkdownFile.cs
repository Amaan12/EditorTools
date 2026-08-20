using UnityEngine;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using System.IO;

public class CreateMarkdownFile
{
    // Adds the option to the right-click Create menu. Priority 81 puts it near other text/script assets.
    [MenuItem("Assets/Create/Markdown File", false, -1000)]
    public static void CreateMarkdown()
    {
        // Get the default text asset icon to make it look native
        Texture2D icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;

        // Triggers the creation and instant renaming state in the Project window
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<DoCreateMarkdownFile>(),
            "MarkdownFile.md", // The default name requested
            icon,
            null
        );
    }
}

// This class handles what actually happens when you press 'Enter' after renaming
public class DoCreateMarkdownFile : EndNameEditAction
{
    public override void Action(int instanceId, string pathName, string resourceFile)
    {
        // 1. Create the actual file on disk. You can add default markdown text here if you want.
        File.WriteAllText(pathName, "");

        // 2. Tell Unity to import the newly created file so it appears in the database
        AssetDatabase.ImportAsset(pathName);

        // 3. Load the asset into memory
        Object asset = AssetDatabase.LoadAssetAtPath<Object>(pathName);

        // 4. Highlight/Select the newly created asset in the Project window
        ProjectWindowUtil.ShowCreatedAsset(asset);
    }
}