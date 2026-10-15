using Harbourview.Journey;
using UnityEngine;

public sealed class CampusRouteCue : MonoBehaviour
{
    private bool shown;
    private void OnTriggerEnter(Collider other)
    {
        if (shown || !other.CompareTag("Player")) return;
        shown = true;
        JourneyDirector.Instance?.Request(JourneyState.Blocked);
        CampusMessagePresenter.Instance?.Show(
            "此路线暂时封闭，请返回走廊并使用东侧楼梯。\nThis route is closed. Return to the corridor and use Stair B.");
        SessionLogger.Write("ROUTE_BLOCKED_REROUTE");
    }
}
