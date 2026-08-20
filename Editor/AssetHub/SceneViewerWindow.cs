using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace SceneViewer
{
    public class SceneViewerWindow : EditorWindow, IHasCustomMenu
    {
        [SerializeField] private SceneViewerData data;
        
        // Split Pane & UI State
        private float sidebarWidth = 150f;
        private float renderSidebarWidth = 150f;
        private bool isResizing = false;
        private Vector2 sidebarScroll;
        private Vector2 gridScroll;
        private string searchFilter = "";
        private string selectedCategoryName = "ALL"; // "ALL" or specific category path string
        private double lastScanTime;
        
        // Cache & Categories
        private List<string> foundAssetGuids = new List<string>();
        private Dictionary<string, CachedAssetInfo> cachedAssets = new Dictionary<string, CachedAssetInfo>();
        private Dictionary<string, List<string>> autoCategories = new Dictionary<string, List<string>>();
        private List<string> itemsToDraw = new List<string>();
        private List<DisplayItem> displayItems = new List<DisplayItem>();

        // Drag and drop state to allow reliable double clicks
        private string dragSourceGuid;
        private UnityEngine.Object dragSourceObject;

        // In-place filter editing state
        [SerializeField] private string customFilter;
        private bool isEditingFilter = false;
        private string filterEditString = "";
        private string lastUsedFilter = "";
        
        // Local selection state
        private string selectedGuid;
        private int cachedFirstVisible = 0;
        private int cachedLastVisible = 0;
        private Dictionary<string, ColorCode> colorCache = new Dictionary<string, ColorCode>();
        private string[] lastRawGuids = new string[0];
        [SerializeField] private bool isGridView = false;
        private int gridColumns = 4;
        private float totalLayoutHeight = 0f;

        // GUI Styles
        private GUIStyle categoryTitleStyle;
        private GUIStyle cardNameStyle;
        private Texture2D folderIcon;
        private Texture2D sceneIcon;
        private Texture2D collectionIcon;
        private Texture2D genericAssetIcon;
        private Texture2D refreshIcon;

        private struct CachedAssetInfo
        {
            public string guid;
            public string name;
            public string nameLower;
            public string path;
            public System.Type type;
            public Texture icon;
        }

        private struct DisplayItem
        {
            public bool isHeader;
            public string headerName;
            public List<string> guids;
            public float yOffset;
            public float height;
        }

        [MenuItem("Window/General/Asset Hub")]
        public static void OpenWindow()
        {
            // CreateWindow allows opening multiple instances of this window
            SceneViewerWindow window = CreateWindow<SceneViewerWindow>("Asset Hub");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Duplicate Window"), false, DuplicateWindow);
        }

        private void DuplicateWindow()
        {
            SceneViewerWindow window = CreateWindow<SceneViewerWindow>("Asset Hub");
            window.minSize = new Vector2(400, 300);
            window.customFilter = this.customFilter;
            window.selectedCategoryName = this.selectedCategoryName;
            window.searchFilter = this.searchFilter;
            window.isGridView = this.isGridView;
            window.Show();
            window.ScanAssets(true);
        }

        private void OnEnable()
        {
            sidebarWidth = EditorPrefs.GetFloat("SceneViewer_SidebarWidth", 150f);
            LoadData();
            if (data != null && string.IsNullOrEmpty(customFilter))
            {
                customFilter = data.customFilter;
            }
            LoadAssetsAndIcons();
            ScanAssets(true);

            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }



        private void OnProjectChanged()
        {
            ScanAssets(true);
            Repaint();
        }

        private void LoadData()
        {
            if (data == null)
            {
                data = SceneViewerData.instance;
            }
        }

        private void LoadAssetsAndIcons()
        {
            folderIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
            sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            collectionIcon = EditorGUIUtility.IconContent("d_ScriptableObject Icon").image as Texture2D;
            genericAssetIcon = EditorGUIUtility.IconContent("DefaultAsset Icon").image as Texture2D;
            refreshIcon = EditorGUIUtility.IconContent("Refresh").image as Texture2D;
        }

        private void ScanAssets(bool forceRefreshCache = false)
        {
            string searchString = string.IsNullOrEmpty(customFilter) ? "l:scene" : customFilter;
            titleContent = new GUIContent(searchString, EditorGUIUtility.IconContent("d_Project").image);

            if (data == null) return;

            bool filterChanged = (searchString != lastUsedFilter);
            lastUsedFilter = searchString;

            string[] guids = AssetDatabase.FindAssets(searchString);
            
            // Check if anything actually changed to avoid redundant processing
            bool changed = forceRefreshCache || filterChanged || (guids.Length != lastRawGuids.Length);
            if (!changed)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    if (guids[i] != lastRawGuids[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed && cachedAssets.Count > 0)
            {
                return;
            }

            lastRawGuids = guids;

            foundAssetGuids.Clear();
            cachedAssets.Clear();
            autoCategories.Clear();
            colorCache.Clear();

            if (data.colorMappings != null)
            {
                foreach (var mapping in data.colorMappings)
                {
                    if (mapping != null && !string.IsNullOrEmpty(mapping.guid))
                    {
                        colorCache[mapping.guid] = mapping.color;
                    }
                }
            }

            HashSet<string> uniqueGuids = new HashSet<string>();
            foreach (var guid in guids)
            {
                if (!uniqueGuids.Add(guid)) continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                // 1. Only show assets under the Assets/ folder
                if (!path.StartsWith("Assets/")) continue;

                // 2. Exclude folder directories themselves
                if (AssetDatabase.IsValidFolder(path)) continue;

                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null) continue;



                string name = Path.GetFileNameWithoutExtension(path);

                Texture icon = AssetDatabase.GetCachedIcon(path);
                if (icon == null)
                {
                    icon = genericAssetIcon;
                }

                CachedAssetInfo info = new CachedAssetInfo
                {
                    guid = guid,
                    name = name,
                    nameLower = name.ToLower(),
                    path = path,
                    type = type,
                    icon = icon
                };

                cachedAssets[guid] = info;
                foundAssetGuids.Add(guid); // Only register successfully cached assets

                // Group by folder path names upwards (root/subroot/lastfoldername)
                string category = GetCategoryNameForPath(path);
                if (!autoCategories.ContainsKey(category))
                {
                    autoCategories[category] = new List<string>();
                }
                autoCategories[category].Add(guid);
            }

            // Fallback selectedCategoryName to ALL if it no longer exists
            if (selectedCategoryName != "ALL" && !autoCategories.ContainsKey(selectedCategoryName))
            {
                selectedCategoryName = "ALL";
            }

            RebuildItemsToDraw();
        }

        private string GetCategoryNameForPath(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directory)) return "Root";

            directory = directory.Replace('\\', '/');
            string[] parts = directory.Split('/');
            if (parts.Length <= 1) return "Root";

            List<string> subFolders = new List<string>();
            for (int i = 1; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    subFolders.Add(parts[i]);
                }
            }

            if (subFolders.Count == 0) return "Root";
            return string.Join("/", subFolders);
        }

        private void InitStyles()
        {
            if (categoryTitleStyle == null)
            {
                categoryTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                categoryTitleStyle.fontSize = 10;
            }
            categoryTitleStyle.normal.textColor = SidebarTextNormal;

            if (cardNameStyle == null)
            {
                cardNameStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                cardNameStyle.fontSize = 10;
                cardNameStyle.fontStyle = FontStyle.Bold;
                cardNameStyle.alignment = TextAnchor.MiddleCenter;
            }
            cardNameStyle.normal.textColor = RowTextNormal;
        }

        private Texture2D MakeSolidTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            LoadData();
            InitStyles();

            if (data == null)
            {
                EditorGUILayout.HelpBox("Could not load/create settings data asset.", MessageType.Error);
                return;
            }

            // (Ctrl+A is handled locally after text field drawing)

            // Sync layout width only on Layout event to prevent mid-frame mismatches
            if (Event.current.type == EventType.Layout)
            {
                renderSidebarWidth = sidebarWidth;
            }

            float currentSidebarWidth = renderSidebarWidth;
            bool isSidebarCollapsed = (currentSidebarWidth <= 40f);

            // Left Pane (Sidebar) Background & Content
            Rect sidebarRect = new Rect(0, 0, currentSidebarWidth, position.height);
            DrawThemeRect(sidebarRect, "ProjectBrowserSidebarBg", SidebarBgColor); // Theme background
            GUILayout.BeginArea(sidebarRect);
            DrawSidebar(currentSidebarWidth, isSidebarCollapsed);
            GUILayout.EndArea();

            // Draggable Resizer Line (splitter)
            Rect dividerRect = new Rect(currentSidebarWidth, 0, 5f, position.height);
            // Check if hovered or resizing to draw interactive highlight color
            bool isSplitterHovered = dividerRect.Contains(Event.current.mousePosition);
            Color splitterColor = (isResizing || isSplitterHovered) ? SidebarSelectBar : SplitterLineColor;
            
            // Draw splitter background & visual center accent line
            DrawThemeRect(dividerRect, "ProjectBrowserSidebarBg", SplitterBgColor);
            EditorGUI.DrawRect(new Rect(dividerRect.xMin + 2f, 0, 1f, position.height), splitterColor);
            EditorGUIUtility.AddCursorRect(dividerRect, MouseCursor.ResizeHorizontal);

            // Handle Resize drag events (updates sidebarWidth for the next frame/repaint)
            HandleResizerEvents(dividerRect);

            // Right Pane (Main Area) Background & Content
            Rect mainRect = new Rect(currentSidebarWidth + 5f, 0, position.width - currentSidebarWidth - 5f, position.height);
            DrawThemeRect(mainRect, "ProjectBrowserIconAreaBg", MainAreaBgColor); // Theme background
            GUILayout.BeginArea(mainRect);
            DrawMainArea(mainRect.width);
            GUILayout.EndArea();

            // Force repaint immediately when GUI values are altered (e.g. typing search/filter strings)
            if (GUI.changed)
            {
                Repaint();
            }
        }

        private void HandleResizerEvents(Rect resizerRect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && resizerRect.Contains(evt.mousePosition))
            {
                isResizing = true;
                evt.Use();
            }

            if (isResizing)
            {
                sidebarWidth = evt.mousePosition.x;
                sidebarWidth = Mathf.Clamp(sidebarWidth, 20f, 500f);
                EditorPrefs.SetFloat("SceneViewer_SidebarWidth", sidebarWidth);
                Repaint();
            }

            if (evt.type == EventType.MouseUp)
            {
                isResizing = false;
            }
        }

        private void DrawSidebar(float width, bool isCollapsed)
        {
            if (isCollapsed) return;

            // Header with premium pill badge
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            
            // Draw rounded pill badge for Active Label / Filter
            string displayFilter = string.IsNullOrEmpty(customFilter) ? "l:scene" : customFilter;
            string fullBadgeText = "FILTER: " + displayFilter;
            
            var badgeLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeLabelStyle.normal.textColor = BadgeText;
            badgeLabelStyle.fontStyle = FontStyle.Bold;
            badgeLabelStyle.alignment = TextAnchor.MiddleLeft;

            float badgeWidth = badgeLabelStyle.CalcSize(new GUIContent(fullBadgeText)).x + 24f;
            badgeWidth = Mathf.Max(120f, badgeWidth);

            Rect badgeRect = GUILayoutUtility.GetRect(badgeWidth, 18f);

            if (isEditingFilter)
            {
                GUI.SetNextControlName("FilterEditField");
                
                // If they press Enter or Escape (check before TextField consumes it)
                if (Event.current.isKey && Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return)
                    {
                        customFilter = filterEditString;
                        if (data != null)
                        {
                            data.customFilter = customFilter;
                            data.SaveData();
                        }
                        ScanAssets();
                        isEditingFilter = false;
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }
                    else if (Event.current.keyCode == KeyCode.Escape)
                    {
                        isEditingFilter = false;
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }
                }

                filterEditString = EditorGUI.TextField(new Rect(badgeRect.xMin, badgeRect.yMin, badgeRect.width, badgeRect.height), filterEditString);

                // Handle Ctrl+A/Cmd+A select all right after drawing
                Event currentEvt = Event.current;
                bool isSelectAllKey = (currentEvt.type == EventType.KeyDown && (currentEvt.control || currentEvt.command) && currentEvt.keyCode == KeyCode.A);
                bool isSelectAllCommand = ((currentEvt.type == EventType.ValidateCommand || currentEvt.type == EventType.ExecuteCommand) && currentEvt.commandName == "SelectAll");

                if (isSelectAllKey || isSelectAllCommand)
                {
                    if (GUI.GetNameOfFocusedControl() == "FilterEditField")
                    {
                        TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                        if (te != null)
                        {
                            if (currentEvt.type == EventType.ExecuteCommand || isSelectAllKey)
                            {
                                te.SelectAll();
                            }
                            currentEvt.Use();
                            Repaint();
                        }
                    }
                }
            }
            else
            {
                EditorGUI.DrawRect(badgeRect, BadgeBorder); // Outline
                EditorGUI.DrawRect(new Rect(badgeRect.xMin + 1f, badgeRect.yMin + 1f, badgeRect.width - 2f, badgeRect.height - 2f), BadgeFill); // Inner pill fill
                
                // green indicator dot inside badge
                float dotY = badgeRect.yMin + (badgeRect.height - 6f) / 2f;
                EditorGUI.DrawRect(new Rect(badgeRect.xMin + 8f, dotY, 6f, 6f), new Color(0.1f, 0.8f, 0.4f, 1.0f));

                GUI.Label(new Rect(badgeRect.xMin + 18f, badgeRect.yMin, badgeRect.width - 18f, badgeRect.height), new GUIContent(fullBadgeText, "Click to edit the active search query filter (e.g. l:scene t:SceneAsset)."), badgeLabelStyle);

                // Handle click on badge to enter edit mode
                if (Event.current.type == EventType.MouseDown && badgeRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
                {
                    isEditingFilter = true;
                    filterEditString = displayFilter;
                    Event.current.Use();
                    GUI.FocusControl("FilterEditField");
                }
            }

            GUILayout.FlexibleSpace();
            // Refresh button
            if (refreshIcon != null)
            {
                if (GUILayout.Button(new GUIContent(refreshIcon, "Reload Assets"), GUIStyle.none, GUILayout.Width(16), GUILayout.Height(16)))
                {
                    ScanAssets(true);
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("R", "Reload Assets"), GUILayout.Width(16), GUILayout.Height(16)))
                {
                    ScanAssets(true);
                }
            }

            // Settings button
            GUILayout.Space(4);
            var settingsIcon = EditorGUIUtility.IconContent("Settings").image as Texture2D;
            if (settingsIcon != null)
            {
                if (GUILayout.Button(new GUIContent(settingsIcon, "Open Settings"), GUIStyle.none, GUILayout.Width(16), GUILayout.Height(16)))
                {
                    Selection.activeObject = data;
                    EditorGUIUtility.PingObject(data);
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("S", "Open Settings"), GUILayout.Width(16), GUILayout.Height(16)))
                {
                    Selection.activeObject = data;
                    EditorGUIUtility.PingObject(data);
                }
            }

            // Duplicate window button
            GUILayout.Space(4);
            var duplicateIconContent = EditorGUIUtility.IconContent("d_TreeEditor.Duplicate");
            Texture2D duplicateIcon = (duplicateIconContent != null) ? duplicateIconContent.image as Texture2D : null;
            if (duplicateIcon != null)
            {
                if (GUILayout.Button(new GUIContent(duplicateIcon, "Duplicate Window"), GUIStyle.none, GUILayout.Width(16), GUILayout.Height(16)))
                {
                    DuplicateWindow();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("+", "Duplicate Window"), GUILayout.Width(16), GUILayout.Height(16)))
                {
                    DuplicateWindow();
                }
            }
            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            // Subtle divider
            Rect divRect = GUILayoutUtility.GetRect(10, 1);
            EditorGUI.DrawRect(divRect, SidebarDividerColor);
            GUILayout.Space(4);

            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);

            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(new GUIContent("CATEGORIES (AUTO)", "Categories automatically generated from asset subfolder structures in the project."), categoryTitleStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Default category button
            DrawSidebarCategoryButton("All Assets", "ALL", folderIcon);

            GUILayout.Space(4);

            // Automatic folder category groups
            List<string> sortedCategories = new List<string>(autoCategories.Keys);
            sortedCategories.Sort();
            for (int i = 0; i < sortedCategories.Count; i++)
            {
                DrawSidebarCategoryButton(sortedCategories[i], sortedCategories[i], folderIcon);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSidebarCategoryButton(string name, string id, Texture2D icon)
        {
            Rect rect = GUILayoutUtility.GetRect(10, 26); // Increased height to 26 for breathing room
            bool isSelected = (selectedCategoryName == id);
            bool isHovered = rect.Contains(Event.current.mousePosition);

            // Selection/Hover Backgrounds
            if (isSelected)
            {
                EditorGUI.DrawRect(rect, SidebarSelectBg); // Steel blue tint backdrop
                EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 3f, rect.height), SidebarSelectBar); // Left selection bar
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(rect, SidebarHoverBg); // Soft hover highlight
            }

            // Draw Icon
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.xMin + 8f, rect.yMin + 5f, 16f, 16f);
                GUI.color = isSelected ? Color.white : new Color(1f, 1f, 1f, 0.7f); // Muted opacity when unselected
                GUI.DrawTexture(iconRect, icon);
                GUI.color = Color.white;
            }

            // Draw Text Label
            Rect textRect = new Rect(rect.xMin + 30f, rect.yMin, rect.width - 30f, rect.height);
            var labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.fontSize = 11;
            if (isSelected)
            {
                labelStyle.normal.textColor = SidebarTextSelected;
                labelStyle.fontStyle = FontStyle.Bold;
            }
            else
            {
                labelStyle.normal.textColor = SidebarTextNormal;
            }

            GUI.Label(textRect, name, labelStyle);

            // Handle Selection click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                selectedCategoryName = id;
                RebuildItemsToDraw();
                Event.current.Use();
            }
        }

        private void DrawMainArea(float width)
        {
            // Dynamic column calculation for Grid View
            if (Event.current.type == EventType.Layout && isGridView)
            {
                float availableWidth = width - 30f; // Subtract scrollbar and margins
                float cardWidth = 88f; // Card width (80f) + spacing (8f)
                int cols = Mathf.Max(1, Mathf.FloorToInt(availableWidth / cardWidth));
                if (cols != gridColumns)
                {
                    gridColumns = cols;
                    RebuildItemsToDraw();
                }
            }

            // Custom premium header bar
            Rect headerRect = GUILayoutUtility.GetRect(width, 36f);
            EditorGUI.DrawRect(headerRect, HeaderBgColor); // Theme header background
            EditorGUI.DrawRect(new Rect(headerRect.xMin, headerRect.yMax - 1f, headerRect.width, 1f), HeaderBorderColor); // Bottom border
            

            // Search bar
            float searchWidth = 180f;
            float searchHeight = 20f;
            float searchX = headerRect.xMax - searchWidth - 12f;
            float searchY = headerRect.yMin + (headerRect.height - searchHeight) / 2f;
            Rect searchRect = new Rect(searchX, searchY, searchWidth, searchHeight);

            // Grid/List view toggle button
            GUIContent toggleContent = null;
            var iconName = isGridView ? "VerticalLayoutGroup Icon" : "GridLayoutGroup Icon";
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                toggleContent = new GUIContent(iconContent.image, isGridView ? "Switch to List View" : "Switch to Grid View");
            }
            else
            {
                toggleContent = new GUIContent(isGridView ? "L" : "G", isGridView ? "Switch to List View" : "Switch to Grid View");
            }

            Rect toggleRect = new Rect(searchX - 28f, searchY, 20f, 20f);
            bool isToggleHovered = toggleRect.Contains(Event.current.mousePosition);
            if (isToggleHovered)
            {
                EditorGUI.DrawRect(toggleRect, new Color(0.25f, 0.25f, 0.25f, 0.4f));
            }
            if (GUI.Button(toggleRect, toggleContent, GUIStyle.none))
            {
                isGridView = !isGridView;
                RebuildItemsToDraw();
                Repaint();
            }

            GUI.SetNextControlName("MainSearchField");
            string prevSearch = searchFilter;
            searchFilter = EditorGUI.TextField(searchRect, searchFilter, EditorStyles.toolbarSearchField);
            if (searchFilter != prevSearch)
            {
                RebuildItemsToDraw();
            }

            // Handle Ctrl+A/Cmd+A select all right after drawing
            Event currentEvt = Event.current;
            bool isSelectAllKey = (currentEvt.type == EventType.KeyDown && (currentEvt.control || currentEvt.command) && currentEvt.keyCode == KeyCode.A);
            bool isSelectAllCommand = ((currentEvt.type == EventType.ValidateCommand || currentEvt.type == EventType.ExecuteCommand) && currentEvt.commandName == "SelectAll");

            if (isSelectAllKey || isSelectAllCommand)
            {
                if (GUI.GetNameOfFocusedControl() == "MainSearchField")
                {
                    TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (te != null)
                    {
                        if (currentEvt.type == EventType.ExecuteCommand || isSelectAllKey)
                        {
                            te.SelectAll();
                        }
                        currentEvt.Use();
                        Repaint();
                    }
                }
            }

            gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
            GUILayout.Space(10);

            // Render List View of scenes (with virtualization for high performance)
            if (displayItems.Count == 0)
            {
                DrawEmptyState();
            }
            else
            {
                int totalCount = displayItems.Count;
                float topPadding = 10f;

                // Sync visible index bounds only on Layout event to prevent layout/repaint mismatches
                if (Event.current.type == EventType.Layout)
                {
                    float viewportMin = gridScroll.y - topPadding;
                    float viewportHeight = Mathf.Max(100f, position.height - 36f);
                    float viewportMax = viewportMin + viewportHeight;

                    // Find first and last visible items based on precalculated yOffset and height
                    cachedFirstVisible = 0;
                    cachedLastVisible = 0;
                    for (int i = 0; i < totalCount; i++)
                    {
                        float itemMin = displayItems[i].yOffset;
                        float itemMax = itemMin + displayItems[i].height;
                        if (itemMax >= viewportMin && itemMin <= viewportMax)
                        {
                            if (cachedLastVisible == 0) cachedFirstVisible = i;
                            cachedLastVisible = i + 1;
                        }
                    }
                    cachedFirstVisible = Mathf.Clamp(cachedFirstVisible, 0, totalCount - 1);
                    cachedLastVisible = Mathf.Clamp(cachedLastVisible, cachedFirstVisible, totalCount);
                }

                // Defensively clamp ranges for the current event frame
                int startVisible = Mathf.Clamp(cachedFirstVisible, 0, totalCount - 1);
                int endVisible = Mathf.Min(totalCount, cachedLastVisible);

                // Add blank spacing above to represent off-screen items
                float spaceAbove = 0f;
                if (startVisible > 0 && startVisible < displayItems.Count)
                {
                    spaceAbove = displayItems[startVisible].yOffset;
                }
                if (spaceAbove > 0f)
                {
                    GUILayout.Space(spaceAbove);
                }

                // Render only the visible elements
                for (int i = startVisible; i < endVisible; i++)
                {
                    if (i < displayItems.Count)
                    {
                        var item = displayItems[i];
                        if (item.isHeader)
                        {
                            DrawFolderHeaderRow(item.headerName);
                        }
                        else
                        {
                            if (isGridView)
                            {
                                Rect rowRect = GUILayoutUtility.GetRect(10, 90f);
                                DrawGridRow(rowRect, item.guids);
                            }
                            else
                            {
                                if (item.guids != null && item.guids.Count > 0)
                                {
                                    DrawAssetRow(item.guids[0]);
                                }
                            }
                        }
                    }
                }

                // Add blank spacing below to represent remaining off-screen items
                float spaceBelow = 0f;
                if (endVisible > 0 && endVisible < totalCount)
                {
                    spaceBelow = totalLayoutHeight - (displayItems[endVisible - 1].yOffset + displayItems[endVisible - 1].height);
                }
                if (spaceBelow > 0f)
                {
                    GUILayout.Space(spaceBelow);
                }
            }

            // Handle external drag/drop
            HandleMainAreaDragAndDrop();

            EditorGUILayout.EndScrollView();
        }

        private void DrawEmptyState()
        {
            GUILayout.BeginVertical();
            GUILayout.Space(60f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(260f));

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 13;
            titleStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 1.0f);
            GUILayout.Label("No Scenes Found", titleStyle);
            
            GUILayout.Space(6f);

            var subStyle = new GUIStyle(EditorStyles.miniLabel);
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.wordWrap = true;
            subStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f, 1.0f);
            string targetLabel = GetLabelFromFilter();
            if (!string.IsNullOrEmpty(targetLabel))
            {
                GUILayout.Label("Ensure your scenes match the custom filter and carry the '" + targetLabel + "' label, or drag and drop an asset here from the Project window to register it.", subStyle);
            }
            else
            {
                GUILayout.Label("Ensure your assets match the custom filter, or drag and drop assets here from the Project window.", subStyle);
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void RebuildItemsToDraw()
        {
            itemsToDraw.Clear();
            if (selectedCategoryName == "ALL")
            {
                itemsToDraw.AddRange(foundAssetGuids);
            }
            else if (autoCategories.ContainsKey(selectedCategoryName))
            {
                itemsToDraw.AddRange(autoCategories[selectedCategoryName]);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string searchLower = searchFilter.ToLower();
                itemsToDraw.RemoveAll(g => {
                    if (!cachedAssets.ContainsKey(g)) return true;
                    return !cachedAssets[g].nameLower.Contains(searchLower);
                });
            }

            // Group by category/folder path
            displayItems.Clear();

            // First, group the itemsToDraw GUIDs by their folder path
            Dictionary<string, List<string>> grouped = new Dictionary<string, List<string>>();
            foreach (var guid in itemsToDraw)
            {
                if (!cachedAssets.ContainsKey(guid)) continue;
                var info = cachedAssets[guid];
                string folder = GetCategoryNameForPath(info.path);
                if (!grouped.ContainsKey(folder))
                {
                    grouped[folder] = new List<string>();
                }
                grouped[folder].Add(guid);
            }

            // Sort the groups alphabetically
            List<string> folders = new List<string>(grouped.Keys);
            folders.Sort();

            foreach (var folder in folders)
            {
                // Add header display item
                displayItems.Add(new DisplayItem { isHeader = true, headerName = folder, guids = null });

                // Sort the assets in this folder by name
                var folderGuids = grouped[folder];
                folderGuids.Sort((a, b) => {
                    if (!cachedAssets.ContainsKey(a) || !cachedAssets.ContainsKey(b)) return 0;
                    return cachedAssets[a].name.CompareTo(cachedAssets[b].name);
                });

                // Add asset display items (chunked if grid view)
                if (isGridView)
                {
                    for (int j = 0; j < folderGuids.Count; j += gridColumns)
                    {
                        int count = Mathf.Min(gridColumns, folderGuids.Count - j);
                        List<string> chunk = folderGuids.GetRange(j, count);
                        displayItems.Add(new DisplayItem { isHeader = false, guids = chunk });
                    }
                }
                else
                {
                    foreach (var guid in folderGuids)
                    {
                        displayItems.Add(new DisplayItem { isHeader = false, guids = new List<string> { guid } });
                    }
                }
            }

            // Precalculate vertical yOffset and height for each layout element to allow mixed-height virtualization
            float currentY = 0f;
            for (int i = 0; i < displayItems.Count; i++)
            {
                DisplayItem item = displayItems[i];
                item.yOffset = currentY;
                item.height = item.isHeader ? 24f : (isGridView ? 90f : 24f);
                currentY += item.height;
                displayItems[i] = item;
            }
            totalLayoutHeight = currentY;
        }

        private List<string> GetItemsToDraw()
        {
            return itemsToDraw;
        }

        private void DrawFolderHeaderRow(string folderName)
        {
            Rect rect = GUILayoutUtility.GetRect(10, 24);

            // Draw a subtle dark background for headers
            EditorGUI.DrawRect(rect, FolderHeaderBg);

            // Folder icon
            if (folderIcon != null)
            {
                float iconY = rect.yMin + (rect.height - 16f) / 2f;
                Rect iconRect = new Rect(rect.xMin + 8f, iconY, 16f, 16f);
                GUI.color = FolderHeaderText;
                GUI.DrawTexture(iconRect, folderIcon);
                GUI.color = Color.white;
            }

            // Path name
            Rect textRect = new Rect(rect.xMin + 28f, rect.yMin, rect.width - 32f, rect.height);
            var headerStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.fontSize = 10;
            headerStyle.normal.textColor = FolderHeaderText;

            GUI.Label(textRect, folderName.ToUpper(), headerStyle);
        }

        private void DrawGridCard(Rect rect, string guid)
        {
            if (!cachedAssets.ContainsKey(guid)) return;
            CachedAssetInfo info = cachedAssets[guid];

            bool isHovered = rect.Contains(Event.current.mousePosition);
            bool isSelected = (guid == selectedGuid);

            // Draw card background
            if (isSelected)
            {
                EditorGUI.DrawRect(rect, RowSelectBg); // Rich deep blue
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(rect, RowHoverBg); // Soft hover highlight
            }
            else
            {
                EditorGUI.DrawRect(rect, CardBaseBg); // Subtle card base
            }

            // Draw border outline for premium feel
            Color outlineColor = isSelected ? CardBorderSelect : (isHovered ? CardBorderHover : CardBorderNormal);
            // Top border
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, 1f), outlineColor);
            // Bottom border
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), outlineColor);
            // Left border
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, 1f, rect.height), outlineColor);
            // Right border
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), outlineColor);

            // Draw Color-coded accent dot in the top-right corner
            ColorCode colorCode = ColorCode.Slate;
            colorCache.TryGetValue(guid, out colorCode);
            if (colorCode != ColorCode.Slate)
            {
                Color themeColor = GetColorForCode(colorCode);
                Rect dotRect = new Rect(rect.xMax - 12f, rect.yMin + 8f, 6f, 6f);
                EditorGUI.DrawRect(dotRect, themeColor);
            }

            // Type Icon (large centered)
            Texture icon = info.icon;
            if (icon != null)
            {
                float iconSize = 32f;
                float iconX = rect.xMin + (rect.width - iconSize) / 2f;
                float iconY = rect.yMin + 12f;
                Rect iconRect = new Rect(iconX, iconY, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon);
            }

            // Name (centered at bottom)
            float textY = rect.yMin + 48f;
            float textHeight = rect.height - 48f - 4f;
            Rect textRect = new Rect(rect.xMin + 4f, textY, rect.width - 8f, textHeight);
            
            var nameStyle = new GUIStyle(EditorStyles.miniLabel);
            nameStyle.alignment = TextAnchor.UpperCenter;
            nameStyle.fontSize = 9;
            nameStyle.wordWrap = true;
            nameStyle.normal.textColor = isSelected ? Color.white : RowTextNormal;

            GUI.Label(textRect, info.name, nameStyle);

            // Handle card input events
            HandleAssetCardEvents(rect, info);
        }

        private void DrawGridRow(Rect rowRect, List<string> guids)
        {
            float cardWidth = 80f;
            float cardHeight = 84f;
            float spacing = 8f; // slightly larger spacing for nice separation
            float startX = rowRect.xMin + 15f; // match sidebar indent of list view

            for (int i = 0; i < guids.Count; i++)
            {
                float x = startX + i * (cardWidth + spacing);
                float y = rowRect.yMin + 3f; // center vertically in the 90px row
                Rect cardRect = new Rect(x, y, cardWidth, cardHeight);
                DrawGridCard(cardRect, guids[i]);
            }
        }

        private void DrawAssetRow(string guid)
        {
            if (!cachedAssets.ContainsKey(guid)) return;

            CachedAssetInfo info = cachedAssets[guid];

            Rect rect = GUILayoutUtility.GetRect(10, 24); // Clean 24px height for rows

            bool isHovered = rect.Contains(Event.current.mousePosition);

            // Selection Check
            bool isSelected = (guid == selectedGuid);

            // Draw Background selection
            if (isSelected)
            {
                EditorGUI.DrawRect(rect, RowSelectBg); // Rich deep blue
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(rect, RowHoverBg); // Soft hover highlight
            }

            // Draw Color-coded accent pill capsule
            ColorCode colorCode = ColorCode.Slate;
            colorCache.TryGetValue(guid, out colorCode);
            Color themeColor = GetColorForCode(colorCode);
            Rect accentBar = new Rect(rect.xMin + 15f, rect.yMin + 4f, 4f, rect.height - 8f);
            EditorGUI.DrawRect(accentBar, themeColor);

            // Type Icon (loaded from memory)
            Texture icon = info.icon;
            if (icon != null)
            {
                float iconY = rect.yMin + (rect.height - 16f) / 2f;
                Rect iconRect = new Rect(rect.xMin + 24f, iconY, 16f, 16f);
                GUI.DrawTexture(iconRect, icon);
            }

            // Name (Title) - Centered vertically at 24px height
            Rect titleRect = new Rect(rect.xMin + 46f, rect.yMin, rect.width - 52f, rect.height);
            var titleStyle = new GUIStyle(EditorStyles.label);
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.fontSize = 11;
            titleStyle.fontStyle = FontStyle.Bold;
            if (isSelected) titleStyle.normal.textColor = Color.white;
            else titleStyle.normal.textColor = RowTextNormal;

            GUI.Label(titleRect, info.name, titleStyle);

            // Handle input
            HandleAssetCardEvents(rect, info);
        }

        private Color GetColorForCode(ColorCode code)
        {
            switch (code)
            {
                case ColorCode.Indigo: return new Color(0.38f, 0.35f, 0.95f, 1f);
                case ColorCode.Teal: return new Color(0.08f, 0.65f, 0.65f, 1f);
                case ColorCode.Emerald: return new Color(0.1f, 0.72f, 0.45f, 1f);
                case ColorCode.Amber: return new Color(0.95f, 0.6f, 0.1f, 1f);
                case ColorCode.Rose: return new Color(0.9f, 0.25f, 0.35f, 1f);
                default: return new Color(0.5f, 0.5f, 0.5f, 0.4f); // Slate
            }
        }

        private void SetItemColor(string guid, ColorCode color)
        {
            if (data == null) return;
            data.SetItemColor(guid, color);
            colorCache[guid] = color;
            Repaint();
        }

        private void HandleAssetCardEvents(Rect rect, CachedAssetInfo info)
        {
            Event evt = Event.current;

            if (rect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    // Select Item locally (prevent global selection/ping)
                    selectedGuid = info.guid;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.path);
                    if (obj != null)
                    {
                        dragSourceGuid = info.guid;
                        dragSourceObject = obj;
                    }

                    if (evt.clickCount == 2 && obj != null)
                    {
                        // Double Click behaves EXACTLY like standard project window double click (opens standard scene or runs SceneCollectionOpener!)
                        AssetDatabase.OpenAsset(obj);
                    }
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    if (dragSourceObject != null && dragSourceGuid == info.guid)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.SetGenericData("SceneViewer_DragType", "asset");
                        DragAndDrop.SetGenericData("SceneViewer_DragGuid", dragSourceGuid);
                        DragAndDrop.objectReferences = new Object[] { dragSourceObject };
                        DragAndDrop.StartDrag("Dragging " + dragSourceObject.name);
                        evt.Use();
                    }
                }

                // Context menus on cards
                if (evt.type == EventType.ContextClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Ping in Project"), false, () => {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.path);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    });
                    
                    menu.AddSeparator("");

                    // Assign Colors
                    menu.AddItem(new GUIContent("Color/Slate (Default)"), false, () => SetItemColor(info.guid, ColorCode.Slate));
                    menu.AddItem(new GUIContent("Color/Indigo"), false, () => SetItemColor(info.guid, ColorCode.Indigo));
                    menu.AddItem(new GUIContent("Color/Teal"), false, () => SetItemColor(info.guid, ColorCode.Teal));
                    menu.AddItem(new GUIContent("Color/Emerald"), false, () => SetItemColor(info.guid, ColorCode.Emerald));
                    menu.AddItem(new GUIContent("Color/Amber"), false, () => SetItemColor(info.guid, ColorCode.Amber));
                    menu.AddItem(new GUIContent("Color/Rose"), false, () => SetItemColor(info.guid, ColorCode.Rose));

                    menu.AddSeparator("");

                    string targetLabel = GetLabelFromFilter();
                    if (!string.IsNullOrEmpty(targetLabel))
                    {
                        menu.AddItem(new GUIContent("Remove Label '" + targetLabel + "'"), false, () => {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(info.path);
                            if (obj != null)
                            {
                                var labels = AssetDatabase.GetLabels(obj);
                                List<string> labelList = new List<string>(labels);
                                labelList.Remove(targetLabel);
                                AssetDatabase.SetLabels(obj, labelList.ToArray());
                                ScanAssets(true);
                            }
                        });
                    }

                    menu.ShowAsContext();
                    evt.Use();
                }
            }
        }

        private string GetLabelFromFilter()
        {
            if (string.IsNullOrEmpty(customFilter)) return null;

            string[] parts = customFilter.Split(' ');
            foreach (var part in parts)
            {
                if (part.StartsWith("l:") && part.Length > 2)
                {
                    return part.Substring(2);
                }
            }
            return null;
        }

        private void HandleMainAreaDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropRect = GUILayoutUtility.GetLastRect();
            dropRect.height = Mathf.Max(dropRect.height, position.height - 30f);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (dropRect.Contains(evt.mousePosition))
                    {
                        var dragType = DragAndDrop.GetGenericData("SceneViewer_DragType") as string;
                        
                        // Drop from outside (Project window) into the grid area (Assigns the "scene" label)
                        if (dragType == null && DragAndDrop.objectReferences.Length > 0)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            if (evt.type == EventType.DragPerform)
                            {
                                DragAndDrop.AcceptDrag();
                                foreach (var obj in DragAndDrop.objectReferences)
                                {
                                    string path = AssetDatabase.GetAssetPath(obj);
                                    if (string.IsNullOrEmpty(path)) continue;

                                    string targetLabel = GetLabelFromFilter();
                                    if (!string.IsNullOrEmpty(targetLabel))
                                    {
                                        var labels = AssetDatabase.GetLabels(obj);
                                        List<string> labelList = new List<string>(labels);
                                        if (!labelList.Contains(targetLabel))
                                        {
                                            labelList.Add(targetLabel);
                                            AssetDatabase.SetLabels(obj, labelList.ToArray());
                                        }
                                    }
                                }
                                ScanAssets(true);
                                evt.Use();
                            }
                        }
                    }
                    break;
            }
        }

        // Theme Colors properties based on EditorSkin
        private Color SidebarBgColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f, 1.0f) : new Color(0.69f, 0.69f, 0.69f, 1.0f);
        private Color MainAreaBgColor => EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 1.0f) : new Color(0.76f, 0.76f, 0.76f, 1.0f);
        private Color HeaderBgColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f, 1.0f) : new Color(0.69f, 0.69f, 0.69f, 1.0f);
        private Color HeaderBorderColor => EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f, 1.0f) : new Color(0.60f, 0.60f, 0.60f, 1.0f);
        private Color SidebarDividerColor => EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f, 1.0f) : new Color(0.60f, 0.60f, 0.60f, 1.0f);
        private Color SplitterBgColor => EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1.0f) : new Color(0.65f, 0.65f, 0.65f, 1.0f);
        private Color SplitterLineColor => EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f, 1.0f) : new Color(0.60f, 0.60f, 0.60f, 1.0f);

        // Sidebar Categories
        private Color SidebarSelectBg => EditorGUIUtility.isProSkin ? new Color(0.20f, 0.24f, 0.30f, 1.0f) : new Color(0.75f, 0.80f, 0.88f, 1.0f);
        private Color SidebarSelectBar => EditorGUIUtility.isProSkin ? new Color(0.23f, 0.49f, 0.85f, 1.0f) : new Color(0.18f, 0.45f, 0.80f, 1.0f);
        private Color SidebarHoverBg => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 0.4f) : new Color(0.75f, 0.75f, 0.75f, 0.4f);
        private Color SidebarTextSelected => EditorGUIUtility.isProSkin ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f);
        private Color SidebarTextNormal => EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);

        // Filter Badge
        private Color BadgeBorder => EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f, 1.0f) : new Color(0.65f, 0.65f, 0.65f, 1.0f);
        private Color BadgeFill => EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f, 1.0f) : new Color(0.72f, 0.72f, 0.72f, 1.0f);
        private Color BadgeText => EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f, 1.0f) : new Color(0.2f, 0.2f, 0.2f, 1.0f);

        // Grid Folder Headers
        private Color FolderHeaderBg => EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f, 1.0f) : new Color(0.72f, 0.72f, 0.72f, 1.0f);
        private Color FolderHeaderText => EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f, 1.0f) : new Color(0.3f, 0.3f, 0.3f, 1.0f);

        // Asset rows & cards
        private Color RowSelectBg => EditorGUIUtility.isProSkin ? new Color(0.15f, 0.28f, 0.48f, 1.0f) : new Color(0.65f, 0.78f, 0.95f, 1.0f);
        private Color RowHoverBg => EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f, 0.5f) : new Color(0.75f, 0.75f, 0.75f, 0.5f);
        private Color CardBaseBg => EditorGUIUtility.isProSkin ? new Color(0.20f, 0.20f, 0.20f, 0.3f) : new Color(0.75f, 0.75f, 0.75f, 0.3f);
        private Color CardBorderNormal => EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.3f) : new Color(0.6f, 0.6f, 0.6f, 0.3f);
        private Color CardBorderHover => EditorGUIUtility.isProSkin ? new Color(0.35f, 0.35f, 0.35f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private Color CardBorderSelect => EditorGUIUtility.isProSkin ? new Color(0.23f, 0.49f, 0.85f, 1.0f) : new Color(0.18f, 0.45f, 0.80f, 1.0f);
        private Color RowTextNormal => EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f, 1.0f) : new Color(0.15f, 0.15f, 0.15f, 1.0f);

        private void DrawThemeRect(Rect rect, string styleName, Color fallbackColor)
        {
            GUIStyle style = GUI.skin.FindStyle(styleName);
            if (style != null && Event.current.type == EventType.Repaint)
            {
                style.Draw(rect, false, false, false, false);
            }
            else
            {
                EditorGUI.DrawRect(rect, fallbackColor);
            }
        }
    }
}
