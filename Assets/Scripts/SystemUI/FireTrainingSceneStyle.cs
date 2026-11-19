using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FireTrainingSceneStyle
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }
    private static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu" && scene.name != "CampusTraining" && scene.name != "SystemUITest") return;
        Apply(scene);
    }
    public static void Apply(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            string art = null;
            switch (rect.name)
            {
                case "MainMenuCanvas": art = "main_terminal_face"; break;
                case "PausePanel": art = "pause_board"; break;
                case "VictoryPanel": case "GameOverPanel": art = "report_sheet_plain"; break;
            }
            if (art == null) continue;
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            FireTrainingUIStyle.Apply(image, "panels/" + art);
            image.raycastTarget = false;
            foreach (var label in rect.GetComponentsInChildren<TMP_Text>(true)) label.color = FireTrainingUIStyle.Ink;
            if (rect.name == "MainMenuCanvas")
            {
                Place(rect, "Title", new Vector2(0, 250), new Vector2(920, 180));
                Place(rect, "StartButton", new Vector2(0, 55), new Vector2(520, 145));
                Place(rect, "ExitButton", new Vector2(0, -100), new Vector2(520, 145));
                Place(rect, "Trigger confirmation hint", new Vector2(0, -430), new Vector2(1280, 100));
                var title = rect.Find("Title").GetComponent<TMP_Text>();
                title.text = "消防疏散训练\n<size=55%>Fire Evacuation Training</size>";
                title.enableAutoSizing = true;
                title.fontSizeMax = 70;
                title.fontSizeMin = 32;
                var hint = rect.Find("Trigger confirmation hint");
                if (hint != null) hint.GetComponent<TMP_Text>().color = Color.white;
            }
        }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var layout in root.GetComponentsInChildren<CampusLessonLayout>(true)) layout.ApplyArtwork();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var button in root.GetComponentsInChildren<FireTrainingButtonVisual>(true))
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                label.color = button.name.Contains("Start") || button.name.Contains("Resume") ? FireTrainingUIStyle.Ink : Color.white;
    }
    private static void Place(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null) return;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var collider = rect.GetComponent<BoxCollider>();
        if (collider != null) collider.size = new Vector3(size.x, size.y, collider.size.z);
    }
}
