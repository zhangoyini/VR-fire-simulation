using UnityEngine;

public class SystemUIView : MonoBehaviour
{
    [SerializeField] private GameObject gameplayUIRoot;
    [SerializeField] private GameObject gameMenuPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    public void ShowGameplay()
    {
        SetVisibility(true, false, false, false);
    }

    public void ShowGameMenu()
    {
        SetVisibility(false, true, false, false);
    }

    public void ShowGameOver()
    {
        SetVisibility(false, false, true, false);
    }

    public void ShowVictory()
    {
        SetVisibility(false, false, false, true);
    }

    private void SetVisibility(bool gameplay, bool menu, bool gameOver, bool victory)
    {
        SetActive(gameplayUIRoot, gameplay);
        SetActive(gameMenuPanel, menu);
        SetActive(gameOverPanel, gameOver);
        SetActive(victoryPanel, victory);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
