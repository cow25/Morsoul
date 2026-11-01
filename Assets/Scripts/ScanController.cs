using UnityEngine;
using UnityEngine.InputSystem;

public class ScanController : MonoBehaviour
{
    public float maxFocus = 120f;
    public float currentFocus = 120f; 
    public float focusDrainPerSecond = 15f;

    [Header("Scan Effect")]
    public float scanTransitionSpeed = 2f; 
    private float _currentScanProgress = 0f;
    private bool _isScanning = false;

    private void Start()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
            HUDManager.Instance.UpdateFocusCost(focusDrainPerSecond); 
        }
    }

    private void Update()
    {
        // 1. Check for Hold Input
        bool wantsToScan = Keyboard.current != null && Keyboard.current.tabKey.isPressed;

        // 2. Check for Focus
        if (wantsToScan && currentFocus > 0)
        {
            _isScanning = true;
            currentFocus -= focusDrainPerSecond * Time.deltaTime;
            HUDManager.Instance.UpdateFocus(Mathf.RoundToInt(currentFocus), Mathf.RoundToInt(maxFocus));
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

            foreach (ClueNode clue in allClues)
            {
                Vector3 screenPoint = cam.WorldToViewportPoint(clue.transform.position);
                bool isOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;

                // Find the outline object we created
                Transform outlineVisual = clue.transform.Find("OutlineVisual");

                if (isOnScreen && clue.state != ClueNode.State.Collected)
                {
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