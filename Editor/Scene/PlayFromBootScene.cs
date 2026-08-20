using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class PlayFromBootScene
{
    private static bool initialized;

    static PlayFromBootScene()
    {
        EditorApplication.update += TryCreateToolbarButton;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void TryCreateToolbarButton()
    {
        if (initialized)
            return;

        var toolbarType =
            typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

        if (toolbarType == null)
            return;

        var toolbars =
            Resources.FindObjectsOfTypeAll(toolbarType);

        if (toolbars.Length == 0)
            return;

        var toolbar = toolbars[0];

        var rootField =
            toolbarType.GetField(
                "m_Root",
                BindingFlags.NonPublic | BindingFlags.Instance);

        if (rootField == null)
            return;

        var root =
            rootField.GetValue(toolbar) as VisualElement;

        if (root == null)
            return;

        var playModeContainer = root.Q("PlayMode");

        if (playModeContainer == null)
            return;

        if (playModeContainer.Q("PlayFromSceneZeroButton") != null)
        {
            initialized = true;
            return;
        }

        var buttonContainer = new IMGUIContainer(() =>
        {
            if (GUILayout.Button(
                    new GUIContent("▶0", "Play from Build Settings Scene 0"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(32)))
            {
                PlayFromSceneZero();
            }
        });

        buttonContainer.name = "PlayFromSceneZeroButton";

        playModeContainer.Insert(0, buttonContainer);

        initialized = true;
    }

    private static void PlayFromSceneZero()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var firstScene = EditorBuildSettings.scenes
            .FirstOrDefault(scene => scene.enabled);

        if (firstScene == null)
        {
            Debug.LogError(
                "No enabled scenes found in Build Settings.");
            return;
        }

        var bootScene =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                firstScene.path);

        if (bootScene == null)
        {
            Debug.LogError(
                $"Failed to load SceneAsset at path: {firstScene.path}");
            return;
        }

        EditorSceneManager.playModeStartScene = bootScene;

        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }
}