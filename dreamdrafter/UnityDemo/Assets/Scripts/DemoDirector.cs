using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dashed
{
    [Serializable] public class PageState
    {
        public int step, candidate = 1, columns, drums, drains, sunMinutes, erases;
        public bool prepared, placed, closed, portal, hoisted, joined, mirror, nave, finalDrawn;
        public int[] spans = { -1, -1, -1 };
        public int[] fragmentOwners = { -1, -1, -1, -1, -1 };
        public float[] columnOffsets = new float[8];
        public float water = .12f;
        public float openness;
    }
    [Serializable] public class DemoSave
    {
        public int chapter, unlocked;
        public bool book, pencil, crayonPiece, crayon, bridge, finished;
        public PageState[] pages = {new PageState(),new PageState(),new PageState(),new PageState(),new PageState(),new PageState(),new PageState()};
    }

    public partial class DemoDirector : MonoBehaviour
    {
        static DemoDirector live;
        public static readonly string[] SceneNames={"00_Workshop","01_Pisa","02_Parthenon","03_Pantheon","04_Cologne","05_Sagrada","06_WorkshopDawn"};
        public static readonly float[] PantheonDiameters={6.4f,7.8f,9.6f};
        public int startChapter;
        public DemoSave data = new DemoSave();
        public PageState page { get { return data.pages[data.chapter]; } }
        public int chapter { get { return data.chapter; } }
        public bool notebook, raised, help, verifying;
        public string message = "这本子里的房子，都不是一双手盖完的。";
        public float messageTime, drawing;
        public int selectedSpan;
        public Camera viewCamera;
        public DemoWorld world;
        public Vector3 viewEye, viewAim;
        public float viewFov;
        public bool nearView { get { return Vector3.Distance(viewCamera.transform.position, viewEye) < 2f; } }
        public float alignment { get { return Vector3.Angle(viewCamera.transform.forward,(viewAim-viewEye).normalized); } }
        string SavePath { get { return Path.Combine(Application.persistentDataPath,"dashed-demo-save.json"); } }
        public static readonly string[] Titles = {"工作室","比萨斜塔","帕特农北柱廊","万神殿","科隆大教堂","圣家堂模型室","天亮了"};
        public static readonly string[] Years = {"1984 · 搬进来那天","1986 · 歪了不要紧","1991 · 用地上的","2004.04.21 · 下雨了","2006.08 · 禾禾六岁","2025 · 我的客户不急","测绘 孟昭 1986 · 复原 秦禾"};
        public static readonly string[] Notes = {
            "里面那本速写本，扔了。画不完的东西留着添堵。",
            "我画错了。往回修一点。",
            "缺的那几根，用地上的。新补的，要让人看得出来是新的。柱距是量过的。",
            "下雨了，我把顶上关了。等天晴的人来擦。",
            "另一半在你那儿。",
            "他说他的客户不急。——我的也不急。",
            "描实的线压得深。"
        };

        void Awake()
        {
            if(live!=null&&live!=this){Destroy(gameObject);return;}
            live=this;DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded+=OnSceneLoaded;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            verifying = Array.Exists(Environment.GetCommandLineArgs(),s=>s=="--verify-demo");
            data.chapter = Mathf.Clamp(startChapter,0,6);
            data.unlocked = data.chapter;
            if (data.chapter > 0) {data.book=data.pencil=data.crayonPiece=true;}
            viewCamera = new GameObject("PlayerCamera").AddComponent<Camera>();
            viewCamera.gameObject.AddComponent<AudioListener>();
            viewCamera.nearClipPlane = .08f;
            viewCamera.farClipPlane = 1500;
            viewCamera.backgroundColor = new Color(.89f,.88f,.84f);
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            world = gameObject.AddComponent<DemoWorld>();
            world.director = this;
            BuildChapter();
            if (verifying) StartCoroutine(VerifyWalkthrough());
        }
        void OnSceneLoaded(Scene scene,LoadSceneMode mode){BuildChapter();}
        void OnDestroy(){if(live==this){SceneManager.sceneLoaded-=OnSceneLoaded;live=null;}}

        void Update()
        {
            messageTime -= Time.deltaTime;
            drawing = Mathf.Max(0,drawing-Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab)) notebook=!notebook;
            if (Input.GetKeyDown(KeyCode.F)) Raise();
            if (Input.GetKeyDown(KeyCode.E)) ContextAction();
            if (Input.GetKeyDown(KeyCode.H)) help=!help;
            if (Input.GetKeyDown(KeyCode.Escape)) {notebook=false;raised=false;help=false;}
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
            if (raised && Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width*.73f) Capture();
            if (!notebook && !help && Input.GetMouseButton(1))
            {
                float yaw=Input.GetAxis("Mouse X")*2f,pitch=-Input.GetAxis("Mouse Y")*2f;
                viewCamera.transform.Rotate(Vector3.up,yaw,Space.World);
                viewCamera.transform.Rotate(Vector3.right,pitch,Space.Self);
                Vector3 e=viewCamera.transform.eulerAngles; e.z=0; viewCamera.transform.eulerAngles=e;
            }
            if (!notebook && !raised && !help)
            {
                float speed = chapter==4 ? 12 : chapter==1 || chapter==2 ? 7 : 3;
                if(Input.GetKey(KeyCode.LeftShift)) speed*=2;
                Vector3 front=Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized;
                Vector3 move=(front*Input.GetAxisRaw("Vertical")+viewCamera.transform.right*Input.GetAxisRaw("Horizontal"))*speed*Time.deltaTime;
                world.MovePlayer(move);
            }
            if(chapter==3 && page.placed && page.water>0)
            {
                // The demo exposes four prescribed circles. Fewer than four keep up with rain,
                // rather than letting a hidden timer dry the room after the first drain.
                if(page.drains>=4) page.water=Mathf.Max(0,page.water-.04f*Time.deltaTime);
                world.UpdateWater(page.water);
                if(page.water==0) {world.ClearWeather();CheckDry();}
            }
            viewCamera.rect=new Rect(0,0,1,1);
        }

        public void Say(string text)
        {
            message=text; messageTime=9f;
            Debug.Log("[Dashed] "+text);
        }
        public void BuildChapter()
        {
            raised=false;notebook=false;
            string desired=SceneNames[data.chapter];
            if(SceneManager.GetActiveScene().name!=desired&&Application.CanStreamedLevelBeLoaded(desired))
            {SceneManager.LoadScene(desired);return;}
            var editorPreview=GameObject.Find("EditorStagePreview");
            if(editorPreview!=null){editorPreview.SetActive(false);Destroy(editorPreview);}
            world.Build(chapter,page);
            SnapView();
            Say(Notes[chapter]);
        }
        public void SnapView()
        {
            world.SetPlayerPose(viewEye,Quaternion.LookRotation(viewAim-viewEye));
            viewCamera.fieldOfView=viewFov;
        }
        public void Raise()
        {
            if(!page.prepared && !(chapter==5 && page.nave && page.finalDrawn) && !(chapter==6&&page.prepared))
            {Say("先在速写本上留下一笔。按 B 打开本子。");return;}
            notebook=false;raised=!raised;
            if(raised)Say("站在 × 上，让她的线与眼前重合。左键落笔。");
        }
        public void Prepare()
        {
            if(chapter==0&&!data.book){Say("先从柜子里取出速写本。");return;}
            if(chapter==2&&page.drums<(page.columns==0?10:16)){Say("缺的那几根，用地上的。先推这一组柱鼓。");return;}
            if(chapter==4&&!page.joined){Say("另一半在封底口袋里。先把两半拼起来。");return;}
            page.prepared=true;drawing=.65f;page.step++;
            Say(chapter==3?"擦口露出来了。选一张硫酸纸，再到门槛落笔。":chapter==4?"三条参考线对上了。塔尖先是实心，得用橡皮擦窗洞。":"收笔。她替你猜了三种，选中的那张才会站起来。");
            if(chapter==0||chapter==6)Say("她画得很确定，本子不用猜。只给一张。");
            if(chapter==5&&page.nave){page.finalDrawn=true;Say("这一面我没看到。你去看。只有一张，没有人说对不对。");}
            SaveQuiet();
        }
        public void SelectCandidate(int index)
        {
            page.candidate=Mathf.Clamp(index,0,2);world.PreviewCandidate();
            Say("留下第 "+(page.candidate+1)+" 张硫酸纸。");SaveQuiet();
        }
        public void Capture()
        {
            if(!raised){Raise();return;}
            if(!nearView){Say("留白只从她当年站的位置对得上。走到铜钉 ×。");return;}
            ValidationResult align=Rules.CheckAlignment(alignment);
            if(!align.success){Say(align.message);return;}
            if(!page.prepared){Say("先收笔并留下这次的画。");return;}
            ValidationResult result;
            switch(chapter)
            {
                case 0:
                    page.placed=true; page.closed=true;world.PlaceShelf();
                    Say("图钉掉了。她的 HB 铅笔和半张蜡笔画落在搁板上。");break;
                case 1:
                    PisaCandidate pisaPaper=DemoCandidates.Pisa[page.candidate];
                    result=Rules.CheckPisa(pisaPaper.k,pisaPaper.delta,pisaPaper.includeBelfry);
                    if(!result.success){Fail(result);return;}
                    page.placed=page.closed=true;world.CompletePisa();world.BellSound();
                    Say("钟响了。它歪着站了八百年，我第一次看见。");break;
                case 2:
                    result=Rules.CheckColumn(page.candidate==2?1.1f:page.candidate==0?.6f:0f,0);
                    if(!result.success){Fail(result);return;}
                    if(page.columnOffsets==null||page.columnOffsets.Length!=8)page.columnOffsets=new float[8];
                    int nextColumns=page.columns==0?5:8;
                    for(int slot=page.columns;slot<nextColumns;slot++)page.columnOffsets[slot]=page.candidate==0?.6f:0f;
                    page.columns=page.columns==0?5:8;page.placed=true;page.prepared=false;
                    world.RestoreColumns(page.columns,page.drums);
                    if(page.columns==8&&page.drums==16){page.closed=true;Say("八根柱子接回来了，地上的旧石全用上了。额枋归位，通道打开。");}
                    else Say("第一轮五根柱子立起来了。通道已经打开，还有三根虚线和旧柱鼓等你接着画。");break;
                case 3:
                    float apertureDiameter=PantheonDiameters[page.candidate];
                    result=Rules.CheckPantheonAperture(apertureDiameter,0);
                    if(!result.success){Fail(result);return;}
                    page.placed=true;world.OpenOculus(apertureDiameter);
                    Say("雨从眼洞落下来了。本子还没合。地面线上的小圆，是排水孔。");break;
                case 4:
                    result=Rules.CheckCologne(new[]{1f,1.4f,1.8f},page.openness,page.hoisted);
                    if(!result.success){Fail(result);return;}
                    page.placed=page.closed=true;world.CompleteCologne(page.candidate);
                    Say(page.candidate==0?"两半接上了。新的比她的浅。":"南塔站起来了。这一页，两个人画的。");break;
                case 5:
                    if(page.nave)
                    {
                        if(!page.finalDrawn){Say("终页是空白的，先留下你自己的画。");return;}
                        page.closed=page.portal=true;world.ShowFinalFacade();
                        Say("没有人见过的那一面，开了一扇门。门外天亮了。");break;
                    }
                    float[] heights=new float[3];int[] fragments=SpanFragmentCounts();
                    for(int i=0;i<3;i++){if(page.spans[i]<0){Say("还有一跨没挂上。碎片是他留下的，别绕开它。");raised=false;return;}heights[i]=SpanHeight(i);}
                    result=Rules.CheckSagrada(heights,fragments,page.mirror);
                    if(!result.success){Fail(result);return;}
                    page.placed=page.closed=page.nave=true;page.prepared=false;page.closed=false;
                    world.EnterNave();SnapView();
                    Say("拱顶就在头上了。翻到最后一页：这一面我没看到。你去看。");break;
                case 6:
                    data.bridge=true;page.closed=true;world.ShowBridge();
                    Say("桥在玻璃上接上了。测绘：孟昭。复原：秦禾。");break;
            }
            raised=false;notebook=false;world.ApplyTone(alignment<1?1:2);
            SaveQuiet();
        }
        void Fail(ValidationResult r)
        {
            Say(r.message);world.FailureLines(r.failureIndex);raised=false;SaveQuiet();
        }
        public void PushDrums()
        {
            if(chapter!=2)return;
            page.drums=page.columns==0?10:16;world.PushDrums(page.drums);
            Say("旧柱鼓滚到了柱脚。深色的旧石，等你的竖线接住它们。");SaveQuiet();
        }
        public void Drain()
        {
            if(!page.placed){Say("先擦眼洞并落笔，让雨进来。");return;}
            page.drains=Mathf.Min(4,page.drains+1);world.AddDrain(page.drains);
            Say("排水孔 "+page.drains+" / 4 · 收笔就生效，不用举页。");SaveQuiet();
        }
        public void SetSun(int minutes)
        {
            page.sunMinutes=minutes;world.SetSun(minutes);
            if(page.water<=0)CheckDry();else Say(minutes==0?"印章回到正午。先等地面排干。":"光会落到藻井上，门槛还在暗处。");
            SaveQuiet();
        }
        void CheckDry()
        {
            if(page.closed)return;
            if(!page.placed||page.drains<4||page.water>0)return;
            ValidationResult dryResult=Rules.CheckPantheon(PantheonDiameters[page.candidate],0,page.sunMinutes);
            if(dryResult.success)
            {page.closed=true;world.ClearWeather();Say("今天晴了。光斑落在门槛的 × 上。");SaveQuiet();}
            else if(page.drains>=4)Say("水退了，光却不在门上。把太阳印章拨回正午。");
        }
        public void JoinPage()
        {
            if(chapter==3)
            {
                if(!page.closed){Say("先让水退、光照到门槛，再翻到留下撕口的下一页。");return;}
                if(!data.crayonPiece){Say("另一半在封底口袋里，先收好工作室落下的蜡笔画。");return;}
                data.pages[4].joined=true;
            }
            else if(chapter==4)page.joined=true;
            else return;
            Say("下一页的撕口严丝合缝。背面：2006 年 8 月。禾禾六岁，她说南塔没盖完，她来画。");SaveQuiet();
        }
        public void EraseSpire()
        {
            page.openness=.52f;page.erases++;world.PreviewSpire(.52f);
            Say("橡皮擦出窗洞。镂空 52%：下面的塔墩才撑得住。");SaveQuiet();
        }
        public void Hoist()
        {
            page.hoisted=true;page.prepared=true;world.HoistFragment();
            Say("残片被吊回塔尖。它是真正的构件，由它定出缺的几何。");SaveQuiet();
        }
        public void EraseMirror()
        {
            page.erases=Mathf.Min(3,page.erases+1);page.mirror=page.erases>=3;
            world.EraseMirror(page.erases);
            Say(page.mirror?"中央擦干净了。倒影里的教堂，还缺你的链子。":"灰退了一些。中央还有看不见的地方，接着擦。");SaveQuiet();
        }
        public float SpanHeight(int index)
        {
            if(page.spans[index]<0)return 0;
            return SpanCandidateHeight(index,page.spans[index]);
        }
        public float SpanCandidateHeight(int span,int candidate)
        {
            return SagradaFixture.Get(span,candidate,page.fragmentOwners).height;
        }
        public int[] SpanFragmentCounts()
        {
            int[] counts=new int[3];
            if(page.fragmentOwners!=null)foreach(int owner in page.fragmentOwners)if(owner>=0&&owner<3)counts[owner]++;
            return counts;
        }
        public void HangChain()
        {
            int i=Mathf.Clamp(selectedSpan,0,2);
            if(page.fragmentOwners==null||page.fragmentOwners.Length!=5)page.fragmentOwners=new[]{-1,-1,-1,-1,-1};
            page.fragmentOwners[i]=i;
            // Fixed demonstration strokes always pass their own fragment. Boundary fragments
            // slide to whichever adjacent span is hung first, and retain that unique identity.
            if(i<=1&&page.fragmentOwners[3]<0)page.fragmentOwners[3]=i;
            if(i>=1&&page.fragmentOwners[4]<0)page.fragmentOwners[4]=i;
            page.spans[i]=page.candidate;page.prepared=true;
            world.HangChain(i,SpanHeight(i));
            Say("第 "+(i+1)+" 跨挂住 "+SpanFragmentCounts()[i]+" 块石膏碎片。净高 "+SpanHeight(i).ToString("F2")+" m · 收笔即时生效。");
            selectedSpan=Mathf.Min(2,i+1);SaveQuiet();
        }
        public void DrawPortal()
        {
            if(!page.closed){Say("这一页还没合上。先把留下的那件事做完。");return;}
            if(chapter==0&&(!data.pencil||!data.crayonPiece)){Say("先把搁板上的铅笔和半张蜡笔画收进本子。");return;}
            if(chapter==3&&!data.pages[4].joined){Say("下一页只有左半。先把封底的蜡笔画拼上，才能画完整的门框。");return;}
            page.portal=true;world.ShowPortal();
            Say(chapter==4?"青铜门倒嵌在地里。向下推开。":"门框从页上消失了。门就在眼前，按 E 进去。");SaveQuiet();
        }
        public void Next()
        {
            if(chapter==6){if(data.bridge){data.finished=true;Say("“比萨那页，你往回修了没有？” “修了。钟响了。”\n这本也是。");SaveQuiet();}return;}
            if(!page.portal){Say("先用门框笔把门画出来。");return;}
            if(chapter==3&&!data.pages[4].joined){Say("先拼上封底的蜡笔半页，再穿过半石半蜡笔的门。");return;}
            data.chapter++;data.unlocked=Mathf.Max(data.unlocked,data.chapter);
            BuildChapter();SaveQuiet();
        }
        public void ContextAction()
        {
            if(page.portal){Next();return;}
            if(chapter==0)
            {
                if(!data.book){data.book=true;notebook=true;Say("扉页：这本子里的房子，都不是一双手盖完的。");}
                else if(page.placed&&!data.pencil){data.pencil=true;Say("拾起她的 HB 铅笔。");}
                else if(page.placed&&!data.crayonPiece){data.crayonPiece=true;Say("小孩画的塔，撕掉了一半。收进封底口袋。");}
            }
            else if(chapter==1&&page.closed){world.BellSound();Say("七口钟都挂正了。");}
            else if(chapter==2)PushDrums();
            else if(chapter==4){data.crayon=true;Say("绞盘座里有一截橘色蜡笔头。六岁的你留下的。");}
            else if(chapter==5)Say("他知道自己看不到盖完，所以留的是模型，不是图。");
            else if(chapter==6){if(!page.prepared){page.prepared=true;notebook=true;Say("这是桥上掉下来的拱石。描两道楔边，延长线交在圆心。");}else Next();}
            SaveQuiet();
        }
        public void Navigate(int target)
        {
            if(target>data.unlocked){Say("先把这一页接着画完。");return;}
            data.chapter=target;BuildChapter();SaveQuiet();
        }
        public void NewGame()
        {
            data=new DemoSave();BuildChapter();SaveQuiet();
        }
        public void SaveQuiet(){if(!verifying)File.WriteAllText(SavePath,JsonUtility.ToJson(data,true));}
        public void Save(){SaveQuiet();Say("本子收好了。下次从这一笔继续。");}
        public void Load()
        {
            try
            {
                if(!File.Exists(SavePath)){Say("还没有收好的本子。");return;}
                DemoSave loaded=JsonUtility.FromJson<DemoSave>(File.ReadAllText(SavePath));
                if(loaded==null||loaded.pages==null||loaded.pages.Length!=7)throw new Exception("存档格式不匹配");
                if(loaded.chapter<0||loaded.chapter>=SceneNames.Length)throw new Exception("存档场景无效");
                foreach(PageState savedPage in loaded.pages)
                {
                    if(savedPage==null||savedPage.spans==null||savedPage.spans.Length!=3)throw new Exception("存档页缺失");
                    savedPage.candidate=Mathf.Clamp(savedPage.candidate,0,2);
                    if(savedPage.columnOffsets==null||savedPage.columnOffsets.Length!=8)savedPage.columnOffsets=new float[8];
                    if(savedPage.fragmentOwners==null||savedPage.fragmentOwners.Length!=5)
                    {
                        savedPage.fragmentOwners=new[]{-1,-1,-1,-1,-1};
                        for(int span=0;span<3;span++)if(savedPage.spans[span]>=0)savedPage.fragmentOwners[span]=span;
                        if(savedPage.spans[0]>=0)savedPage.fragmentOwners[3]=0;else if(savedPage.spans[1]>=0)savedPage.fragmentOwners[3]=1;
                        if(savedPage.spans[1]>=0)savedPage.fragmentOwners[4]=1;else if(savedPage.spans[2]>=0)savedPage.fragmentOwners[4]=2;
                    }
                }
                data=loaded;BuildChapter();Say("翻回上一次留下的那一页。");
            }
            catch(Exception e){Say("这份本子没能打开："+e.Message);}
        }
    }
}
