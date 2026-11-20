using TMPro;
using UnityEngine;

// Reading card and compact objective both stay inside the forward comfort cone.
public sealed class CampusLessonLayout : MonoBehaviour
{
    [SerializeField] private Transform trackedHead, panel, demo;
    [SerializeField] private RectTransform background;
    [SerializeField] private TextMeshProUGUI title, body, footer;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private UnityEngine.UI.Image readingProgress;
    private Renderer[] demoRenderers;
    private Color[] demoColours;
    private MaterialPropertyBlock demoBlock;
    private int[] demoColourProperties;
    private bool full=true;
    private float progress=1;
    private float readingWidth=660f;
    private Vector3 fromPosition, fromScale, targetPosition, targetScale;
    public bool IsFull => full;
    public bool IsAnimating => progress<1;
    public void ApplyArtwork()
    {
        if (background != null) FireTrainingUIStyle.Apply(background.GetComponent<UnityEngine.UI.Image>(), "panels/" + (full ? "lesson_card_large" : "lesson_card_compact"));
        if (title != null) title.color = FireTrainingUIStyle.Ink;
        if (body != null) body.color = FireTrainingUIStyle.Ink;
        if (footer != null) footer.color = new Color32(36,40,43,190);
        if (readingProgress != null) FireTrainingUIStyle.Apply(readingProgress, "hud/reading_fill");
    }
    public void Arrange(bool large) { Arrange(large,false); }
    public void ArrangeMainBrief()
    {
        // Main objective is a text-only card. Keep all text inside the paper,
        // rather than reusing positions reserved for the teaching animation.
        background.sizeDelta = new Vector2(760, 440);
        background.anchoredPosition = Vector2.zero;
        PlaceBriefText(title, new Vector2(0, 130), new Vector2(620, 56), 28, 24);
        PlaceBriefText(body, new Vector2(0, -20), new Vector2(620, 224), 24, 20);
        footer.gameObject.SetActive(false);
        demo.gameObject.SetActive(false);
        if (readingProgress != null)
        {
            readingProgress.rectTransform.anchorMin = readingProgress.rectTransform.anchorMax = Vector2.one * .5f;
            readingProgress.rectTransform.pivot = new Vector2(0, .5f);
            readingProgress.rectTransform.anchoredPosition = new Vector2(-300, -160);
            readingWidth = 600;
        }
    }
    private static void PlaceBriefText(TMP_Text text, Vector2 position, Vector2 size, float maximum, float minimum)
    {
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.one * .5f;
        text.rectTransform.pivot = Vector2.one * .5f;
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.rectTransform.localScale = Vector3.one;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSize = text.fontSizeMax = maximum;
        text.fontSizeMin = minimum;
    }
    public void Arrange(bool large,bool cinematic)
    {
        full=large; progress=0; readingWidth=660f; ApplyArtwork();
        fromPosition=trackedHead!=null?trackedHead.InverseTransformPoint(panel.position):new Vector3(0,-.10f,1.6f);
        fromScale=panel.localScale;
        targetPosition=full?new Vector3(0,-.04f,1.6f):cinematic?new Vector3(0,-.20f,1.6f):new Vector3(0,.04f,1.6f);
        targetScale=Vector3.one*(full?.9f:.85f);
        footer.gameObject.SetActive(full);
        if(full) ArrangeReadingCard(); else ArrangeObjectiveCard();
        demo.gameObject.SetActive(full);
        demo.localPosition=new Vector3(0,-.20f,-.04f);
        if(textGroup!=null) textGroup.alpha=0;
    }
    // Reading card: stack heading, copy and hint from the top and grow with the text.
    private void ArrangeReadingCard()
    {
        const float cardWidth=760f;
        const float textWidth=676f;
        const float padding=34f;
        const float gap=18f;
        SetFont(title,27,19);
        SetFont(body,22,15);
        SetFont(footer,17,13);
        title.alignment=TextAlignmentOptions.Center;
        body.alignment=TextAlignmentOptions.Center;
        footer.alignment=TextAlignmentOptions.Center;
        float titleHeight=Mathf.Max(38f,Measure(title,textWidth));
        float bodyHeight=Mathf.Max(54f,Measure(body,textWidth));
        float footerHeight=Mathf.Max(26f,Measure(footer,textWidth));
        float contentHeight=titleHeight+gap+bodyHeight+gap+footerHeight;
        float height=Mathf.Clamp(contentHeight+padding*2f,360f,560f);
        background.sizeDelta=new Vector2(cardWidth,height);
        float cursor=height*.5f-Mathf.Max(padding,(height-contentHeight)*.5f);
        title.rectTransform.sizeDelta=new Vector2(textWidth,titleHeight);
        title.rectTransform.anchoredPosition=new Vector2(0,cursor-titleHeight*.5f);
        cursor-=titleHeight+gap;
        body.rectTransform.sizeDelta=new Vector2(textWidth,bodyHeight);
        body.rectTransform.anchoredPosition=new Vector2(0,cursor-bodyHeight*.5f);
        cursor-=bodyHeight+gap;
        footer.rectTransform.sizeDelta=new Vector2(textWidth,footerHeight);
        footer.rectTransform.anchoredPosition=new Vector2(0,cursor-footerHeight*.5f);
    }
    private void ArrangeObjectiveCard()
    {
        background.sizeDelta=new Vector2(500,155);
        title.alignment=TextAlignmentOptions.Center;
        body.alignment=TextAlignmentOptions.Center;
        title.rectTransform.anchoredPosition=new Vector2(0,40);
        title.rectTransform.sizeDelta=new Vector2(470,64);
        SetFont(title,24,19);
        body.rectTransform.anchoredPosition=new Vector2(0,-30);
        body.rectTransform.sizeDelta=new Vector2(470,58);
        SetFont(body,21,17);
        footer.rectTransform.anchoredPosition=new Vector2(0,-211);
        footer.rectTransform.sizeDelta=new Vector2(710,78);
        SetFont(footer,19,15);
    }
    private static float Measure(TMP_Text text,float width)
    {
        if(text==null) return 0f;
        return Mathf.Ceil(text.GetPreferredValues(text.text,width,0f).y)+4f;
    }
    private static void SetFont(TMP_Text text,float maximum,float minimum)
    { text.fontSize=text.fontSizeMax=maximum; text.fontSizeMin=minimum; }
    public void SetReadingProgress(float ratio)
    { if(readingProgress!=null) { readingProgress.gameObject.SetActive(full); readingProgress.rectTransform.sizeDelta=new Vector2(readingWidth*Mathf.Clamp01(ratio),4); } }
    public void Tick(float seconds)
    {
        progress=Mathf.Clamp01(progress+Mathf.Max(0,seconds)/.9f);
        float eased=1-Mathf.Pow(1-progress,3);
        panel.localScale=Vector3.Scale(Vector3.Lerp(fromScale,targetScale,eased),new Vector3(1,Mathf.Lerp(.65f,1,eased),1));
        float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.15f)/.65f));
        if(textGroup!=null) textGroup.alpha=alpha;
        // Native graphics resources are created in play-time, not during serialization.
        if(demoBlock==null) demoBlock=new MaterialPropertyBlock();
        if(demoRenderers==null) demoRenderers=demo.GetComponentsInChildren<Renderer>(true);
        if(demoColours==null || demoColours.Length!=demoRenderers.Length ||
            demoColourProperties==null || demoColourProperties.Length!=demoRenderers.Length)
        {
            demoColours=new Color[demoRenderers.Length];
            demoColourProperties=new int[demoRenderers.Length];
            for(int i=0;i<demoRenderers.Length;i++)
            {
                var renderer=demoRenderers[i];
                if(renderer==null)
                { demoColourProperties[i]=-1; demoColours[i]=Color.white; continue; }
                var material=renderer.sharedMaterial;
                string colourProperty=material==null?null:material.HasProperty("_BaseColor")?"_BaseColor":material.HasProperty("_Color")?"_Color":null;
                demoColourProperties[i]=string.IsNullOrEmpty(colourProperty)?-1:Shader.PropertyToID(colourProperty);
                demoColours[i]=demoColourProperties[i]>=0?material.GetColor(demoColourProperties[i]):Color.white;
            }
        }
        for(int i=0;i<demoRenderers.Length;i++)
        {
            if(demoRenderers[i]==null) continue;
            var colour=demoColours[i]; colour.a*=alpha;
            demoBlock.Clear();
            if(demoColourProperties[i]>=0) demoBlock.SetColor(demoColourProperties[i],colour);
            demoRenderers[i].SetPropertyBlock(demoBlock);
        }
        demo.localScale=Vector3.one*Mathf.Lerp(.7f,1,eased); Place();
    }
    private void Place()
    {
        if(trackedHead==null || panel==null) return;
        float eased=1-Mathf.Pow(1-progress,3);
        panel.SetPositionAndRotation(trackedHead.TransformPoint(Vector3.Lerp(fromPosition,targetPosition,eased)),trackedHead.rotation);
    }
    private void LateUpdate() { Place(); }
    private void OnEnable() { Application.onBeforeRender+=Place; }
    private void OnDisable() { Application.onBeforeRender-=Place; }
}
