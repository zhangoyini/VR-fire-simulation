using System.Collections.Generic;
using Jiao.HealthTime;
using UnityEngine;

// Only the explicitly bound hand colliders may start a pile-wide contact session.
public sealed class CampusDebrisTouchPrompt : MonoBehaviour
{
    [SerializeField] private Collider leftHand;
    [SerializeField] private Collider rightHand;
    [SerializeField] private ResolutionCoordinator resolution;
    private readonly HashSet<(CampusDebrisTouchRelay source, Collider hand)> contacts =
        new HashSet<(CampusDebrisTouchRelay source, Collider hand)>();
    private float nextPromptAt = float.NegativeInfinity;
    private const string Message = "堵塞物无法搬开。\n" +
        "The debris cannot be moved.";

    internal void Touch(CampusDebrisTouchRelay source, Collider hand)
    {
        if (!isActiveAndEnabled || hand == null || (hand != leftHand && hand != rightHand)) return;
        PruneContacts();
        bool firstContact = contacts.Count == 0;
        if (!contacts.Add((source, hand)) || !firstContact || Time.time < nextPromptAt) return;

        var health = HealthTimeController.Instance;
        if (Time.timeScale == 0 || (health != null && (health.IsPaused || health.IsDepleted)) ||
            (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None)) return;
        var presenter = CampusMessagePresenter.Instance;
        if (presenter == null || presenter.HasPriorityWarning) return;
        presenter.Show(Message);
        // Show has no return value; a missing camera can also prevent it from displaying.
        if (presenter.IsVisible && presenter.CurrentMessage == Message)
            nextPromptAt = Time.time + 5f;
    }

    internal void Leave(CampusDebrisTouchRelay source, Collider hand) => contacts.Remove((source, hand));
    internal void RemoveSource(CampusDebrisTouchRelay source) => contacts.RemoveWhere(pair => pair.source == source);
    private void Update() => PruneContacts();
    private void PruneContacts() => contacts.RemoveWhere(pair =>
        pair.source == null || !pair.source.IsContactEnabled || pair.hand == null ||
        !pair.hand.enabled || !pair.hand.gameObject.activeInHierarchy);
    private void OnDisable() => contacts.Clear();
}
