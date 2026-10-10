using UnityEngine;

public class DoorProximityTrigger : MonoBehaviour
{
    [SerializeField] private DoorInteraction doorInteraction;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Proximity Enter: " + other.name);

        DummyController controller =
            other.GetComponentInParent<DummyController>();

        if (controller != null)
        {
            Debug.Log("Controller entered door proximity");

            doorInteraction.ShowHint();
        }

        //if (other.CompareTag("Player"))
        //{
        //    doorInteraction.ShowHint();
        //}
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Proximity Exit: " + other.name);

        DummyController controller =
            other.GetComponentInParent<DummyController>();

        if (controller != null)
        {
            doorInteraction.HideHint();
            doorInteraction.CloseDoor();
        }

        //if (other.CompareTag("Player"))
        //{
        //    doorInteraction.HideHint();
        //}
    }
}