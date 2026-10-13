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
    private bool full=true;
    private float progress=1;
    private Vector3 fromPosition, fromScale, targetPosition, targetScale;
    public bool IsFull => full;
    public bool IsAnimating => progress<1;
    public void Arrange(bool large) { Arrange(large,false); }
    public void Arrange(bool large,bool cinematic)
    {
        full=large; progress=0;
        fromPosition=trackedHead!=null?trackedHead.InverseTransformPoint(panel.position):new Vector3(0,-.10f,1.6f);
        fromScale=panel.localScale;
        targetPosition=full?new Vector3(0,-.04f,1.6f):cinematic?new Vector3(0,-.20f,1.6f):new Vector3(0,.04f,1.6f);
        targetScale=Vector3.one*(full?.9f:.85f);
        background.sizeDelta=new Vector2(full?760:500,full?500:155);
        title.rectTransform.anchoredPosition=new Vector2(0,full?210:40);
        title.rectTransform.sizeDelta=new Vector2(full?710:470,full?58:64);
        SetFont(title,full?29:24,full?20:19);
        body.rectTransform.anchoredPosition=new Vector2(0,full?95:-30);
        body.rectTransform.sizeDelta=new Vector2(full?710:470,full?176:58);
        SetFont(body,full?24:21,full?18:17);
        footer.rectTransform.anchoredPosition=new Vector2(0,-211);
        footer.rectTransform.sizeDelta=new Vector2(710,78); SetFont(footer,19,15);
        footer.gameObject.SetActive(full); demo.gameObject.SetActive(full);
        demo.localPosition=new Vector3(0,-.20f,-.04f);
        if(textGroup!=null) textGroup.alpha=0;
    }
    private static void SetFont(TMP_Text text,float maximum,float minimum)
    { text.fontSize=text.fontSizeMax=maximum; text.fontSizeMin=minimum; }
    public void SetReadingProgress(float ratio)
    { if(readingProgress!=null) { readingProgress.gameObject.SetActive(full); readingProgress.rectTransform.sizeDelta=new Vector2(710*Mathf.Clamp01(ratio),4); } }
    public void Tick(float seconds)
    {
        progress=Mathf.Clamp01(progress+Mathf.Max(0,seconds)/.9f);
        float eased=1-Mathf.Pow(1-progress,3);
        panel.localScale=Vector3.Scale(Vector3.Lerp(fromScale,targetScale,eased),new Vector3(1,Mathf.Lerp(.65f,1,eased),1));
        float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.15f)/.65f));
        if(textGroup!=null) textGroup.alpha=alpha;
        // Native graphics resources are created in play-time, not during serialization.
        if(demoBlock==null) demoBlock=new MaterialPropertyBlock();
        if(demoRenderers==null)
        {
            demoRenderers=demo.GetComponentsInChildren<Renderer>(true); demoColours=new Color[demoRenderers.Length];
            for(int i=0;i<demoRenderers.Length;i++) demoColours[i]=demoRenderers[i].sharedMaterial.GetColor("_BaseColor");
        }
        for(int i=0;i<demoRenderers.Length;i++)
        {
            var colour=demoColours[i]; colour.a*=alpha;
            demoBlock.Clear(); demoBlock.SetColor("_BaseColor",colour); demoRenderers[i].SetPropertyBlock(demoBlock);
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
