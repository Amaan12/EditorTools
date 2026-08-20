using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

using static System.Environment;
using static System.IO.Path;
using static UnityEditor.AssetDatabase;

namespace Project.Setup
{
    /// <summary>
    /// 1. Auto-install Assets
    /// 2. Auto-install packages
    /// 3. Create Project Folders
    /// 4. Create Scene Empties
    /// 5. EnableFastEnterPlayMode
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Tools/Setup/ImportAssets/Import Essential Assets")]
        /// <summary>
        /// How to use this command?
        /// C:\Users\imama\AppData\Roaming\Unity\Asset Store-5.x\ => this is your Assets folder where everything is cached once, you need to download an asset once to cache it to be able to auto-install it
        /// The folder will have a unity package whose relative path you have to add in the 2nd parameter, and 1st parameter is for the .unitypackage
        /// </summary>
        public static void ImportEssentialAssets()
        {
            Assets.ImportAsset("Editor Auto Save.unitypackage", "IntenseNation/Editor ExtensionsUtilities");  // New Auto-Save
            Assets.ImportAsset("DOTween HOTween v2.unitypackage", "Demigiant/Editor ExtensionsAnimation");
            Assets.ImportAsset("Graphy - Ultimate FPS Counter - Stats Monitor Debugger.unitypackage", "Tayx/ScriptingGUI");
            Assets.ImportAsset("Quantum Console.unitypackage", "QFSW/Editor ExtensionsUtilities");
            Assets.ImportAsset("Better Hierarchy.unitypackage", "Toaster Head/Editor ExtensionsUtilities");
            Assets.ImportAsset("TimeScale Toolbar.unitypackage", "bl4st/Editor ExtensionsUtilities"); // Remove this in Unity 7 probably
            Assets.ImportAsset("Audio Preview Tool.unitypackage", "Warped Imagination/Editor ExtensionsAudio");
            Assets.ImportAsset("DarkMode for Unity Editor on Windows.unitypackage", "0x7c13/ScriptingGUI");
            Assets.ImportAsset("Update manager.unitypackage", "Feiko Joosten/Editor ExtensionsUtilities");
            
            // Not required anymore.
            // Assets.ImportAsset("AutoSave.unitypackage", "EckTech Games/Editor ExtensionsUtilities");  // Old Auto-Save, it only saved on Play Mode not periodically
            // Assets.ImportAsset("Custom Inspector.unitypackage", "mb services/Editor ExtensionsUtilities");
            // Assets.ImportAsset("Super Pivot FREE Modifier.unitypackage", "Tech Salad/Editor ExtensionsUtilities");

            // Examples, replace the folder as per your setup
            // Assets.ImportAsset("Odin Inspector and Serializer.unitypackage", "Sirenix/Editor ExtensionsSystem");
            // Assets.ImportAsset("Odin Validator.unitypackage", "Sirenix/Editor ExtensionsUtilities");
            // Assets.ImportAsset("Editor Console Pro.unitypackage", "FlyingWorm/Editor ExtensionsSystem");
            // and so on...
        }

        [MenuItem("Tools/Setup/ImportAssets/Import Texture Assets")]
        public static void ImportTextureAssets()
        {
            Assets.ImportAsset("Prototype Textures Pack.unitypackage", "iPoly3D/Textures Materials");
            Assets.ImportAsset("Gridbox Prototype Materials.unitypackage", "Ciathyza/Textures Materials");
            Assets.ImportAsset("Prototype Map.unitypackage", "AngeloMaN87/3D ModelsEnvironments");
            Assets.ImportAsset("Fantasy Skybox FREE.unitypackage", "Render Knight/Textures MaterialsSkies");
            Assets.ImportAsset("AllSky Free - 10 Sky Skybox Set.unitypackage", "rpgwhitelock/Textures MaterialsSkies");
        }

        [MenuItem("Tools/Setup/ImportAssets/Import PFX Assets")]
        public static void ImportPFXAssets()
        {
            Assets.ImportAsset("Cartoon FX Remaster Free.unitypackage", "Jean Moreno/Particle Systems");
            Assets.ImportAsset("Particle Pack.unitypackage", "Unity Technologies/Particle Systems");
            Assets.ImportAsset("VFX URP - Fire Package.unitypackage", "Cartoon VFX by Wallcoeur/Particle SystemsFire");
            Assets.ImportAsset("Free Fire VFX - URP.unitypackage", "Vefects/Particle SystemsFire");
            Assets.ImportAsset("Trails VFX - URP.unitypackage", "Vefects/VFX");
            Assets.ImportAsset("Free Stylized Smoke Effects Pack.unitypackage", "Maiami Studio/Particle SystemsFire");
            Assets.ImportAsset("Free Quick Effects Vol 1.unitypackage", "Gabriel Aguiar Prod/Particle Systems");
            Assets.ImportAsset("Free 2D Impact FX.unitypackage", "Inguz Media/Particle SystemsFire");
            Assets.ImportAsset("Toon Muzzleflash Pack.unitypackage", "Infima Games/Textures Materials");
            Assets.ImportAsset("Free Slash VFX.unitypackage", "MaykerStudio/Shaders");
            Assets.ImportAsset("Hit Effects FREE.unitypackage", "Matthew Guz/Particle Systems");
            Assets.ImportAsset("FreeStylizedVFX Fire Pack.unitypackage", "Hun0FX/Particle SystemsFire");
        }

