using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.StackCraft
{
    public class GameOptionsUI : MonoBehaviour
    {
        [Header("Graphics")]
        [SerializeField, Tooltip("The button used to cycle through available screen resolutions.")]
        private TextButton resolutionButton;

        [SerializeField, Tooltip("The button used to cycle through fullscreen modes (e.g., Windowed, Fullscreen).")]
        private TextButton fullscreenButton;

        [SerializeField, Tooltip("The button used to toggle Vertical Sync (V-Sync) ON or OFF.")]
        private TextButton vSyncButton;

        [SerializeField, Tooltip("The button used to cycle through the maximum frame rate cap (FPS limit).")]
        private TextButton fpsButton;

        [SerializeField, Tooltip("The button used to cycle through different Shadow quality presets.")]
        private TextButton shadowButton;

        [Header("Audio")]
        [SerializeField, Tooltip("The TextMeshPro label displaying the current volume percentage for Sound Effects (SFX).")]
        private TextMeshProUGUI labelSFX;

        [SerializeField, Tooltip("The Slider component used to adjust the volume level of Sound Effects (SFX).")]
        private Slider sliderSFX;

        [SerializeField, Tooltip("The TextMeshPro label displaying the current volume percentage for Background Music (BGM).")]
        private TextMeshProUGUI labelBGM;

        [SerializeField, Tooltip("The Slider component used to adjust the volume level of Background Music (BGM).")]
        private Slider sliderBGM;

        [Header("Footer")]
        [SerializeField, Tooltip("The button that opens a confirmation modal to reset all game settings to default.")]
        private TextButton resetButton;

        [SerializeField, Tooltip("The button used to save and close the Game Options UI panel.")]
        private TextButton closeButton;

        [Header("Screens")]
        [SerializeField, Tooltip("Reference to the generic Modal Window used for confirmation prompts, such as resetting settings.")]
        private ModalWindow modalWindow;

        private void Start()
        {
            resolutionButton.SetOnClick(() =>
            {
                var res = GraphicsManager.Instance.CycleScreenResolution();
                resolutionButton.SetText($"分辨率 {res.width}x{res.height}");
            });

            fullscreenButton.SetOnClick(() =>
            {
                GraphicsManager.Instance.CycleFullscreenMode();
                fullscreenButton.SetText(GraphicsManager.Instance.GetFullscreenLabel());
            });

            vSyncButton.SetOnClick(() =>
            {
                GraphicsManager.Instance.CycleVSync();
                vSyncButton.SetText(GraphicsManager.Instance.FormatVSyncLabel());
            });

            fpsButton.SetOnClick(() =>
            {
                GraphicsManager.Instance.CycleFrameRateCap();
                fpsButton.SetText(GraphicsManager.Instance.FormatFpsLabel());
            });

            shadowButton.SetOnClick(() =>
            {
                GraphicsManager.Instance.CycleShadowPreset();
                shadowButton.SetText(GraphicsManager.Instance.FormatShadowLabel());
            });

            InitButtonLabels();

            sliderSFX.onValueChanged.AddListener(value =>
            {
                AudioManager.Instance?.SetSFXVolume(value);
                labelSFX.text = $"音效 {Mathf.RoundToInt(value * 100)}%";
            });

            sliderBGM.onValueChanged.AddListener(value =>
            {
                AudioManager.Instance?.SetBGMVolume(value);
                labelBGM.text = $"音乐 {Mathf.RoundToInt(value * 100)}%";
            });

            InitVolumeSliders();

            resetButton.SetOnClick(() =>
                modalWindow.Show(
                    "重置所有设置？",
                    "这会把全部游戏设置恢复为默认值，且无法撤销。",
                    ResetAllSettings
                )
            );

            closeButton.SetOnClick(Close);
        }

        private void InitButtonLabels()
        {
            resolutionButton.SetText(GraphicsManager.Instance.GetResolutionLabel());
            fullscreenButton.SetText(GraphicsManager.Instance.GetFullscreenLabel());
            vSyncButton.SetText(GraphicsManager.Instance.FormatVSyncLabel());
            fpsButton.SetText(GraphicsManager.Instance.FormatFpsLabel());
            shadowButton.SetText(GraphicsManager.Instance.FormatShadowLabel());
        }

        private void InitVolumeSliders()
        {
            sliderSFX.value = AudioManager.Instance.GetSavedSFXVolumeSlider();
            sliderBGM.value = AudioManager.Instance.GetSavedBGMVolumeSlider();
        }

        private void OnDestroy()
        {
            sliderSFX.onValueChanged.RemoveAllListeners();
            sliderBGM.onValueChanged.RemoveAllListeners();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }

        private void ResetAllSettings()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            GraphicsManager.Instance?.InitGraphicsSettings();
            InitButtonLabels();

            AudioManager.Instance?.InitAudioMixerVolumes();
            InitVolumeSliders();
        }
    }
}
