using UnityEngine;

namespace Morsoul.Cutscene
{
    /// <summary>
    /// An ordered list of shots played by <see cref="CutscenePlayerController"/>.
    /// When <see cref="nextSceneName"/> is set, finishing the cutscene loads that
    /// scene. <see cref="skipToScene"/> controls what an ESC-hold skip does: when
    /// true the skip jumps straight to <see cref="nextSceneName"/>; when false the
    /// skip just ends the cutscene in place (CutsceneEnded).
    /// </summary>
    [CreateAssetMenu(menuName = "Morsoul/Cutscene")]
    public class CutsceneData : ScriptableObject
    {
        public string id;
        public CutsceneShotData[] shots;
        public AudioClip music;
        public string nextSceneName;
        public bool skipToScene;
    }
}