        [MenuItem("Tools/Setup/ImportAssets/Import Shader Assets")]
        public static void ImportShaderAssets()
        {
            Assets.ImportAsset("PSX Shader Kit.unitypackage", "Valerie Moza/Shaders");
            Assets.ImportAsset("Ultimate 10 Shaders.unitypackage", "The Developer/Shaders");
            Assets.ImportAsset("Painterly Normals Shader.unitypackage", "Detox/Shaders");
            Assets.ImportAsset("Censor Effect.unitypackage", "Staggart Creations/Shaders");
            Assets.ImportAsset("Height Fog.unitypackage", "SKGames/Shaders");
        }

        [MenuItem("Tools/Setup/ImportAssets/Import UI Assets")]
        public static void ImportUIAssets()
        {
            Assets.ImportAsset("Animated Loading Icons.unitypackage", "Infima Games/Textures MaterialsIcons UI");
            Assets.ImportAsset("FPS Icons Pack.unitypackage", "Infima Games/Textures MaterialsIcons UI");
            Assets.ImportAsset("Flat pack - GUI.unitypackage", "CorePro/Textures MaterialsGUI Skins");
            Assets.ImportAsset("Skymon Icon Pack Free.unitypackage", "Amanz/Textures MaterialsIcons UI");
        }

        [MenuItem("Tools/Setup/ImportAssets/Import Audio Assets")]
        public static void ImportAudioAssets()
        {
            Assets.ImportAsset("HyperCasual Music Pack Demo.unitypackage", "VOiD1 Gaming/AudioMusic");
            Assets.ImportAsset("Free Sound Effects Pack.unitypackage", "Olivier Girardot/AudioSound FX");
            Assets.ImportAsset("Free - Casual Relaxing Game Music Pack.unitypackage", "SLD Audio/AudioMusic");
            Assets.ImportAsset("FREE Casual Game SFX Pack.unitypackage", "Dustyroom/AudioSound FX");
        }

        [MenuItem("Tools/Setup/ImportAssets/Import Mobile Tools Assets")]
        public static void ImportMobileToolsAssets()
        {
            Assets.ImportAsset("Mobile Haptic Feedback.unitypackage", "Solo Player/ScriptingIntegration");
        }

        [MenuItem("Tools/Setup/Install Essential Packages")]
        public static void InstallPackages()
        {
            Packages.InstallPackages(new[] {
            // "com.unity.2d.animation",

            // * Camera shake independent of Cinemachine, need to read docs though
            "https://github.com/gasgiant/Camera-Shake.git#upm",

            // * Important utilities by git-amend
            "git+https://github.com/adammyhre/Unity-Utils.git",

            // * Improved Timers
            "git+https://github.com/Amaan12/Unity-Improved-Timers.git",

            // * Cinemachine
            "com.unity.cinemachine",

            // * ValidatedMonobehavior, Idk. Will look into it later.
            "git+https://github.com/KyleBanks/scene-ref-attribute.git"

            // * If necessary, import new Input System last as it requires a Unity Editor restart. I think it's come installed by default so it's fine for now.
            // "com.unity.inputsystem"
            });
        }

        [MenuItem("Tools/Setup/Create Folders")]
        public static void CreateFolders()
        {
            Folders.Create(
                "_Project",
                "Art",
                "Prefabs",
                "Scripts/Tests",
                "Scripts/Tests/Editor",
                "Scripts/Tests/Runtime",
                "Resources"
            );

            Folders.Create(
                "_Project/Prefabs",
                "Gameplay",
                "Environment"
            );

            Folders.Create(
                "_Project/Art",
                "Models",
                "Sprites/UI",
                "VFX",
                "Fonts",
                "Animation",
                "Materials/Shader",
                "Materials/Physics",
                "Audio/SFX",
                "Audio/Music"
            );

            Folders.Create("Imported Assets");

            Refresh();

            Folders.Move("_Project", "Scenes");
            Folders.Move("_Project", "Settings");

            Folders.Delete("TutorialInfo");

            Refresh();

            MoveAsset(
                "Assets/InputSystem_Actions.inputactions",
                "Assets/_Project/Settings/InputSystem_Actions.inputactions"
            );

            DeleteAsset("Assets/Readme.asset");

            Refresh();
        }

