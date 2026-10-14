using System;
using UnityEngine;

namespace Dashed
{
    public partial class DemoDirector
    {
        Font chinese;
        Texture2D white,paper;
        GUIStyle titleStyle,bodyStyle,smallStyle,buttonStyle,largeButton,inkTitle,paperBody,smallInk;
        Color ink=new Color(.16f,.19f,.17f),cream=new Color(.94f,.91f,.82f),accent=new Color(.72f,.42f,.19f);
        float uiScale;
        bool uiReady;
        string ChapterTitle { get { return chapter==5&&page.nave?"圣家堂中殿":Titles[chapter]; } }
        void InitUI()
        {
            if(uiReady)return;uiReady=true;
            chinese=Resources.Load<Font>("Fonts/Chinese");
            if(chinese==null)chinese=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},22);
            white=Texture2D.whiteTexture;
            paper=new Texture2D(128,128);Color[] pixels=new Color[128*128];var rng=new System.Random(1984);
            for(int i=0;i<pixels.Length;i++){float n=(float)rng.NextDouble()*.035f;pixels[i]=new Color(.95f-n,.925f-n,.85f-n);}
            paper.SetPixels(pixels);paper.Apply();paper.wrapMode=TextureWrapMode.Repeat;
            titleStyle=Style(30,cream);titleStyle.fontStyle=FontStyle.Bold;
            bodyStyle=Style(18,cream);bodyStyle.wordWrap=true;
            smallStyle=Style(14,new Color(.75f,.76f,.69f));smallStyle.wordWrap=true;
            inkTitle=Style(27,ink);inkTitle.fontStyle=FontStyle.Bold;
            paperBody=Style(17,ink);paperBody.wordWrap=true;
            smallInk=Style(14,new Color(.38f,.38f,.31f));smallInk.wordWrap=true;
            buttonStyle=Style(17,cream);buttonStyle.alignment=TextAnchor.MiddleLeft;buttonStyle.padding=new RectOffset(16,12,4,4);
            buttonStyle.normal.background=white;buttonStyle.hover.background=white;buttonStyle.active.background=white;
            largeButton=new GUIStyle(buttonStyle);largeButton.fontSize=19;
        }
        GUIStyle Style(int size,Color color)
        {
            var s=new GUIStyle(GUI.skin.label);s.font=chinese;s.fontSize=size;s.normal.textColor=color;s.richText=false;return s;
        }
        void Fill(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,white);GUI.color=old;}
        void Label(Rect r,string text,GUIStyle s){GUI.Label(r,text,s);}
        bool Button(Rect r,string text,bool enabled=true,bool primary=false)
        {
            bool previous=GUI.enabled;GUI.enabled=enabled;
            Color old=GUI.backgroundColor;GUI.backgroundColor=primary?accent:new Color(.25f,.30f,.26f);
            bool pressed=GUI.Button(r,text,primary?largeButton:buttonStyle);
            GUI.backgroundColor=old;GUI.enabled=previous;return pressed;
        }
        void OnGUI()
        {
            InitUI();uiScale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*uiScale)/2,(Screen.height-900*uiScale)/2,0),Quaternion.identity,Vector3.one*uiScale);
            if(notebook){BookGUI();return;}
            Fill(new Rect(0,0,1600,82),new Color(.10f,.15f,.13f,.95f));
            Label(new Rect(30,16,125,48),"虚  线",titleStyle);
            Label(new Rect(158,19,255,26),"把别人没画完的画完",smallStyle);
            Label(new Rect(158,44,300,24),Years[chapter],smallStyle);
            for(int i=1;i<=5;i++)
            {
                Rect r=new Rect(500+(i-1)*144,21,132,40);
                string shortName=new[]{"","比萨","帕特农","万神殿","科隆","圣家堂"}[i];
                if(Button(r,i.ToString("00")+"  "+shortName,i<=data.unlocked,chapter==i))Navigate(i);
            }
            if(Button(new Rect(1240,21,150,40),"B  速写本"))notebook=true;
            if(Button(new Rect(1404,21,164,40),"H  操作说明"))help=!help;
            if(raised)FramingGUI();
            SidebarGUI();
            Fill(new Rect(0,845,1600,55),new Color(.10f,.15f,.13f,.92f));
            Label(new Rect(30,855,1160,32),"WASD 行走   ·   按住右键转头   ·   B / Tab 翻开本子   ·   F 举页   ·   左键落笔   ·   E 互动 / 推门",smallStyle);
            Label(new Rect(1280,855,290,32),"F5 收好本子    F9 继续",smallStyle);
            if(messageTime>0)
            {
                Fill(new Rect(110,740,1010,84),new Color(.13f,.18f,.15f,.91f));
                Fill(new Rect(110,740,4,84),accent);
                Label(new Rect(134,757,966,63),message,bodyStyle);
            }
            if(help)HelpGUI();
        }
        void SidebarGUI()
        {
            Rect panel=new Rect(1225,99,350,730);Fill(panel,new Color(.12f,.18f,.15f,.96f));
            Label(new Rect(1250,124,298,43),ChapterTitle,titleStyle);
            Label(new Rect(1250,176,298,96),Notes[chapter],bodyStyle);
            Fill(new Rect(1250,288,298,1),new Color(.38f,.43f,.36f));
            Label(new Rect(1250,306,298,80),StatusText(),smallStyle);
            float y=397;ActionsGUI(1250,ref y,298);
            if(y<722)
            {
                if(Button(new Rect(1250,y+8,298,42),"回到取景点  ×"))SnapView();
                if(Button(new Rect(1250,y+58,298,42),chapter==5?(page.nave?"抬头看看中殿":"看看模型室"):chapter==4?"登上 56 m 塔墩":chapter==1?"登钟室 / 敲钟":"走近看看"))world.Inspect("detail");
            }
            if(data.finished)
            {
                Fill(new Rect(1250,650,298,108),new Color(.25f,.32f,.26f));
                Label(new Rect(1265,663,268,50),"这本也是。",titleStyle);
                if(Button(new Rect(1265,718,268,36),"重新翻开这本子"))NewGame();
            }
        }
        string StatusText()
        {
            if(chapter==0)return !data.book?"柜子里有一本她让你扔掉的速写本。":!page.placed?"第一页只有一条书架虚线。":!data.pencil||!data.crayonPiece?"搁板上落下了她的笔和半张蜡笔画。":"HB 铅笔与蜡笔画已收好。门框笔解锁。";
            if(chapter==1)return page.closed?"钟室倾斜 0.1° · 七口钟挂正\n复原：秦禾 · 新石浅一度":"原有三层倾斜 5.5°\n上方四层和钟室还停在虚线。";
            if(chapter==2)return "复原柱 "+page.columns+" / 8   ·   旧柱鼓 "+page.drums+" / 16\n"+(page.closed?"全部原物归位，八根柱子接回来了。":page.columns>=5?"四个堵路跨已通。还有旧石等你用。":"柱距约 4.3 m，节奏要从两端读。一次最多五笔。");
            if(chapter==3)return !page.placed?"穹顶封闭 · 脚下有浅积水\n43.3 m 是她留给你的比例尺。":"眼洞 "+new[]{6.4f,7.8f,9.6f}[page.candidate].ToString("F1")+" m   ·   排水孔 "+page.drains+" / 4\n"+(page.closed?(data.pages[4].joined?"两半页已接上，可以画完整门框。":"今天晴了。下一页只有左半，另一半在封底。"):"积水 "+(page.water*100).ToString("F0")+" cm · 太阳 "+SunText());
            if(chapter==4)return !page.joined?"她只画了左半。另一半在封底口袋里。":"镂空 "+(page.openness*100).ToString("F0")+"% · "+(page.hoisted?"花饰残片已归位":"三条参考线处的镜像偏差均在 2 m 内")+"\n"+(page.closed?"南塔 157 m · "+(page.candidate==0?"盖住蜡笔":"两个人画的")+"":"北塔已完成，南塔还停在 56 m。");
            if(chapter==5&&page.nave)return page.portal?"荣耀立面显影了。推门回工作室。":"最后一页。她什么都没留下，只有一个框。";
            if(chapter==5)
            {
                string h="";for(int i=0;i<3;i++)h+=(i>0?" / ":"")+(page.spans[i]<0?"虚线":SpanHeight(i).ToString("F1")+"m");
                return (page.mirror?"镜子中央已经擦净":"中央还有 1936 年的灰")+"\n三跨："+h;
            }
            return data.bridge?"桥接在窗玻璃上。走开一步，画就错位。":"压图的石头，是桥上掉下来的拱石。";
        }
        string SunText(){return page.sunMinutes==0?"12:00":page.sunMinutes<0?"09:00":"15:00";}
        void ActionsGUI(float x,ref float y,float w)
        {
            switch(chapter)
            {
                case 0:
                    if(!data.book){ActionButton(x,ref y,w,"E  从柜子取出本子",ContextAction,true);return;}
                    if(!page.placed){DrawingActions(x,ref y,w,"横线笔 · 画一块搁板");return;}
                    if(!data.pencil){ActionButton(x,ref y,w,"E  拾起她的 HB 铅笔",ContextAction,true);return;}
                    if(!data.crayonPiece){ActionButton(x,ref y,w,"E  收好半张蜡笔画",ContextAction,true);return;}
                    break;
                case 1:
                    if(!page.closed){DrawingActions(x,ref y,w,"轴线笔 · 往回修一点");return;}break;
                case 2:
                    if(!page.closed)
                    {
                        int needed=page.columns==0?10:16;
                        if(page.drums<needed)ActionButton(x,ref y,w,"E  推这一组旧柱鼓",PushDrums,true);
                        DrawingActions(x,ref y,w,page.columns==0?"柱子笔 · 第一轮五根":"柱子笔 · 再补三根");return;
                    }break;
                case 3:
                    if(!page.placed){DrawingActions(x,ref y,w,"橡皮 · 擦开她封住的顶");return;}
                    if(!page.closed)
                    {
                        if(page.drains<4)ActionButton(x,ref y,w,"排水孔笔 · 画一个小圆",Drain,true);
                        ActionButton(x,ref y,w,"太阳印章 · 拨回正午",()=>SetSun(0));
                        ActionButton(x,ref y,w,"太阳印章 · 看看九点",()=>SetSun(-180));return;
                    }
                    if(!data.pages[4].joined){ActionButton(x,ref y,w,"拼上封底的蜡笔半页",JoinPage,true);return;}
                    break;
                case 4:
                    if(!page.joined){ActionButton(x,ref y,w,"拼上封底的半张蜡笔画",JoinPage,true);return;}
                    if(!page.closed)
                    {
                        if(!page.prepared)ActionButton(x,ref y,w,"轮廓笔 · 照着北塔画",()=>{Prepare();notebook=true;},true);
                        if(page.erases==0)ActionButton(x,ref y,w,"橡皮 · 擦出塔尖窗洞",EraseSpire,true);
                        else ActionButton(x,ref y,w,"B  选择一张硫酸纸",()=>notebook=true);
                        ActionButton(x,ref y,w,"吊索笔 · 用地上的残片",Hoist);
                        FramingActions(x,ref y,w);return;
                    }break;
                case 5:
                    if(page.nave)
                    {
                        if(!page.portal){DrawingActions(x,ref y,w,"终页 · 留下你自己的画");return;}
                        ActionButton(x,ref y,w,"E  推门，回到天亮",Next,true);return;
                    }
                    if(!page.mirror)ActionButton(x,ref y,w,"橡皮 · 擦镜子上的灰",EraseMirror,true);
                    if(Array.Exists(page.spans,v=>v<0))ActionButton(x,ref y,w,"链子笔 · 挂下一跨",()=>{notebook=true;},true);
                    else ActionButton(x,ref y,w,"B  比一比三跨的净高",()=>notebook=true);
                    FramingActions(x,ref y,w);return;
                case 6:
                    if(!page.prepared){ActionButton(x,ref y,w,"E  拿起拱石，描两道楔边",ContextAction,true);return;}
                    if(!data.bridge){DrawingActions(x,ref y,w,"轴线笔 · 把断桥虚线描实");return;}
                    if(!data.finished)ActionButton(x,ref y,w,"E  把描实的线说给她听",Next,true);return;
            }
            if(!page.portal)ActionButton(x,ref y,w,"门框笔 · 画出下一扇门",DrawPortal,true);
            else ActionButton(x,ref y,w,chapter==4?"E  向下推开地面的门":"E  推门，接着下一页",Next,true);
        }
        void ActionButton(float x,ref float y,float w,string text,Action action,bool primary=false)
        {
            if(Button(new Rect(x,y,w,44),text,true,primary))action();y+=52;
        }
        void DrawingActions(float x,ref float y,float w,string brush)
        {
            if(!page.prepared)ActionButton(x,ref y,w,brush,()=>{Prepare();notebook=true;},true);
            else ActionButton(x,ref y,w,"B  看这一次留下的画",()=>notebook=true);
            FramingActions(x,ref y,w);
        }
        void FramingActions(float x,ref float y,float w)
        {
            if(!page.prepared)return;
            if(!raised)ActionButton(x,ref y,w,"F  走到 ×，举起这一页",()=>{SnapView();Raise();},true);
            else ActionButton(x,ref y,w,"左键  落笔",Capture,true);
        }
        void BookGUI()
        {
            raised=false;
            Fill(new Rect(0,0,1600,900),new Color(.06f,.09f,.08f,.84f));
            Fill(new Rect(77,47,1446,806),new Color(.23f,.18f,.12f));
            GUI.DrawTextureWithTexCoords(new Rect(92,60,1409,777),paper,new Rect(0,0,11,6));
            Fill(new Rect(929,62,3,771),new Color(.65f,.57f,.43f));
            Label(new Rect(128,86,610,46),ChapterTitle+(chapter==5&&page.nave?" · 最后一页":""),inkTitle);
            Label(new Rect(130,140,695,30),Years[chapter],smallInk);
            if(Button(new Rect(1280,88,177,42),"B  合起本子"))notebook=false;
            Rect picture=new Rect(128,192,765,430);
            Texture2D art=Resources.Load<Texture2D>("Art/"+ArtKey());
            if(art!=null){Color old=GUI.color;GUI.color=new Color(.94f,.91f,.81f,.92f);GUI.DrawTexture(picture,art,ScaleMode.ScaleToFit);GUI.color=old;}
            else Fill(picture,new Color(.86f,.83f,.75f));
            SketchGUI(picture);
            Label(new Rect(134,646,740,76),chapter==5&&page.nave?"这一面我没看到。你去看。":Notes[chapter],paperBody);
            Label(new Rect(134,752,742,34),chapter==5&&page.nave?"她没有留下答案。你自己留下这一笔。":chapter==6?"楔边延长交在圆心，沿半径把缺的半桥描实。只留下你自己的这一张。":chapter==0?"只有一张。她画得很确定，本子不用猜。":"实线是现存的。虚线，是别人还没有画完的。",smallInk);
            Label(new Rect(970,158,460,52),chapter==5&&page.nave?"全部笔刷都在。":BrushTitle(),inkTitle);
            Label(new Rect(970,215,470,74),StatusText(),smallInk);
            if(chapter==5&&!page.nave)SagradaBook();
            else
            {
                if(chapter==3)
                {
                    if(Button(new Rect(970,301,224,40),"太阳印章  12:00"))SetSun(0);
                    if(Button(new Rect(1205,301,224,40),"太阳印章  09:00"))SetSun(-180);
                }
                if(chapter==3&&page.closed)
                {
                    Label(new Rect(970,354,460,89),"下一页只剩左半。\n另一半，一直在封底口袋里。",paperBody);
                    if(!data.pages[4].joined)
                    {
                        if(Button(new Rect(970,457,460,48),"取出蜡笔画，拼上下一页",true,true))JoinPage();
                    }
                    else Label(new Rect(970,457,460,84),"撕口接上了。\n门的另一半，将带着你六岁那年的蜡笔。",paperBody);
                }
                else if(chapter==4&&!page.joined)
                {
                    if(Button(new Rect(970,309,460,44),"从封底拿出半张蜡笔画",true,true))JoinPage();
                }
                else if(!page.prepared)
                {
                    if(Button(new Rect(970,355,460,48),chapter==4?"轮廓笔 · 收下这一次的线":chapter==5?"留下一面没人见过的立面":"收笔 · 让虚线成为实线",true,true))Prepare();
                }
                else if(chapter!=0&&chapter!=6&&!(chapter==5&&page.nave))CandidateGUI(970,359,460);
                else
                {
                    Label(new Rect(970,371,460,100),chapter==5?"只留下这一张。\n没有硫酸纸，没有页角的数字。":chapter==6?"这一张已经留下了。\n圆心与半径，是你从拱石找出来的。":"这一张已经留下了。\n她画得很确定，本子不用猜。",paperBody);
                }
                if(chapter==4&&page.prepared&&page.erases==0)
                    if(Button(new Rect(970,672,460,44),"橡皮 · 擦出 52% 的窗洞",true,true))EraseSpire();
                if(page.prepared)
                    if(Button(new Rect(970,735,460,48),"合起本子，走到 × 举页",true,true)){notebook=false;SnapView();Raise();}
            }
            Label(new Rect(960,809,468,25),"HB 铅笔 · 橡皮 · 你留下的这一笔",smallInk);
            if(drawing>0)
            {
                Fill(new Rect(128,602,765,4),new Color(.57f,.52f,.41f));
                Fill(new Rect(128,602,765*(1-drawing/.65f),4),accent);
            }
        }
        string BrushTitle()
        {
            return new[]{"横线笔","轴线笔","柱子笔","橡皮 · 太阳印章","轮廓 · 橡皮 · 吊索","链子笔 · 橡皮","轴线笔"}[chapter];
        }
        string ArtKey()
        {
            if(chapter==0||chapter==6)return "WKS_Page";
            string prefix=new[]{"","PIS","PAR","PAN","COL","SAG"}[chapter];
            if(chapter==5&&page.nave)return "Final_Page";
            return prefix+(page.prepared?"_"+((char)('A'+page.candidate)).ToString():"_Page");
        }
        string CandidateText(int i)
        {
            if(chapter==1)return DemoCandidates.Pisa[i].label;
            if(chapter==2)return new[]{"落点偏0.6 m\n仍能承接原物额枋","落点对齐\n旧柱鼓全部接回柱身","落点偏1.1 m\n额枋节奏之外"}[i];
            if(chapter==3)return new[]{"眼洞 6.4 m\n圆心在穹顶顶点","眼洞 7.8 m\n圆心在穹顶顶点","眼洞 9.6 m\n圆心在穹顶顶点"}[i];
            if(chapter==4)return "蜡笔 "+new[]{"0%","30%","60%"}[i]+"\n"+(i==0?"盖住蜡笔底图":"留下六岁的笔迹");
            if(chapter==5)return "碎片补全 · "+new[]{"更小","原尺度","更长"}[i]+"\n净高 "+SpanCandidateHeight(selectedSpan,i).ToString("F2")+" m";
            return "留下这一张";
        }
        void CandidateGUI(float x,float y,float width)
        {
            float h=88;
            for(int i=0;i<3;i++)
            {
                Rect r=new Rect(x,y+i*(h+10),width,h);
                Fill(r,page.candidate==i?new Color(.8f,.67f,.46f,.8f):new Color(.87f,.83f,.72f,.8f));
                Texture2D art=Resources.Load<Texture2D>("Art/"+new[]{"","PIS","PAR","PAN","COL","SAG"}[chapter]+"_"+((char)('A'+i)));
                if(art!=null)GUI.DrawTexture(new Rect(x+6,r.y+6,117,h-12),art,ScaleMode.ScaleToFit);
                Label(new Rect(x+138,r.y+11,width-178,h-14),CandidateText(i),paperBody);
                Label(new Rect(x+width-33,r.y+27,28,30),page.candidate==i?"●":"○",paperBody);
                Color old=GUI.color;GUI.color=Color.clear;
                if(GUI.Button(r,""))SelectCandidate(i);GUI.color=old;
            }
        }
        void SagradaBook()
        {
            for(int i=0;i<3;i++)
            {
                if(Button(new Rect(970+i*155,309,145,38),"第 "+(i+1)+" 跨",true,selectedSpan==i))selectedSpan=i;
            }
            CandidateGUI(970,363,460);
            if(Button(new Rect(970,674,460,44),"链子笔 · 挂住这一跨的碎片",true,true))HangChain();
            if(Button(new Rect(970,730,224,44),page.mirror?"中央已擦干净":"橡皮 · 继续擦镜",!page.mirror))EraseMirror();
            if(Button(new Rect(1205,730,225,44),"到镜子中央举页",page.prepared,true)){notebook=false;SnapView();Raise();}
        }
        void SketchGUI(Rect p)
        {
            Color pencil=new Color(.2f,.24f,.22f,.7f),red=new Color(.75f,.29f,.14f,.88f);
            if(chapter==1)
            {
                Dash(new Vector2(p.x+p.width*.49f,p.y+p.height*.96f),new Vector2(p.x+p.width*.51f,p.y+p.height*.13f),pencil,1);
                if(page.prepared)
                {
                    float dx=page.candidate==1?-22:page.candidate==0?6:-45;
                    DrawLine(new Vector2(p.x+p.width*.50f,p.y+p.height*.55f),new Vector2(p.x+p.width*.52f+dx,p.y+p.height*.13f),red,2);
                }
            }
            if(chapter==2&&page.prepared)
            {
                int count=page.columns==0?5:3;for(int i=0;i<count;i++){float x=p.x+p.width*(.34f+i*.057f);DrawLine(new Vector2(x,p.y+p.height*.62f),new Vector2(x,p.y+p.height*.40f),red,2);}
            }
            if(chapter==3)
            {
                if(!page.prepared)Fill(new Rect(p.x+4,p.y+4,p.width-8,p.height-8),new Color(.06f,.07f,.07f,.74f));
                else Circle(new Vector2(p.center.x,p.y+57),44,red);
            }
            if(chapter==4&&!page.joined)
            {
                Fill(new Rect(p.center.x,p.y,p.width*.5f,p.height),new Color(.94f,.91f,.82f));
                Dash(new Vector2(p.center.x,p.y),new Vector2(p.center.x,p.yMax),pencil,1);
            }
            if(chapter==5&&!page.nave)
            {
                for(int i=0;i<3;i++)
                {
                    Vector2 a=new Vector2(p.x+185+i*130,p.y+70),b=a+new Vector2(130,0);
                    if(page.spans[i]<0)Dash(a,b,pencil,1);
                    else {Vector2 previous=a;for(int j=1;j<=24;j++){float t=j/24f;Vector2 next=Vector2.Lerp(a,b,t)+new Vector2(0,100*Mathf.Sin(t*Mathf.PI));DrawLine(previous,next,red,2);previous=next;}}
                }
            }
            if(chapter==0)DrawLine(new Vector2(p.x+160,p.center.y),new Vector2(p.xMax-160,p.center.y),page.prepared?red:pencil,2);
            if(chapter==6)
            {
                Vector2 center=p.center+new Vector2(0,45);const float radius=154;
                Dash(center+new Vector2(-radius-25,0),center+new Vector2(radius+25,0),pencil,1);
                for(int half=0;half<2;half++)
                {
                    Vector2 previous=center+new Vector2(half==0?-radius:0,half==0?0:-radius);
                    for(int i=1;i<=32;i++)
                    {
                        float a=Mathf.Lerp(half==0?Mathf.PI:Mathf.PI*1.5f,half==0?Mathf.PI*1.5f:Mathf.PI*2,i/32f);
                        Vector2 next=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                        if(half==0||page.prepared)DrawLine(previous,next,half==0?pencil:red,2);else Dash(previous,next,pencil,1);
                        previous=next;
                    }
                }
                foreach(float a in new[]{-1.73f,-1.41f})
                {
                    Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    DrawLine(center,center+direction*radius,red,1.4f);
                    DrawLine(center+direction*(radius-23),center+direction*(radius+10),pencil,3);
                }
                Circle(center,3,red);
                Label(new Rect(p.x+25,p.yMax-39,p.width-50,30),"两道楔边 → 圆心 → 半径 → 你的半桥",smallInk);
            }
        }
        void DrawLine(Vector2 a,Vector2 b,Color color,float width)
        {
            Matrix4x4 matrix=GUI.matrix;Color old=GUI.color;GUI.color=color;Vector2 d=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y-width/2,d.magnitude,width),white);
            GUI.matrix=matrix;GUI.color=old;
        }
        void Dash(Vector2 a,Vector2 b,Color color,float width)
        {
            float length=Vector2.Distance(a,b);for(float f=0;f<length;f+=14)DrawLine(Vector2.Lerp(a,b,f/length),Vector2.Lerp(a,b,Mathf.Min(length,f+7)/length),color,width);
        }
        void Circle(Vector2 center,float radius,Color c)
        {
            Vector2 previous=center+Vector2.right*radius;for(int i=1;i<=64;i++){float a=i*Mathf.PI*2/64;Vector2 next=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;DrawLine(previous,next,c,1.4f);previous=next;}
        }
        void FramingGUI()
        {
            Color c=nearView&&alignment<3?new Color(.79f,.64f,.35f):new Color(.87f,.38f,.23f);
            Rect frame=new Rect(145,126,970,546);
            const float k=45;
            DrawLine(new Vector2(frame.x,frame.y),new Vector2(frame.x+k,frame.y),c,2);DrawLine(new Vector2(frame.x,frame.y),new Vector2(frame.x,frame.y+k),c,2);
            DrawLine(new Vector2(frame.xMax,frame.y),new Vector2(frame.xMax-k,frame.y),c,2);DrawLine(new Vector2(frame.xMax,frame.y),new Vector2(frame.xMax,frame.y+k),c,2);
            DrawLine(new Vector2(frame.x,frame.yMax),new Vector2(frame.x+k,frame.yMax),c,2);DrawLine(new Vector2(frame.x,frame.yMax),new Vector2(frame.x,frame.yMax-k),c,2);
            DrawLine(new Vector2(frame.xMax,frame.yMax),new Vector2(frame.xMax-k,frame.yMax),c,2);DrawLine(new Vector2(frame.xMax,frame.yMax),new Vector2(frame.xMax,frame.yMax-k),c,2);
            Fill(new Rect(418,688,430,42),new Color(.11f,.16f,.13f,.92f));
            Label(new Rect(438,698,402,27),"取景误差 "+alignment.ToString("F2")+"°   ·   "+(nearView&&alignment<3?"重合了，左键落笔":"还没有重合"),smallStyle);
        }
        void HelpGUI()
        {
            Fill(new Rect(310,164,890,560),new Color(.10f,.16f,.13f,.98f));
            Label(new Rect(348,199,800,45),"接着她的线画下去",titleStyle);
            Label(new Rect(348,266,795,290),"WASD 行走，Shift 快走，按住右键转头。\nB 或 Tab 打开速写本。选本页的笔，收下一次的画，再留一张硫酸纸。\nF 举页，站在铜钉 × 上；误差小于 3°，左键落笔。\nE 拾笔、推柱鼓、看批注，或推开已经画出的门。\n排水孔、链子和擦镜收笔就生效。其他笔要举页落笔。\n右侧的“回到取景点”可以直接走回她当年站的位置。\nF5 收好本子，F9 翻回上次留下的一页。",bodyStyle);
            if(Button(new Rect(348,601,340,48),"合起说明，继续",true,true))help=false;
            if(Button(new Rect(716,601,340,48),"重新翻开这本子")){help=false;NewGame();}
        }
    }
}
