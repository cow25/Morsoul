using System;
using UnityEngine;
using UnityEngine.Video;

namespace Morsoul.Cutscene
{
    /// <summary>
    /// A single cutscene shot. A shot shows either a still <see cref="image"/>
    /// or a <see cref="video"/> clip (image takes precedence when both are set).
    /// Timed shots advance after <see cref="durationSeconds"/>; when
    /// <see cref="waitForInput"/> is set the shot instead waits for the player
    /// to press E. The caption is optional.
    /// </summary>
    [Serializable]
    public class CutsceneShotData
    {
        public Sprite image;
        public VideoClip video;
        public AudioClip sfx;
        public float durationSeconds = 4f;
        public bool waitForInput;
        [TextArea] public string caption;
        public string captionSpeaker;
    }
}
