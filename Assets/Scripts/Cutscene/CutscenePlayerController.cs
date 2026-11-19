using System;
using System.Collections;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Morsoul.Cutscene
{
    /// <summary>
    /// UI overlay controller that plays a <see cref="CutsceneData"/> shot list.
    /// Sits on the same GameObject as a PanelRenderer (same pattern as
    /// DialogueController / BoardController). Still images render into the
    /// StillImage element; video clips play through a VideoPlayer (on the same
    /// GameObject) into a RenderTexture bound to the VideoSurface element's
    /// background. E advances the current shot; holding ESC skips the cutscene.
    /// </summary>
    public class CutscenePlayerController : MonoBehaviour
    {
        public CutsceneData cutsceneData;
        public bool playOnStart;
        public bool manageCursor = true;
        public float skipHoldSeconds = 1.5f;

        /// <summary>Raised when the cutscene ends without a scene request.</summary>
        public event Action CutsceneEnded;
        /// <summary>Raised when the cutscene requests a scene load (it also loads it).</summary>
        public event Action<string> LoadSceneRequested;

        PanelRenderer _panelRenderer;
        VideoPlayer _videoPlayer;
        RenderTexture _videoTexture;

        // Cached elements (re-queried on every UI reload).
        VisualElement _root;
        VisualElement _stillImage;
        VisualElement _videoSurface;
        VisualElement _captionBox;
        Label _captionSpeaker;
        Label _captionText;

        // Playback state.
        bool _isPlaying;
        int _shotIndex = -1;
        Coroutine _shotRoutine;
        float _skipHold;
        int _uiVersion;
        Func<bool> _modalBlocker;
        CursorLockMode _prevCursorLock = CursorLockMode.Locked;
        bool _prevCursorVisible;

        public bool IsPlaying => _isPlaying;
        public int CurrentShotIndex => _shotIndex;

        void OnEnable()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null)
            {
                _panelRenderer.sortingOrder = 100;
                _panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }
            _videoPlayer = GetComponent<VideoPlayer>();
            _modalBlocker ??= () => _isPlaying;
            PauseMenuController.RegisterExternalModal(_modalBlocker);
        }

        void OnDisable()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            if (_modalBlocker != null) PauseMenuController.UnregisterExternalModal(_modalBlocker);
        }

        void OnDestroy()
        {
            ReleaseVideoTexture();
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !_isPlaying) return;

            if (keyboard.escapeKey.isPressed)
            {
                _skipHold += Time.deltaTime;
                if (_skipHold >= Mathf.Max(0.1f, skipHoldSeconds))
                {
                    Skip();
                    return;
                }
            }
            else
            {
                _skipHold = 0f;
            }

            if (keyboard.eKey.wasPressedThisFrame) Advance();
        }

        /// <summary>Plays the assigned <see cref="cutsceneData"/>.</summary>
        public void Play()
        {
            Play(cutsceneData);
        }

        /// <summary>Plays the given cutscene shot list from the start.</summary>
        public void Play(CutsceneData data)
        {
            Stop();
            cutsceneData = data;
            if (data == null)
            {
                Debug.LogWarning("CutscenePlayerController: no CutsceneData assigned; panel stays hidden.", this);
                return;
            }
            _isPlaying = true;
            _skipHold = 0f;
            CoreRuntime.Instance?.Audio?.SetDucked(true);
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            if (manageCursor)
            {
                _prevCursorLock = UnityEngine.Cursor.lockState;
                _prevCursorVisible = UnityEngine.Cursor.visible;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            AdvanceTo(CutscenePlanner.FirstIndex(data.shots));
        }

        /// <summary>Stops playback and hides the panel without firing end events.</summary>
        public void Stop()
        {
            EndPlayback();
        }

        /// <summary>
        /// Skips the rest of the cutscene (ESC hold). When the data opts into
        /// <see cref="CutsceneData.skipToScene"/> and names a scene, the skip
        /// jumps straight there; otherwise it fires <see cref="CutsceneEnded"/>.
        /// </summary>
        public void Skip()
        {
            if (!_isPlaying) return;
            var data = cutsceneData;
            EndPlayback();
            if (data != null && data.skipToScene && !string.IsNullOrEmpty(data.nextSceneName))
                RequestScene(data.nextSceneName);
            else
                CutsceneEnded?.Invoke();
        }

        /// <summary>Advances to the next playable shot, or ends the cutscene.</summary>
        public void Advance()
        {
            if (!_isPlaying || cutsceneData == null) return;
            AdvanceTo(CutscenePlanner.AdvanceIndex(cutsceneData.shots, _shotIndex));
        }

        void AdvanceTo(int index)
        {
            StopShotRoutine();
            StopVideo();
            _shotIndex = index;
            if (index < 0)
            {
                FinishCutscene();
                return;
            }
            var shot = cutsceneData.shots[index];
            RenderShot(shot);
            if (!shot.waitForInput)
                _shotRoutine = StartCoroutine(AutoAdvanceAfter(Mathf.Max(0f, shot.durationSeconds)));
        }

        IEnumerator AutoAdvanceAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _shotRoutine = null;
            Advance();
        }

        void FinishCutscene()
        {
            var data = cutsceneData;
            EndPlayback();
            if (data != null && !string.IsNullOrEmpty(data.nextSceneName))
                RequestScene(data.nextSceneName);
            else
                CutsceneEnded?.Invoke();
        }

        void RequestScene(string sceneName)
        {
            LoadSceneRequested?.Invoke(sceneName);
            // With Core live the GSM checkpoints + loads; without it
            // (standalone panel tests) the cutscene loads the scene itself.
            if (CoreRuntime.Instance != null)
                CoreRuntime.Instance.Bus.Publish(new SceneLoadRequestedEvent { SceneName = sceneName });
            else
                SceneManager.LoadScene(sceneName);
        }

        void EndPlayback()
        {
            _isPlaying = false;
            _shotIndex = -1;
            _skipHold = 0f;
            CoreRuntime.Instance?.Audio?.SetDucked(false);
            StopShotRoutine();
            StopVideo();
            HideCaption();
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (manageCursor)
            {
                UnityEngine.Cursor.lockState = _prevCursorLock;
                UnityEngine.Cursor.visible = _prevCursorVisible;
            }
            PauseMenuController.NotifyExternalModalClosed();
        }

        void StopShotRoutine()
        {
            if (_shotRoutine == null) return;
            StopCoroutine(_shotRoutine);
            _shotRoutine = null;
        }

        // --- Rendering -------------------------------------------------------

        void RenderShot(CutsceneShotData shot)
        {
            if (shot == null)
            {
                HideVisuals();
                return;
            }
            if (shot.video != null) ShowVideo(shot);
            else ShowStill(shot);
            ShowCaption(shot);
        }

        void ShowStill(CutsceneShotData shot)
        {
            if (_stillImage != null)
            {
                _stillImage.style.backgroundImage = shot.image != null
                    ? Background.FromSprite(shot.image)
                    : new Background();
                _stillImage.style.display = shot.image != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_videoSurface != null) _videoSurface.style.display = DisplayStyle.None;
        }

        void ShowVideo(CutsceneShotData shot)
        {
            if (_stillImage != null) _stillImage.style.display = DisplayStyle.None;
            if (_videoPlayer == null)
            {
                Debug.LogWarning(
                    "CutscenePlayerController: video shot but no VideoPlayer component; skipping shot.", this);
                Advance();
                return;
            }
            try
            {
                EnsureVideoTexture(shot.video);
                _videoPlayer.source = VideoSource.VideoClip;
                _videoPlayer.clip = shot.video;
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.targetTexture = _videoTexture;
                _videoPlayer.isLooping = shot.waitForInput;
                _videoPlayer.Play();
                if (_videoSurface != null)
                {
                    _videoSurface.style.backgroundImage = Background.FromRenderTexture(_videoTexture);
                    _videoSurface.style.display = DisplayStyle.Flex;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"CutscenePlayerController: failed to play video shot - {e.Message}", this);
                Advance();
            }
        }

        void EnsureVideoTexture(VideoClip clip)
        {
            int width = clip != null && clip.width > 0 ? (int)clip.width : 1280;
            int height = clip != null && clip.height > 0 ? (int)clip.height : 720;
            if (_videoTexture != null && (_videoTexture.width != width || _videoTexture.height != height))
                ReleaseVideoTexture();
            if (_videoTexture == null)
            {
                _videoTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "CutsceneVideoRT"
                };
                _videoTexture.Create();
            }
        }

        void ReleaseVideoTexture()
        {
            if (_videoTexture == null) return;
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }

        void StopVideo()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
            if (_videoSurface != null) _videoSurface.style.display = DisplayStyle.None;
        }

        // Mirrors DialogueRunner.PlayerNameToken — kept local because the
        // Cutscene assembly does not reference Morsoul.Dialogue.
        const string PlayerNameToken = "{PlayerName}";

        void ShowCaption(CutsceneShotData shot)
        {
            bool hasCaption = CutscenePlanner.HasCaption(shot);
            if (_captionBox != null)
                _captionBox.style.display = hasCaption ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasCaption) return;
            if (_captionText != null)
            {
                var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
                var playerName = session != null && !string.IsNullOrEmpty(session.PlayerName)
                    ? session.PlayerName : "Detective";
                _captionText.text = shot.caption.Replace(PlayerNameToken, playerName);
            }
            if (_captionSpeaker != null)
            {
                bool hasSpeaker = !string.IsNullOrEmpty(shot.captionSpeaker);
                _captionSpeaker.text = hasSpeaker ? shot.captionSpeaker : "";
                _captionSpeaker.style.display = hasSpeaker ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        void HideCaption()
        {
            if (_captionBox != null) _captionBox.style.display = DisplayStyle.None;
        }

        void HideVisuals()
        {
            if (_stillImage != null) _stillImage.style.display = DisplayStyle.None;
            if (_videoSurface != null) _videoSurface.style.display = DisplayStyle.None;
            HideCaption();
        }

        // --- UI reload --------------------------------------------------------

        void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            _root = root.Q<VisualElement>("Cutscene-Root");
            _stillImage = root.Q<VisualElement>("StillImage");
            _videoSurface = root.Q<VisualElement>("VideoSurface");
            _captionBox = root.Q<VisualElement>("CaptionBox");
            _captionSpeaker = root.Q<Label>("CaptionSpeaker");
            _captionText = root.Q<Label>("CaptionText");

            if (_root != null)
                _root.style.display = _isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isPlaying && cutsceneData != null && cutsceneData.shots != null
                && _shotIndex >= 0 && _shotIndex < cutsceneData.shots.Length)
            {
                RenderShot(cutsceneData.shots[_shotIndex]);
            }
            else
            {
                HideVisuals();
            }
        }
    }
}
