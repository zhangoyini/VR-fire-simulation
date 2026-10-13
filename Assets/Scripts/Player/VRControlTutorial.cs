using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRFire.Utils;

/// <summary>Shows the two essential VR controls when the player starts.</summary>
public sealed class VRControlTutorial : MonoBehaviour
{
    private const float DisplayDuration = 8f;
    private const float UiScale = 0.001f;

    private CanvasGroup canvasGroup;
    private RectTransform panel;
    private RectTransform joystickKnob;
    private RectTransform gripRing;
    private Image gripGlow;
    private Image joystickGlow;
    private RectTransform joystickRing;
    private Sprite circleSprite;
    private Sprite ringSprite;
    private Sprite arrowHeadSprite;
    private Sprite controllerSprite;

    private void Start()
    {
        BuildReminder();
        StartCoroutine(PlayIntro());
    }

    private void BuildReminder()
    {
        GameObject canvasObject = new GameObject("VR Control Reminder");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0.12f, 1.35f);
        canvasObject.transform.localRotation = Quaternion.identity;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetComponent<Camera>();
        canvas.sortingOrder = 900;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        panel = canvasObject.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(1100f, 440f);
        panel.localScale = Vector3.one * UiScale;
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        MakeImage(canvasObject.transform, "White Background", Vector2.zero,
            new Vector2(1100f, 440f), Color.white);
        MakeText(canvasObject.transform, "VR CONTROLS", new Vector2(0f, 174f),
            new Vector2(1000f, 60f), 46f, Color.black, FontStyles.Bold);

        MakeImage(canvasObject.transform, "Teleport Card", new Vector2(-270f, -12f),
            new Vector2(500f, 320f), new Color(0.15f, 0.23f, 0.36f));
        MakeImage(canvasObject.transform, "Interact Card", new Vector2(270f, -12f),
            new Vector2(500f, 320f), new Color(0.84f, 0.89f, 0.93f));
        MakeText(canvasObject.transform, "TELEPORT", new Vector2(-270f, 115f),
            new Vector2(450f, 48f), 34f, Color.white, FontStyles.Bold);
        MakeText(canvasObject.transform, "Push joystick to teleport", new Vector2(-270f, -139f),
            new Vector2(460f, 50f), 27f, Color.white, FontStyles.Normal);
        MakeText(canvasObject.transform, "INTERACT", new Vector2(130f, 115f),
            new Vector2(215f, 48f), 30f, new Color(0.06f, 0.08f, 0.1f), FontStyles.Bold);
        MakeText(canvasObject.transform, "Squeeze Grip Button\nto interact", new Vector2(130f, -139f),
            new Vector2(215f, 72f), 22f, new Color(0.06f, 0.08f, 0.1f), FontStyles.Normal);

