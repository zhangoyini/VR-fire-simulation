using UnityEngine;


public sealed class CampusStairWindow : MonoBehaviour
{
    [SerializeField] private CampusHingeInteractable hinge;
    public CampusHingeInteractable Hinge => hinge;
    public void Configure(CampusHingeInteractable value) { hinge = value; }
}
