using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CryingSnow.StackCraft
{
    public class GraphicsManager : MonoBehaviour
    {
        public static GraphicsManager Instance { get; private set; }

        private List<Resolution> resolutions = new();
        private Resolution currentRes;

        private static readonly FullScreenMode[] fullscreenModes =
        {
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed,
        };
        private FullScreenMode currentMode;

        private int currentVSync;

        private static readonly int[] fpsCaps =
        {
            -1, 30, 60, 120, 144, 240
        };
        private int currentFpsCap;

        private enum ShadowPreset { Off, Low, Medium, High, Ultra }
        private ShadowPreset currentShadowPreset;

        private const string SCREEN_WIDTH_KEY = "ScreenWidth";
        private const string SCREEN_HEIGHT_KEY = "ScreenHeight";
        private const string FULLSCREEN_MODE_KEY = "FullscreenMode";
        private const string VSYNC_KEY = "VSyncMode";
        private const string FPS_CAP_KEY = "FrameRateCap";
        private const string SHADOW_KEY = "ShadowPreset";

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            PopulateScreenResolutions();
            InitGraphicsSettings();
        }

        private void Update()
        {
            // Update the global "_UnscaledTime" shader variable each frame.
            // Used by card outline/highlight shaders to animate effects with real-time,
            // independent of Time.timeScale (continues animating while the game is paused).
            Shader.SetGlobalFloat("_UnscaledTime", Time.unscaledTime);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// The main entry point for applying all saved graphics settings upon game startup.
        /// </summary>
        /// <remarks>
        /// This method sequentially calls the initialization routines for screen resolution (including
        /// fullscreen mode), performance settings (VSync and FPS cap), and shadow quality,
        /// ensuring the game state reflects the user's last saved preferences.
        /// </remarks>
        public void InitGraphicsSettings()
        {
            InitScreenResolution();
            InitPerformanceSettings();
            InitShadowQuality();
        }

        private void PopulateScreenResolutions()
        {
            resolutions.Clear();

            Resolution[] hardwareResolutions = Screen.resolutions;

            var uniqueResolutions = new List<Resolution>();

            foreach (var res in hardwareResolutions)
            {
                // Check if we already have this resolution (ignoring refresh rate)
                if (!uniqueResolutions.Any(x => x.width == res.width && x.height == res.height))
                {
                    uniqueResolutions.Add(res);
                }
            }

            resolutions = uniqueResolutions
                .OrderBy(r => r.width)
                .ThenBy(r => r.height)
                .ToList();
        }

        private void InitScreenResolution()
        {
            currentMode = (FullScreenMode)PlayerPrefs.GetInt(
                FULLSCREEN_MODE_KEY,
                (int)FullScreenMode.FullScreenWindow
            );

            if (!fullscreenModes.Contains(currentMode))
                currentMode = FullScreenMode.FullScreenWindow;

            Screen.fullScreenMode = currentMode;

            if (PlayerPrefs.HasKey(SCREEN_WIDTH_KEY) && PlayerPrefs.HasKey(SCREEN_HEIGHT_KEY))
            {
                int width = PlayerPrefs.GetInt(SCREEN_WIDTH_KEY);
                int height = PlayerPrefs.GetInt(SCREEN_HEIGHT_KEY);

                Screen.SetResolution(width, height, currentMode);

                currentRes = resolutions.FirstOrDefault(r =>
                    r.width == width &&
                    r.height == height
                );

                if (currentRes.width == 0 || currentRes.height == 0)
                {
                    currentRes = resolutions[0];
                }
            }
            else
            {
                const int targetW = 1920;
                const int targetH = 1080;

                Resolution defaultRes = resolutions[0];
                int bestScore = int.MaxValue;

                foreach (var res in resolutions)
                {
                    int dx = res.width - targetW;
                    int dy = res.height - targetH;
                    int score = dx * dx + dy * dy;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        defaultRes = res;
                    }
                }

                Screen.SetResolution(defaultRes.width, defaultRes.height, currentMode);

                currentRes = defaultRes;

                PlayerPrefs.SetInt(SCREEN_WIDTH_KEY, defaultRes.width);
                PlayerPrefs.SetInt(SCREEN_HEIGHT_KEY, defaultRes.height);
                PlayerPrefs.Save();
            }
        }

        private void InitPerformanceSettings()
        {
            // --- Restore vSync ---
            currentVSync = PlayerPrefs.GetInt(VSYNC_KEY, 1); // default: ON (1)
            QualitySettings.vSyncCount = Mathf.Clamp(currentVSync, 0, 2);

            // --- Restore FPS Cap ---
            currentFpsCap = PlayerPrefs.GetInt(FPS_CAP_KEY, -1); // -1 = unlimited
            Application.targetFrameRate = currentFpsCap;
        }

        private void InitShadowQuality()
        {
            currentShadowPreset = (ShadowPreset)PlayerPrefs.GetInt(SHADOW_KEY, (int)ShadowPreset.High);
            ApplyShadowPreset(currentShadowPreset);
        }
        #endregion

        #region Public Control API
        /// <summary>
        /// Cycles the current screen resolution to the next available option in the list 
        /// of supported resolutions and immediately applies the new setting.
        /// </summary>
        /// <remarks>
        /// The list of resolutions is populated at startup. When the end of the list is reached, 
        /// it wraps back to the beginning. The change is saved to PlayerPrefs.
        /// </remarks>
        /// <returns>The newly set screen Resolution object.</returns>
        public Resolution CycleScreenResolution()
        {
            int index = resolutions.IndexOf(currentRes);
            if (index < 0) index = 0;

            int next = (index + 1) % resolutions.Count;
            currentRes = resolutions[next];

            Screen.SetResolution(currentRes.width, currentRes.height, currentMode);

            PlayerPrefs.SetInt(SCREEN_WIDTH_KEY, currentRes.width);
            PlayerPrefs.SetInt(SCREEN_HEIGHT_KEY, currentRes.height);
            PlayerPrefs.SetInt(FULLSCREEN_MODE_KEY, (int)Screen.fullScreenMode);
            PlayerPrefs.Save();

            return currentRes;
        }

        /// <summary>
        /// Cycles the screen's full-screen mode between available options (e.g., FullScreenWindow, Windowed)
        /// and immediately applies the new setting.
        /// </summary>
        /// <remarks>
        /// The new mode is applied via Screen.SetResolution() and the selection is saved to PlayerPrefs.
        /// </remarks>
        public void CycleFullscreenMode()
        {
            int index = System.Array.IndexOf(fullscreenModes, currentMode);

            if (index < 0) index = 0;

            int next = (index + 1) % fullscreenModes.Length;
            currentMode = fullscreenModes[next];

            Screen.fullScreenMode = currentMode;
            Screen.SetResolution(currentRes.width, currentRes.height, currentMode);

            PlayerPrefs.SetInt(FULLSCREEN_MODE_KEY, (int)currentMode);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Toggles VSync (Vertical Synchronization) between the available modes (e.g., On, Off, Every VBlank)
        /// and applies the change to QualitySettings.
        /// </summary>
        /// <remarks>
        /// Setting VSync to anything other than 'Off' automatically overrides the frame rate cap.
        /// The new VSync mode is saved to PlayerPrefs.
        /// </remarks>
        public void CycleVSync()
        {
            currentVSync++;

            if (currentVSync > 2)
                currentVSync = 0;

            QualitySettings.vSyncCount = currentVSync;

            PlayerPrefs.SetInt(VSYNC_KEY, currentVSync);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Cycles the target frame rate (FPS cap) through a predefined list of values.
        /// </summary>
        /// <remarks>
        /// A value of -1 means the frame rate is uncapped or controlled by VSync.
        /// The new frame rate is applied to Application.targetFrameRate and saved to PlayerPrefs.
        /// </remarks>
        public void CycleFrameRateCap()
        {
            int index = System.Array.IndexOf(fpsCaps, currentFpsCap);
            if (index < 0) index = 0;

            int next = (index + 1) % fpsCaps.Length;
            currentFpsCap = fpsCaps[next];

            Application.targetFrameRate = currentFpsCap;

            PlayerPrefs.SetInt(FPS_CAP_KEY, currentFpsCap);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Cycles the shadow quality preset through a predefined list (Off, Low, Medium, High, Ultra)
        /// and applies the corresponding quality settings.
        /// </summary>
        /// <remarks>
        /// This adjusts the QualitySettings.shadows and QualitySettings.shadowResolution properties.
        /// The selected preset is saved to PlayerPrefs.
        /// </remarks>
        public void CycleShadowPreset()
        {
            currentShadowPreset = (ShadowPreset)(((int)currentShadowPreset + 1) % System.Enum.GetValues(typeof(ShadowPreset)).Length);

            ApplyShadowPreset(currentShadowPreset);

            PlayerPrefs.SetInt(SHADOW_KEY, (int)currentShadowPreset);
            PlayerPrefs.Save();
        }
        #endregion

        #region Public Formatters
        /// <summary>
        /// Generates a formatted string representing the current screen resolution and refresh rate.
        /// </summary>
        /// <returns>A string representing the currently applied screen resolution setting.</returns>
        public string GetResolutionLabel()
        {
            return $"分辨率 {currentRes.width}x{currentRes.height}";
        }

        /// <summary>
        /// Generates a user-friendly string label for the current fullscreen mode.
        /// </summary>
        /// <returns>A string representing the currently applied fullscreen mode.</returns>
        public string GetFullscreenLabel()
        {
            return currentMode == FullScreenMode.Windowed
                ? "全屏（窗口化）"
                : "全屏（开启）";
        }

        /// <summary>
        /// Generates a user-friendly string label for the current VSync (Vertical Synchronization) setting.
        /// </summary>
        /// <returns>A string representing the currently applied VSync setting.</returns>
        public string FormatVSyncLabel()
        {
            return currentVSync switch
            {
                0 => "垂直同步 关闭",
                1 => "垂直同步 开启",
                2 => "垂直同步 半速",
                _ => "垂直同步 未知"
            };
        }

        /// <summary>
        /// Generates a user-friendly string label for the current target frame rate (FPS cap).
        /// </summary>
        /// <returns>A string representing the currently applied frame rate cap.</returns>
        public string FormatFpsLabel()
        {
            return currentFpsCap == -1
                ? "帧率 无限制"
                : $"帧率 {currentFpsCap}";
        }

        /// <summary>
        /// Generates a user-friendly string label for the current shadow quality preset.
        /// </summary>
        /// <returns>A string representing the currently applied shadow quality preset.</returns>
        public string FormatShadowLabel()
        {
            return $"阴影 {ChineseLocalization.ShadowPresetName(currentShadowPreset.ToString())}";
        }
        #endregion

        #region Helper Methods
        private void ApplyShadowPreset(ShadowPreset preset)
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (urpAsset != null)
            {
                ApplyURPShadows(urpAsset, preset);
            }
            else
            {
                // Fallback for Built-in Render Pipeline
                switch (preset)
                {
                    case ShadowPreset.Off:
                        QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                        break;
                    case ShadowPreset.Low:
                        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
                        break;
                    case ShadowPreset.Medium:
                        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                        break;
                    case ShadowPreset.High:
                        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                        break;
                    case ShadowPreset.Ultra:
                        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                        break;
                }
            }
        }

        private void ApplyURPShadows(UniversalRenderPipelineAsset asset, ShadowPreset preset)
        {
            int resolution = 2048;
            float distance = 50f;
            int cascades = 4;

            switch (preset)
            {
                case ShadowPreset.Off:
                    distance = 0f;
                    break;
                case ShadowPreset.Low:
                    resolution = 512;
                    distance = 10f;
                    cascades = 1;
                    break;
                case ShadowPreset.Medium:
                    resolution = 1024;
                    distance = 20f;
                    cascades = 2;
                    break;
                case ShadowPreset.High:
                    resolution = 2048;
                    distance = 30f;
                    cascades = 3;
                    break;
                case ShadowPreset.Ultra:
                    resolution = 4096;
                    distance = 40f;
                    cascades = 4;
                    break;
            }

            // 1. Set Main Light Shadow Resolution
            var shadowResProp = typeof(UniversalRenderPipelineAsset).GetProperty("mainLightShadowmapResolution",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (shadowResProp != null)
            {
                shadowResProp.SetValue(asset, resolution);
            }

            // 2. Set Shadow Distance
            asset.shadowDistance = distance;

            // 3. Set Cascades
            asset.shadowCascadeCount = cascades;
        }
        #endregion
    }
}
