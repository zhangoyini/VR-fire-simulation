using UnityEngine;

// Identification/configuration on the complete reusable stair-window prefab.
// The same CampusHingeInteractable owns contact, proportional push and fire warnings.
public sealed class CampusStairWindow : MonoBehaviour
{
    [SerializeField] private CampusHingeInteractable hinge;
    public CampusHingeInteractable Hinge => hinge;
    public void Configure(CampusHingeInteractable value) { hinge = value; }
}
