using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

/// <summary>
/// Ctrl+Tab / Ctrl+Shift+Tab cycles through the docked tabs in whichever
/// dock group the cursor is hovering over — no click required, works with
/// multiple windows simultaneously.
/// </summary>
public static class TabNavigator
{
    // ── Binding flag aliases ─────────────────────────────────────────────────

    private const BindingFlags NonPublicInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // ── Cached reflection ────────────────────────────────────────────────────
    //    Resolved once on the first tab-switch, then reused until domain reload.
    //    EditorWindow.m_Parent is stable across all versions; cache it statically.

    private static readonly FieldInfo s_ParentField =
        typeof(EditorWindow).GetField("m_Parent", NonPublicInstance);

    private static System.Type s_DockAreaType;       // UnityEditor.DockArea
    private static FieldInfo   s_PanesField;         // List<EditorWindow> m_Panes
    private static FieldInfo   s_SelectedIndexField; // int m_Selected
    private static MethodInfo  s_RepaintMethod;      // inherited from GUIView

    // ── Shortcuts ────────────────────────────────────────────────────────────

    [Shortcut("Tabs/Next Tab", KeyCode.Tab, ShortcutModifiers.Action)]
    private static void NextTab() => SwitchTab(+1);

    [Shortcut("Tabs/Previous Tab", KeyCode.Tab, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
    private static void PreviousTab() => SwitchTab(-1);

    // ── Core ─────────────────────────────────────────────────────────────────

    private static void SwitchTab(int direction)
    {
        // mouseOverWindow tracks the cursor independently of keyboard focus,
        // so the correct dock group is always targeted regardless of which
        // window is "active" or how many windows you have open.
        var hovered = EditorWindow.mouseOverWindow;
        if (hovered == null || s_ParentField == null) return;

        var dockArea = s_ParentField.GetValue(hovered);
        if (dockArea == null) return;

        // Bail out for floating / undocked windows — their parent is a plain
        // HostView that has no pane list.
        if (!EnsureReflectionCache(dockArea)) return;

        var panes = s_PanesField.GetValue(dockArea) as IList;
        if (panes == null || panes.Count <= 1) return;

        int current = (int)s_SelectedIndexField.GetValue(dockArea);
        int next    = (current + direction + panes.Count) % panes.Count;
        if (next == current) return;

        // Write the new index, then repaint the DockArea itself.
        // The DockArea owns both the tab bar and the content viewport, so a
        // single Repaint() on it is all that's needed — no per-window repaints,
        // no Focus() calls, no Hierarchy/Project repaints.
        s_SelectedIndexField.SetValue(dockArea, next);
        s_RepaintMethod?.Invoke(dockArea, null);
    }

    // ── Reflection helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Populates the cached reflection members the first time a DockArea is seen.
    /// Returns false if <paramref name="dockArea"/> is not a DockArea (e.g. a
    /// floating HostView), so the caller can exit cleanly.
    /// </summary>
    private static bool EnsureReflectionCache(object dockArea)
    {
        var type = dockArea.GetType();

        // Fast path — already resolved for this exact type.
        if (type == s_DockAreaType) return true;

        // DockArea is the only container that holds a pane list.
        // Floating windows use a plain HostView; reject them here.
        if (type.Name != "DockArea") return false;

        s_DockAreaType       = type;
        s_PanesField         = type.GetField("m_Panes",    NonPublicInstance);
        s_SelectedIndexField = type.GetField("m_Selected", NonPublicInstance);

        // Repaint() is declared on GUIView and inherited by DockArea;
        // searching AnyInstance on 'type' walks the full hierarchy automatically.
        s_RepaintMethod = type.GetMethod(
            "Repaint", AnyInstance, null, System.Type.EmptyTypes, null);

        if (s_PanesField == null || s_SelectedIndexField == null)
        {
            Debug.LogWarning(
                "[TabNavigator] Could not find expected internal fields on DockArea. " +
                "The Unity internal API may have changed — tab switching is disabled.");
            s_DockAreaType = null; // force retry next call in case of a domain reload
            return false;
        }

        return true;
    }
}