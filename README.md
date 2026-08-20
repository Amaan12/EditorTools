# Unity Editor Tools

A comprehensive suite of Unity Editor productivity tools, workflow enhancements, and utilities designed to streamline game development.

---

## 📦 Installation

### Via Git URL in Unity Package Manager:
1. Open your Unity Project.
2. Navigate to **Window** > **Package Manager**.
3. Click the **+** (Add) button in the top-left corner.
4. Select **Add package from git URL...**.
5. Enter the repository URL:
   ```text
   https://github.com/Amaan12/EditorTools.git
   ```

### Via `manifest.json`:
Add the following line to `Packages/manifest.json` under `"dependencies"`:
```json
"com.amaan.editortools": "https://github.com/Amaan12/EditorTools.git"
```

---

## 🛠️ Included Tools

### 1. Asset Hub (`Editor/AssetHub`)
* **Scene & Asset Viewer**: An integrated editor window for quickly inspecting, searching, and managing project assets, scenes, and configurations in one place.

### 2. Compilation Feedback / Compile Monkey (`Editor/CompilationFeedback`)
* **Visual & Audio Build Feedback**: Displays responsive visual overlays (success, warning, error) in the Scene view and plays audio cues upon script compilation completion.
* **Persistent MCI Audio**: Supports continuous audio playback through Unity domain reloads on Windows.

### 3. Unity Enums Generator (`Editor/EnumGenerator`)
* **Auto-Generated Enums**: Automatically generates `SceneId`, `Tag`, and `LayerMaskId` strongly-typed enums at `Assets/Imported Assets/UnityEnums/UnityEnums.cs`.
* **Label-Based Scene Discovery**: Discovers all scenes tagged with the `Scene` label across your project.
* **Menu**: `Tools > Generate > Unity Enums` and right-click context menu `UnityEnums > Add Scene Label`.

### 4. Project Folder Setup (`Editor/FolderSetup`)
* **Automated Project Scaffolding**: Fast creation of standard folder hierarchies, scene structure empties, fast play mode settings, and batch asset/package importing.
* **Menu**: `Tools > Setup > ...`

### 5. GameObject Utilities (`Editor/GameObject`)
* **Create From Script** (`GameObject > Create From Script` or `Shift+A`): Instantly creates a GameObject with the selected MonoBehaviour script attached or creates a new script.
* **Hierarchy Section Header**: Cleanly formats hierarchy headers (game objects starting with `//`) with dark background styling and uppercase text.

### 6. Hierarchy Traversal (`Editor/HierarchyTraversal`)
* **Keyboard Hierarchy Reordering** (`Alt+Up` / `Alt+Down`): Quickly moves selected GameObjects up/down through siblings and parent hierarchies in the Scene Hierarchy window.

### 7. Tab Navigator (`Editor/Navigation`)
* **Fast Tab Cycling** (`Ctrl+Tab` / `Ctrl+Shift+Tab`): Cycle through docked editor window tabs hovering under the cursor without needing to click.

### 8. Scene Utilities (`Editor/Scene`)
* **Play From Boot Scene**: Adds a quick toolbar button (`▶0`) to start Play Mode directly from Build Settings Scene 0.
* **Additive Scene Loader**: Manage multi-scene setups with `SceneCollection` ScriptableObjects (`Scenes > Scene Collection`) that open additively when double-clicked.

### 9. Text Utilities (`Editor/Text`)
* **Markdown File Creator** (`Assets > Create > Markdown File`): Quickly create `.md` markdown files directly from the Project window right-click menu.

---

## 📂 Package Structure

```text
EditorTools/
├── package.json
├── README.md
├── CHANGELOG.md
├── LICENSE.md
├── .gitignore
├── Editor/                     # Editor tools & scripts (with dedicated .asmdefs)
│   ├── AssetHub/
│   ├── CompilationFeedback/
│   ├── EnumGenerator/
│   ├── FolderSetup/
│   ├── GameObject/
│   ├── HierarchyTraversal/
│   ├── Navigation/
│   ├── Scene/
│   └── Text/
├── Runtime/                    # Runtime scripts & asmdef (if applicable)
└── Samples~/                   # Sample scenes and test assets
    ├── AdditiveSceneLoader/
    ├── CompileMonkey/
    └── HierarchyTraversal/
```

---

## 🧪 Samples
Samples can be imported via **Package Manager > Editor Tools > Samples**:
* **Additive Scene Loader Sample**: Example scenes and `SceneCollection` asset.
* **Hierarchy Traversal Sample**: Test scene for hierarchy reordering.
* **Compile Monkey Test Scripts**: Scripts to test compilation warning/error overlays.

---

## 📄 License
This project is licensed under the MIT License.
