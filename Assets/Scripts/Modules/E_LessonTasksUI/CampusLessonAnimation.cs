using UnityEngine;

// Real 3D props animate locally; no movie texture, camera motion or fabricated HMD pose.
public sealed class CampusLessonAnimation : MonoBehaviour
{
    [SerializeField] private GameObject headsetGroup, controllerGroup, towelGroup, smokeGroup;
    [SerializeField] private Transform headset, leftController, rightController, stick, trigger,
        door, cloth, tapLever, water, face, healthFill;
    [SerializeField] private Transform leftTrigger,confirmButton;
    [SerializeField] private CampusTeachingHand leftHand,rightHand;
    [SerializeField] private TMPro.TMP_Text actionLabel;
    private CampusLessonDirector.Phase phase;
    private int page;
    private float time;
    public void Show(CampusLessonDirector.Phase current,int currentPage)
    {
        phase=current; page=currentPage; time=0;
        headsetGroup.SetActive(current==CampusLessonDirector.Phase.Controls && page==0);
        controllerGroup.SetActive(current==CampusLessonDirector.Phase.Controls && page>0);
        towelGroup.SetActive(current==CampusLessonDirector.Phase.WetTowel || current==CampusLessonDirector.Phase.FindTowel);
        smokeGroup.SetActive(current==CampusLessonDirector.Phase.SmokeLesson);
    }
    public void Tick(float dt)
    {
        time+=Mathf.Max(0,dt); float wave=.5f+.5f*Mathf.Sin(time*2);
        if(headsetGroup.activeSelf) { headset.localRotation=Quaternion.Euler(0,-18+Mathf.Sin(time)*20,0); headset.localPosition=new Vector3(0,0,-.025f+wave*.04f); }
        if(controllerGroup.activeSelf)
        {
            int beat=Mathf.FloorToInt(time/5)%4;
            float press=(Mathf.Sin(time*2.5f)+1)*.5f;
            stick.localPosition=new Vector3(-.024f+Mathf.Sin(time*2)*.012f,.119f-(page==1 && beat==3?press*.008f:0),-.014f+Mathf.Cos(time*2)*.012f);
            leftController.localPosition=new Vector3(-.15f,0,0);
            rightController.localPosition=new Vector3(.15f,0,page==2?wave*.10f:0);
            leftController.localRotation=Quaternion.Euler(-25,Mathf.Sin(time*.5f)*18,-12);
            rightController.localRotation=Quaternion.Euler(-25,-Mathf.Sin(time*.5f)*18,12);
            trigger.localScale=new Vector3(.055f,.025f,Mathf.Lerp(.025f,.012f,wave));
            door.localRotation=Quaternion.Euler(0,page==2?wave*75:0,0);
            if(leftTrigger!=null) leftTrigger.localScale=new Vector3(.035f,.045f,Mathf.Lerp(.028f,.016f,press));
            if(confirmButton!=null) confirmButton.localScale=Vector3.one*(page==2 && beat==3?Mathf.Lerp(.022f,.027f,press):.022f);
            leftHand?.SetGrip(page==2?Mathf.Lerp(.55f,1,press):.65f); rightHand?.SetGrip(page==2?Mathf.Lerp(.55f,1,press):.65f);
            if(actionLabel!=null) actionLabel.text=page==1?(beat==3?"左摇杆按下：暂停\nClick left stick: pause":"左摇杆：行走\nLeft stick: move"):
                beat==0?"双扳机：握持\nEither trigger: grab":beat==1?"握住推动：开门\nHold and push: open":beat==2?"挥手松开：投掷\nSwing and release: throw":"右 A：确认/开始\nRight A: confirm / begin";
        }
        if(towelGroup.activeSelf)
        {
            float t=Mathf.Repeat(time,6)/6;
            Vector3 atWater=new Vector3(-.12f,-.02f,0),atFace=new Vector3(.24f,.04f,0);
            cloth.localPosition=t<.5f?atWater:Vector3.Lerp(atWater,atFace,Mathf.SmoothStep(0,1,(t-.5f)*2));
            cloth.localRotation=Quaternion.Euler(90,0,Mathf.Sin(time)*8);
            tapLever.localRotation=Quaternion.Euler(0,0,-30); water.gameObject.SetActive(true);
        }
        if(smokeGroup.activeSelf) healthFill.localScale=new Vector3(Mathf.Lerp(1,.65f,wave),1,1);
    }
}
