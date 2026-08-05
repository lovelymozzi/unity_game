using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField, Tooltip("Audio mixer to control the game's audio groups.")]
        private AudioMixer audioMixer;

        [SerializeField, Tooltip("Audio mixer group used for all background music (BGM).")]
        private AudioMixerGroup _BGMMixerGroup;

        [SerializeField, Tooltip("Audio mixer group used for all sound effects (SFX).")]
        private AudioMixerGroup _SFXMixerGroup;

        [Header("BGM Settings")]
        [SerializeField, Tooltip("Default volume level for background music audio source.")]
        private float defaultBGMVolume = 0.5f;

        [SerializeField, Tooltip("Duration (in seconds) used for fading BGM in and out.")]
        private float fadeDuration = 0.75f;

        [Header("SFX Settings")]
        [SerializeField, Tooltip("Minimum time between playing sound effects.")]
        private float minPlayRate = 0.05f;

        [SerializeField, Tooltip("List of audio data for sound effects.")]
        private List<AudioData> SFXList;

        public bool IsSFXOn { get; private set; }
        public bool IsBGMOn { get; private set; }

        private AudioSource _BGMPlayer;
        private AudioSource _SFXPlayer;

        private Dictionary<AudioID, AudioData> _SFXLookup;
        private AudioClip currentBGM;
        private float originalBGMVolume;
        private float cooldown;

        void Awake()
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

            InitializeAudioSources();
            InitializeAudioSettings();

            originalBGMVolume = _BGMPlayer.volume;
            _SFXLookup = SFXList.ToDictionary(x => x.id);
        }

        void Update()
        {
            if (cooldown > 0) cooldown -= Time.deltaTime;
        }

        private void InitializeAudioSources()
        {
            _BGMPlayer = gameObject.AddComponent<AudioSource>();
            _BGMPlayer.outputAudioMixerGroup = _BGMMixerGroup;
            _BGMPlayer.playOnAwake = false;
            _BGMPlayer.volume = defaultBGMVolume;

            _SFXPlayer = gameObject.AddComponent<AudioSource>();
            _SFXPlayer.outputAudioMixerGroup = _SFXMixerGroup;
            _SFXPlayer.playOnAwake = false;
        }

        private void InitializeAudioSettings()
        {
            // Load saved preferences
            IsSFXOn = PlayerPrefs.GetInt("SFX ON", 1) > 0;
            IsBGMOn = PlayerPrefs.GetInt("BGM ON", 1) > 0;

            // Apply settings immediately
            SetSFX(IsSFXOn);
            SetBGM(IsBGMOn);
        }

        public void SetSFX(bool isOn)
        {
            IsSFXOn = isOn;
            audioMixer.SetFloat("SFX Volume", isOn ? 0f : -80f);
            PlayerPrefs.SetInt("SFX ON", isOn ? 1 : 0);
        }

        public void SetBGM(bool isOn)
        {
            IsBGMOn = isOn;
            audioMixer.SetFloat("BGM Volume", isOn ? 0f : -80f);
            PlayerPrefs.SetInt("BGM ON", isOn ? 1 : 0);
        }

        /// <summary>
        /// Plays the background music (BGM) with optional looping and fading effects.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="loop">Whether the clip should loop.</param>
        /// <param name="fade">Whether to apply fading effects when changing clips.</param>
        public void PlayBGM(AudioClip clip, bool loop = true, bool fade = true)
        {
            if (clip == null || clip == currentBGM) return;

            currentBGM = clip;
            StartCoroutine(PlayBGMAsync(clip, loop, fade));
        }

        /// <summary>
        /// Plays a sound effect (SFX) with optional BGM pausing and pitch adjustment.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="pauseBGM">Whether to pause the BGM while playing the SFX.</param>
        /// <param name="pitch">The pitch of the SFX playback.</param>
        /// <param name="waitForCooldown">Whether to wait for cooldown before playing another SFX.</param>
        public void PlaySFX(AudioClip clip, bool pauseBGM = false, float pitch = 1f, bool waitForCooldown = true)
        {
            if (clip == null) return;
            if (waitForCooldown && cooldown > 0f) return;

            if (pauseBGM)
            {
                _BGMPlayer.Pause();
                StartCoroutine(UnPauseBGM(clip.length));
            }

            _SFXPlayer.pitch = pitch;
            _SFXPlayer.PlayOneShot(clip);
            cooldown = minPlayRate;
        }

        /// <summary>
        /// Plays a sound effect (SFX) identified by an AudioID.
        /// </summary>
        /// <param name="audioID">The AudioID of the sound effect to play.</param>
        /// <param name="pauseBGM">Whether to pause the BGM while playing the SFX.</param>
        /// <param name="pitch">The pitch of the SFX playback.</param>
        /// <param name="waitForCooldown">Whether to wait for cooldown before playing another SFX.</param>
        public void PlaySFX(AudioID audioID, bool pauseBGM = false, float pitch = 1f, bool waitForCooldown = true)
        {
            if (!_SFXLookup.ContainsKey(audioID)) return;

            var audioData = _SFXLookup[audioID];
            PlaySFX(audioData.clip, pauseBGM, pitch, waitForCooldown);
        }

        /// <summary>
        /// Coroutine for playing the background music (BGM) with optional fading effects.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="loop">Whether the clip should loop.</param>
        /// <param name="fade">Whether to apply fading effects when changing clips.</param>
        /// <returns>An enumerator for coroutine.</returns>
        IEnumerator PlayBGMAsync(AudioClip clip, bool loop, bool fade)
        {
            if (fade) yield return _BGMPlayer.DOFade(0, fadeDuration).WaitForCompletion();

            _BGMPlayer.clip = clip;
            _BGMPlayer.loop = loop;
            _BGMPlayer.Play();

            if (fade) yield return _BGMPlayer.DOFade(originalBGMVolume, fadeDuration).WaitForCompletion();
        }

        /// <summary>
        /// Coroutine for unpausing the background music (BGM) after a delay and applying fading effects.
        /// </summary>
        /// <param name="delay">The delay before unpausing the BGM.</param>
        /// <returns>An enumerator for coroutine.</returns>
        IEnumerator UnPauseBGM(float delay)
        {
            yield return new WaitForSeconds(delay);
            _BGMPlayer.volume = 0;
            _BGMPlayer.UnPause();
            _BGMPlayer.DOFade(originalBGMVolume, fadeDuration);
        }
    }

    public enum AudioID { Pop, Coin, Cash, UI_Accept, UI_Decline, UI_Select }

    [System.Serializable]
    public class AudioData
    {
        public AudioID id; // Unique identifier for the audio data
        public AudioClip clip; // The audio clip associated with the audio data
    }
}
