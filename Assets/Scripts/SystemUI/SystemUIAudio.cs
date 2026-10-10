using UnityEngine;

// One scene-local audio source shared by menu buttons and the outcome gate.
public class SystemUIAudio : MonoBehaviour
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioClip victoryClip;
    private AudioSource source;
    private int lastClickFrame = -1;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    public void PlayClick()
    {
        if (lastClickFrame == Time.frameCount) return;
        lastClickFrame = Time.frameCount;
        if (source != null && clickClip != null) source.PlayOneShot(clickClip);
    }

    public void PlayOutcome(ResolutionCoordinator.Outcome outcome)
    {
        AudioClip clip = outcome == ResolutionCoordinator.Outcome.Victory ? victoryClip : gameOverClip;
        if (source != null && clip != null) source.PlayOneShot(clip);
    }
}
