using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Step = CampusClassroomTutorial.Step;

internal sealed class CampusTutorialView
{
    internal enum Demo { None, Teleport, Grab, Release, Press, X, Y, Menu }
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
    private readonly CanvasGroup promptGroup, demoGroup, healthGroup;
    private readonly TMP_Text prompt, demoCaption, hpNumber;
    private readonly RectTransform promptBackground;
    private readonly Image hpFill, hpLost, hpGlow;
    private readonly CampusNavigationPanels panels;
    private readonly RectTransform demoStick;
    private readonly Image demoX, demoY, demoTrigger, demoStickImage, arcDot, playerDot;
    private readonly Image[] arc = new Image[18];
    private readonly LineRenderer marker, contact, bookHighlight, placement;
    private readonly Renderer buttonFace, lamp;
    private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    private Step step;
    private Demo demo;
    private float promptAge, fadeFrom;
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
        demoCaption = Label(demoGroup.transform, new Vector2(95, 25), new Vector2(285, 126), 26, "");
        Label(demoGroup.transform, new Vector2(0, -101), new Vector2(480, 38), 18, "Quest 3  ·  左手柄 / Left controller");

        healthGroup = CanvasAt("Teaching health", hud, new Vector3(0, .40f, 1.45f), .0015f);
        Picture(healthGroup.transform, Vector2.zero, new Vector2(370, 61), Ink);
        hpGlow = Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(267, 26), Mint);
        Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(260, 19), Dark);
        hpLost = Picture(healthGroup.transform, new Vector2(-23, 0), new Vector2(260, 19), Gold);
        hpFill = Picture(healthGroup.transform, new Vector2(-153, 0), new Vector2(260, 19), Green);
        hpFill.rectTransform.pivot = new Vector2(0, .5f);
        hpFill.rectTransform.anchoredPosition = new Vector2(-153, 0);
        hpNumber = Label(healthGroup.transform, new Vector2(143, 0), new Vector2(75, 42), 25, "100");

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
    }

    internal void Show(Step next, string text, Demo animation)
    {
        step = next;
        demo = animation;
        if (step == Step.ToDoor) Button.BeginFreePractice();
        if (step != Step.FadePrompt) UpdatePrompt(text);
        fadeFrom = promptGroup.alpha;
        promptAge = 0;
        bool multiline = step == Step.Health || step == Step.ToDoor || step == Step.MenuPractice;
        float width = demo != Demo.None || multiline ? 700 : 450;
        float height = multiline ? 164 : 122;
        promptBackground.sizeDelta = new Vector2(width, height);
        prompt.rectTransform.sizeDelta = new Vector2(width - 50, height - 12);
        demoGroup.gameObject.SetActive(demo != Demo.None);
        promptGroup.gameObject.SetActive(step == Step.FadePrompt || !string.IsNullOrEmpty(text));
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
        // The main prompt lasts seven seconds. If input is still pending, leave a readable reminder.
        float reminder = Fade(promptAge - CampusClassroomTutorial.PromptSeconds, .2f);
        promptGroup.alpha = step == Step.FadePrompt ? fadeFrom * (1f - Fade(age, .65f)) :
            Fade(promptAge, .2f) * Mathf.Lerp(1f, .82f, reminder);
        float hintY = step == Step.MapClose || step == Step.TaskClose ? -.36f : .22f;
        promptGroup.transform.localPosition = new Vector3(0, hintY, 1.35f);
        demoGroup.alpha = Fade(age, .45f);
        if (demo != Demo.None) AnimateDiagram(age);
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
        demoStick.anchoredPosition = new Vector2(-154, aiming ? 73 : 59);
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
        string caption;
        switch (demo)
        {
            case Demo.Teleport:
                caption = "前推摇杆瞄准蓝色落点\n让摇杆回中，完成瞬移\n<size=80%>Push the stick to aim.\nRelease it to teleport.</size>";
                demoCaption.rectTransform.anchoredPosition = new Vector2(95, -45);
                break;
            case Demo.Grab: caption = "用任一手柄\n持续按住扳机\n<size=80%>Hold the trigger on\neither controller.</size>"; break;
            case Demo.Release: caption = "手移到桌面上方\n松开扳机\n<size=80%>Move over the desk.\nRelease the trigger.</size>"; break;
            case Demo.Press:
                caption = "手靠近按钮，按下扳机\n指示灯亮起后松开\n<size=80%>Press near the button.\nRelease when it lights up.</size>";
                break;
            case Demo.X: caption = "左手柄下方的 X 键\n按一次展开，再按一次收起\n<size=80%>Press X to open the map.\nPress again to close it.</size>"; break;
            case Demo.Y: caption = "左手柄上方的 Y 键\n按一次查看，再按一次收起\n<size=80%>Press Y to view Tasks.\nPress again to close it.</size>"; break;
            default: caption = "按下左摇杆打开菜单\n选中「继续」返回游戏\n<size=80%>Click the left stick to pause.\nSelect Resume to return.</size>"; break;
        }
        if (demo != Demo.Teleport) demoCaption.rectTransform.anchoredPosition = new Vector2(95, 25);
        if (demoCaption.text != caption) demoCaption.text = caption;
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
        DrawController(diagram, Vector2.zero, left, out _, out _, out _, out _);
    }

    private void DrawController(Transform parent, Vector2 origin, bool left,
        out Image stick, out Image lower, out Image upper, out Image trigger)
    {
        float side = left ? -1 : 1;
        Color shell = new Color(.88f, .92f, .91f);
        var grip = Picture(parent, origin + new Vector2(side * 20, -40), new Vector2(57, 110), shell, disc);
        grip.rectTransform.localRotation = Quaternion.Euler(0, 0, side * -18f);
        trigger = Picture(parent, origin + new Vector2(side * 60, 3), new Vector2(25, 47), Dark, disc);
        Picture(parent, origin + new Vector2(0, 24), new Vector2(121, 106), shell, disc);
        stick = Picture(parent, origin + new Vector2(side * 26, 29), Vector2.one * 37, Dark, disc);
        Picture(stick.transform, Vector2.zero, Vector2.one * 23, new Color(.30f, .39f, .43f), disc);
        Vector2 low = origin + new Vector2(side * -25, 15);
        Vector2 high = origin + new Vector2(side * -21, 46);
        lower = Picture(parent, low, Vector2.one * 28, Dark, disc);
        upper = Picture(parent, high, Vector2.one * 28, Dark, disc);
        Label(parent, low, new Vector2(32, 42), 20, left ? "X" : "A");
        Label(parent, high, new Vector2(32, 42), 20, left ? "Y" : "B");
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
        hud.gameObject.SetActive(false);
        world.gameObject.SetActive(false);
        Object.Destroy(hud.gameObject);
        Object.Destroy(world.gameObject);
        Object.Destroy(worldMaterial);
        Object.Destroy(disc);
        Object.Destroy(discTexture);
    }
}
