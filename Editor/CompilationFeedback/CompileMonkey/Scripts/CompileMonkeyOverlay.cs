using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CompileMonkey
{
    [InitializeOnLoad]
    public static class CompileMonkeyOverlay
    {
        // Keys for persistent session state
        const string StateKey = "CompileMonkey_State";
        const string ErrorCountKey = "CompileMonkey_ErrorCount";
        const string WarningCountKey = "CompileMonkey_WarningCount";
        const string PendingStateKey = "CompileMonkey_PendingResultState";
        const string PlayWarningSoundKey = "CompileMonkey_PlayWarningSound";
        const string IsCompilingOrReloadingKey = "CompileMonkey_IsCompilingOrReloading";

        // In-memory state variables
        static CompileMonkeyState currentState = CompileMonkeyState.Idle;
        static CompileMonkeyState fadingFromState = CompileMonkeyState.Idle;
        static int currentErrorCount = 0;
        static int currentWarningCount = 0;

        static float fadeAlpha = 1f;
        static double resultStateStartTime = -1;
        static double fadeStartTime = 0;

        // Accumulated counts during the active build phase
        static int accumulatedErrorCount = 0;
        static int accumulatedWarningCount = 0;

        // Deferred SFX playback flags to prevent sounds during blocking assembly reloads
        static bool pendingResultSfxAndStopMusic = false;
        static bool pendingPlaySuccessWarningSfx = false;
        static bool pendingStopCompilationMusic = false;
        static bool playWarningSoundOnDeferred = true;
        static bool isMusicPlaying = false;
        static double musicPlayStartTime = 0;

        // Native Windows MCI player to keep compilation music playing through Unity domain reload
        static class CompileMonkeyMCI
        {
            [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "mciSendString", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            static extern int mciSendString(string strCommand, System.Text.StringBuilder strReturn, int iReturnLength, IntPtr hwndCallback);

            [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "mciGetErrorString", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            static extern bool mciGetErrorString(int dwError, System.Text.StringBuilder lpstrBuffer, int uLength);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            static extern int GetShortPathName(string lpszLongPath, System.Text.StringBuilder lpszShortPath, int cchBuffer);

            static bool isOpen = false;

            public static bool IsOpen => isOpen;

            static string GetShortPath(string longPath)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(260);
                int result = GetShortPathName(longPath, sb, sb.Capacity);
                if (result == 0)
                {
                    return longPath;
                }
                return sb.ToString();
            }

            static string GetMciError(int err)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(260);
                if (mciGetErrorString(err, sb, sb.Capacity))
                {
                    return sb.ToString();
                }
                return $"Error code {err}";
            }

            public static bool Play(string filePath, bool loop)
            {
                try
                {
                    // Always try to stop and close any existing alias first to ensure it's not locked/open
                    Stop();

                    string shortPath = GetShortPath(filePath);
                    string openCommand = $"open \"{shortPath}\" type mpegvideo alias compile_music";
                    int err = mciSendString(openCommand, null, 0, IntPtr.Zero);
                    if (err != 0)
                    {
                        // Fallback: forcefully close and retry open
                        mciSendString("close compile_music", null, 0, IntPtr.Zero);
                        err = mciSendString(openCommand, null, 0, IntPtr.Zero);
                        if (err != 0)
                        {
                            return false;
                        }
                    }

                    isOpen = true;

                    string playCommand = "play compile_music";
                    if (loop)
                    {
                        playCommand += " REPEAT";
                    }

                    err = mciSendString(playCommand, null, 0, IntPtr.Zero);
                    if (err != 0)
                    {
                        Stop();
                        return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public static void Stop()
            {
                try
                {
                    mciSendString("stop compile_music", null, 0, IntPtr.Zero);
                    mciSendString("close compile_music", null, 0, IntPtr.Zero);
                    isOpen = false;
                }
                catch { }
            }
        }

        static void PlayCompilationMusic(AudioClip clip)
        {
            if (clip == null) return;
            CompileMonkeySettings settings = GetSettings();
            if (settings != null && !settings.useMusic) return;

            try
            {
                string relativePath = AssetDatabase.GetAssetPath(clip);
                string absolutePath = System.IO.Path.GetFullPath(relativePath);

                // Try Windows MCI first so it persists through reloading assemblies
                bool played = CompileMonkeyMCI.Play(absolutePath, true);
                if (!played)
                {
                    CompileMonkeyAudio.Play(clip, true);
                }
            }
            catch
            {
                CompileMonkeyAudio.Play(clip, true);
            }
            isMusicPlaying = true;
            musicPlayStartTime = EditorApplication.timeSinceStartup;
        }

        static void ForceRepaintImmediately(EditorWindow window)
        {
            if (window == null) return;
            try
            {
                var parentField = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (parentField != null)
                {
                    var parentValue = parentField.GetValue(window);
                    if (parentValue != null)
                    {
                        var repaintMethod = parentValue.GetType().GetMethod("RepaintImmediately", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (repaintMethod != null)
                        {
                            repaintMethod.Invoke(parentValue, null);
                            return;
                        }
                    }
                }
                window.Repaint();
            }
            catch
            {
                window.Repaint();
            }
        }

        static void StopCompilationMusic()
        {
            CompileMonkeyMCI.Stop();
            var settings = GetSettings();
            if (settings != null && settings.compileMusic != null)
            {
                CompileMonkeyAudio.Stop(settings.compileMusic);
            }
            else
            {
                CompileMonkeyAudio.StopAll();
            }
            isMusicPlaying = false;
        }

        // Reflection helper for AudioUtil
        static class CompileMonkeyAudio
        {
            static MethodInfo playClipMethod;
            static MethodInfo stopClipMethod;
            static MethodInfo stopAllClipsMethod;
            static MethodInfo isClipPlayingMethod;

            static CompileMonkeyAudio()
            {
                try
                {
                    System.Reflection.Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
                    Type audioUtilType = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
                    if (audioUtilType != null)
                    {
                        MethodInfo[] methods = audioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);


                        foreach (var m in methods)
                        {
                            if (m.Name == "PlayPreviewClip" || m.Name == "PlayClip")
                            {
                                var parameters = m.GetParameters();
                                if (parameters.Length == 3 && parameters[0].ParameterType == typeof(AudioClip))
                                {
                                    playClipMethod = m;
                                }
                                else if (parameters.Length == 2 && playClipMethod == null && parameters[0].ParameterType == typeof(AudioClip))
                                {
                                    playClipMethod = m;
                                }
                                else if (parameters.Length == 1 && playClipMethod == null && parameters[0].ParameterType == typeof(AudioClip))
                                {
                                    playClipMethod = m;
                                }
                            }
                            else if ((m.Name == "StopPreviewClip" || m.Name == "StopClip") && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(AudioClip))
                            {
                                stopClipMethod = m;
                            }
                            else if ((m.Name == "StopAllPreviewClips" || m.Name == "StopAllClips") && m.GetParameters().Length == 0)
                            {
                                stopAllClipsMethod = m;
                            }
                            else if (m.Name == "IsPreviewClipPlaying" || m.Name == "IsClipPlaying")
                            {
                                var parameters = m.GetParameters();
                                if (parameters.Length == 0)
                                {
                                    isClipPlayingMethod = m;
                                }
                                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AudioClip))
                                {
                                    isClipPlayingMethod = m;
                                }
                            }
                        }

                    }
                }
                catch { }
            }

            public static void Play(AudioClip clip, bool loop)
            {
                if (clip == null)
                {
                    return;
                }
                if (playClipMethod != null)
                {
                    try
                    {
                        var parameters = playClipMethod.GetParameters();
                        if (parameters.Length == 3)
                        {
                            playClipMethod.Invoke(null, new object[] { clip, 0, loop });
                        }
                        else if (parameters.Length == 2)
                        {
                            playClipMethod.Invoke(null, new object[] { clip, 0 });
                        }
                        else if (parameters.Length == 1)
                        {
                            playClipMethod.Invoke(null, new object[] { clip });
                        }
                    }
                    catch { }
                }
            }

            public static void Stop(AudioClip clip)
            {
                if (clip == null) return;

                // Try stopClipMethod if present, else fallback to StopAll
                if (stopClipMethod != null)
                {
                    try
                    {
                        stopClipMethod.Invoke(null, new object[] { clip });
                    }
                    catch
                    {
                        StopAll();
                    }
                }
                else
                {
                    StopAll();
                }
            }

            public static void StopAll()
            {
                if (stopAllClipsMethod != null)
                {
                    try
                    {
                        stopAllClipsMethod.Invoke(null, null);
                    }
                    catch { }
                }
            }

            public static bool IsPlayingSupported()
            {
                return isClipPlayingMethod != null;
            }

            public static bool IsPlaying(AudioClip clip)
            {
                if (clip == null) return false;
                if (isClipPlayingMethod != null)
                {
                    try
                    {
                        var parameters = isClipPlayingMethod.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AudioClip))
                        {
                            return (bool)isClipPlayingMethod.Invoke(null, new object[] { clip });
                        }
                        else if (parameters.Length == 0)
                        {
                            return (bool)isClipPlayingMethod.Invoke(null, null);
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }

            public static bool IsAnyPlaying()
            {
                if (isClipPlayingMethod != null)
                {
                    try
                    {
                        var parameters = isClipPlayingMethod.GetParameters();
                        if (parameters.Length == 0)
                        {
                            return (bool)isClipPlayingMethod.Invoke(null, null);
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }
        }

        // Static constructor called on load/compilation finish
        static CompileMonkeyOverlay()
        {
            // Subscribe to compilation events
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;

            // Subscribe to focus change events
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.focusChanged += OnFocusChanged;

            // Check if there is a pending result to display post-domain-reload
            CheckForPendingResult();

            // If we are currently compiling or reloading after a reload, make sure we show the compiling overlay
            bool isCompilingOrReloading = EditorApplication.isCompiling || SessionState.GetBool(IsCompilingOrReloadingKey, false);
            if (isCompilingOrReloading && currentState == CompileMonkeyState.Idle)
            {
                currentState = CompileMonkeyState.Compiling;
                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    var settings = GetSettings();
                    if (settings != null && settings.compileMusic != null && !isMusicPlaying)
                    {
                        PlayCompilationMusic(settings.compileMusic);
                    }
                }
                SubscribeToLoops();
            }
        }

        static CompileMonkeySettings GetSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:CompileMonkeySettings");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<CompileMonkeySettings>(path);
            }
            return null;
        }

        static void SubscribeToLoops()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void UnsubscribeFromLoops()
        {
            EditorApplication.update -= Update;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        static void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                // When we gain focus, check if there's a pending result to show
                CheckForPendingResult();

                // If compiling or reloading, start displaying overlay and playing compile music (if not already playing)
                bool isCompilingOrReloading = EditorApplication.isCompiling || SessionState.GetBool(IsCompilingOrReloadingKey, false);
                if (isCompilingOrReloading)
                {
                    currentState = CompileMonkeyState.Compiling;
                    fadeAlpha = 1f;

                    var settings = GetSettings();
                    if (settings != null && settings.compileMusic != null && !isMusicPlaying)
                    {
                        PlayCompilationMusic(settings.compileMusic);
                    }
                    SubscribeToLoops();
                    var sceneViews = SceneView.sceneViews;
                    if (sceneViews != null)
                    {
                        foreach (SceneView sv in sceneViews)
                        {
                            ForceRepaintImmediately(sv);
                        }
                    }
                }
            }
            else
            {
                // When focus is lost, stop compilation music to avoid annoying background noise
                if (currentState == CompileMonkeyState.Compiling)
                {
                    StopCompilationMusic();
                }
            }
        }

        // Checks if compilation completed and left a pending status before the domain reloaded
        static void CheckForPendingResult()
        {
            // Only trigger showing the result overlay if the Unity Editor is currently active/focused.
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                return;
            }

            int pendingState = SessionState.GetInt(PendingStateKey, (int)CompileMonkeyState.Idle);
            if (pendingState != (int)CompileMonkeyState.Idle)
            {
                CompileMonkeyState resultState = (CompileMonkeyState)pendingState;
                int warnings = SessionState.GetInt(WarningCountKey, 0);
                int errors = SessionState.GetInt(ErrorCountKey, 0);
                bool playWarningSound = SessionState.GetBool(PlayWarningSoundKey, true);

                // Clear session state flags so we don't trigger again
                SessionState.SetInt(PendingStateKey, (int)CompileMonkeyState.Idle);
                SessionState.SetInt(WarningCountKey, 0);
                SessionState.SetInt(ErrorCountKey, 0);
                SessionState.EraseBool(PlayWarningSoundKey);
                SessionState.EraseBool(IsCompilingOrReloadingKey);

                // Defer the sound to play on the first active Update frame after assembly reload is complete
                playWarningSoundOnDeferred = playWarningSound;

                // Show the results overlay
                TransitionToResultState(resultState, errors, warnings);
            }
        }

        static void OnCompilationStarted(object context)
        {
            // Reset compiler message counters
            accumulatedErrorCount = 0;
            accumulatedWarningCount = 0;

            currentState = CompileMonkeyState.Compiling;
            fadeAlpha = 1f;

            // Save state and active compile to session
            SessionState.SetInt(StateKey, (int)CompileMonkeyState.Compiling);
            SessionState.SetBool(IsCompilingOrReloadingKey, true);

            // Play background music only if focused
            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                CompileMonkeySettings settings = GetSettings();
                if (settings != null && settings.compileMusic != null)
                {
                    PlayCompilationMusic(settings.compileMusic);
                }
            }

            SubscribeToLoops();
            var sceneViews = SceneView.sceneViews;
            if (sceneViews != null)
            {
                foreach (SceneView sv in sceneViews)
                {
                    ForceRepaintImmediately(sv);
                }
            }
        }

        static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            int errs = 0;
            int warns = 0;
            foreach (var msg in messages)
            {
                if (msg.type == CompilerMessageType.Error)
                {
                    accumulatedErrorCount++;
                    errs++;
                }
                else if (msg.type == CompilerMessageType.Warning)
                {
                    accumulatedWarningCount++;
                    warns++;
                }
            }
        }

        static void OnCompilationFinished(object context)
        {
            CompileMonkeySettings settings = GetSettings();

            // Prioritize errors over warnings
            if (accumulatedErrorCount > 0)
            {
                SessionState.EraseBool(IsCompilingOrReloadingKey);

                // Compile error occurred. Unity will NOT reload the domain when compile errors exist.
                // Thus, we transition to the error state in-memory immediately.
                if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    StopCompilationMusic(); // Stop music immediately in background
                    // If we are in the background, store as pending error state to show when focused
                    SessionState.SetInt(PendingStateKey, (int)CompileMonkeyState.Error);
                    SessionState.SetInt(ErrorCountKey, accumulatedErrorCount);
                    SessionState.SetInt(WarningCountKey, accumulatedWarningCount);

                    currentState = CompileMonkeyState.Idle;
                    UnsubscribeFromLoops();
                }
                else
                {
                    TransitionToResultState(CompileMonkeyState.Error, accumulatedErrorCount, accumulatedWarningCount);
                }
            }
            else
            {
                // Compilation succeeded with no errors (domain reload is imminent).
                // Determine whether we ended in a Success or Warning state.
                bool hasWarnings = accumulatedWarningCount > 0;
                bool useWarningOverlay = hasWarnings && !(settings != null && settings.ignoreWarningOverlay);
                bool useWarningSound = hasWarnings && !(settings != null && settings.ignoreWarningSound);

                CompileMonkeyState nextState = useWarningOverlay ? CompileMonkeyState.Warning : CompileMonkeyState.Success;

                // Save to SessionState so it persists across the domain reload
                SessionState.SetInt(PendingStateKey, (int)nextState);
                SessionState.SetInt(ErrorCountKey, 0);
                SessionState.SetInt(WarningCountKey, accumulatedWarningCount);
                SessionState.SetBool(PlayWarningSoundKey, useWarningSound);

                // Do not unsubscribe from loops here so the overlay remains alive and active
                // during the domain reload preparation window.
            }
        }

        static void TransitionToResultState(CompileMonkeyState state, int errors, int warnings)
        {
            currentState = state;
            currentErrorCount = errors;
            currentWarningCount = warnings;
            resultStateStartTime = -1; // Set to -1 so it starts on the first active Update tick
            fadeAlpha = 1f;

            SessionState.SetInt(StateKey, (int)state);

            if (state == CompileMonkeyState.Error)
            {
                pendingResultSfxAndStopMusic = true;
                pendingPlaySuccessWarningSfx = false;
                pendingStopCompilationMusic = false;
            }
            else
            {
                pendingResultSfxAndStopMusic = false;
                pendingPlaySuccessWarningSfx = true;
                pendingStopCompilationMusic = true;
            }

            SubscribeToLoops();
            SceneView.RepaintAll();
        }

        static void PlayDeferredSfx()
        {
            CompileMonkeySettings settings = GetSettings();
            if (settings == null) return;
            if (!settings.useCompileSFX) return;

            if (currentState == CompileMonkeyState.Success)
            {
                if (currentWarningCount > 0 && playWarningSoundOnDeferred && settings.warningSfx != null)
                {
                    CompileMonkeyAudio.Play(settings.warningSfx, false);
                }
                else if (settings.successSfx != null)
                {
                    CompileMonkeyAudio.Play(settings.successSfx, false);
                }
            }
            else if (currentState == CompileMonkeyState.Warning)
            {
                if (playWarningSoundOnDeferred && settings.warningSfx != null)
                {
                    CompileMonkeyAudio.Play(settings.warningSfx, false);
                }
                else if (settings.successSfx != null)
                {
                    CompileMonkeyAudio.Play(settings.successSfx, false);
                }
            }
            else if (currentState == CompileMonkeyState.Error)
            {
                if (settings.errorSfx != null)
                {
                    CompileMonkeyAudio.Play(settings.errorSfx, false);
                }
            }
        }

        static void Update()
        {
            CompileMonkeySettings settings = GetSettings();
            float displayTime = settings != null ? settings.resultDisplayTime : 2f;
            float fadeTime = settings != null ? settings.fadeDuration : 0.5f;

            double now = EditorApplication.timeSinceStartup;

            if (currentState == CompileMonkeyState.Compiling)
            {
                // Auto-loop time-based fallback: If not playing via MCI and compile music has finished its duration, restart it
                if (isMusicPlaying && !CompileMonkeyMCI.IsOpen && UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    if (settings != null && settings.compileMusic != null)
                    {
                        double elapsed = EditorApplication.timeSinceStartup - musicPlayStartTime;
                        if (elapsed >= settings.compileMusic.length - 0.05f)
                        {
                            CompileMonkeyAudio.Play(settings.compileMusic, false);
                            musicPlayStartTime = EditorApplication.timeSinceStartup;
                        }
                    }
                }
                // Constantly repaint to animate compile dots
                SceneView.RepaintAll();
            }
            else if (currentState == CompileMonkeyState.Success ||
                     currentState == CompileMonkeyState.Warning ||
                     currentState == CompileMonkeyState.Error)
            {
                if (resultStateStartTime < 0)
                {
                    resultStateStartTime = now; // Initialize timer on first active update tick
                }

                // Fallback in case no Scene View is open to trigger DrawOverlay
                if (pendingResultSfxAndStopMusic)
                {
                    double elapsedMusicDelay = now - resultStateStartTime;
                    float delay = (settings != null && currentState != CompileMonkeyState.Error) ? settings.musicStopDelay : 0f;
                    if (elapsedMusicDelay >= delay)
                    {
                        pendingResultSfxAndStopMusic = false;
                        StopCompilationMusic();
                        PlayDeferredSfx();
                    }
                }

                if (pendingPlaySuccessWarningSfx)
                {
                    double elapsedSfxDelay = now - resultStateStartTime;
                    float sfxDelay = settings != null ? settings.successWarningSfxDelay : 0.15f;
                    if (elapsedSfxDelay >= sfxDelay)
                    {
                        pendingPlaySuccessWarningSfx = false;
                        PlayDeferredSfx();
                    }
                }

                if (pendingStopCompilationMusic)
                {
                    double elapsedMusicDelay = now - resultStateStartTime;
                    float delay = settings != null ? settings.musicStopDelay : 0.5f;
                    if (elapsedMusicDelay >= delay)
                    {
                        pendingStopCompilationMusic = false;
                        StopCompilationMusic();
                    }
                }

                double elapsed = now - resultStateStartTime;
                if (elapsed >= displayTime)
                {
                    fadingFromState = currentState;
                    currentState = CompileMonkeyState.Fading;
                    fadeStartTime = now;
                    SessionState.SetInt(StateKey, (int)CompileMonkeyState.Fading);
                }
                SceneView.RepaintAll();
            }
            else if (currentState == CompileMonkeyState.Fading)
            {
                double elapsed = now - fadeStartTime;
                if (elapsed >= fadeTime)
                {
                    currentState = CompileMonkeyState.Idle;
                    fadeAlpha = 0f;
                    SessionState.SetInt(StateKey, (int)CompileMonkeyState.Idle);
                    UnsubscribeFromLoops();
                }
                else
                {
                    fadeAlpha = 1f - (float)(elapsed / fadeTime);
                }
                SceneView.RepaintAll();
            }
            else
            {
                UnsubscribeFromLoops();
            }
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (currentState == CompileMonkeyState.Idle)
                return;

            // Block mouse/keyboard interactions with the Scene View during active overlay
            Event current = Event.current;
            if (current != null)
            {
                if (current.type == EventType.MouseDown ||
                    current.type == EventType.MouseUp ||
                    current.type == EventType.MouseMove ||
                    current.type == EventType.MouseDrag ||
                    current.type == EventType.KeyDown ||
                    current.type == EventType.KeyUp ||
                    current.type == EventType.ScrollWheel)
                {
                    current.Use();
                }
            }

            Handles.BeginGUI();
            DrawOverlay(sceneView);
            Handles.EndGUI();

            // Force Scene View to repaint on next frame during compilation
            // This bypasses EditorApplication.update suspension during compilation freezes
            if (currentState == CompileMonkeyState.Compiling)
            {
                // If the application became active/focused, start playing the music
                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive && !isMusicPlaying)
                {
                    CompileMonkeySettings settings = GetSettings();
                    if (settings != null && settings.compileMusic != null)
                    {
                        PlayCompilationMusic(settings.compileMusic);
                    }
                }

                sceneView.Repaint();
            }
        }

        static void DrawOverlay(SceneView sceneView)
        {
            CompileMonkeySettings settings = GetSettings();
            if (pendingResultSfxAndStopMusic)
            {
                if (resultStateStartTime < 0)
                {
                    resultStateStartTime = EditorApplication.timeSinceStartup;
                }

                double elapsed = EditorApplication.timeSinceStartup - resultStateStartTime;
                float delay = (settings != null && currentState != CompileMonkeyState.Error) ? settings.musicStopDelay : 0f;
                if (elapsed >= delay)
                {
                    pendingResultSfxAndStopMusic = false;
                    StopCompilationMusic();
                    PlayDeferredSfx();
                }
            }

            if (pendingPlaySuccessWarningSfx)
            {
                if (resultStateStartTime < 0)
                {
                    resultStateStartTime = EditorApplication.timeSinceStartup;
                }

                double elapsed = EditorApplication.timeSinceStartup - resultStateStartTime;
                float sfxDelay = settings != null ? settings.successWarningSfxDelay : 0.15f;
                if (elapsed >= sfxDelay)
                {
                    pendingPlaySuccessWarningSfx = false;
                    PlayDeferredSfx();
                }
            }

            if (pendingStopCompilationMusic)
            {
                if (resultStateStartTime < 0)
                {
                    resultStateStartTime = EditorApplication.timeSinceStartup;
                }

                double elapsed = EditorApplication.timeSinceStartup - resultStateStartTime;
                float delay = settings != null ? settings.musicStopDelay : 0.5f;
                if (elapsed >= delay)
                {
                    pendingStopCompilationMusic = false;
                    StopCompilationMusic();
                }
            }

            Rect rect = new Rect(0, 0, sceneView.position.width, sceneView.position.height);

            // 1. Determine background color based on active state
            Color backgroundColor = Color.black;
            CompileMonkeyState displayState = (currentState == CompileMonkeyState.Fading) ? fadingFromState : currentState;

            switch (displayState)
            {
                case CompileMonkeyState.Compiling:
                    backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
                    break;
                case CompileMonkeyState.Success:
                    backgroundColor = new Color(0.02f, 0.15f, 0.03f, 0.9f);
                    break;
                case CompileMonkeyState.Warning:
                    backgroundColor = new Color(0.18f, 0.12f, 0.02f, 0.9f);
                    break;
                case CompileMonkeyState.Error:
                    backgroundColor = new Color(0.18f, 0.03f, 0.03f, 0.9f);
                    break;
            }

            // Apply fade factor if in fading state
            if (currentState == CompileMonkeyState.Fading)
            {
                backgroundColor.a *= fadeAlpha;
            }

            // Fill full Scene View rect
            EditorGUI.DrawRect(rect, backgroundColor);

            // 2. Load texture asset
            Texture2D image = null;
            if (settings != null)
            {
                switch (displayState)
                {
                    case CompileMonkeyState.Compiling:
                        image = settings.compilingImage;
                        break;
                    case CompileMonkeyState.Success:
                        image = settings.successImage;
                        break;
                    case CompileMonkeyState.Warning:
                        image = settings.warningImage;
                        break;
                    case CompileMonkeyState.Error:
                        image = settings.errorImage;
                        break;
                }
            }

            // 3. Layout coordinates (centered but vertically offset)
            float contentCenterY = rect.height * 0.5f;
            float yOffset = settings != null ? settings.offsetY : 50f;
            float shiftedCenterY = contentCenterY + yOffset;

            float maxImgSizeSetting = settings != null ? settings.maxImageSize : 200f;
            float maxImageSize = Mathf.Min(maxImgSizeSetting, rect.width * 0.4f);

            // Calculate text Y coordinate (placed below the shifted center)
            float textStartY = shiftedCenterY + 10f;

            // 4. Draw Image
            if (image != null)
            {
                float imageWidth = maxImageSize;
                float imageHeight = maxImageSize * ((float)image.height / image.width);

                float imageX = (rect.width - imageWidth) * 0.5f;
                float imageY = shiftedCenterY - imageHeight - 15f; // Position above the shifted center

                Rect imageRect = new Rect(imageX, imageY, imageWidth, imageHeight);

                Color prevColor = GUI.color;
                if (currentState == CompileMonkeyState.Fading)
                {
                    GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
                }
                GUI.DrawTexture(imageRect, image, ScaleMode.ScaleToFit);
                GUI.color = prevColor;
            }

            // 4. Create and customize text styles
            GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Normal
            };

            Color titleColor = Color.white;
            string titleText = "";
            string subtitleText = "";

            switch (displayState)
            {
                case CompileMonkeyState.Compiling:
                    titleColor = Color.white;
                    int dotCount = (int)(EditorApplication.timeSinceStartup * 2.5f) % 4;
                    titleText = "Compiling" + new string('.', dotCount);
                    subtitleText = "Building assemblies, please wait...";
                    break;
                case CompileMonkeyState.Success:
                    titleColor = new Color(0.4f, 1.0f, 0.4f);
                    titleText = "SUCCESS!";
                    subtitleText = "Compilation completed with no errors.";
                    break;
                case CompileMonkeyState.Warning:
                    titleColor = new Color(1.0f, 0.85f, 0.2f);
                    titleText = "WARNING!";
                    subtitleText = $"Compilation completed with {currentWarningCount} warning{(currentWarningCount > 1 ? "s" : "")}.";
                    break;
                case CompileMonkeyState.Error:
                    titleColor = new Color(1.0f, 0.3f, 0.3f);
                    titleText = "ERROR!";
                    subtitleText = $"Compilation failed with {currentErrorCount} error{(currentErrorCount > 1 ? "s" : "")}.";
                    break;
            }

            // Apply fading to texts
            if (currentState == CompileMonkeyState.Fading)
            {
                titleColor.a *= fadeAlpha;
            }
            titleStyle.normal.textColor = titleColor;

            Color subColor = new Color(0.85f, 0.85f, 0.85f, currentState == CompileMonkeyState.Fading ? fadeAlpha * 0.7f : 0.7f);
            subtitleStyle.normal.textColor = subColor;

            Rect titleRect = new Rect(0, textStartY, rect.width, 35);
            Rect subtitleRect = new Rect(0, textStartY + 35, rect.width, 25);

            GUI.Label(titleRect, titleText, titleStyle);
            GUI.Label(subtitleRect, subtitleText, subtitleStyle);

            // 5. Draw setup configuration fallback message
            if (settings == null)
            {
                GUIStyle fallbackStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Italic
                };
                fallbackStyle.normal.textColor = new Color(1f, 1f, 0.5f, currentState == CompileMonkeyState.Fading ? fadeAlpha * 0.8f : 0.8f);

                Rect fallbackRect = new Rect(0, rect.height - 40, rect.width, 25);
                GUI.Label(fallbackRect, "CompileMonkey: Create Settings via Right-Click -> Create -> CompileMonkey -> Settings", fallbackStyle);
            }
        }
    }
}
