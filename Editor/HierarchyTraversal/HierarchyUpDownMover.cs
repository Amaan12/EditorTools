using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HierarchyUpDownMover
{
    private static System.Type sceneHierarchyWindowType;

    static HierarchyUpDownMover()
    {
        sceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
    }

    private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.alt)
        {
            if (e.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(true);
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(false);
                e.Use();
            }
        }
    }

    [MenuItem("GameObject/Move Sibling Up", false, -10)]
    public static void MoveUp()
    {
        MoveSelection(true);
    }

    [MenuItem("GameObject/Move Sibling Up", true)]
    public static bool ValidateMoveUp()
    {
        return IsHierarchyWindowFocused() && Selection.activeGameObject != null;
    }

    [MenuItem("GameObject/Move Sibling Down", false, -10)]
    public static void MoveDown()
    {
        MoveSelection(false);
    }

    [MenuItem("GameObject/Move Sibling Down", true)]
    public static bool ValidateMoveDown()
    {
        return IsHierarchyWindowFocused() && Selection.activeGameObject != null;
    }

    private static bool IsHierarchyWindowFocused()
    {
        var focused = EditorWindow.focusedWindow;
        return focused != null && focused.GetType().Name == "SceneHierarchyWindow";
    }

    private static void MoveSelection(bool moveUp)
    {
        var activeGO = Selection.activeGameObject;
        if (activeGO == null) return;

        var expandedIDs = GetExpandedIDs();
        var visibleObjects = GetVisibleHierarchyObjects(expandedIDs);

        int idx = visibleObjects.IndexOf(activeGO);
        if (idx == -1) return;

        if (moveUp)
        {
            if (idx > 0)
            {
                var targetGO = visibleObjects[idx - 1];
                MoveGameObjectUp(activeGO, targetGO);
            }
        }
        else
        {
            var parent = activeGO.transform.parent;
            if (parent != null && activeGO.transform.GetSiblingIndex() == parent.childCount - 1)
            {
                // Last sibling of a parent: unparent and move below its parent
                var grandparent = parent.parent;
                var parentIndex = parent.GetSiblingIndex();
                MoveGameObject(activeGO, grandparent, activeGO.scene, parentIndex + 1);
            }
            else if (idx < visibleObjects.Count - 1)
            {
                var targetGO = visibleObjects[idx + 1];
                MoveGameObjectDown(activeGO, targetGO, expandedIDs);
            }
        }

        // Keep selection on the active object
        Selection.activeGameObject = activeGO;
        EditorApplication.RepaintHierarchyWindow();
    }

    private static void MoveGameObjectUp(GameObject activeGO, GameObject targetGO)
    {
        var targetParent = targetGO.transform.parent;
        var targetScene = targetGO.scene;
        var targetIndex = targetGO.transform.GetSiblingIndex();

        // Check if targetGO is the last sibling of its parent,
        // and activeGO is not already a child of that same parent (entering from below)
        if (targetParent != null && targetIndex == targetParent.childCount - 1 && activeGO.transform.parent != targetParent)
        {
            // Enter parent from below, going below the last sibling
            MoveGameObject(activeGO, targetParent, targetScene, targetIndex + 1);
        }
        else
        {
            // Normal move up (above targetGO)
            MoveGameObject(activeGO, targetParent, targetScene, targetIndex);
        }
    }

    private static void MoveGameObjectDown(GameObject activeGO, GameObject targetGO, HashSet<int> expandedIDs)
    {
        // Check if targetGO has visible children
        bool targetHasVisibleChildren = targetGO.transform.childCount > 0 && expandedIDs.Contains(targetGO.GetInstanceID());

        if (targetHasVisibleChildren)
        {
            // Move as first child of targetGO
            MoveGameObject(activeGO, targetGO.transform, targetGO.scene, 0);
        }
        else
        {
            // Move as sibling after targetGO
            var targetParent = targetGO.transform.parent;
            var targetScene = targetGO.scene;
            var targetIndex = targetGO.transform.GetSiblingIndex();

            bool sameParent = activeGO.transform.parent == targetParent;
            int newSiblingIndex = sameParent ? targetIndex : targetIndex + 1;

            MoveGameObject(activeGO, targetParent, targetScene, newSiblingIndex);
        }
    }

    private static void MoveGameObject(GameObject go, Transform targetParent, Scene targetScene, int targetSiblingIndex)
    {
        // 1. Scene transition if needed
        if (targetParent == null)
        {
            if (go.scene != targetScene)
            {
                if (go.transform.parent != null)
                {
                    Undo.SetTransformParent(go.transform, null, "Move GameObject");
                }
                Undo.MoveGameObjectToScene(go, targetScene, "Move GameObject");
            }
            else if (go.transform.parent != null)
            {
                Undo.SetTransformParent(go.transform, null, "Move GameObject");
            }

            Undo.RegisterCompleteObjectUndo(go.transform, "Move GameObject");
            go.transform.SetSiblingIndex(targetSiblingIndex);
        }
        else
        {
            // Moving to a parent
            if (go.transform.parent != targetParent)
            {
                Undo.SetTransformParent(go.transform, targetParent, "Move GameObject");
            }

            Undo.RegisterChildrenOrderUndo(targetParent, "Move GameObject");
            go.transform.SetSiblingIndex(targetSiblingIndex);
        }

        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetScene);
            if (go.scene != targetScene)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }

    private static List<GameObject> GetVisibleHierarchyObjects(HashSet<int> expandedIDs)
    {
        var visibleObjects = new List<GameObject>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                TraverseVisible(root, visibleObjects, expandedIDs);
            }
        }

        return visibleObjects;
    }

    private static void TraverseVisible(GameObject go, List<GameObject> list, HashSet<int> expandedIDs)
    {
        list.Add(go);

        if (go.transform.childCount > 0 && expandedIDs.Contains(go.GetInstanceID()))
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                TraverseVisible(go.transform.GetChild(i).gameObject, list, expandedIDs);
            }
        }
    }

    private static HashSet<int> GetExpandedIDs()
    {
        var expandedSet = new HashSet<int>();
        if (sceneHierarchyWindowType == null) return expandedSet;

        var lastInteracted = sceneHierarchyWindowType.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (lastInteracted == null)
        {
            var windows = Resources.FindObjectsOfTypeAll(sceneHierarchyWindowType);
            if (windows != null && windows.Length > 0)
            {
                lastInteracted = windows[0];
            }
        }

        if (lastInteracted != null)
        {
            var getExpandedIDs = sceneHierarchyWindowType.GetMethod("GetExpandedIDs", BindingFlags.NonPublic | BindingFlags.Instance);
            var expanded = getExpandedIDs?.Invoke(lastInteracted, null) as int[];
            if (expanded != null)
            {
                foreach (var id in expanded)
                {
                    expandedSet.Add(id);
                }
            }
        }

        return expandedSet;
    }
}
