using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

public static class AddScript
{
    [MenuItem("GameObject/Create From Script #a", false, 10)]
    private static void CreateFromSelectedScript()
    {
        if (TryCreateGameObjectFromSelectedScript())
            return;

        CreateMonoBehaviourScript();
    }

    private static bool TryCreateGameObjectFromSelectedScript()
    {
        if (Selection.activeObject is not MonoScript script)
            return false;

        System.Type type = script.GetClass();

        if (type == null || !type.IsSubclassOf(typeof(MonoBehaviour)))
            return false;

        GameObject go = new GameObject(type.Name);
        go.AddComponent(type);

        Undo.RegisterCreatedObjectUndo(go, "Create GameObject From Script");
        Selection.activeGameObject = go;

        return true;
    }

    private static void CreateMonoBehaviourScript()
    {
        string folder = GetSelectedFolder();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(folder, "NewMonoBehaviour.cs"));

        Texture2D icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;

        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateMonoBehaviourAction>(),
            path,
            icon,
            null);
    }

    private static string GetSelectedFolder()
    {
        Object selected = Selection.activeObject;

        if (selected == null)
            return "Assets";

        string path = AssetDatabase.GetAssetPath(selected);

        if (AssetDatabase.IsValidFolder(path))
            return path;

        return Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
    }

    private class CreateMonoBehaviourAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string className = Path.GetFileNameWithoutExtension(pathName);

            string content =
$@"using UnityEngine;

public class {className} : MonoBehaviour
{{

}}";

            File.WriteAllText(pathName, content);

            AssetDatabase.ImportAsset(pathName);

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(pathName);
            ProjectWindowUtil.ShowCreatedAsset(asset);
        }
    }
}