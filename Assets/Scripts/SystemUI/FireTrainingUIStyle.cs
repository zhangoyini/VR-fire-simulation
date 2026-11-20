using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shared artwork for authored canvases and dynamically constructed lesson cards.
public static class FireTrainingUIStyle
{
    public static readonly Color Ink = new Color32(36, 40, 43, 255);
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Sprite Load(string path)
    {
        if (sprites.TryGetValue(path, out var cached) && cached != null) return cached;
        var texture = Resources.Load<Texture2D>("FireTrainingUI/" + path);
        if (texture == null) return null;
        // Shared inset for all horizontal button states removes the delivery's
        // generous transparent margins without changing their alignment.
        var rect = path.StartsWith("buttons/button_secondary_")
            ? new Rect(texture.width * .04f, texture.height * .13f, texture.width * .92f, texture.height * .74f)
            : new Rect(0, 0, texture.width, texture.height);
        if (path == "icons/icon_alarm_bell")
            rect = new Rect(texture.width * .10f, texture.height * .21f, texture.width * .80f, texture.height * .59f);
        var sprite = Sprite.Create(texture, rect, Vector2.one * .5f, 100);
        sprites[path] = sprite;
        return sprite;
    }

    public static void Apply(Image image, string path)
    {
        var sprite = Load(path);
        if (sprite == null || image == null) return;
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
    }

    public static bool StyleCard(Image image, Transform parent, Vector2 size)
    {
        string path = null;
        switch (parent.name)
        {
            case "Current instruction": path = "lesson_card_compact"; break;
            case "2D controller animation":
            case "Teleport feedback animation": path = "lesson_card_large"; break;
            case "Task details panel": path = "task_clipboard"; break;
            case "Minimap panel": path = "map_tab"; break;
            case "Exercise task notification": path = "main_screen_base"; break;
            case "Nearby exercise operation hint": path = "tooltip_card"; break;
            case "Campus debrief": if (size.x > 600 && size.y > 300) path = "report_sheet_plain"; break;
            case "Refuge stair warning": path = "warning_card"; break;
            case "Rescue assistance notice": path = "notice_card"; break;
        }
        if (path == null) return false;
        Apply(image, "panels/" + path);
        return true;
    }
}
