using UnityEngine;

namespace CompileMonkey
{
    [CreateAssetMenu(fileName = "CompileMonkeySettings", menuName = "CompileMonkey/Settings")]
    public class CompileMonkeySettings : ScriptableObject
    {
        [Header("Overlay Textures")]
        public Texture2D compilingImage;
        public Texture2D successImage;
        public Texture2D warningImage;
        public Texture2D errorImage;

        [Header("Audio Clips")]
        public AudioClip compileMusic;
        public AudioClip successSfx;
        public AudioClip warningSfx;
        public AudioClip errorSfx;

        [Header("Timing Configuration")]
        [Tooltip("How long the success/warning/error screen remains visible before starting to fade.")]
        public float resultDisplayTime = 2f;

        [Tooltip("The duration of the fade out transition in seconds.")]
        public float fadeDuration = 0.5f;

        [Tooltip("Delay in seconds before the compilation music stops once the overlay is visible.")]
        public float musicStopDelay = 0.5f;

        [Tooltip("Delay in seconds before the success/warning chime plays once the overlay is visible. This helps ensure the sound plays after the Unity compilation progress bar is fully gone.")]
        public float successWarningSfxDelay = 0.15f;

        [Header("Overlay Layout")]
        [Tooltip("Maximum size of the overlay status image in pixels.")]
        public float maxImageSize = 200f;

        [Tooltip("Vertical offset of the overlay content from the center of the Scene View. Positive values shift it lower, negative values shift it higher.")]
        public float offsetY = 50f;

        [Header("Preferences")]
        [Tooltip("If true, compilation background music will be played.")]
        public bool useMusic = true;

        [Tooltip("If true, results sound effects (Success/Warning/Error chimes) will be played.")]
        public bool useCompileSFX = true;

        [Tooltip("If true, compilation warnings will not show the yellow warning overlay (falls back to success overlay).")]
        public bool ignoreWarningOverlay = false;

        [Tooltip("If true, compilation warnings will not play the warning SFX (falls back to success SFX).")]
        public bool ignoreWarningSound = false;
    }
}

