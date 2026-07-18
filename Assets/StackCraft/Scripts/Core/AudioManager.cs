using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace CryingSnow.StackCraft
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX Settings")]
        [SerializeField, Tooltip("Number of AudioSource objects in the pool for playing SFX.")]
        private int _SFXPoolSize = 8;

        [SerializeField, Tooltip("Randomly vary pitch for sound effects for a more natural feel.")]
        private bool _randomizeSFXPitch = true;

        [Header("BGM Settings")]
        [SerializeField, Tooltip("Default music clip to play on scene load.")]
        private AudioClip _BGMClip;

        [SerializeField, Tooltip("Default music volume for the BGM AudioSource.")]
        private float _defaultMusicVolume = 0.3f;

        [SerializeField, Tooltip("Minimum delay (in seconds) before the same SFX can be played again.")]
        private float _SFXCooldown = 0.05f;

        [Header("Audio Mixer")]
        [SerializeField, Tooltip("Main AudioMixer controlling overall game audio.")]
        private AudioMixer _audioMixer;

        [SerializeField, Tooltip("AudioMixerGroup assigned to all sound effects.")]
        private AudioMixerGroup _SFXAudioGroup;

        [SerializeField, Tooltip("AudioMixerGroup assigned to background music.")]
        private AudioMixerGroup _BGMAudioGroup;

        [Header("Sound Effects")]
        [SerializeField, Tooltip("List of all sound effects with their AudioId identifiers.")]
        private List<AudioData> _SFXDataList;

        private Dictionary<AudioId, AudioClip> _SFXClipLookup;
        private Dictionary<AudioId, float> _lastPlayedTime = new();
        private List<AudioSource> _SFXSourcePool;
        private int _nextSFXSourceIndex = 0;

        private AudioSource _BGMSource;

        private const string SFX_VOL_KEY = "VolumeSFX";
        private const string BGM_VOL_KEY = "VolumeBGM";

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

            _SFXClipLookup = _SFXDataList.ToDictionary(
                data => data.audioId,
                data => data.audioClip
            );

            // Create SFX AudioSource Pool
            _SFXSourcePool = new List<AudioSource>(_SFXPoolSize);
            for (int i = 0; i < _SFXPoolSize; i++)
            {
                var sourceObj = new GameObject($"SFX_AudioSource_{i + 1}");
                sourceObj.transform.SetParent(transform);
                var source = sourceObj.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = _SFXAudioGroup;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _SFXSourcePool.Add(source);
            }

            // Initialize BGM AudioSource
            _BGMSource = gameObject.AddComponent<AudioSource>();
            _BGMSource.clip = _BGMClip;
            _BGMSource.outputAudioMixerGroup = _BGMAudioGroup;
            _BGMSource.volume = _defaultMusicVolume;
            _BGMSource.playOnAwake = false;
            _BGMSource.spatialBlend = 0f;
            _BGMSource.loop = true;
            _BGMSource.Play();
        }

        private void Start()
        {
            InitAudioMixerVolumes();
        }

        /// <summary>
        /// Initializes the volume levels of the Sound Effects (SFX) and Background Music (BGM)
        /// mixer groups by loading the saved decibel values from PlayerPrefs.
        /// </summary>
        /// <remarks>
        /// If no saved value is found for a mixer group, it defaults the volume to 0 dB.
        /// This method ensures the AudioMixer reflects the user's last saved volume preferences upon startup.
        /// </remarks>
        public void InitAudioMixerVolumes()
        {
            _audioMixer.SetFloat(SFX_VOL_KEY, PlayerPrefs.GetFloat(SFX_VOL_KEY, 0f));
            _audioMixer.SetFloat(BGM_VOL_KEY, PlayerPrefs.GetFloat(BGM_VOL_KEY, 0f));
        }

        private AudioSource GetNextSource()
        {
            var source = _SFXSourcePool[_nextSFXSourceIndex];
            _nextSFXSourceIndex = (_nextSFXSourceIndex + 1) % _SFXPoolSize;

            if (_randomizeSFXPitch)
                source.pitch = Random.Range(0.95f, 1.05f);
            else
                source.pitch = 1f;

            return source;
        }

        /// <summary>
        /// Plays a sound effect (SFX) based on the given AudioId.
        /// Uses a pooled AudioSource to prevent allocating new sources at runtime.
        /// Supports both 2D (UI / general) and 3D positional sound effects.
        /// Optionally lowers or pauses background music if interruptBGM is true.
        /// </summary>
        /// <param name="audioId">
        /// The identifier of the sound effect to play.
        /// </param>
        /// <param name="worldPosition">
        /// If null, the SFX is played as a 2D sound (spatialBlend = 0).  
        /// If a value is provided, the AudioSource is positioned in 3D space and played as a positional sound.
        /// </param>
        /// <param name="interruptBGM">
        /// If true, temporarily lowers the BGM volume for the duration of the SFX.  
        /// Useful for important or emphasized sounds (e.g., win / lose events).
        /// </param>
        public void PlaySFX(AudioId audioId, Vector3? worldPosition = null, bool interruptBGM = false)
        {
            // Cooldown check to avoid rapid spam of the same SFX
            if (_lastPlayedTime.TryGetValue(audioId, out float lastTime))
            {
                if (Time.unscaledTime - lastTime < _SFXCooldown)
                    return;
            }
            _lastPlayedTime[audioId] = Time.unscaledTime;

            // Attempt to look up the AudioClip by its AudioId
            if (!_SFXClipLookup.TryGetValue(audioId, out var clip))
            {
                Debug.LogWarning($"No clip found for: {audioId}");
                return;
            }

            // Retrieve the next pooled AudioSource
            var source = GetNextSource();

            // 2D UI / general SFX (non-spatial)
            if (!worldPosition.HasValue)
            {
                source.spatialBlend = 0f;
            }
            else
            {
                // 3D positional SFX (world-space audio)
                source.transform.position = worldPosition.Value;
                source.spatialBlend = 1f;
            }

            // Play the clip once without looping
            source.PlayOneShot(clip);

            // Optionally interrupt or lower background music
            if (interruptBGM)
            {
                _audioMixer.GetFloat(BGM_VOL_KEY, out float value);

                // Only pause BGM if it is currently audible
                if (value >= 0f)
                    StartCoroutine(PauseBGM(clip.length));
            }
        }

        private IEnumerator PauseBGM(float duration)
        {
            _audioMixer.GetFloat(BGM_VOL_KEY, out float originalVolume);
            _audioMixer.SetFloat(BGM_VOL_KEY, -80f);
            yield return new WaitForSeconds(duration);
            _audioMixer.SetFloat(BGM_VOL_KEY, originalVolume);
        }

        /// <summary>
        /// Sets the Sound Effects (SFX) volume using a normalized slider value (0–1).
        /// Updates the AudioMixer and persists the value using PlayerPrefs.
        /// </summary>
        /// <param name="value">
        /// Normalized slider value.
        /// 0 = muted, 1 = full volume.
        /// </param>
        public void SetSFXVolume(float value)
        {
            float dB = LinearToDecibels(value);

            _audioMixer.SetFloat(SFX_VOL_KEY, dB);
            PlayerPrefs.SetFloat(SFX_VOL_KEY, dB);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Sets the Background Music (BGM) volume using a normalized slider value (0–1).
        /// Updates the AudioMixer and persists the value using PlayerPrefs.
        /// </summary>
        /// <param name="value">
        /// Normalized slider value.
        /// 0 = muted, 1 = full volume.
        /// </param>
        public void SetBGMVolume(float value)
        {
            float dB = LinearToDecibels(value);

            _audioMixer.SetFloat(BGM_VOL_KEY, dB);
            PlayerPrefs.SetFloat(BGM_VOL_KEY, dB);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Converts a normalized slider value (0–1) into decibels for the AudioMixer.
        /// Uses a floor value to avoid -Infinity when slider is at 0.
        /// </summary>
        /// /// <param name="value">
        /// Normalized slider value.
        /// 0 = muted, 1 = full volume.
        /// </param>
        private float LinearToDecibels(float value)
        {
            // Slider at 0 = mute
            if (value <= 0.0001f)
                return -80f;

            // Convert to logarithmic dB scale
            return Mathf.Log10(value) * 20f;
        }

        /// <summary>
        /// Retrieves the current Sound Effects (SFX) volume from the AudioMixer and converts
        /// the decibel value back into a normalized slider value (0 to 1).
        /// </summary>
        /// <returns>A float between 0 (muted) and 1 (full volume) for use with a UI slider.</returns>
        public float GetSavedSFXVolumeSlider()
        {
            _audioMixer.GetFloat(SFX_VOL_KEY, out float dB);
            return Mathf.Clamp01(Mathf.Pow(10f, dB / 20f));
        }

        /// <summary>
        /// Retrieves the current Background Music (BGM) volume from the AudioMixer and converts
        /// the decibel value back into a normalized slider value (0 to 1).
        /// </summary>
        /// <returns>A float between 0 (muted) and 1 (full volume) for use with a UI slider.</returns>
        public float GetSavedBGMVolumeSlider()
        {
            _audioMixer.GetFloat(BGM_VOL_KEY, out float dB);
            return Mathf.Clamp01(Mathf.Pow(10f, dB / 20f));
        }
    }

    public enum AudioId
    {
        CardPick, CardDrop, CardSwipe,
        Coin, Coins, CashRegister,
        AttackMelee, AttackRanged, AttackMagic,
        HitMelee, HitRanged, HitMagic,
        Miss, Critical,
        Pop,
        Eat,
        Click,
        Puff
    }

    [System.Serializable]
    public class AudioData
    {
        public AudioId audioId;
        public AudioClip audioClip;
    }
}