        [MenuItem("Tools/Setup/Create Default Scene Objects")]
        public static void CreateSceneEssentials()
        {
            Folders.CreateEmptyObject("_____ESSENTIALS_____");
            Folders.CreateEmptyObject(" ");
            Folders.CreateEmptyObject("_____MANAGERS_____");
            Folders.CreateEmptyObject(" ");
            Folders.CreateEmptyObject("_____CANVASES_____");
            Folders.CreateEmptyObject(" ");
            Folders.CreateEmptyObject("_____ENVIRONMENT_____");
            Folders.CreateEmptyObject(" ");
            Folders.CreateEmptyObject("_____GAMEPLAY_____");
        }

        [MenuItem("Tools/Setup/Enable Fast Enter Play Mode")]
        public static void EnableFastEnterPlayMode()
        {
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload |
                EnterPlayModeOptions.DisableSceneReload;

            EditorSettings.enterPlayModeOptionsEnabled = true;

            Debug.Log("Fast Enter Play Mode enabled (Domain + Scene Reload disabled)");
        }

        static class Assets
        {
            public static void ImportAsset(string asset, string folder)
            {
                string basePath;
                if (OSVersion.Platform is PlatformID.MacOSX or PlatformID.Unix)
                {
                    string homeDirectory = GetFolderPath(SpecialFolder.Personal);
                    basePath = Combine(homeDirectory, "Library/Unity/Asset Store-5.x");
                }
                else
                {
                    string defaultPath = Combine(GetFolderPath(SpecialFolder.ApplicationData), "Unity");
                    basePath = Combine(EditorPrefs.GetString("AssetStoreCacheRootPath", defaultPath), "Asset Store-5.x");
                }

                asset = asset.EndsWith(".unitypackage") ? asset : asset + ".unitypackage";

                string fullPath = Combine(basePath, folder, asset);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"The asset package was not found at the path: {fullPath}");
                }

                ImportPackage(fullPath, false);
            }
        }

        static class Packages
        {
            static AddRequest request;
            static Queue<string> packagesToInstall = new Queue<string>();

            public static void InstallPackages(string[] packages)
            {
                foreach (var package in packages)
                {
                    packagesToInstall.Enqueue(package);
                }

                if (packagesToInstall.Count > 0)
                {
                    StartNextPackageInstallation();
                }
            }

            static async void StartNextPackageInstallation()
            {
                request = Client.Add(packagesToInstall.Dequeue());

                while (!request.IsCompleted) await Task.Delay(10);

                if (request.Status == StatusCode.Success) Debug.Log("Installed: " + request.Result.packageId);
                else if (request.Status >= StatusCode.Failure) Debug.LogError(request.Error.message);

                if (packagesToInstall.Count > 0)
                {
                    await Task.Delay(1000);
                    StartNextPackageInstallation();
                }
            }
        }

        static class Folders
        {
            public static void Create(string root, params string[] folders)
            {
                var fullpath = Combine(Application.dataPath, root);
                if (!Directory.Exists(fullpath))
                {
                    Directory.CreateDirectory(fullpath);
                }

                foreach (var folder in folders)
                {
                    CreateSubFolders(fullpath, folder);
                }
            }

            static void CreateSubFolders(string rootPath, string folderHierarchy)
            {
                var folders = folderHierarchy.Split('/');
                var currentPath = rootPath;

                foreach (var folder in folders)
                {
                    currentPath = Combine(currentPath, folder);
                    if (!Directory.Exists(currentPath))
                    {
                        Directory.CreateDirectory(currentPath);
                    }
                }
            }

            public static void Move(string newParent, string folderName)
            {
                var sourcePath = $"Assets/{folderName}";
                if (IsValidFolder(sourcePath))
                {
                    var destinationPath = $"Assets/{newParent}/{folderName}";
                    var error = MoveAsset(sourcePath, destinationPath);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"Failed to move {folderName}: {error}");
                    }
                }
            }

            public static void Delete(string folderName)
            {
                var pathToDelete = $"Assets/{folderName}";

                if (IsValidFolder(pathToDelete))
                {
                    DeleteAsset(pathToDelete);
                }
            }

            public static void CreateEmptyObject(string name)
            {
                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                Debug.Log("Created GameObject: " + name);
            }
        }
    }
}