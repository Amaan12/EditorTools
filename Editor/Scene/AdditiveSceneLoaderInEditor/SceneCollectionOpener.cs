using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneCollectionOpener
{
    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceId, int line)
    {
        var asset =
            EditorUtility.InstanceIDToObject(instanceId)
            as SceneCollection;

        if (asset == null)
            return false;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return true;

        bool first = true;

        foreach (var sceneAsset in asset.scenes)
        {
            if (sceneAsset == null)
                continue;

            string path =
                AssetDatabase.GetAssetPath(sceneAsset);

            if (first)
            {
                EditorSceneManager.OpenScene(path);
                first = false;
            }
            else
            {
                EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
            }
        }

        return true;
    }
}