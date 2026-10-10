using UnityEngine;

public class StairTeleportTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FloorManager floorManager;
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private Transform playerRig;

    private bool isTeleporting = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (!floorManager.CanGoDown())
        {
            Debug.Log("Already on 1F. Cannot go down.");
            return;
        }

        isTeleporting = true;

        bool success = floorManager.GoDown();

        if (success)
        {
            TeleportPlayer();
        }

        isTeleporting = false;
    }

    private void TeleportPlayer()
    {
        if (teleportTarget == null || playerRig == null)
        {
            Debug.LogWarning("Teleport Target or Player Rig is not assigned.");
            return;
        }

        playerRig.position = teleportTarget.position;
        playerRig.rotation = teleportTarget.rotation;
    }

//#if UNITY_EDITOR
//    private void Update()
//    {
//        if (Input.GetKeyDown(KeyCode.T))
//        {
//            TestTeleportDown();
//        }
//    }

//    private void TestTeleportDown()
//    {
//        if (!floorManager.CanGoDown())
//        {
//            Debug.Log("Already on 1F. Cannot go down.");
//            return;
//        }

//        if (floorManager.GoDown())
//        {
//            TeleportPlayer();
//        }
//    }
//#endif
}
