using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PhotoRealmGame : MonoBehaviour
{
    [Serializable] public class Site
    {
        public string name,hint;
        public Vector3 anchor,lookTarget,pickup;
        public GameObject objectRoot,paper;
        public bool taken,placed;
    }
    public Font chineseFont;
    [NonSerialized] public Site[] sites;
    [NonSerialized] public GameObject closedWall,drainStream;
    [NonSerialized] public Transform doorPivot,waterSurface;
    [NonSerialized] public Transform[] windFlags,windStreaks;
    [NonSerialized] public Transform[] lampDust;
    [NonSerialized] public Vector3[] lampDustRest;
    [NonSerialized] public Material waterMaterial;
    [NonSerialized] public Vector3 goal;
    public CharacterController player {get;private set;}
    public Camera view {get;private set;}
    public int stage {get;private set;}
    public bool completed {get;private set;}
    public bool aimed {get;private set;}
    public bool busy {get;private set;}
    public bool doorOpen {get;private set;}
    public bool drained {get;private set;}
    public bool draining {get;private set;}
    public bool verifying {get;private set;}
    public int falls {get;private set;}
    public float waterY {get;private set;}=5.72f;
    public float walkSpeed=3.25f;
    public string EvidenceDirectory {get;private set;}
    public string status="找到露台上的桥照片，通往远处的亮光。";
    float yaw,pitch,vertical,doorAngle,drainTime,messageUntil,reveal;
    Vector3 checkpoint;float checkpointYaw;
    Quaternion aimRotation;
    RenderTexture photograph;
    Camera photoCamera;
    AudioSource windSound,mechanism;
    GUIStyle text,small,headline;
    bool paused=true,stylesReady;
    Vector2 qaMovement;
    string message;
    public Vector3 Position => player.transform.position;

    void Start()
    {
        Application.targetFrameRate=60;
        QualitySettings.vSyncCount=1;
        verifying=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--verify-photo-realm");
        EvidenceDirectory=Path.Combine(Application.dataPath,"../../../artifacts/photo-realm-qa");
        if(Application.isEditor) EvidenceDirectory=Path.Combine(Application.dataPath,"../artifacts/photo-realm-qa");
        EvidenceDirectory=Path.GetFullPath(EvidenceDirectory);Directory.CreateDirectory(EvidenceDirectory);
        PhotoRealmWorld.Build(this);
        DynamicGI.UpdateEnvironment();
        var body=new GameObject("First person controller — no character mesh");
        body.transform.position=new Vector3(0,.05f,-2);
        player=body.AddComponent<CharacterController>();player.height=1.75f;player.radius=.25f;player.center=new Vector3(0,.9f,0);
        player.stepOffset=.23f;player.slopeLimit=48;player.skinWidth=.025f;
        var cam=new GameObject("First person view");cam.tag="MainCamera";cam.transform.SetParent(body.transform,false);cam.transform.localPosition=Vector3.up*1.62f;
        view=cam.AddComponent<Camera>();view.nearClipPlane=.06f;view.farClipPlane=350;view.fieldOfView=65;
        view.clearFlags=CameraClearFlags.Skybox;view.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        cam.AddComponent<AudioListener>();
        var photo=new GameObject("Photograph capture camera");photoCamera=photo.AddComponent<Camera>();photoCamera.enabled=false;
        photoCamera.CopyFrom(view);photoCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;photoCamera.enabled=false;
        checkpoint=body.transform.position;checkpointYaw=20;SetLook(20,0);
        CreateSound();
        Physics.SyncTransforms();
        if(verifying) {paused=false;gameObject.AddComponent<PhotoRealmVerification>().game=this;}
        else {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }

    void Update()
    {
        if(player==null)return;
        AnimateEnvironment();
        if(!verifying)
        {
            if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused;}
            if(paused)return;
            if(Input.GetKeyDown(KeyCode.R)) Respawn();
            if(!busy)
            {
                yaw+=Input.GetAxisRaw("Mouse X")*1.6f;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.6f,-75,75);ApplyLook();
                if(Input.GetKeyDown(KeyCode.E)) Interact();
                if(Input.GetKeyDown(KeyCode.F)) {if(aimed) CancelAim();else TryAim();}
                if(aimed&&Input.GetMouseButtonDown(0)) TryPlace();
            }
        }
        if(!aimed&&!busy)
        {
            Vector2 input=verifying?qaMovement:new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            StepMovement(input,Time.deltaTime);
        }
        if(Position.y < -6 || (Position.x>21&&Position.x<43&&Position.z>31.3f&&Position.z<49&&Position.y<waterY-.32f&&!drained)) Respawn();
        if(sites[3].placed&&Position.y>5.8f&&Position.x>3&&Position.x<8&&Position.z<29.5f) SetCheckpoint(new Vector3(5.7f,6.05f,27.3f),90);
        if(Position.x>24.5f&&Position.y>5.7f&&Position.z<31.3f) SetCheckpoint(new Vector3(27,6.05f,27),0);
        if(drained&&!completed&&Vector3.Distance(Position,goal)<2.6f)
        {completed=true;status="抵达。回望你亲手连接的空间。";Toast("照片里的路，已经成为来时的路。",8);Chime(740);Log("COMPLETE");}
    }

    public void SetLook(float y,float p){yaw=y;pitch=p;ApplyLook();}
    public void LookAt(Vector3 p)
    {Vector3 d=(p-view.transform.position).normalized;yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;ApplyLook();}
    void ApplyLook(){player.transform.rotation=Quaternion.Euler(0,yaw,0);view.transform.localRotation=Quaternion.Euler(pitch,0,0);}
    public void SetVerificationMovement(Vector2 v){qaMovement=v;}
    public void StepMovement(Vector2 input,float dt)
    {
        input=Vector2.ClampMagnitude(input,1);
        Vector3 motion=player.transform.right*input.x+player.transform.forward*input.y;
        Vector3 wind=WindAt(Position);
        vertical=player.isGrounded&&vertical<0?-2:Mathf.Max(vertical-20*dt,-24);
        player.Move((motion*walkSpeed+wind+Vector3.up*vertical)*dt);
    }
    public Vector3 WindAt(Vector3 p)
    {
        if(p.x>8.3f&&p.x<23.7f&&p.y>5.4f&&p.y<9.5f&&p.z>24&&p.z<31)
        {
            bool sheltered=sites[4].placed&&p.z>26&&p.z<28.9f;
            float amount=sheltered?.06f:1.3f+.3f*Mathf.Sin(Time.time*2.3f);
            return Vector3.back*amount;
        }
        return Vector3.zero;
    }
    public bool Interact()
    {
        if(busy||aimed)return false;
        if(stage==1&&sites[1].placed&&!doorOpen&&HorizontalDistance(Position,new Vector3(0,0,21))<3)
        {doorOpen=true;stage=2;status="进入暗室，寻找桌面上的射灯照片。";SetCheckpoint(new Vector3(0,.05f,24.5f),0);Chime(220);Log("DOOR_OPEN");return true;}
        if(stage==5&&sites[5].placed&&!draining&&!drained&&HorizontalDistance(Position,new Vector3(29.5f,6,30.5f))<4)
        {draining=true;drainTime=0;drainStream.SetActive(true);status="水位正在下降，等待庭院步道显露。";Chime(160);Log("PUMP_STARTED");return true;}
        if(stage<6&&!sites[stage].taken&&HorizontalDistance(Position,sites[stage].pickup)<2.6f&&Mathf.Abs(Position.y-sites[stage].anchor.y)<1.2f)
        {
            sites[stage].taken=true;sites[stage].paper.SetActive(false);Toast("已拾取「"+sites[stage].name+"」照片。前往取景位置，按 F 举起。",4);Chime(520);Log("PICKUP "+stage);return true;
        }
        return false;
    }
    public bool TryAim()
    {
        if(stage>=6||busy||aimed||!sites[stage].taken||sites[stage].placed)return false;
        Site s=sites[stage];
        if(HorizontalDistance(Position,s.anchor)>2.4f||Mathf.Abs(Position.y-s.anchor.y)>.5f){Toast("靠近照片对应的取景位置。",2);return false;}
        StartCoroutine(EnterPhoto(s));return true;
    }
    IEnumerator EnterPhoto(Site s)
    {
        busy=true;Vector3 from=Position;Quaternion start=view.transform.rotation;
        aimRotation=Quaternion.LookRotation(s.lookTarget-(s.anchor+Vector3.up*1.62f));
        for(float t=0;t<1;t+=Time.deltaTime*2.5f)
        {
            float eased=Mathf.SmoothStep(0,1,t);Vector3 next=Vector3.Lerp(from,s.anchor+Vector3.up*.03f,eased);
            player.Move(next-Position);view.transform.rotation=Quaternion.Slerp(start,aimRotation,eased);yield return null;
        }
        player.Move(s.anchor+Vector3.up*.03f-Position);LookAt(s.lookTarget);
        photoCamera.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
        photoCamera.fieldOfView=view.fieldOfView;photoCamera.aspect=view.aspect;
        if(photograph){photograph.Release();Destroy(photograph);}
        photograph=new RenderTexture(Mathf.Max(640,Screen.width),Mathf.Max(400,Screen.height),24,RenderTextureFormat.ARGB32);
        photograph.Create();photoCamera.targetTexture=photograph;
        bool wall=closedWall.activeSelf;s.objectRoot.SetActive(true);if(stage==1)closedWall.SetActive(false);
        Physics.SyncTransforms();photoCamera.Render();
        s.objectRoot.SetActive(false);closedWall.SetActive(wall);photoCamera.targetTexture=null;Physics.SyncTransforms();
        aimed=true;busy=false;Log("AIM "+stage);
    }
    public void CancelAim(){if(busy)return;aimed=false;Log("CANCEL");}
    public bool IsAligned => aimed&&Quaternion.Angle(view.transform.rotation,aimRotation)<3;
    public bool TryPlace()
    {
        if(stage>=6||!aimed||busy||!IsAligned)return false;
        var s=sites[stage];if(s.placed||!s.taken)return false;
        StartCoroutine(PlacePhoto(s,stage));return true;
    }
    IEnumerator PlacePhoto(Site s,int index)
    {
        busy=true;s.objectRoot.SetActive(true);s.placed=true;if(index==1)closedWall.SetActive(false);
        Physics.SyncTransforms();Chime(430+index*50);reveal=1;
        for(float t=0;t<1;t+=Time.deltaTime*1.5f){reveal=1-t;yield return null;}
        reveal=0;aimed=false;busy=false;
        switch(index)
        {
            case 0:stage=1;status="走过桥，寻找塔楼入口的门照片。";break;
            case 1:status="门已经成真。靠近后按 E 开门。";break;
            case 2:stage=3;status="光照出了楼梯井。找到螺旋楼梯照片。";SetCheckpoint(new Vector3(-3,.05f,27),25);break;
            case 3:stage=4;status="走上螺旋楼梯，前往塔顶。";SetCheckpoint(new Vector3(0,.05f,29.4f),0);break;
            case 4:stage=5;status="沿挡风屏背后的连廊走向岛屿。";break;
            case 5:status="排水设备已连接。靠近泵，按 E 启动。";break;
        }
        Log("PLACE "+index);
    }
    public void Respawn()
    {
        if(busy)return;
        aimed=false;falls++;player.enabled=false;player.transform.position=checkpoint;player.enabled=true;vertical=0;SetLook(checkpointYaw,0);
        Toast("已回到安全位置，环境改造仍然保留。",3);Log("RESPAWN "+falls);
    }
    void SetCheckpoint(Vector3 p,float y){checkpoint=p;checkpointYaw=y;}
    public void VerificationTeleport(Vector3 p) {player.enabled=false;player.transform.position=p;player.enabled=true;vertical=0;Physics.SyncTransforms();}
    void AnimateEnvironment()
    {
        if(doorOpen&&doorAngle<105){doorAngle=Mathf.MoveTowards(doorAngle,105,Time.deltaTime*75);doorPivot.localRotation=Quaternion.Euler(0,-doorAngle,0);}
        if(draining&&!drained)
        {
            drainTime+=Time.deltaTime;waterY=Mathf.Lerp(5.72f,2.58f,Mathf.SmoothStep(0,1,Mathf.Clamp01(drainTime/9)));
            Vector3 p=waterSurface.position;p.y=waterY;waterSurface.position=p;
            if(drainTime>=9){drained=true;draining=false;stage=6;drainStream.SetActive(false);status="沿露出的石阶穿过水庭，抵达亮着灯的终点亭。";Log("DRAINED");}
        }
        if(waterMaterial&&waterMaterial.HasProperty("_BaseMap"))waterMaterial.mainTextureOffset=new Vector2(Time.time*.016f,Time.time*.008f);
        if(windFlags!=null)for(int i=0;i<windFlags.Length;i++)windFlags[i].localRotation=Quaternion.Euler(Mathf.Sin(Time.time*7+i)*6,Mathf.Sin(Time.time*5+i)*8,0);
        if(lampDust!=null)for(int i=0;i<lampDust.Length;i++)lampDust[i].position=lampDustRest[i]+new Vector3(Mathf.Sin(Time.time*.5f+i)*.1f,Mathf.Sin(Time.time*.36f+i*.4f)*.12f,Mathf.Cos(Time.time*.4f+i)*.08f);
        if(windStreaks!=null)for(int i=0;i<windStreaks.Length;i++)
        {var p=windStreaks[i].position;p.z-=Time.deltaTime*(5+i%4);if(p.z<23)p.z=34;windStreaks[i].position=p;}
        if(windSound&&player)windSound.volume=Mathf.Lerp(windSound.volume,Position.x>8&&Position.x<24?.22f:.045f,Time.deltaTime*2);
    }
    public static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    void Toast(string s,float seconds){message=s;messageUntil=Time.time+seconds;}
    public void Log(string s){Debug.Log("PHOTO_REALM "+s+" @ "+Position.ToString("F2"));}

    void CreateSound()
    {
        windSound=gameObject.AddComponent<AudioSource>();windSound.loop=true;windSound.spatialBlend=0;
        int count=44100*3;var data=new float[count];var rng=new System.Random(43);float filtered=0;
        for(int i=0;i<count;i++){filtered=filtered*.975f+(float)(rng.NextDouble()*2-1)*.025f;data[i]=filtered*.6f;}
        var clip=AudioClip.Create("Wind ambience",count,1,44100,false);clip.SetData(data,0);windSound.clip=clip;windSound.volume=.07f;windSound.Play();
        mechanism=gameObject.AddComponent<AudioSource>();
    }
    void Chime(float hz)
    {
        int count=11025;float[] data=new float[count];for(int i=0;i<count;i++){float t=i/44100f;data[i]=Mathf.Sin(t*hz*Mathf.PI*2)*Mathf.Exp(-t*18)*.12f;}
        var c=AudioClip.Create("Photo feedback",count,1,44100,false);c.SetData(data,0);mechanism.PlayOneShot(c);
    }
    void Styles()
    {
        if(stylesReady)return;stylesReady=true;
        text=new GUIStyle(GUI.skin.label){font=chineseFont,fontSize=18,wordWrap=true};text.normal.textColor=new Color(.94f,.93f,.88f);
        small=new GUIStyle(text){fontSize=14};small.normal.textColor=new Color(.77f,.8f,.8f);
        headline=new GUIStyle(text){fontSize=30,fontStyle=FontStyle.Normal};
    }
    void OnGUI()
    {
        if(player==null)return;Styles();float w=Screen.width,h=Screen.height;
        if(paused&&!verifying)
        {
            GUI.color=new Color(.04f,.06f,.075f,.94f);GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(w*.19f,h*.24f,w*.65f,60),"照片成境",headline);
            GUI.Label(new Rect(w*.19f,h*.36f,w*.62f,90),"让照片中的建筑成为现实。\n穿过暗室、螺旋楼梯与风廊，抵达远处的亮光。",text);
            GUI.Label(new Rect(w*.19f,h*.51f,w*.65f,80),"WASD / 方向键  行走     鼠标  观察\nE  拾取 / 操作     F  举起 / 收起照片     左键  确认\nR  返回安全点     Esc  暂停",small);
            if(GUI.Button(new Rect(w*.19f,h*.68f,200,45),"进入 / 继续",text)){paused=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            return;
        }
        GUI.color=new Color(.025f,.04f,.05f,.68f);GUI.DrawTexture(new Rect(20,18,410,83),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(36,25,380,28),completed?"照片成境  /  抵达":"照片成境  /  "+Mathf.Min(stage+1,6).ToString("00")+" — "+(stage<6?sites[stage].name:"水庭"),text);
        GUI.Label(new Rect(36,58,380,38),status,small);
        if(aimed&&photograph)
        {
            Rect r=new Rect(w*.20f,h*.19f,w*.60f,h*.60f);
            GUI.color=new Color(.95f,.93f,.86f,busy?reveal:1);GUI.DrawTexture(new Rect(r.x-12,r.y-12,r.width+24,r.height+48),Texture2D.whiteTexture);
            GUI.color=new Color(1,1,1,busy?reveal:1);GUI.DrawTextureWithTexCoords(r,photograph,new Rect(.2f,.21f,.6f,.6f));
            GUI.color=Color.white;
            GUI.Label(new Rect(r.x,r.y+r.height+7,r.width,26),sites[Mathf.Min(stage,5)].name+"   /   "+(IsAligned?"已对齐 · 左键让照片成真":"调整视角，与空间对齐"),new GUIStyle(small){normal={textColor=new Color(.15f,.19f,.20f)}});
        }
        else
        {
            GUI.color=new Color(1,1,1,.7f);GUI.DrawTexture(new Rect(w*.5f-2,h*.5f-2,4,4),Texture2D.whiteTexture);GUI.color=Color.white;
            string prompt="";
            if(stage<6)
            {
                Site s=sites[stage];
                if(!s.placed)
                {
                    Vector3 target=s.taken?s.anchor+Vector3.up*.08f:s.pickup;
                    Vector3 screen=view.WorldToScreenPoint(target);
                    if(screen.z>0&&Vector3.Distance(Position,target)<18&&Mathf.Abs(Position.y-s.anchor.y)<1.3f)
                    {
                        GUI.color=new Color(.95f,.78f,.42f,.9f);GUI.DrawTexture(new Rect(screen.x-4,h-screen.y-4,8,8),Texture2D.whiteTexture);GUI.color=Color.white;
                        GUI.Label(new Rect(screen.x+13,h-screen.y-10,170,28),s.taken?"取景位置":"照片 · "+s.name,small);
                    }
                }
                if(!s.taken&&HorizontalDistance(Position,s.pickup)<2.6f&&Mathf.Abs(Position.y-s.anchor.y)<1.2f) prompt="E  拾取「"+s.name+"」照片";
                else if(s.taken&&!s.placed&&HorizontalDistance(Position,s.anchor)<2.4f&&Mathf.Abs(Position.y-s.anchor.y)<.5f)prompt="F  举起「"+s.name+"」照片";
                else if(stage==1&&s.placed&&!doorOpen)prompt="靠近入口 · E 开门";
                else if(stage==5&&s.placed&&!draining&&!drained)prompt="靠近水泵 · E 启动排水";
                else if(s.taken&&!s.placed)prompt="寻找取景位置 · "+s.hint;
            }
            if(prompt!="") PanelLabel(new Rect(w*.23f,h-100,w*.54f,38),prompt);
        }
        if(Time.time<messageUntil)PanelLabel(new Rect(w*.19f,h-150,w*.62f,38),message);
        GUI.Label(new Rect(24,h-32,w-48,28),"WASD 行走    E 操作    F 照片    左键 确认    R 安全点    Esc 暂停",small);
        if(completed)PanelLabel(new Rect(w*.34f,h*.38f,w*.32f,55),"抵达 / 空间已重新连接");
    }
    void PanelLabel(Rect r,string s){GUI.color=new Color(.025f,.04f,.05f,.76f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(r.x+12,r.y+6,r.width-24,r.height-8),s,text);}
}
