using System.Collections;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Minimal shared audio runtime on the CoreRuntime object (plan §G):
    /// owns persisted volume settings, music/ambience crossfades, cinematic
    /// ducking and one-shot SFX so 2D and future 3D emitters share one
    /// policy instead of duplicating fade/duck logic. Channel volumes are
    /// device settings kept in PlayerPrefs, not save-slot data. No mixer
    /// asset exists yet — levels apply directly to managed AudioSources.
    /// </summary>
    public class AudioCoordinator : MonoBehaviour
    {
        [Tooltip("Seconds for music/ambience fade-out + fade-in transitions.")]
        public float transitionSeconds = 1.5f;
        [Tooltip("Multiplier applied to music/ambience while cinematics duck the mix.")]
        public float duckLevel = 0.35f;

        const string MasterKey = "Morsoul.Audio.Master";
        const string MusicKey = "Morsoul.Audio.Music";
        const string SfxKey = "Morsoul.Audio.Sfx";

        AudioSource _music;
        AudioSource _ambience;
        AudioSource _sfx;
        float _musicLevel = 1f;
        float _ambienceLevel = 1f;
        bool _ducked;
        Coroutine _musicFade;
        Coroutine _ambienceFade;

        /// <summary>Master multiplier applied to every channel; persisted.</summary>
        public float MasterVolume { get; private set; } = 1f;
        /// <summary>Music/ambience channel level; persisted.</summary>
        public float MusicVolume { get; private set; } = 1f;
        /// <summary>SFX channel level; persisted.</summary>
        public float SfxVolume { get; private set; } = 1f;
        /// <summary>True while cinematics hold music/ambience at duckLevel.</summary>
        public bool IsDucked => _ducked;

        void Awake()
        {
            LoadVolumes();
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        void LoadVolumes()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        }

        /// <summary>Crossfades the music loop to a new clip; null fades out.</summary>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            EnsureSources();
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(FadeSwap(_music, clip, loop, true));
        }

        /// <summary>Crossfades the ambience loop to a new clip; null fades out.</summary>
        public void PlayAmbience(AudioClip clip, bool loop = true)
        {
            EnsureSources();
            if (_ambienceFade != null) StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(FadeSwap(_ambience, clip, loop, false));
        }

        public void StopMusic() => PlayMusic(null);
        public void StopAmbience() => PlayAmbience(null);

        /// <summary>Fires a one-shot at the SFX channel level.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            EnsureSources();
            _sfx.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume * MasterVolume);
        }

        /// <summary>Ducks (or restores) music and ambience for cinematic playback.</summary>
        public void SetDucked(bool ducked)
        {
            _ducked = ducked;
            ApplyVolumes();
        }

        float MusicGain() => MusicVolume * MasterVolume * (_ducked ? duckLevel : 1f);
        float AmbienceGain() => MusicGain(); // ambience rides the music channel policy

        void ApplyVolumes()
        {
            if (_music != null) _music.volume = _musicLevel * MusicGain();
            if (_ambience != null) _ambience.volume = _ambienceLevel * AmbienceGain();
        }

        void EnsureSources()
        {
            if (_music == null) _music = MakeSource("Music");
            if (_ambience == null) _ambience = MakeSource("Ambience");
            if (_sfx == null) _sfx = MakeSource("Sfx");
            ApplyVolumes();
        }

        AudioSource MakeSource(string name)
        {
            var child = new GameObject($"Audio.{name}");
            child.transform.SetParent(transform, false);
            return child.AddComponent<AudioSource>();
        }

        IEnumerator FadeSwap(AudioSource source, AudioClip next, bool loop, bool isMusic)
        {
            float half = Mathf.Max(0.01f, transitionSeconds * 0.5f);
            float level = isMusic ? _musicLevel : _ambienceLevel;
            while (level > 0f && source.clip != null)
            {
                level = Mathf.MoveTowards(level, 0f, Time.deltaTime / half);
                SetLevel(source, level, isMusic);
                yield return null;
            }
            if (next == null)
            {
                source.Stop();
                source.clip = null;
                SetLevel(source, 1f, isMusic);
                yield break;
            }
            source.clip = next;
            source.loop = loop;
            source.Play();
            while (level < 1f)
            {
                level = Mathf.MoveTowards(level, 1f, Time.deltaTime / half);
                SetLevel(source, level, isMusic);
                yield return null;
            }
        }

        void SetLevel(AudioSource source, float level, bool isMusic)
        {
            if (isMusic) _musicLevel = level; else _ambienceLevel = level;
            source.volume = level * (isMusic ? MusicGain() : AmbienceGain());
        }
    }
}
