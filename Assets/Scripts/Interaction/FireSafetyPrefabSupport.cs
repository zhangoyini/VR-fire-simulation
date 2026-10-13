using System.Collections;
using UnityEngine;
using Valve.VR;

// Stored on each interactable prefab. A scene needs only its usual SteamVR rig.
[DisallowMultipleComponent]
public sealed class FireSafetyPrefabSupport : MonoBehaviour
{
    [SerializeField] private Font warningFont;
    [SerializeField] private Material rayMaterial;
    public Font WarningFont => warningFont;
    public Material RayMaterial => rayMaterial;

    private void OnEnable()
    {
        if (Application.isPlaying) StartCoroutine(ConnectRightHand());
    }

    private IEnumerator ConnectRightHand()
    {
        // Wait until scene Awake calls have completed before discovering the rig.
        yield return null;
        if (FindObjectOfType<CampusHandInteractor>() != null) yield break;
        var window = GetComponent<RuntimeFireWindow>();
        if ((window != null && window.UsesPhysicalHands) || GetComponentInChildren<CampusElevatorButton>() != null)
        {
            foreach (SteamVR_Behaviour_Pose pose in FindObjectsOfType<SteamVR_Behaviour_Pose>())
            {
                if (pose.inputSource != SteamVR_Input_Sources.LeftHand && pose.inputSource != SteamVR_Input_Sources.RightHand) continue;
                var hand = pose.GetComponent<CampusHandInteractor>() ?? pose.gameObject.AddComponent<CampusHandInteractor>();
                hand.ConfigureSource(pose.inputSource);
            }
            if (CampusMessagePresenter.Instance == null)
            {
                var host = new GameObject("Contact safety messages");
                var presenter = host.AddComponent<CampusMessagePresenter>(); presenter.ConfigureFont(warningFont);
            }
            yield break;
        }
        bool warned = false;
        while (isActiveAndEnabled)
        {
            VRFireSafetyInteraction interaction = FindObjectOfType<VRFireSafetyInteraction>();
            if (interaction != null && interaction.isActiveAndEnabled)
            {
                interaction.ConfigurePrefabResources(warningFont);
                yield break;
            }

            foreach (SteamVR_Behaviour_Pose pose in FindObjectsOfType<SteamVR_Behaviour_Pose>())
            {
                if (!pose.isActiveAndEnabled || pose.inputSource != SteamVR_Input_Sources.RightHand) continue;
                var host = new GameObject("Fire Safety Right Hand Ray");
                host.transform.SetParent(pose.transform, false);
                var line = host.AddComponent<LineRenderer>();
                line.sharedMaterial = rayMaterial;
                line.widthMultiplier = 0.01f;
                line.enabled = false;
                interaction = host.AddComponent<VRFireSafetyInteraction>();
                interaction.ConfigurePrefabResources(warningFont);
                yield break;
            }

            if (!warned)
            {
                Debug.LogWarning("Fire safety prefab is waiting for a SteamVR right-hand pose. Add the project's configured VR rig to use controller interaction.", this);
                warned = true;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnDisable() => StopAllCoroutines();
}