        BuildJoystick(canvasObject.transform);
        BuildController(canvasObject.transform);
    }

    private void BuildJoystick(Transform parent)
    {
        Sprite sprite = GetControllerSprite();
        MakeImage(parent, "Teleport Quest 3 Controller", new Vector2(-270f, -5f),
            new Vector2(135f, 168f), Color.white, sprite);

        // Position is mapped to the joystick in the supplied full-controller image.
        Vector2 joystickPosition = new Vector2(-249.5f, 51.5f);
        joystickGlow = MakeImage(parent, "Joystick Teleport Glow", joystickPosition,
            new Vector2(32f, 32f), new Color(0.02f, 0.68f, 0.98f, 0.4f), GetCircleSprite())
            .GetComponent<Image>();
        joystickRing = MakeImage(parent, "Joystick Teleport Highlight", joystickPosition,
            new Vector2(25f, 25f), new Color(0.02f, 0.55f, 1f, 1f), GetRingSprite())
            .GetComponent<RectTransform>();
        joystickKnob = MakeImage(parent, "Joystick Teleport Dot", joystickPosition,
            new Vector2(8f, 8f), new Color(0.02f, 0.68f, 0.98f, 1f), GetCircleSprite())
            .GetComponent<RectTransform>();
        MakeArrow(parent, "Teleport Joystick Arrow", new Vector2(-207f, 88f),
            joystickPosition, new Color(0.02f, 0.55f, 1f, 1f));
    }

    private void BuildController(Transform parent)
    {
        Sprite sprite = GetControllerSprite();
        if (sprite == null) return;

        MakeImage(parent, "Quest 3 Controller Reference Image", new Vector2(330f, -10f),
            new Vector2(135f, 168f), Color.white, sprite);

        // Match the raised grip area on the upper-left side shown in the user's marked reference.
        Vector2 gripPosition = new Vector2(298f, 24f);
        gripGlow = MakeImage(parent, "Grip Button Glow", gripPosition,
            new Vector2(25f, 25f), new Color(1f, 0.22f, 0f, 0.24f), GetCircleSprite())
            .GetComponent<Image>();
        gripRing = MakeImage(parent, "Grip Button Highlight Ring", gripPosition,
            new Vector2(22f, 22f), new Color(1f, 0.12f, 0f, 1f), GetRingSprite())
            .GetComponent<RectTransform>();
        gripGlow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
        gripRing.localEulerAngles = new Vector3(0f, 0f, -18f);
        MakeArrow(parent, "Interact Grip Button Arrow", new Vector2(220f, 65f),
            gripPosition, new Color(1f, 0.12f, 0f, 1f));
    }

    private GameObject MakeImage(Transform parent, string objectName, Vector2 position,
        Vector2 size, Color color, Sprite sprite = null)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;
        return imageObject;
    }

    private void MakeText(Transform parent, string value, Vector2 position, Vector2 size,
        float fontSize, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(value);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        // 使用内置中文字体资产，避免中文显示为方块（LiberationSans 默认不含中文 glyph）
        text.font = ChineseFontProvider.GetFontOrDefault();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
    }

    private void MakeArrow(Transform parent, string objectName, Vector2 start, Vector2 end, Color color)
    {
        Vector2 direction = (end - start).normalized;
        const float headLength = 22f;
        Vector2 shaftEnd = end - direction * (headLength * 0.62f);
        Vector2 shaft = shaftEnd - start;

        GameObject lineObject = MakeImage(parent, objectName + " Shaft",
            (start + shaftEnd) * 0.5f, new Vector2(shaft.magnitude, 7f), color);
        lineObject.GetComponent<RectTransform>().localEulerAngles =
            new Vector3(0f, 0f, Mathf.Atan2(shaft.y, shaft.x) * Mathf.Rad2Deg);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        GameObject headObject = MakeImage(parent, objectName + " Head", end,
            new Vector2(headLength, headLength), color, GetArrowHeadSprite());
        headObject.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private Sprite GetArrowHeadSprite()
    {
        if (arrowHeadSprite != null) return arrowHeadSprite;

        const int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "VR Tutorial Arrow Head";
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float normalizedY = (y + 0.5f) / resolution;
            float halfWidth = (1f - normalizedY) * 0.44f;
            float normalizedX = (x + 0.5f) / resolution - 0.5f;
            float alpha = normalizedY > 0.08f && Mathf.Abs(normalizedX) < halfWidth ? 1f : 0f;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        arrowHeadSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f));
        return arrowHeadSprite;
    }

    private Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "VR Tutorial Circle";
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = (x + 0.5f) / resolution * 2f - 1f;
            float dy = (y + 0.5f) / resolution * 2f - 1f;
            float alpha = 1f - Mathf.SmoothStep(0.94f, 1f, Mathf.Sqrt(dx * dx + dy * dy));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    private Sprite GetControllerSprite()
    {
        if (controllerSprite != null) return controllerSprite;

        Texture2D source = Resources.Load<Texture2D>("Quest3ControllerGrip");
        if (source == null)
        {
            Debug.LogWarning("Missing controller tutorial image at Resources/Quest3ControllerGrip.png.", this);
            return null;
        }

        controllerSprite = Sprite.Create(source, new Rect(0f, 0f, source.width, source.height),
            new Vector2(0.5f, 0.5f), 100f);
        return controllerSprite;
    }

    private Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "VR Tutorial Highlight Ring";
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = (x + 0.5f) / resolution * 2f - 1f;
            float dy = (y + 0.5f) / resolution * 2f - 1f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float outer = 1f - Mathf.SmoothStep(0.82f, 0.98f, radius);
            float inner = Mathf.SmoothStep(0.68f, 0.82f, radius);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, outer * inner));
        }
        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f));
        return ringSprite;
    }

    private IEnumerator PlayIntro()
    {
        const float fadeDuration = 0.4f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = progress;
            panel.localScale = Vector3.one * (UiScale * Mathf.Lerp(0.9f, 1f, progress));
            AnimateControls();
            yield return null;
        }

        panel.localScale = Vector3.one * UiScale;
        float shownFor = 0f;
        while (shownFor < DisplayDuration)
        {
            shownFor += Time.unscaledDeltaTime;
            AnimateControls();
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            AnimateControls();
            yield return null;
        }

        Destroy(canvasGroup.gameObject);
        Destroy(this);
    }

    private void AnimateControls()
    {
        float press = (Mathf.Sin(Time.unscaledTime * 3.5f) + 1f) * 0.5f;
        if (joystickKnob != null)
        {
            joystickKnob.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.05f, press);
        }

        if (joystickGlow != null)
        {
            Color color = joystickGlow.color;
            color.a = Mathf.Lerp(0.18f, 0.62f, press);
            joystickGlow.color = color;
            joystickGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.16f, press);
        }

        if (joystickRing != null)
            joystickRing.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.12f, press);

        if (gripGlow != null)
        {
            Color color = gripGlow.color;
            color.a = Mathf.Lerp(0.18f, 0.65f, press);
            gripGlow.color = color;
            gripGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.16f, press);
        }

        if (gripRing != null)
        {
            gripRing.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.12f, press);
        }
    }

    private void OnDestroy()
    {
        if (circleSprite != null)
        {
            Texture2D texture = circleSprite.texture;
            Destroy(circleSprite);
            if (texture != null) Destroy(texture);
        }

        if (ringSprite != null)
        {
            Texture2D texture = ringSprite.texture;
            Destroy(ringSprite);
            if (texture != null) Destroy(texture);
        }

        if (arrowHeadSprite != null)
        {
            Texture2D texture = arrowHeadSprite.texture;
            Destroy(arrowHeadSprite);
            if (texture != null) Destroy(texture);
        }

        if (controllerSprite != null)
            Destroy(controllerSprite);
    }
}
