using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Scene-scoped music/ambience adapter feeding the shared
    /// <see cref="AudioCoordinator"/>. Whatever the scene assigns is
    /// crossfaded in on Start and faded back out when the scene unloads, so
    /// loops never leak across scenes or after interruption. Unassigned
    /// channels leave the current mix untouched — an empty slot does not
    /// kill music a previous scene legitimately handed off.
    /// </summary>
    public class SceneAudioLoop : MonoBehaviour
    {
        [Tooltip("Optional music loop for this scene; crossfades over the current track.")]
        public AudioClip music;
        [Tooltip("Optional ambience loop for this scene; crossfades over the current bed.")]
        public AudioClip ambience;

        bool _ownsMusic;
        bool _ownsAmbience;
        bool _quitting;

        void Start() => Play();

        void OnApplicationQuit() => _quitting = true;

        void OnDestroy()
        {
            if (_quitting) return;
            var audio = Audio();
            if (audio == null) return;
            if (_ownsAmbience) audio.StopAmbience();
            if (_ownsMusic) audio.StopMusic();
        }

        /// <summary>
        /// Begins the assigned loops (idempotent). Exposed for tests and for
        /// scenes that install the component after Start.
        /// </summary>
        public void Play()
        {
            var audio = Audio();
            if (audio == null) return;
            if (music != null)
            {
                audio.PlayMusic(music);
                _ownsMusic = true;
            }
            if (ambience != null)
            {
                audio.PlayAmbience(ambience);
                _ownsAmbience = true;
            }
        }

        static AudioCoordinator Audio() =>
            CoreRuntime.Instance != null ? CoreRuntime.Instance.Audio : null;
    }
}
