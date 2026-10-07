using UnityEngine;
using UnityEngine.SceneManagement;

public class SystemUINavigation : MonoBehaviour
{
    [SerializeField] private string gameplayEntrySceneName;
    [SerializeField] private string mainMenuSceneName;

    private bool isLoading;

    public void StartGame()
    {
        LoadDestination(gameplayEntrySceneName, "Gameplay Entry Scene Name");
    }

    public void RestartGame()
    {
        // Restart the entire experience, including when called from a later scene.
        LoadDestination(gameplayEntrySceneName, "Gameplay Entry Scene Name");
    }

    public void ReturnToMainMenu()
    {
        LoadDestination(mainMenuSceneName, "Main Menu Scene Name");
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        Debug.Log("[SystemUINavigation] Exit requested. A built application would quit; " +
            "Editor Play Mode remains running.", this);
#else
        Application.Quit();
#endif
    }

    private void LoadDestination(string sceneName, string inspectorField)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SystemUINavigation] Cannot load destination: assign " +
                inspectorField + " in the Inspector.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[SystemUINavigation] Cannot load scene '" +
                sceneName + "': verify its name and enable it in Build Settings.", this);
            return;
        }

        isLoading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }
}
