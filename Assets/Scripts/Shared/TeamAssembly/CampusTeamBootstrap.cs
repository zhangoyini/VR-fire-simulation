using UnityEngine;

// The SAME scene is included in all six projects. Independent assets select modules.
[DefaultExecutionOrder(-32000)]
public sealed class CampusTeamBootstrap : MonoBehaviour
{
    public GameObject playerRoot, lessonRoot, fireOrigin, doorwayPlume, gameplayHUD, minimap;
    public Camera previewCamera;

    private void Awake()
    {
        CampusTeamRuntime.Load();
        bool any = CampusTeamRuntime.Any;
        if (previewCamera != null) previewCamera.gameObject.SetActive(!any);
        if (!CampusTeamRuntime.Complete)
        {
            foreach (var item in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (item == null || item == this) continue;
                string owner = CampusTeamRuntime.Owner(item);
                if (owner != null && !CampusTeamRuntime.Has(owner)) item.enabled = false;
                // Health controller and message renderer are infrastructure also needed by other modules.
                if (any && (item is Jiao.HealthTime.HealthTimeController || item is CampusMessagePresenter || item is CampusLocomotion)) item.enabled = true;
                if (!any && (item is CampusHandInteractor || item is DummyController || item is ResolutionCoordinator)) item.enabled = false;
            }
        }
        // Set these before root activation: OnEnable can otherwise briefly reveal fire/UI.
        if (fireOrigin != null && !CampusTeamRuntime.Has("E")) fireOrigin.SetActive(CampusTeamRuntime.Has("D"));
        if (doorwayPlume != null && !CampusTeamRuntime.Has("E")) doorwayPlume.SetActive(false);
        if (gameplayHUD != null && !CampusTeamRuntime.Has("E")) gameplayHUD.SetActive(CampusTeamRuntime.Has("D"));
        if (minimap != null && !CampusTeamRuntime.Has("E")) minimap.SetActive(false);
        if (playerRoot != null) playerRoot.SetActive(any);
        if (lessonRoot != null) lessonRoot.SetActive(CampusTeamRuntime.Has("E"));
        // Practice books are environment props, not part of the hidden tutorial UI.
        // Keep their world poses and original serialized lesson references intact.
        if (!CampusTeamRuntime.Has("E") && lessonRoot != null)
            foreach (var book in FindObjectsOfType<CampusPracticeBook>(true))
                if (book.transform.IsChildOf(lessonRoot.transform))
                { book.transform.SetParent(transform, true); book.gameObject.SetActive(true); }
    }

    private void LateUpdate()
    {
        if (!CampusTeamRuntime.Has("D"))
        {
            if (fireOrigin != null && fireOrigin.activeSelf) fireOrigin.SetActive(false);
            if (doorwayPlume != null && doorwayPlume.activeSelf) doorwayPlume.SetActive(false);
        }
    }
}
