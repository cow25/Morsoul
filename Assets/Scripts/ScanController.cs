using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScanController : MonoBehaviour
{
    public float maxFocus = 100f;
    public float currentFocus = 100f; 
    public float focusDrainPerSecond = 15f;

    [Header("Scan Effect")]
    public float scanTransitionSpeed = 2f; 
    private float _currentScanProgress = 0f;
    private bool _isScanning = false;
    private float _forcedScanTimer = 0f;
    private ClueNode[] _cachedClues;
    private MeshRenderer[] _cachedOutlineRenderers;
    private Camera _cachedCam;
    private bool _visualsMayBeActive = true;
    private bool _lastZeroCost = false;

    private static float FocusDrainRate =>
        Morsoul.Core.FocusTuning.FocusDrainPerTick / Morsoul.Core.FocusTuning.FocusTickSeconds;

    public bool IsScanning => _isScanning;
    public bool IsAetherSightActive { get; private set; }

    public void TriggerAccidentalScan(float duration = 1.6f)
    {
        _forcedScanTimer = Mathf.Max(_forcedScanTimer, duration);
    }

    public void RefillFocusToMax()
    {
        var core = Morsoul.Core.CoreRuntime.Instance;
        if (core != null)
        {
            core.Focus.Refill();
            maxFocus = core.Focus.Maximum;
            currentFocus = core.Focus.Percent;
        }
        else
        {
            currentFocus = maxFocus;
        }
        if (HUDManager.Instance != null)
            HUDManager.Instance.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
    }

    private void Start()
    {
        var core = Morsoul.Core.CoreRuntime.Ensure();
        maxFocus = core.Focus.Maximum;
        currentFocus = core.Focus.Percent;
        _lastZeroCost = core.ZeroScanFocusCost;
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
            HUDManager.Instance.UpdateFocusCost(_lastZeroCost ? 0f : FocusDrainRate);
        }
    }

    private void OnDisable() => SetAetherSight(false);
    private void OnDestroy() => SetAetherSight(false);

    private void SetAetherSight(bool active)
    {
        if (IsAetherSightActive == active) return;
        bool wasAether = IsAetherSightActive;
        IsAetherSightActive = active;
        if (active) Shader.EnableKeyword("_AETHER_SIGHT_ACTIVE");
        else Shader.DisableKeyword("_AETHER_SIGHT_ACTIVE");
        if (active != wasAether)
            Morsoul.Core.AudioCoordinator.PlayBankSfx(b => b.sfxAetherToggle, 0.7f);
        if (active)
            Morsoul.Core.TutorialOverlayController.TryShow("TUT_AETHER_COMBO");
    }

    private void Update()
    {
        if (Morsoul.Core.PauseMenuController.AnyModalOrPauseOpen ||
            Morsoul.Dev.DevMenuOverlay.IsOpen)
            return;

        var core = Morsoul.Core.CoreRuntime.Ensure();
        var session = core.Session;
        bool zeroCost = core.ZeroScanFocusCost;
        bool hasAether = session != null && session.Tools.Contains(Morsoul.Data.Tool.AetherSight);
        if (!hasAether && IsAetherSightActive)
            SetAetherSight(false);
        else if (hasAether && Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (!IsAetherSightActive && !zeroCost && core.Focus.IsDepleted)
                SetAetherSight(false);
            else
                SetAetherSight(!IsAetherSightActive);
        }

        if (IsAetherSightActive && !zeroCost)
        {
            core.Focus.TickAetherSight(Time.deltaTime);
            currentFocus = core.Focus.Percent;
            maxFocus = core.Focus.Maximum;
            HUDManager.Instance?.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
            if (core.Focus.IsDepleted)
                SetAetherSight(false);
        }

        if (_forcedScanTimer > 0f)
            _forcedScanTimer = Mathf.Max(0f, _forcedScanTimer - Time.deltaTime);

        // 1. Check for Hold Input or Accidental Tutorial Pulse
        bool manualScan = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
        bool forcedScan = _forcedScanTimer > 0f;

        if (zeroCost != _lastZeroCost)
        {
            _lastZeroCost = zeroCost;
            HUDManager.Instance?.UpdateFocusCost(zeroCost ? 0f : FocusDrainRate);
        }

        // 2. Check for Focus: Prologue reboots on 0 Focus; Cases 1+ allow strained scan when held empty (latches StrainedScanUsed).
        bool wasScanning = _isScanning;
        bool isPrologue = session != null && session.CurrentCase == 0;
        bool canManualScan = manualScan && (zeroCost || !core.Focus.IsDepleted || !isPrologue);
        if (canManualScan || forcedScan)
        {
            _isScanning = true;
            if (!wasScanning)
                Morsoul.Core.AudioCoordinator.PlayBankSfx(b => b.sfxScanPulse, 0.6f);
            if (canManualScan && !forcedScan)
            {
                if (!zeroCost)
                {
                    bool wasStrained = core.Focus.StrainedScanUsed;
                    core.Focus.TickScan(Time.deltaTime);
                    if (!wasStrained && core.Focus.StrainedScanUsed && session != null)
                        session.MarkStrainedScan(session.CurrentCase);
                }
                currentFocus = core.Focus.Percent;
                maxFocus = core.Focus.Maximum;
                HUDManager.Instance?.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
            }
        }
        else
        {
            _isScanning = false;
        }

        // 3. Smoothly animate the Shader's crawl effect
        float targetProgress = _isScanning ? 1f : 0f;
        _currentScanProgress = Mathf.MoveTowards(_currentScanProgress, targetProgress, scanTransitionSpeed * Time.deltaTime);
        
        // Send this value to the shader
        Shader.SetGlobalFloat("_ScanProgress", _currentScanProgress);

        // 4. Handle Clue Particles & Outlines
        UpdateClueParticles();
    }

    private static void HideClueVisuals(ClueNode clue, Transform outlineVisual)
    {
        if (clue.scanParticles != null && clue.scanParticles.isPlaying)
            clue.scanParticles.Stop();
        if (outlineVisual != null && outlineVisual.TryGetComponent<MeshRenderer>(out var mr))
            mr.enabled = false;
    }

    private void EnsureClueCache()
    {
        if (_cachedClues != null) return;
        _cachedClues = FindObjectsByType<ClueNode>(FindObjectsInactive.Exclude);
        _cachedOutlineRenderers = new MeshRenderer[_cachedClues.Length];
        for (int i = 0; i < _cachedClues.Length; i++)
        {
            if (_cachedClues[i] == null) continue;
            var ov = _cachedClues[i].transform.Find("OutlineVisual");
            if (ov != null) _cachedOutlineRenderers[i] = ov.GetComponent<MeshRenderer>();
        }
    }

    private void UpdateClueParticles()
    {
        EnsureClueCache();
        // If we are actively scanning, check what is on screen
        if (_currentScanProgress > 0.1f && _isScanning)
        {
            _visualsMayBeActive = true;
            if (_cachedCam == null) _cachedCam = Camera.main;
            Camera cam = _cachedCam;
            if (cam == null) return;
            var core = Morsoul.Core.CoreRuntime.Instance;
            var session = core?.Session;
            bool hasAether = session != null && session.Tools.Contains(Morsoul.Data.Tool.AetherSight);
            bool hasGolemEye = session != null && session.Tools.Contains(Morsoul.Data.Tool.GolemEye);

            for (int i = 0; i < _cachedClues.Length; i++)
            {
                ClueNode clue = _cachedClues[i];
                if (clue == null || !clue.gameObject.activeInHierarchy) continue;
                bool needsAether = clue.requiredTool == ClueNode.Tool.AetherSight || clue.requiredTool == ClueNode.Tool.Both;
                bool needsGolemEye = clue.requiredTool == ClueNode.Tool.GolemEye || clue.requiredTool == ClueNode.Tool.Both;
                MeshRenderer outlineMr = _cachedOutlineRenderers[i];

                bool blocked = (needsAether && !(hasAether && IsAetherSightActive)) || (needsGolemEye && !hasGolemEye);
                if (!blocked && core != null && session != null && !string.IsNullOrEmpty(clue.id))
                {
                    var clueData = core.ResolveClue(clue.id);
                    if (clueData != null)
                    {
                        Morsoul.Data.RunicLogData log = clueData.type == Morsoul.Data.ClueType.Latent
                            ? core.ResolveCase(clueData.caseId)?.latentLog
                            : null;
                        if (!Morsoul.Core.ClueAcquisition.CanAcquire(clueData, session, log, out _))
                            blocked = true;
                    }
                }

                if (blocked)
                {
                    HideClueVisuals(clue, outlineMr);
                    continue;
                }

                Vector3 screenPoint = cam.WorldToViewportPoint(clue.transform.position);
                bool isOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;

                if (isOnScreen && clue.state != ClueNode.State.Collected)
                {
                    clue.MarkScanned();

                    if (needsAether && clue.scanParticles != null)
                    {
                        var main = clue.scanParticles.main;
                        main.startColor = new Color(1f, 0.85f, 0.15f, 1f);
                    }

                    // Turn on particles
                    if (clue.scanParticles != null && !clue.scanParticles.isPlaying) 
                        clue.scanParticles.Play();
                    
                    // Turn on thick outline
                    if (outlineMr != null && !outlineMr.enabled) 
                        outlineMr.enabled = true;
                }
                else
                {
                    HideClueVisuals(clue, outlineMr);
                }
            }
        }
        else if (_visualsMayBeActive)
        {
            _visualsMayBeActive = false;
            for (int i = 0; i < _cachedClues.Length; i++)
            {
                if (_cachedClues[i] != null)
                    HideClueVisuals(_cachedClues[i], _cachedOutlineRenderers[i]);
            }
        }
    }

    private static void HideClueVisuals(ClueNode clue, MeshRenderer outlineMr)
    {
        if (clue.scanParticles != null && clue.scanParticles.isPlaying)
            clue.scanParticles.Stop();
        if (outlineMr != null && outlineMr.enabled)
            outlineMr.enabled = false;
    }
}