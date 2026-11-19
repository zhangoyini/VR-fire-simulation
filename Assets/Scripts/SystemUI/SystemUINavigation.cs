using UnityEngine;
using UnityEngine.SceneManagement;

public class SystemUINavigation : MonoBehaviour
{
    private static bool skipTutorialOnNextSceneLoad;
    [SerializeField] private string gameplayEntrySceneName;
    [SerializeField] private string mainMenuSceneName;

    private bool isLoading;

    public void StartGame()
    {
        LoadDestination(gameplayEntrySceneName, "Gameplay Entry Scene Name");
    }

    public void RestartGame()
    {
        // A fresh scene reload resets scene-owned health and task state. Mark this
        // one load so the lesson director skips only the controller tutorial.
        skipTutorialOnNextSceneLoad = true;
        LoadDestination(gameplayEntrySceneName, "Gameplay Entry Scene Name");
    }

    public static bool ConsumeRestartTutorialSkip()
    {
        bool skip = skipTutorialOnNextSceneLoad;
        skipTutorialOnNextSceneLoad = false;
        return skip;
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
            skipTutorialOnNextSceneLoad = false;
            Debug.LogWarning("[SystemUINavigation] Cannot load destination: assign " +
                inspectorField + " in the Inspector.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            skipTutorialOnNextSceneLoad = false;
            Debug.LogWarning("[SystemUINavigation] Cannot load scene '" +
                sceneName + "': verify its name and enable it in Build Settings.", this);
            return;
        }

        isLoading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }
}
