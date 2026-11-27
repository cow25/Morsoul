using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("Loading Screen Setup")]
    public Texture2D[] loadingImages;
    public float imageDisplayDuration = 2f; 
    public float fadeDuration = 0.5f;       
    public float minimumLoadTime = 2f;      

    private PanelRenderer panelRenderer;
    private VisualElement _loadingRoot;
    private Image _imageA;
    private Image _imageB;
    private bool _isImageAActive = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null)
        {
            panelRenderer.sortingOrder = 130;
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }

    private void OnDisable()
    {
        if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        _loadingRoot = root.Q<VisualElement>("LoadingRoot");
        _imageA = root.Q<Image>("ImageA");
        _imageB = root.Q<Image>("ImageB");

        if (_loadingRoot != null) _loadingRoot.style.display = DisplayStyle.None;
    }

    public void TransitionToScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (_loadingRoot == null) yield break;

        // 1. HIDE THE HUD
        if (HUDManager.Instance != null)
            HUDManager.Instance.SetHUDVisibility(false);

        // 2. Fade in the black background
        _loadingRoot.style.display = DisplayStyle.Flex;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _loadingRoot.style.opacity = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        _loadingRoot.style.opacity = 1f;

        // 3. Start loading the scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;
        float loadStartTime = Time.unscaledTime;

        // 4. Start the image slideshow
        int currentImageIndex = 0;
        if (loadingImages != null && loadingImages.Length > 0)
        {
            _imageA.image = loadingImages[0];
            _imageA.style.opacity = 1f;
            _imageB.style.opacity = 0f;
        }

        while (asyncLoad.progress < 0.9f || (Time.unscaledTime - loadStartTime) < minimumLoadTime)
        {
            if (loadingImages != null && loadingImages.Length > 1)
            {
                yield return new WaitForSecondsRealtime(imageDisplayDuration);

                currentImageIndex = (currentImageIndex + 1) % loadingImages.Length;
                Image inactiveLayer = _isImageAActive ? _imageB : _imageA;
                Image activeLayer = _isImageAActive ? _imageA : _imageB;

                inactiveLayer.image = loadingImages[currentImageIndex];

                float crossfadeElapsed = 0f;
                while (crossfadeElapsed < fadeDuration)
                {
                    crossfadeElapsed += Time.unscaledDeltaTime;
                    float t = crossfadeElapsed / fadeDuration;
                    inactiveLayer.style.opacity = t;
                    activeLayer.style.opacity = 1f - t;
                    yield return null;
                }

                inactiveLayer.style.opacity = 1f;
                activeLayer.style.opacity = 0f;
                _isImageAActive = !_isImageAActive;
            }
            else
            {
                yield return null;
            }
        }

        // 5. Fade out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _loadingRoot.style.opacity = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        _loadingRoot.style.display = DisplayStyle.None;
        
        // 6. Activate scene and SHOW THE HUD
        asyncLoad.allowSceneActivation = true;
        
        // Wait one frame for the new scene's HUD to initialize
        yield return new WaitForEndOfFrame(); 
        
        if (HUDManager.Instance != null)
            HUDManager.Instance.SetHUDVisibility(true);
    }
}