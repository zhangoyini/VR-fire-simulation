using UnityEngine;

public class TeleportArea : MonoBehaviour
{
    // You can expand this later to change the visual color of your teleport reticle
    [SerializeField] private Color areaColor = Color.cyan;
    public Color GetAreaColor() => areaColor;

    internal enum StairPart { None, UpFlight, ReturnFlight, Landing }
    private StairPart? stairPart;

    // CampusSceneBuilder uses these names for both towers, including existing saved scenes.
    internal StairPart Stair
    {
        get
        {
            if (stairPart.HasValue) return stairPart.Value;
            string surface = gameObject.name;
            if (surface.StartsWith("Up flight ", System.StringComparison.Ordinal)) stairPart = StairPart.UpFlight;
            else if (surface.StartsWith("Return flight ", System.StringComparison.Ordinal)) stairPart = StairPart.ReturnFlight;
            else if (surface.StartsWith("Full landing ", System.StringComparison.Ordinal) || surface == "Half landing")
                stairPart = StairPart.Landing;
            else stairPart = StairPart.None;
            return stairPart.Value;
        }
    }
}
