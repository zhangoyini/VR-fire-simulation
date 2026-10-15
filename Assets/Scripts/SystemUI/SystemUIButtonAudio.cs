using UnityEngine;
using UnityEngine.UI;

// Desktop Button path. VRMenuButton uses the same audio service and frame guard.
[RequireComponent(typeof(Button))]
public class SystemUIButtonAudio : MonoBehaviour
{
    [SerializeField] private SystemUIAudio audio;
    private Button button;
    private void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Play);
    }
    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Play);
    }
    private void Play() { if (audio != null) audio.PlayClick(); }
}
