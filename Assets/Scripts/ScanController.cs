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

    public bool IsScanning => _isScanning;

    public void TriggerAccidentalScan(float duration = 1.6f)
    {
        _forcedScanTimer = Mathf.Max(_forcedScanTimer, duration);
    }

    public void RefillFocusToMax()
    {
        var core = Morsoul.Core.CoreRuntime.Instance;
        if (core != null)
        {
            maxFocus = core.Focus.Maximum;
            currentFocus = maxFocus;
            core.Focus.Sync(currentFocus, maxFocus);
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
        var core = Morsoul.Core.CoreRuntime.Instance;
        if (core != null)
        {
            maxFocus = core.Focus.Maximum;
            currentFocus = core.Focus.Percent;
        }
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
            HUDManager.Instance.UpdateFocusCost(focusDrainPerSecond); 
        }
    }

    private void Update()
    {
        if (Morsoul.Core.PauseMenuController.AnyModalOrPauseOpen ||
            Morsoul.Dev.DevMenuOverlay.IsOpen)
            return;

        if (_forcedScanTimer > 0f)
            _forcedScanTimer = Mathf.Max(0f, _forcedScanTimer - Time.deltaTime);

        // 1. Check for Hold Input or Accidental Tutorial Pulse
        bool manualScan = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
        bool forcedScan = _forcedScanTimer > 0f;

        // 2. Check for Focus
        if ((manualScan && currentFocus > 0f) || forcedScan)
        {
            _isScanning = true;
            if (manualScan && !forcedScan)
            {
                currentFocus = Mathf.Max(0f, currentFocus - focusDrainPerSecond * Time.deltaTime);
                Morsoul.Core.CoreRuntime.Instance?.Focus.Sync(currentFocus, maxFocus);
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

    private void UpdateClueParticles()
    {
        // If we are actively scanning, check what is on screen
        if (_currentScanProgress > 0.1f && _isScanning)
        {
            ClueNode[] allClues = FindObjectsByType<ClueNode>(FindObjectsInactive.Exclude);
            Camera cam = Camera.main;
            var session = Morsoul.Core.CoreRuntime.Instance?.Session;
            bool hasAether = session != null && session.Tools.Contains(Morsoul.Data.Tool.AetherSight);

            foreach (ClueNode clue in allClues)
            {
                bool needsAether = clue.requiredTool == ClueNode.Tool.AetherSight || clue.requiredTool == ClueNode.Tool.Both;
                Transform outlineVisual = clue.transform.Find("OutlineVisual");

                if (needsAether && !hasAether)
                {
                    if (clue.scanParticles != null && clue.scanParticles.isPlaying)
                        clue.scanParticles.Stop();
                    if (outlineVisual != null)
                        outlineVisual.GetComponent<MeshRenderer>().enabled = false;
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
                    if (outlineVisual != null) 
                        outlineVisual.GetComponent<MeshRenderer>().enabled = true;
                }
                else
                {
                    // Turn off particles
                    if (clue.scanParticles != null && clue.scanParticles.isPlaying) 
                        clue.scanParticles.Stop();
                    
                    // Turn off thick outline
                    if (outlineVisual != null) 
                        outlineVisual.GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
        else
        {
            // If we are NOT scanning (or progress is 0), turn everything off globally
            ClueNode[] allClues = FindObjectsByType<ClueNode>(FindObjectsInactive.Exclude);
            foreach (ClueNode clue in allClues)
            {
                if (clue.scanParticles != null && clue.scanParticles.isPlaying) 
                    clue.scanParticles.Stop();
                
                Transform outlineVisual = clue.transform.Find("OutlineVisual");
                if (outlineVisual != null) 
                    outlineVisual.GetComponent<MeshRenderer>().enabled = false;
            }
        }
    }
}