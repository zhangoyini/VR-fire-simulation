using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Step = CampusClassroomTutorial.Step;

internal sealed class CampusTutorialView
{
    internal enum Demo { None, Teleport, Feedback, Grab, Release, Press, X, Y, Menu }
    private static readonly Color Ink = new Color(.025f, .07f, .085f, .96f);
    private static readonly Color Mint = new Color(.45f, .95f, .83f);
    private static readonly Color Green = new Color(.48f, .94f, .53f);
    private static readonly Color Red = new Color(1f, .22f, .16f);
    private static readonly Color Blue = new Color(.32f, .81f, 1f);
    private static readonly Color Gold = new Color(1f, .67f, .29f);
    private static readonly Color Dark = new Color(.10f, .18f, .21f);

    private readonly Transform world, hud;
    private readonly GameObject boardSign;
    private readonly bool boardSignWasActive;
    private readonly TMP_FontAsset font;
    private readonly Material worldMaterial;
    private readonly Sprite disc;
    private readonly Texture2D discTexture;
    private readonly CanvasGroup promptGroup, demoGroup, feedbackGroup, healthGroup;
    private readonly TMP_Text prompt, hpNumber;
    private readonly RectTransform[] feedbackIcons = new RectTransform[2];
    private readonly RectTransform promptBackground;
    private readonly Image hpFill, hpLost, hpGlow;
    private readonly CampusNavigationPanels panels;
    private float promptDuration = CampusClassroomTutorial.PromptSeconds;
    private readonly RectTransform demoStick;
    private readonly Image demoX, demoY, demoTrigger, demoStickImage, arcDot, playerDot;
    private readonly Image[] arc = new Image[18];
    private readonly LineRenderer marker, contact, bookHighlight, placement;
    private readonly Renderer buttonFace, lamp;
    private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    private Step step;
    private Demo demo;
    private float promptAge, fadeFrom;
    private readonly CanvasGroup skipGroup;
    private System.Action advancePage;
    internal void BindAdvance(System.Action action) => advancePage = action;
    internal void PracticeInput(bool xDown, bool yDown) => panels.TogglePracticePanel(xDown, yDown);
    internal CampusTutorialButton Button { get; }

