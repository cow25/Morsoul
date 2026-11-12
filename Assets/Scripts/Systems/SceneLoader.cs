using UnityEngine;

public class SceneLoader : MonoBehaviour
{
    [Tooltip("Type the exact name of the scene you want to load.")]
    public string sceneToLoad; 

    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError("Scene name is empty on " + gameObject.name);
            return;
        }

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.TransitionToScene(sceneToLoad);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
        }
    }
}