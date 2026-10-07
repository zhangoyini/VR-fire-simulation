using UnityEngine;

// Development/test-only component for the SystemUITest scene.
public class SystemUITestController : MonoBehaviour
{
    [SerializeField] private ResolutionCoordinator resolutionCoordinator;

    private void Update()
    {
        if (resolutionCoordinator == null)
            return;

        if (Input.GetKeyDown(KeyCode.V))
            resolutionCoordinator.ShowVictory();

        if (Input.GetKeyDown(KeyCode.G))
            resolutionCoordinator.ShowGameOver();

        if (Input.GetKeyDown(KeyCode.R))
            resolutionCoordinator.ResetResolution();
    }
}