    internal CampusTutorialView(Transform parent, Transform head, Transform board, GameObject boardSign,
        TMP_FontAsset font, CampusNavigationPanels panels)
    {
        this.font = font;
        this.panels = panels;
        this.boardSign = boardSign;
        world = new GameObject("Classroom tutorial props").transform;
        world.SetParent(parent, false);
        hud = new GameObject("Classroom tutorial HUD").transform;
        hud.SetParent(head, false);
        worldMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        const int size = 64;
        discTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f).magnitude;
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - radius));
        }
        discTexture.SetPixels32(pixels);
        discTexture.Apply(false, true);
        disc = Sprite.Create(discTexture, new Rect(0, 0, size, size), Vector2.one * .5f);

        promptGroup = CanvasAt("Current instruction", hud, new Vector3(0, .22f, 1.35f), .0014f);
        promptBackground = Picture(promptGroup.transform, Vector2.zero, new Vector2(700, 105), Ink).rectTransform;
        prompt = Label(promptGroup.transform, Vector2.zero, new Vector2(650, 92), 29, "");

        demoGroup = CanvasAt("2D controller animation", hud, new Vector3(0, -.07f, 1.38f), .00125f);
        Picture(demoGroup.transform, Vector2.zero, new Vector2(560, 270), Ink);
        DrawController(demoGroup.transform, new Vector2(-128, 30), true, out demoStickImage, out demoX, out demoY, out demoTrigger);
        demoStick = demoStickImage.rectTransform;
        for (int i = 0; i < arc.Length; i++)
            arc[i] = Picture(demoGroup.transform, Vector2.zero, Vector2.one * 7, Blue, disc);
        arcDot = Picture(demoGroup.transform, Vector2.zero, Vector2.one * 18, Green, disc);
        playerDot = Picture(demoGroup.transform, Vector2.zero, Vector2.one * 9, Color.white, disc);
        Label(demoGroup.transform, new Vector2(0, -101), new Vector2(480, 38), 18, "Quest 3  ·  左手柄 / Left controller");

        feedbackGroup = CanvasAt("Teleport feedback animation", hud, new Vector3(0, -.07f, 1.38f), .00125f);
        Picture(feedbackGroup.transform, Vector2.zero, new Vector2(560, 270), Ink);
        for (int i = 0; i < feedbackIcons.Length; i++)
        {
            var icon = new GameObject(i == 0 ? "Valid blue landing" : "Invalid red cross", typeof(RectTransform));
            icon.transform.SetParent(feedbackGroup.transform, false);
            feedbackIcons[i] = icon.GetComponent<RectTransform>();
            feedbackIcons[i].anchoredPosition = new Vector2(i == 0 ? -130 : 130, 0);
            Color color = TeleportDestinationMarker.TargetColor(i == 0);
            for (int segment = 0; segment < 48; segment++)
            {
                float angle = segment * 360f / 48;
                Vector2 at = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 62;
                var stroke = Picture(icon.transform, at, new Vector2(10, 5), color);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, angle + 90);
            }
            if (i == 0)
            {
                Picture(icon.transform, Vector2.zero, new Vector2(5, 60), Color.white);
                for (int side = -1; side <= 1; side += 2)
                    Picture(icon.transform, new Vector2(side * 10, 19), new Vector2(5, 30), Color.white)
                        .rectTransform.localRotation = Quaternion.Euler(0, 0, side * 45);
            }
            else
                for (int side = -1; side <= 1; side += 2)
                    Picture(icon.transform, Vector2.zero, new Vector2(75, 7), color)
                        .rectTransform.localRotation = Quaternion.Euler(0, 0, side * 45);
        }

        healthGroup = CanvasAt("Teaching health", hud, new Vector3(0, .40f, 1.45f), .0015f);
        Picture(healthGroup.transform, Vector2.zero, new Vector2(370, 61), Ink);
        hpGlow = Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(267, 26), Mint);
        Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(260, 19), Dark);
        hpLost = Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(260, 19), Gold);
        hpFill = Picture(healthGroup.transform, new Vector2(-153, 0), new Vector2(260, 19), Green);
        hpFill.rectTransform.pivot = new Vector2(0, .5f);
        hpFill.rectTransform.anchoredPosition = new Vector2(-153, 0);
        hpNumber = Label(healthGroup.transform, new Vector2(143, 0), new Vector2(75, 42), 25, "100");
        hpNumber.color = Color.white;
        FireTrainingUIStyle.Apply(hpFill, "hud/hud_fill");

        var foregroundHud = hud.gameObject.AddComponent<CampusUIForeground>();
        foregroundHud.Configure(Shader.Find("Campus/Foreground UI"));
        if (board != null)
        {
            boardSignWasActive = boardSign != null && boardSign.activeSelf;
            if (boardSign != null) boardSign.SetActive(false);
            BuildBoard(board);
        }

        marker = Outline("Teleport landing marker", .022f, 48);
        contact = Outline("Window contact point", .012f, 32);
        bookHighlight = Outline("Book highlight", .010f, 32);
        placement = Outline("Book placement outline", .012f, 4);

        Vector3 button = CampusClassroomTutorial.ButtonPosition;
        Shape("Teaching button plate", PrimitiveType.Cube, button, new Vector3(.065f, .29f, .24f), Dark);
        buttonFace = Shape("Teaching button", PrimitiveType.Cube, button - Vector3.right * .045f,
            new Vector3(.045f, .115f, .115f), Blue, true);
        Button = buttonFace.gameObject.AddComponent<CampusTutorialButton>();
        lamp = Shape("Teaching indicator", PrimitiveType.Sphere, button + new Vector3(-.055f, .10f, 0),
            Vector3.one * .055f, Dark);
        skipGroup = CanvasAt("Manual tutorial advance", hud, new Vector3(0, -.2f, 1.35f), .0014f);
        var skip = Picture(skipGroup.transform, Vector2.zero, new Vector2(230, 77), Color.white,
            FireTrainingUIStyle.Load("buttons/button_skip_wide"));
        skip.name = "Tutorial skip";
        skip.preserveAspect = true;
        var collider = skip.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(230, 77, 18); collider.isTrigger = true;
        var advance = skip.gameObject.AddComponent<VRMenuButton>();
        advance.Bind(() => advancePage?.Invoke(), Object.FindObjectOfType<SystemUIAudio>());
        var appearance = skip.GetComponent<FireTrainingButtonVisual>();
        if (appearance != null) appearance.ArtworkOverride = "buttons/button_skip_wide";
        foreach (var ray in Object.FindObjectsOfType<VRMenuRayPointer>(true)) ray.RegisterMenuButton(advance);
    }

    internal void Show(Step next, string text, Demo animation, float readingSeconds)
    {
        step = next;
        demo = animation;
        promptDuration = readingSeconds;
        if (step == Step.ToDoor) Button.BeginFreePractice();
        if (step != Step.FadePrompt) UpdatePrompt(text);
        fadeFrom = promptGroup.alpha;
        promptAge = 0;
        bool multiline = text.Split('\n').Length > 2;
        float width = 700;
        float height = multiline ? 190 : 122;
        promptBackground.sizeDelta = new Vector2(width, height);
        prompt.rectTransform.sizeDelta = new Vector2(width - 50, height - 12);
        demoGroup.gameObject.SetActive(demo != Demo.None && demo != Demo.Feedback);
        feedbackGroup.gameObject.SetActive(demo == Demo.Feedback);
        promptGroup.gameObject.SetActive(step == Step.FadePrompt || !string.IsNullOrEmpty(text));
        skipGroup.gameObject.SetActive(!string.IsNullOrEmpty(text) && CampusClassroomTutorial.CanSkipStep(step));
        healthGroup.gameObject.SetActive(step >= Step.Health);
        contact.gameObject.SetActive(step == Step.WindowDemo || step == Step.OpenWindow || step == Step.WindowDone);
        bookHighlight.gameObject.SetActive(false);
        buttonFace.transform.localScale = new Vector3(.045f, .115f, .115f);
        Tint(buttonFace, step == Step.PressButton ? Mint : Blue);
        Tick(0, 0);
    }

    internal void UpdatePrompt(string text)
    {
        if (prompt.text == text) return;
        prompt.text = text;
        promptAge = 0;
    }

    internal void Tick(float age, float dt)
    {
        promptAge += dt;
        // Long instructions remain legible while the interaction is pending.
        promptGroup.alpha = step == Step.FadePrompt ? fadeFrom * (1f - Fade(age, .65f)) :
            Fade(promptAge, .2f);
        float hintY = step == Step.MapClose || step == Step.TaskClose ? -.36f : .22f;
        promptGroup.transform.localPosition = new Vector3(0, hintY, 1.35f);
        float bottom = hintY - promptBackground.rect.height * .0014f * .5f;
        if (demo != Demo.None) bottom = Mathf.Min(bottom, -.07f - 270 * .00125f * .5f);
        if (step == Step.WindowDemo) bottom = Mathf.Min(bottom, -.40f);
        skipGroup.transform.localPosition = new Vector3(0, bottom - .025f - 77 * .0014f * .5f, 1.35f);
        demoGroup.alpha = Fade(age, .45f);
        feedbackGroup.alpha = Fade(age, .45f);
        if (demo == Demo.Feedback)
        {
            float blend = .5f - .5f * Mathf.Cos(age * Mathf.PI * 2f / promptDuration);
            feedbackIcons[0].localScale = Vector3.one * Mathf.Lerp(1.12f, .85f, blend);
            feedbackIcons[1].localScale = Vector3.one * Mathf.Lerp(.85f, 1.12f, blend);
        }
        else if (demo != Demo.None) AnimateDiagram(age);
        if (step >= Step.Health) AnimateHealth(age);
        if (Button.Pressed) Tint(lamp, Button.GreenLight ? Green : Red);
        panels.ShowPractice(step, age, dt);
    }

    private void AnimateDiagram(float age)
    {
        float pulse = .5f + .5f * Mathf.Sin(age * 8f);
        Color active = Color.Lerp(Blue, Mint, pulse);
        demoStickImage.color = demo == Demo.Teleport || demo == Demo.Menu ? active : Dark;
        demoStick.localScale = Vector3.one * (demo == Demo.Menu ? Mathf.Lerp(1f, .79f, pulse) : 1f);
        demoX.color = demo == Demo.X ? active : Dark;
        demoY.color = demo == Demo.Y ? active : Dark;
        bool trigger = demo == Demo.Grab || demo == Demo.Release || demo == Demo.Press;
        bool squeezed = demo == Demo.Grab ? age >= 1.4f : demo == Demo.Release ? age < 2f : age >= 1.6f && age < 3.2f;
        demoTrigger.color = trigger ? squeezed ? Mint : Blue : Dark;
        demoTrigger.rectTransform.localScale = trigger && squeezed ? new Vector3(.65f, 1f, 1f) : Vector3.one;
        bool aiming = demo == Demo.Teleport && age >= 1f && age < 4.5f;
        demoStick.anchoredPosition = new Vector2(-125, aiming ? 99 : 93);
        foreach (var highlight in new[] { demoStickImage, demoX, demoY, demoTrigger })
        { var tint = highlight.color; tint.a = tint == Dark ? 0 : .48f; highlight.color = tint; }
        for (int i = 0; i < arc.Length; i++)
        {
            float t = (i + 1f) / arc.Length;
            arc[i].gameObject.SetActive(aiming && t <= (age - 1f) * 1.6f);
            arc[i].rectTransform.anchoredPosition = ArcPoint(t, age);
        }
        arcDot.gameObject.SetActive(demo == Demo.Teleport && age >= 1.7f);
        arcDot.color = aiming ? TeleportDestinationMarker.TargetColor(true) : Green;
        arcDot.rectTransform.anchoredPosition = ArcPoint(1f, Mathf.Min(age, 4.5f));
        arcDot.rectTransform.localScale = Vector3.one * (aiming ? 1f : 1.3f);
        playerDot.gameObject.SetActive(demo == Demo.Teleport && age >= 4.5f);
        playerDot.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-66, -8),
            arcDot.rectTransform.anchoredPosition, Fade(age - 4.5f, .45f));
    }

    private static Vector2 ArcPoint(float t, float age) =>
        new Vector2(-66f + t * (232f + Mathf.Sin(age * 2f) * 24f),
            47f + t * (1f - t) * 210f - t * t * 55f);

    private void AnimateHealth(float age)
    {
        float t = step == Step.Health ? age : 10f;
        float loss = .18f * Fade(t - 3.5f, .8f) * (1f - Fade(t - 6.1f, .8f));
        hpFill.rectTransform.sizeDelta = new Vector2(260 * (1 - loss), 19);
        hpLost.color = new Color(Gold.r, Gold.g, Gold.b, .85f * Fade(t - 3.5f, .8f) * (1f - Fade(t - 5.1f, 1f)));
        hpGlow.color = new Color(Mint.r, Mint.g, Mint.b, .6f * Fade(t, .8f) * (1f - Fade(t - 1.1f, 1.4f)));
        string number = Mathf.RoundToInt(100 * (1 - loss)).ToString();
        if (hpNumber.text != number) hpNumber.text = number;
        healthGroup.alpha = Fade(t, 1.1f);
    }

    internal void SetMarker(Vector3? point, bool success)
    {
        SetRing(marker, point + Vector3.up * .019f, .255f, .255f, true);
        Tint(marker, success ? Green : Blue);
    }

    internal void SetContact(Vector3? point) => SetRing(contact, point, .073f, .073f, false);

    internal void SetBookHighlight(Vector3? point) =>
        SetRing(bookHighlight, point + Vector3.up * .055f, .12f, .155f, true);

    internal void SetPlacement(Vector3? point)
    {
        placement.gameObject.SetActive(point.HasValue);
        if (!point.HasValue) return;
        Vector3 center = point.Value - Vector3.up * .022f;
        placement.SetPosition(0, center + new Vector3(-.13f, 0, -.16f));
        placement.SetPosition(1, center + new Vector3(-.13f, 0, .16f));
        placement.SetPosition(2, center + new Vector3(.13f, 0, .16f));
        placement.SetPosition(3, center + new Vector3(.13f, 0, -.16f));
    }

    private static void SetRing(LineRenderer line, Vector3? point, float width, float height, bool horizontal)
    {
        line.gameObject.SetActive(point.HasValue);
        if (!point.HasValue) return;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * (2f * Mathf.PI / line.positionCount);
            line.SetPosition(i, point.Value + Vector3.right * (width * Mathf.Cos(angle)) +
                (horizontal ? Vector3.forward : Vector3.up) * (height * Mathf.Sin(angle)));
        }
    }

    private LineRenderer Outline(string name, float width, int count)
    {
        var node = new GameObject(name);
        node.transform.SetParent(world, false);
        var line = node.AddComponent<LineRenderer>();
        line.sharedMaterial = worldMaterial;
        line.positionCount = count;
        line.loop = true;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        Tint(line, Mint);
        node.SetActive(false);
        return line;
    }

    private Renderer Shape(string name, PrimitiveType shape, Vector3 point, Vector3 scale, Color color, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(world, false);
        go.transform.position = point;
        go.transform.localScale = scale;
        go.GetComponent<Collider>().enabled = solid;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = worldMaterial;
        Tint(renderer, color);
        return renderer;
    }

    private void Tint(Renderer renderer, Color color)
    {
        tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
    }

    private void BuildBoard(Transform board)
    {
        var group = CanvasAt("Quest 3 diagrams on classroom board", world, Vector3.zero, .0036f);
        group.transform.SetPositionAndRotation(board.position + board.forward * .04f,
            board.rotation * Quaternion.Euler(0, 180, 0));
        DrawBoardController(group.transform, -290, true);
        DrawBoardController(group.transform, 290, false);
    }

    private void DrawBoardController(Transform parent, float x, bool left)
    {
        var diagram = new GameObject(left ? "Left controller diagram" : "Right controller diagram",
            typeof(RectTransform)).GetComponent<RectTransform>();
        diagram.SetParent(parent, false);
        diagram.anchoredPosition = new Vector2(x, 14);
        diagram.localScale = Vector3.one * 1.9f;
        var art = Picture(diagram, Vector2.zero, new Vector2(180, 180), Color.white,
            FireTrainingUIStyle.Load("controller/controller_" + (left ? "left" : "right") + "_base"));
        art.preserveAspect = true;
    }

    private void DrawController(Transform parent, Vector2 origin, bool left,
        out Image stick, out Image lower, out Image upper, out Image trigger)
    {
        var art = Picture(parent, origin, new Vector2(200, 200), Color.white,
            FireTrainingUIStyle.Load("controller/controller_" + (left ? "left" : "right") + "_base"));
        art.preserveAspect = true;
        float mirror = left ? 1 : -1;
        // Highlight centres follow the supplied illustrated control faces.
        stick = Picture(parent, origin + new Vector2(3 * mirror, 63), Vector2.one * 30, Dark, disc);
        lower = Picture(parent, origin + new Vector2(-3 * mirror, 43), Vector2.one * 19, Dark, disc);
        upper = Picture(parent, origin + new Vector2(20 * mirror, 55), Vector2.one * 19, Dark, disc);
        trigger = Picture(parent, origin + new Vector2(49 * mirror, 21), new Vector2(15, 23), Dark, disc);
        // The supplied base already includes readable ABXY labels. Highlights pulse
        // translucent over the controls, rather than replacing their lettering.

    }

    internal static CanvasGroup CanvasAt(string name, Transform parent, Vector3 offset, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        go.transform.localScale = Vector3.one * scale;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        return group;
    }

    internal static Image Picture(Transform parent, Vector2 at, Vector2 size, Color color, Sprite sprite = null)
    {
        var go = new GameObject("Tutorial graphic", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = at;
        image.raycastTarget = false;
        image.color = color;
        image.sprite = sprite;
        if (sprite == null) FireTrainingUIStyle.StyleCard(image, parent, size);
        return image;
    }

    private TMP_Text Label(Transform parent, Vector2 at, Vector2 size, int pointSize, string value) =>
        CreateLabel(font, parent, at, size, pointSize, value);

    internal static TMP_Text CreateLabel(TMP_FontAsset font, Transform parent, Vector2 at, Vector2 size, int pointSize, string value)
    {
        var go = new GameObject("Tutorial label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = size;
        label.rectTransform.anchoredPosition = at;
        label.font = font;
        label.enableAutoSizing = true;
        label.fontSizeMin = pointSize * .75f;
        label.fontSize = label.fontSizeMax = pointSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = value;
        label.color = FireTrainingUIStyle.Ink;
        return label;
    }

    private static float Fade(float age, float duration) =>
        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / duration));

    internal void SetVisible(bool visible)
    {
        hud.gameObject.SetActive(visible);
        if (!visible) panels.Hide(false, false);
    }

    internal void Dispose()
    {
        if (boardSign != null) boardSign.SetActive(boardSignWasActive);
        panels.Hide(false, false, fold: true);
        if (hud != null) { hud.gameObject.SetActive(false); Object.Destroy(hud.gameObject); }
        if (world != null) { world.gameObject.SetActive(false); Object.Destroy(world.gameObject); }
        Object.Destroy(worldMaterial);
        Object.Destroy(disc);
        Object.Destroy(discTexture);
    }
}
