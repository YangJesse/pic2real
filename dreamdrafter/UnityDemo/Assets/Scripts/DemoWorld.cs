using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dashed
{
    public class DemoWorld : MonoBehaviour
    {
        public DemoDirector director;
        GameObject root, initial, complete, props, liveNave, mirrorRig, finalFacade, portal, shelf, bridge, oldBridge, collisionRoot;
        CharacterController player;
        Camera mirrorCamera;
        RenderTexture mirrorTexture;
        Texture2D dustMask;
        Material mirrorMaterial, lineMaterial;
        Transform water;
        Vector3 waterBase;
        readonly List<GameObject> drains=new List<GameObject>();
        readonly List<LineRenderer> chainLines=new List<LineRenderer>();
        readonly Dictionary<MeshFilter,Mesh> apertureOriginalMeshes=new Dictionary<MeshFilter,Mesh>();
        readonly Dictionary<Transform,Vector3> authoredPositions=new Dictionary<Transform,Vector3>();
        readonly Dictionary<Renderer,Material[]> cologneAuthoredMaterials=new Dictionary<Renderer,Material[]>();
        readonly Dictionary<Material,Material> cologneStoneMaterials=new Dictionary<Material,Material>();
        readonly List<string> missing=new List<string>();
        public int modelCount;
        public IList<string> MissingModels { get { return missing; } }
        public bool HasBridgeGeometry { get { return bridge!=null&&oldBridge!=null&&bridge.activeInHierarchy&&oldBridge.activeInHierarchy; } }
        public float CologneCrayonMix { get; private set; }
        PageState State {get{return director.page;}}
        public static Vector3 B(float x,float y,float z){return new Vector3(x,z,y);}
        public static Vector3 B(Vector3 v){return B(v.x,v.y,v.z);}

        public GameObject Model(string key,Transform parent=null)
        {
            var prefab=Resources.Load<GameObject>("Models/"+key);
            if(prefab==null){missing.Add(key);Debug.LogError("Missing model: "+key);return new GameObject("MISSING_"+key);}
            var go=Instantiate(prefab,parent==null?root.transform:parent);go.name=key;modelCount++;
            return go;
        }
        Material Stone(Color color)
        {
            Material m=new Material(Shader.Find("Standard"));m.color=color;m.SetFloat("_Glossiness",.08f);return m;
        }
        GameObject Box(string name,Vector3 center,Vector3 scale,Color color,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent==null?root.transform:parent);
            go.transform.position=center;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=Stone(color);return go;
        }
        public void Build(int chapter,PageState state)
        {
            if(root!=null){root.SetActive(false);Destroy(root);}
            if(mirrorTexture!=null){mirrorTexture.Release();Destroy(mirrorTexture);mirrorTexture=null;}
            if(dustMask!=null)Destroy(dustMask);
            foreach(var material in cologneStoneMaterials.Values)Destroy(material);
            cologneStoneMaterials.Clear();cologneAuthoredMaterials.Clear();CologneCrayonMix=0;
            missing.Clear();modelCount=0;drains.Clear();chainLines.Clear();apertureOriginalMeshes.Clear();authoredPositions.Clear();water=null;mirrorRig=null;liveNave=null;portal=null;bridge=null;oldBridge=null;shelf=null;initial=null;complete=null;finalFacade=null;collisionRoot=null;mirrorCamera=null;
            root=new GameObject("Chapter_"+chapter+"_"+DemoDirector.Titles[chapter]);
            root.transform.SetParent(director.transform);
            props=new GameObject("RuntimeInteractions");props.transform.SetParent(root.transform);
            lineMaterial=new Material(Shader.Find("Sprites/Default"));
            RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.58f,.56f,.51f);
            var keyLight=new GameObject("PencilWorldLight").AddComponent<Light>();keyLight.transform.SetParent(root.transform);
            keyLight.type=LightType.Directional;keyLight.intensity=1.15f;keyLight.color=new Color(1f,.95f,.84f);
            keyLight.transform.rotation=Quaternion.Euler(42,-35,0);keyLight.shadows=LightShadows.Soft;
            if(player==null)
            {
                var playerObject=new GameObject("Player");playerObject.transform.SetParent(director.transform);player=playerObject.AddComponent<CharacterController>();
                player.height=1.8f;player.radius=.3f;player.center=Vector3.up*.9f;player.stepOffset=.3f;player.slopeLimit=55;
                director.viewCamera.transform.SetParent(playerObject.transform);
                director.viewCamera.transform.localPosition=Vector3.up*1.65f;
            }
            switch(chapter)
            {
                case 0: Workshop(false);break;
                case 1:
                    initial=Model("PIS_initial_preview");complete=Model("PIS_scene_preview");complete.SetActive(state.placed);initial.SetActive(!state.placed);
                    SetView(B(-50,0,1.65f),B(0,-2,27),64);Collision("PIS_collision");break;
                case 2:
                    initial=Model("PAR_initial_preview");complete=Model("PAR_scene_preview");
                    SetView(B(0,48,1.65f),B(0,14,6.5f),64);Collision("PAR_collision");RestoreColumns(state.columns,state.drums);PushDrums(state.drums);break;
                case 3:
                    initial=Model("PAN_initial");complete=Model("PAN_complete");complete.SetActive(false);
                    SetView(B(0,19.8f,1.65f),B(0,0,26),80);Collision("PAN_collision_initial");
                    water=Find(initial,"PAN_DYN_Water");if(water!=null)waterBase=water.position;
                    if(state.placed)OpenOculus(DemoDirector.PantheonDiameters[state.candidate]);
                    for(int i=1;i<=state.drains;i++)AddDrain(i);
                    UpdateWater(state.water);SetSun(state.sunMinutes);if(state.placed&&state.water<=0)ClearWeather();
                    break;
                case 4:
                    initial=Model("COL_initial");complete=Model("COL_complete");complete.SetActive(state.placed);initial.SetActive(!state.placed);
                    SetView(B(-150,0,1.65f),B(0,0,74),68);Collision(state.placed?"COL_collision_complete":"COL_collision_initial");
                    if(state.hoisted)HoistFragment();if(state.placed)CompleteCologne(state.candidate);break;
                case 5:
                    initial=Model("SAG_room_initial");Collision("SAG_collision");SwitchSagradaCollision(false);
                    Hide(initial,s=>s.Contains("ChainSegment")||s.Contains("RT_")||s.Contains("Fill_F")||s.Contains("MirrorDust")||s.Contains("STA_Mirror_")||s.Contains("Ghost"));
                    SetView(B(0,0,1.65f),B(-1.2f,0,.03f),70);
                    BuildMirror();EraseMirror(state.erases);
                    for(int i=0;i<3;i++)if(state.spans[i]>=0)HangChain(i,director.SpanHeight(i));
                    if(state.nave)EnterNave();if(state.finalDrawn&&state.portal)ShowFinalFacade();break;
                case 6: Workshop(true);if(director.data.bridge)ShowBridge();break;
            }
            if(chapter!=5)AddFloor(chapter==4?400:chapter==1||chapter==2?160:60);
            if(state.portal&&chapter!=5&&chapter!=6)ShowPortal();
            var viewMarker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);viewMarker.name="Fixed_View_Copper_X";viewMarker.transform.SetParent(props.transform);
            viewMarker.transform.position=new Vector3(director.viewEye.x,director.viewEye.y-1.63f,director.viewEye.z);
            viewMarker.transform.localScale=new Vector3(.7f,.015f,.7f);viewMarker.GetComponent<Renderer>().sharedMaterial=Stone(new Color(.57f,.35f,.15f));Destroy(viewMarker.GetComponent<Collider>());
        }
        void SetView(Vector3 eye,Vector3 aim,float fov){director.viewEye=eye;director.viewAim=aim;director.viewFov=fov;}
        void AddFloor(float size)
        {
            var go=Box("SafetyFloor",new Vector3(0,-.3f,0),new Vector3(size,.3f,size),Color.white);go.GetComponent<Renderer>().enabled=false;
        }
        void Collision(string key)
        {
            var go=Model(key);collisionRoot=go;
            foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                var mc=filter.GetComponent<MeshCollider>();
                if(mc==null)mc=filter.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh=filter.sharedMesh;
            }
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        }
        public void SetPlayerPose(Vector3 eye,Quaternion rotation)
        {
            player.enabled=false;player.transform.position=eye-Vector3.up*1.65f;player.transform.rotation=Quaternion.identity;
            director.viewCamera.transform.localPosition=Vector3.up*1.65f;director.viewCamera.transform.rotation=rotation;player.enabled=true;
        }
        public void MovePlayer(Vector3 move)
        {
            if(player!=null&&player.enabled)player.Move(move+Vector3.down*Time.deltaTime*2);
        }
        Transform Find(GameObject go,string prefix)
        {
            if(go==null)return null;foreach(Transform t in go.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith(prefix))return t;return null;
        }
        void Hide(GameObject go,Func<string,bool> predicate)
        {
            if(go==null)return;foreach(var r in go.GetComponentsInChildren<Renderer>(true))if(predicate(r.name))r.enabled=false;
        }
        void Visible(GameObject go,Func<string,bool> predicate)
        {
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=predicate(r.name);
        }
        void Workshop(bool dawn)
        {
            SetView(B(0,-4.5f,1.65f),B(0,2,1.8f),65);
            initial=new GameObject("GrandmothersWorkshop");initial.transform.SetParent(root.transform);
            Color wall=new Color(.74f,.72f,.66f),wood=new Color(.34f,.25f,.17f);
            Box("Floor",B(0,0,-.12f),new Vector3(10,.2f,12),new Color(.47f,.43f,.36f));
            Box("BackWall",B(0,3,2.2f),new Vector3(10,4.5f,.2f),wall);
            Box("LeftWall",B(-5,0,2.2f),new Vector3(.2f,4.5f,12),wall);
            Box("Desk",B(-2.4f,1.2f,.85f),new Vector3(2.4f,.12f,1.2f),wood);
            for(int i=0;i<4;i++)Box("DeskLeg_"+i,B(-2.4f+(i%2==0?-.95f:.95f),1.2f+(i<2?-.4f:.4f),.4f),new Vector3(.1f,.8f,.1f),wood);
            Box("SketchbookCabinet",B(3,2,1.15f),new Vector3(1.6f,2.3f,.9f),wood);
            Box("BridgeDrawing",B(-2.4f,1.2f,.925f),new Vector3(1.25f,.01f,.8f),new Color(.91f,.87f,.75f));
            Keystone(B(-2.6f,1.2f,1.08f));
            for(int i=0;i<3;i++)
            {
                var paper=Box("SurveyDrawing_"+i,B(-3+i*2.3f,2.87f,2.8f),new Vector3(1.7f,1.1f,.02f),new Color(.87f,.83f,.72f));
                Line("ExistingMeasuredLine",new[]{paper.transform.position+new Vector3(-.6f,-.2f,-.03f),paper.transform.position+new Vector3(.6f,.2f,-.03f)},new Color(.2f,.2f,.18f),.018f);
            }
            for(int i=0;i<14;i++)Line("Shelf_Dashed_"+i,new[]{B(-1.6f+i*.23f,2.82f,1.6f),B(-1.46f+i*.23f,2.82f,1.6f)},new Color(.38f,.37f,.32f),.022f);
            Box("WindowFrameTop",B(4.9f,-2,3.35f),new Vector3(.12f,.12f,2.6f),wood);
            Box("WindowFrameBottom",B(4.9f,-2,.65f),new Vector3(.12f,.12f,2.6f),wood);
            Box("WindowFrameLeft",B(4.9f,-3.25f,2),new Vector3(.12f,2.7f,.12f),wood);
            Box("WindowFrameRight",B(4.9f,-.75f,2),new Vector3(.12f,2.7f,.12f),wood);
            // A single unlit pane keeps both the actual outside bridge and the pencil mark visible.
            // Its shader is a Resources asset, so player builds retain this alpha pass directly.
            var window=GameObject.CreatePrimitive(PrimitiveType.Quad);window.name="WindowGlass";window.transform.SetParent(props.transform);
            window.transform.position=B(4.95f,-2,2);window.transform.rotation=Quaternion.Euler(0,90,0);window.transform.localScale=new Vector3(2.3f,2.5f,1);
            Destroy(window.GetComponent<Collider>());
            Shader glassShader=Resources.Load<Shader>("Shaders/WindowGlass");if(glassShader==null)glassShader=Shader.Find("Sprites/Default");
            Material glass=new Material(glassShader);glass.color=new Color(.66f,.78f,.8f,.08f);glass.renderQueue=2990;
            var glassRenderer=window.GetComponent<Renderer>();glassRenderer.sharedMaterial=glass;glassRenderer.shadowCastingMode=ShadowCastingMode.Off;glassRenderer.receiveShadows=false;
            if(dawn)
            {
                SetView(B(2,-2,1.65f),B(4.95f,-2,2),60);
                RenderSettings.ambientLight=new Color(.63f,.61f,.54f);
                Model("SAG_wks_return_preview").SetActive(false);
                BridgeHalf(false);
            }
            else if(State.placed)PlaceShelf();
        }
        public void PlaceShelf()
        {
            if(shelf!=null)return;
            shelf=Box("GeneratedCedarShelf",B(0,2.55f,1.58f),new Vector3(3.4f,.12f,.8f),new Color(.65f,.47f,.27f));
            var pencil=Box("InheritedHBPencil",B(-.4f,2.45f,1.7f),new Vector3(.02f,.025f,.22f),new Color(.87f,.66f,.2f));
            var piece=Box("HalfCrayonDrawing",B(.55f,2.45f,1.67f),new Vector3(.45f,.015f,.36f),new Color(.98f,.91f,.73f));
            Line("ChildCrayonTower",new[]{piece.transform.position+new Vector3(-.1f,.02f,-.13f),piece.transform.position+new Vector3(.1f,.02f,.1f)},new Color(.96f,.32f,.07f),.014f);
        }
        public void CompletePisa(){initial.SetActive(false);complete.SetActive(true);}
        int ColumnSlot(string name)
        {
            var match=Regex.Match(name,"_N(\\d{2})(?:_|\\.|$)");return match.Success?int.Parse(match.Groups[1].Value):-1;
        }
        int DrumId(string name)
        {
            var match=Regex.Match(name,"(?:Drum_|Stone)(\\d{3})");return match.Success?int.Parse(match.Groups[1].Value):-1;
        }
        public void RestoreColumns(int count,int usedDrums)
        {
            if(complete==null||initial==null)return;
            foreach(var renderer in complete.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.name.StartsWith("PAR_DYN_"))continue;
                int slot=ColumnSlot(renderer.name),drum=DrumId(renderer.name);
                int column=slot>=6&&slot<=13?slot-6:drum>0&&drum<=16?(drum-1)/2:-1;
                if(column<0||State.columnOffsets==null||State.columnOffsets.Length!=8)continue;
                Vector3 authored;if(!authoredPositions.TryGetValue(renderer.transform,out authored)){authored=renderer.transform.localPosition;authoredPositions.Add(renderer.transform,authored);}
                renderer.transform.localPosition=authored+Vector3.right*State.columnOffsets[column];
            }
            Visible(complete,s=>
            {
                if(!s.StartsWith("PAR_DYN_"))return false;
                int slot=ColumnSlot(s),id=DrumId(s);
                if(slot>=6&&slot<=13)return slot<6+count;
                if(id>0)return id<=count*2;
                var span=Regex.Match(s,"Architrave_(\\d{2})_(\\d{2})");
                if(span.Success)return int.Parse(span.Groups[2].Value)<6+count;
                return count==8&&(s.Contains("Frieze")||s.Contains("Reward"));
            });
            foreach(var r in initial.GetComponentsInChildren<Renderer>(true))
            {
                string s=r.name;int slot=ColumnSlot(s),id=DrumId(s);
                if(s.StartsWith("PAR_GHO_Column"))r.enabled=slot>=6+count;
                if(s.StartsWith("PAR_DYN_")&&id>0)r.enabled=id>count*2;
                var span=Regex.Match(s,"(?:DYN|GHO)_Architrave_(\\d{2})_(\\d{2})");
                if(span.Success)
                {
                    int left=int.Parse(span.Groups[1].Value),right=int.Parse(span.Groups[2].Value);
                    r.enabled=right>=6+count;
                    // The collision exchange is the completed layout. Keep the four original
                    // ground beams as blockers until their two support columns exist.
                    if(s.StartsWith("PAR_DYN_")&&left>=6&&left<=9&&right==left+1)
                    {
                        var blocker=r.GetComponent<BoxCollider>();if(blocker==null)blocker=r.gameObject.AddComponent<BoxCollider>();
                        blocker.enabled=r.enabled;
                    }
                }
            }
            if(collisionRoot!=null)foreach(var collider in collisionRoot.GetComponentsInChildren<Collider>(true))
            {
                var blockedSpan=Regex.Match(collider.name,"RouteBlocker_(\\d{2})_(\\d{2})");
                if(blockedSpan.Success)collider.enabled=int.Parse(blockedSpan.Groups[2].Value)>=6+count;
                int oldDrum=DrumId(collider.name);if(oldDrum>0)collider.enabled=oldDrum>count*2;
                int column=ColumnSlot(collider.name);if(column>=6&&column<=13)collider.enabled=column<6+count;
            }
        }
        public void PushDrums(int count)
        {
            if(initial==null||complete==null)return;
            foreach(var r in initial.GetComponentsInChildren<Renderer>(true))
            {
                int id=DrumId(r.name);if(!r.name.StartsWith("PAR_DYN_Drum")||id<1||id>count||id<=State.columns*2)continue;
                Transform destination=Find(complete,"PAR_DYN_Drum_"+id.ToString("D3"));if(destination==null)continue;
                Vector3 p=destination.position;p.y=1.15f;p.z+=2.2f+(id%2)*.65f;
                r.transform.position=p;r.transform.rotation=Quaternion.Euler(0,0,90);
            }
        }
        public void OpenOculus(float diameter)
        {
            Hide(initial,s=>s.Contains("TopPatch_Sealed")||s.Contains("GHO_Top")||s.Contains("TopBlack"));
            complete.SetActive(true);Visible(complete,s=>s.Contains("DYN_TopPatch")||s.Contains("DYN_Ring"));
            // Imported annular modules are the authored 7.8 m opening. Scale the annulus' inner vertices,
            // leaving its outside join fixed, so a 6.4 m result is real geometry rather than a decal.
            foreach(var f in complete.GetComponentsInChildren<MeshFilter>())
            {
                if(!f.name.Contains("TopPatch")&&!f.name.Contains("DYN_Ring"))continue;
                Mesh original;if(!apertureOriginalMeshes.TryGetValue(f,out original)){original=f.sharedMesh;apertureOriginalMeshes.Add(f,original);}
                var mesh=Instantiate(original);Vector3[] points=mesh.vertices;
                for(int i=0;i<points.Length;i++)
                {
                    Vector3 wp=f.transform.TransformPoint(points[i]);float r=new Vector2(wp.x,wp.z).magnitude;
                    if(r>3f&&r<5f){float desired=Mathf.Lerp(diameter*.5f,5f,Mathf.InverseLerp(3.9f,5f,r));if(r>0){wp.x*=desired/r;wp.z*=desired/r;points[i]=f.transform.InverseTransformPoint(wp);}}
                }
                mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();f.sharedMesh=mesh;
            }
            foreach(var existing in props.GetComponentsInChildren<LineRenderer>())if(existing.name=="RainThroughOculus")Destroy(existing.gameObject);
            for(int i=0;i<24;i++)
            {
                float a=i*2.399f,r=diameter*.35f*Mathf.Sqrt((i+1)/24f);
                Line("RainThroughOculus",new[]{new Vector3(Mathf.Cos(a)*r,38+(i%3),Mathf.Sin(a)*r),new Vector3(Mathf.Cos(a)*r,2,Mathf.Sin(a)*r)},new Color(.54f,.62f,.69f,.5f),.016f);
            }
            RenderSettings.ambientLight=new Color(.41f,.43f,.45f);
        }
        public void UpdateWater(float height)
        {
            if(water==null)return;water.position=new Vector3(waterBase.x,height+.018f,waterBase.z);
            var r=water.GetComponent<Renderer>();if(r!=null)r.enabled=height>.002f;
        }
        public void AddDrain(int count)
        {
            if(count<=drains.Count)return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Instant_Drain_"+count;go.transform.SetParent(props.transform);
            go.transform.position=B(-2.5f+count*1.2f,18,.022f);go.transform.localScale=new Vector3(.2f,.005f,.2f);
            go.GetComponent<Renderer>().sharedMaterial=Stone(new Color(.075f,.07f,.06f));Destroy(go.GetComponent<Collider>());drains.Add(go);
        }
        public void SetSun(int minutes)
        {
            foreach(var t in props.GetComponentsInChildren<Transform>())if(t.name=="SunMark")Destroy(t.gameObject);
            var p=GameObject.CreatePrimitive(PrimitiveType.Cylinder);p.name="SunMark";p.transform.SetParent(props.transform);
            p.transform.position=Mathf.Abs(minutes)<=20?B(minutes*.035f,19.4f,.04f):B(-8,1,25);p.transform.localScale=new Vector3(2,.003f,2);
            p.GetComponent<Renderer>().sharedMaterial=Stone(new Color(1,.91f,.58f));Destroy(p.GetComponent<Collider>());
            p.GetComponent<Renderer>().enabled=State.water<=0;
        }
        public void ClearWeather()
        {
            foreach(var l in props.GetComponentsInChildren<LineRenderer>())if(l.name.Contains("Rain"))l.gameObject.SetActive(false);
            foreach(var l in props.GetComponentsInChildren<LineRenderer>())if(l.name=="OculusLight")Destroy(l.gameObject);
            RenderSettings.ambientLight=new Color(.67f,.63f,.53f);UpdateWater(0);SetSun(State.sunMinutes);
            Vector3 target=Mathf.Abs(State.sunMinutes)<=20?B(State.sunMinutes*.035f,19.4f,.2f):B(-8,1,25);
            Line("OculusLight",new[]{B(0,0,43),target},new Color(1,.93f,.7f,.14f),1.5f);
        }
        public void PreviewCandidate()
        {
            if(director.chapter==4&&State.closed&&complete!=null)ApplyCologneSurface(State.candidate);
        }
        public void PreviewSpire(float openness){State.openness=openness;}
        public void CompleteCologne(int candidate)
        {
            initial.SetActive(false);complete.SetActive(true);
            ApplyCologneSurface(candidate);
            // The completed source already places its crane at the authored cleared position.
            // A surface preview must never add another translation to that position.
        }
        void ApplyCologneSurface(int candidate)
        {
            candidate=Mathf.Clamp(candidate,0,2);
            string[] names={"COL_Stone","COL_Crayon30","COL_Crayon60"};
            Texture2D surface=Resources.Load<Texture2D>("Surfaces/"+names[candidate]);
            if(surface==null){Debug.LogError("Missing delivered Cologne surface: "+names[candidate]);return;}
            CologneCrayonMix=candidate*.3f;
            foreach(var r in complete.GetComponentsInChildren<Renderer>(true))
            {
                if(!r.name.Contains("SouthBody")&&!r.name.Contains("SouthTurn")&&!r.name.Contains("SpireSector")&&!r.name.Contains("Finial"))continue;
                Material[] authored;
                if(!cologneAuthoredMaterials.TryGetValue(r,out authored)){authored=r.sharedMaterials;cologneAuthoredMaterials.Add(r,authored);}
                var selected=new Material[authored.Length];
                for(int i=0;i<authored.Length;i++)
                {
                    Material source=authored[i];selected[i]=source;
                    if(source==null||!source.name.StartsWith("M_New_Sandstone"))continue;
                    Material derived;
                    if(!cologneStoneMaterials.TryGetValue(source,out derived))
                    {
                        derived=new Material(source);derived.name="COL_DeliveredCrayonSurface";cologneStoneMaterials.Add(source,derived);
                    }
                    // Each PNG is the original Blender export at exactly 0/30/60 percent mix.
                    // Restore the authored factor on every switch, rather than accumulating tint.
                    derived.color=source.color;derived.mainTexture=surface;selected[i]=derived;
                }
                r.sharedMaterials=selected;
            }
        }
        public void HoistFragment()
        {
            var go=initial!=null&&initial.activeSelf?initial:complete;
            Transform fragment=Find(go,"COL_DYN_Fragment_");
            if(fragment!=null)fragment.position=B(0,-15,145);
            Line("DrawnHoistCable",new[]{B(0,-15,145),B(-4,-13,61)},new Color(.22f,.2f,.15f),.1f);
        }
        void BuildMirror()
        {
            mirrorRig=new GameObject("LiveMirrorNaveRig");mirrorRig.transform.SetParent(root.transform);mirrorRig.transform.position=new Vector3(5000,0,0);
            liveNave=Model("SAG_nave_complete",mirrorRig.transform);SetLayer(liveNave,26);
            RefreshMirrorBays();
            mirrorCamera=new GameObject("LiveMirrorCamera").AddComponent<Camera>();mirrorCamera.transform.SetParent(root.transform);
            mirrorCamera.transform.position=new Vector3(5000,0,0)+B(0,-20,2.2f);mirrorCamera.transform.LookAt(new Vector3(5000,0,0)+B(0,6,27));
            mirrorCamera.fieldOfView=62;mirrorCamera.cullingMask=1<<26;mirrorCamera.clearFlags=CameraClearFlags.SolidColor;mirrorCamera.backgroundColor=new Color(.82f,.81f,.76f);
            mirrorTexture=new RenderTexture(1024,1024,24);mirrorTexture.name="SagradaLiveMirror";mirrorTexture.Create();mirrorCamera.targetTexture=mirrorTexture;
            dustMask=new Texture2D(256,256,TextureFormat.RGBA32,false);dustMask.wrapMode=TextureWrapMode.Clamp;
            Color[] dust=new Color[256*256];for(int i=0;i<dust.Length;i++)dust[i]=Color.white;dustMask.SetPixels(dust);dustMask.Apply();
            var surface=GameObject.CreatePrimitive(PrimitiveType.Quad);surface.name="LiveMirrorWithErasableDust";surface.transform.SetParent(props.transform);
            surface.transform.position=B(0,0,.038f);surface.transform.rotation=Quaternion.Euler(90,0,0);surface.transform.localScale=new Vector3(5.88f,5.88f,1);
            Destroy(surface.GetComponent<Collider>());
            mirrorMaterial=new Material(Shader.Find("Dashed/LiveMirror"));mirrorMaterial.SetTexture("_MainTex",mirrorTexture);mirrorMaterial.SetTexture("_DustMask",dustMask);
            surface.GetComponent<Renderer>().sharedMaterial=mirrorMaterial;
            director.viewCamera.cullingMask&=~(1<<26);
        }
        void SetLayer(GameObject go,int layer){foreach(Transform t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
        public void EraseMirror(int passes)
        {
            if(dustMask==null)return;
            Color[] dust=dustMask.GetPixels();
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                bool clear=passes>=3 ? Mathf.Abs(x-128)<98&&Mathf.Abs(y-128)<98 : passes==2 ? x<110 || y<95 : passes==1&&x<80;
                dust[y*256+x]=clear?Color.clear:Color.white;
            }
            dustMask.SetPixels(dust);dustMask.Apply();
        }
        public void HangChain(int span,float height)
        {
            string bay=((char)('A'+span)).ToString();
            foreach(var l in props.GetComponentsInChildren<LineRenderer>())if(l.name=="SettledChain_"+bay)Destroy(l.gameObject);
            SagradaFixture.Solution solution=SagradaFixture.Get(span,State.spans[span],State.fragmentOwners);
            Vector3[] nodes=solution.nodes;
            chainLines.Add(Line("SettledChain_"+bay,nodes,new Color(.21f,.17f,.1f),.036f));
            foreach(var loadPoint in solution.loadPoints)
            {
                Transform fragment=Find(initial,"SAG_DYN_Fragment_F"+(loadPoint.Key+1));
                if(fragment!=null)fragment.position=loadPoint.Value;
                // The attached original fragment keeps its mesh identity; its old suspension vanishes.
                Hide(initial,n=>n.Contains("Suspension_F"+(loadPoint.Key+1))||n.Contains("FragmentThread_F"+(loadPoint.Key+1)));
            }
            RefreshMirrorBays();
        }
        void RefreshMirrorBays()
        {
            if(liveNave==null)return;
            foreach(var r in liveNave.GetComponentsInChildren<Renderer>(true))
            {
                string n=r.name;int span=n.Contains("Bay_A")?0:n.Contains("Bay_B")?1:n.Contains("Bay_C")?2:-1;
                if(span>=0)
                {
                    r.enabled=State.spans[span]>=0;
                    if(r.enabled)r.transform.localScale=new Vector3(1,director.SpanHeight(span)/44f,1);
                }
                if(n.Contains("JointCap"))r.enabled=State.spans[0]>=0&&State.spans[1]>=0&&State.spans[2]>=0;
            }
        }
        public void EnterNave()
        {
            if(initial!=null)initial.SetActive(false);
            if(mirrorCamera!=null)mirrorCamera.enabled=false;
            if(liveNave!=null){liveNave.transform.SetParent(root.transform,false);SetLayer(liveNave,0);RefreshMirrorBays();}
            SetView(B(0,-14,1.65f),B(0,-22.5f,6.2f),85);
            finalFacade=Model("SAG_final_complete");
            Visible(finalFacade,n=>IsFinalRegion(n)&&!n.Contains("DYN_FinalFacade")&&!n.Contains("DYN_FinalSideSeal")&&!n.Contains("DYN_FinalDoor"));
            Hide(liveNave,n=>n.Contains("FinalDoorFrame"));
            SwitchSagradaCollision(true);
            var east=new GameObject("EastBlueGreenLight").AddComponent<Light>();east.transform.SetParent(root.transform);east.type=LightType.Point;east.transform.position=B(10,5,14);east.color=new Color(.2f,.85f,.88f);east.range=60;east.intensity=5;
            var west=new GameObject("WestRedOrangeLight").AddComponent<Light>();west.transform.SetParent(root.transform);west.type=LightType.Point;west.transform.position=B(-10,5,14);west.color=new Color(1,.34f,.09f);west.range=60;west.intensity=5;
            AddFloor(80);
        }
        public void ShowFinalFacade()
        {
            if(finalFacade==null)finalFacade=Model("SAG_final_complete");
            Visible(finalFacade,IsFinalRegion);
            Hide(liveNave,n=>n.Contains("FinalBaseWall")||n.Contains("FinalDoorCover"));
            Transform left=Find(finalFacade,"SAG_DYN_FinalDoor_001"),right=Find(finalFacade,"SAG_DYN_FinalDoor_002");
            if(left!=null)left.Rotate(Vector3.up,-100);if(right!=null)right.Rotate(Vector3.up,100);
            if(collisionRoot!=null)foreach(var collider in collisionRoot.GetComponentsInChildren<Collider>(true))if(collider.name.Contains("SAG_COLL_FinalDoor"))collider.enabled=false;
        }
        static bool IsFinalRegion(string name)
        {
            return name.Contains("FinalFacade")||name.Contains("FinalSideSeal")||name.Contains("FinalDoor")||name.Contains("FinalBaseWall")||name.Contains("DoorHardware");
        }
        void SwitchSagradaCollision(bool nave)
        {
            if(collisionRoot==null)return;
            foreach(var collider in collisionRoot.GetComponentsInChildren<Collider>(true))
            {
                bool room=collider.name.Contains("Room")||collider.name.Contains("Workbench");
                collider.enabled=nave?!room:room;
            }
        }
        public void ShowPortal()
        {
            if(portal!=null)return;
            if(director.chapter==4){portal=Model("COL_ground_portal");return;}
            if(director.chapter==3)
            {
                Hide(initial,n=>n.Contains("PAN_DYN_PortalWallPlug"));
                if(collisionRoot!=null){collisionRoot.SetActive(false);Destroy(collisionRoot);}
                Collision("PAN_collision");portal=Model("PAN_portal_halves");return;
            }
            if(director.chapter==2){portal=Model("PAR_bronze_portal");return;}
            if(director.chapter==1)
            {
                portal=new GameObject("DrawnDoricPortal");portal.transform.SetParent(props.transform);
                Box("NewDoricLintel",B(-6,-4,3.3f),new Vector3(3.4f,.45f,.6f),new Color(.95f,.93f,.84f),portal.transform);
                Box("NewDoricLeft",B(-7.4f,-4,1.5f),new Vector3(.4f,3,.6f),new Color(.95f,.93f,.84f),portal.transform);
                Box("NewDoricRight",B(-4.6f,-4,1.5f),new Vector3(.4f,3,.6f),new Color(.95f,.93f,.84f),portal.transform);return;
            }
            portal=Box("DrawnMarbleDoor",B(3.6f,2.7f,1.45f),new Vector3(1.7f,2.9f,.08f),new Color(.97f,.95f,.84f));
        }
        void BridgeHalf(bool right)
        {
            Vector3[] p=new Vector3[31];for(int i=0;i<31;i++){float a=Mathf.Lerp(right?0:Mathf.PI/2,right?Mathf.PI/2:Mathf.PI,i/30f);p[i]=new Vector3(4.91f,1.6f+Mathf.Sin(a)*.75f,-2+Mathf.Cos(a)*1.03f);}
            for(int i=0;i<p.Length;i++)p[i]=BridgePoint(p[i],right);
            Color graphite=right?new Color(.14f,.15f,.13f):new Color(.27f,.28f,.25f);
            var line=Line(right?"BridgeOnWindowGlass":"ExistingHalfBridge",p,graphite,right?.029f:.075f);
            line.sharedMaterial.renderQueue=3001;
            if(right)bridge=line.gameObject;else oldBridge=line.gameObject;

            // A second arch edge, a straight deck, and radial joints make the two halves read as
            // a stone bridge. The restored half remains ink on the pane; only the old half is outside.
            Vector3[] outer=new Vector3[31];
            for(int i=0;i<outer.Length;i++)
            {
                float a=Mathf.Lerp(right?0:Mathf.PI/2,right?Mathf.PI/2:Mathf.PI,i/30f);
                outer[i]=BridgePoint(new Vector3(4.91f,1.6f+Mathf.Sin(a)*.9f,-2+Mathf.Cos(a)*1.13f),right);
            }
            Line(right?"BridgeGlassOuterArch":"ExistingBridgeOuterArch",outer,graphite,right?.021f:.055f);
            Vector3[] deck={new Vector3(4.91f,2.58f,right?-2:-3.18f),new Vector3(4.91f,2.58f,right?-.82f:-2)};
            for(int i=0;i<deck.Length;i++)deck[i]=BridgePoint(deck[i],right);
            Line(right?"BridgeGlassDeck":"ExistingBridgeDeck",deck,graphite,right?.027f:.07f);
            for(int i=1;i<7;i++)
            {
                float a=Mathf.Lerp(right?0:Mathf.PI/2,right?Mathf.PI/2:Mathf.PI,i/7f);
                Vector3 inside=BridgePoint(new Vector3(4.91f,1.6f+Mathf.Sin(a)*.75f,-2+Mathf.Cos(a)*1.03f),right);
                Vector3 outside=BridgePoint(new Vector3(4.91f,1.6f+Mathf.Sin(a)*.9f,-2+Mathf.Cos(a)*1.13f),right);
                Line(right?"BridgeGlassStoneJoint":"ExistingBridgeStoneJoint",new[]{inside,outside},graphite,right?.015f:.04f);
            }
        }
        Vector3 BridgePoint(Vector3 panePoint,bool onGlass)
        {
            if(onGlass)return panePoint;
            Vector3 eye=director.viewEye;
            return eye+(panePoint-eye)*((9f-eye.x)/(panePoint.x-eye.x));
        }
        void Keystone(Vector3 center)
        {
            var go=new GameObject("BridgeKeystone");go.transform.SetParent(root.transform);go.transform.position=center;
            Mesh mesh=new Mesh();mesh.name="OriginalWedgeKeystone";
            mesh.vertices=new[]{new Vector3(-.17f,-.15f,-.11f),new Vector3(.17f,-.15f,-.11f),new Vector3(.095f,.15f,-.11f),new Vector3(-.095f,.15f,-.11f),new Vector3(-.17f,-.15f,.11f),new Vector3(.17f,-.15f,.11f),new Vector3(.095f,.15f,.11f),new Vector3(-.095f,.15f,.11f)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            mesh.RecalculateNormals();mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=Stone(new Color(.4f,.41f,.4f));go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        public void ShowBridge(){if(bridge==null)BridgeHalf(true);}
        LineRenderer Line(string name,Vector3[] nodes,Color color,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(props.transform);var l=go.AddComponent<LineRenderer>();
            l.sharedMaterial=lineMaterial;l.useWorldSpace=true;l.positionCount=nodes.Length;l.SetPositions(nodes);l.startWidth=l.endWidth=width;l.startColor=l.endColor=color;l.numCornerVertices=2;return l;
        }
        public void FailureLines(int index)
        {
            Vector3 center=director.viewAim;for(int i=0;i<9;i++){Vector3 a=center+new Vector3((i-4)*.3f,i*.07f,0);Line("PersistentFailedPencilLine",new[]{a,a+new Vector3(.18f,.24f,0)},new Color(.4f,.25f,.2f,.7f),director.chapter==4?.1f:.03f);}
        }
        public void ApplyTone(int levels)
        {
            GameObject go=director.chapter==5?liveNave:complete;
            if(go==null)return;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))if(r.name.Contains("DYN"))foreach(var m in r.materials)m.color=Color.Lerp(m.color,Color.white,levels==1?.06f:.15f);
        }
        public void BellSound()
        {
            const int rate=22050;float[] samples=new float[rate*3];float[] notes={261.63f,293.66f,329.63f,349.23f,392,440,493.88f};
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate,v=0;for(int n=0;n<7;n++){float s=t-n*.12f;if(s>=0)v+=(float)(Math.Sin(s*notes[n]*2*Math.PI)+.35*Math.Sin(s*notes[n]*4.71*Math.PI))*Mathf.Exp(-s*2f)*.08f;}samples[i]=v;}
            AudioClip clip=AudioClip.Create("SevenPisaBells",samples.Length,1,rate,false);clip.SetData(samples,0);
            var source=GetComponent<AudioSource>();if(source==null)source=gameObject.AddComponent<AudioSource>();source.PlayOneShot(clip,.7f);
        }
        public void Inspect(string view)
        {
            if(director.chapter==1&&State.closed)SetPlayerPose(B(0,-3.7f,51),Quaternion.LookRotation(B(3,-3.7f,52)-B(0,-3.7f,51)));
            else if(director.chapter==2)SetPlayerPose(B(-9,18,2.7f),Quaternion.LookRotation(B(4,14,5)-B(-9,18,2.7f)));
            else if(director.chapter==4)SetPlayerPose(B(0,16,57.65f),Quaternion.LookRotation(B(0,-16,92)-B(0,16,57.65f)));
            else if(director.chapter==5&&State.nave)SetPlayerPose(B(0,-20,2.2f),Quaternion.LookRotation(B(0,6,27)-B(0,-20,2.2f)));
            else if(director.chapter==5)SetPlayerPose(B(4.6f,-4.2f,2.4f),Quaternion.LookRotation(B(-1,.25f,3.5f)-B(4.6f,-4.2f,2.4f)));
        }
    }

    /// <summary>
    /// Port of the delivered Scripts/sag_solver.py force integral. The offline demo fixes stroke
    /// endpoints and length; the three papers only change reconstructed-fragment relative weight.
    /// All points are mapped once from Blender Z-up to Unity Y-up. Height is the same solved sag x10.
    /// </summary>
    public static class SagradaFixture
    {
        public sealed class Solution
        {
            public float height;
            public double length;
            public Vector3[] nodes;
            public Dictionary<int,Vector3> loadPoints=new Dictionary<int,Vector3>();
        }
        sealed class Load {public int id; public double at,weight;}
        static readonly double[] Lengths={9.004134030637964,9.251162690935256,9.05366509413674};
        static readonly double[] FixedFractions={.5051107241697901,.5168042625730184,.50562739316955};
        static readonly double[] FixedWeights={.036,.045,.030};
        static readonly double[] Volumes={.7,1,1.3};
        static readonly Dictionary<int,Solution> Cache=new Dictionary<int,Solution>();
        const double Density=.03;

        public static double SpanLength(int span){return Lengths[span];}
        public static Solution Get(int span,int candidate,int[] owners)
        {
            span=Math.Max(0,Math.Min(2,span));candidate=Math.Max(0,Math.Min(2,candidate));
            bool hasOwners=owners!=null&&owners.Length==5;
            bool f4=span<=1&&(!hasOwners||owners[3]<0||owners[3]==span);
            bool f5=span>=1&&(!hasOwners||owners[4]<0||owners[4]==span);
            int key=span*12+candidate*4+(f4?1:0)+(f5?2:0);
            Solution result;if(Cache.TryGetValue(key,out result))return result;
            double length=Lengths[span],volume=Volumes[candidate];
            List<Load> loads=new List<Load>();
            loads.Add(new Load{id=span,at=FixedFractions[span]*length,weight=FixedWeights[span]*volume});
            if(f4)loads.Add(new Load{id=3,at=(span==0?.5925171026177349:.4074828973822651)*length,weight=.025*volume});
            if(f5)loads.Add(new Load{id=4,at=(span==1?.595789916167316:.404210083832684)*length,weight=.020*volume});
            loads.Sort((a,b)=>a.at.CompareTo(b.at));
            result=Solve(span,length,loads);Cache.Add(key,result);return result;
        }

        static double Asinh(double x)
        {
            double positive=Math.Abs(x),value=Math.Log(positive+Math.Sqrt(positive*positive+1));
            return x<0?-value:value;
        }
        static double Hypot(double x,double y){return Math.Sqrt(x*x+y*y);}
        static void Integral(double horizontal,double initialVertical,double to,double length,List<Load> loads,out double x,out double z)
        {
            x=z=0;double s0=0,mass=0;
            for(int i=0;i<=loads.Count;i++)
            {
                double at=i<loads.Count?loads[i].at:length,s1=Math.Min(to,at);
                if(s1>s0)
                {
                    double va=initialVertical+Density*s0+mass,vb=initialVertical+Density*s1+mass;
                    x+=horizontal/Density*(Asinh(vb/horizontal)-Asinh(va/horizontal));
                    z+=(Hypot(horizontal,vb)-Hypot(horizontal,va))/Density;
                }
                if(at>=to)break;
                s0=at;if(i<loads.Count)mass+=loads[i].weight;
            }
        }
        static double Vertical(double horizontal,double total,double length,List<Load> loads)
        {
            double lo=-total*1e4,hi=total*1e4;
            for(int i=0;i<75;i++)
            {
                double v=(lo+hi)/2,x,z;Integral(horizontal,v,length,length,loads,out x,out z);
                if(z<0)lo=v;else hi=v;
            }
            return (lo+hi)/2;
        }
        static Vector3 Point(int span,double horizontal,double vertical,double arc,double length,List<Load> loads)
        {
            double along,z;Integral(horizontal,vertical,arc,length,loads,out along,out z);
            return new Vector3(-1.2f,(float)(6+z),(float)(-2.25+1.5*span+along));
        }
        static Solution Solve(int span,double length,List<Load> loads)
        {
            double total=Density*length;foreach(Load load in loads)total+=load.weight;
            double lo=total*1e-10,hi=total*1e5;
            for(int i=0;i<78;i++)
            {
                double h=Math.Sqrt(lo*hi),v=Vertical(h,total,length,loads),x,z;
                Integral(h,v,length,length,loads,out x,out z);
                if(x<1.5)lo=h;else hi=h;
            }
            double horizontal=Math.Sqrt(lo*hi),vertical=Vertical(horizontal,total,length,loads);
            double mass=0,s0=0,minZ=6;
            for(int i=0;i<=loads.Count;i++)
            {
                double at=i<loads.Count?loads[i].at:length;
                double zero=-(vertical+mass)/Density,x,z;
                if(zero>=s0&&zero<=at){Integral(horizontal,vertical,zero,length,loads,out x,out z);minZ=Math.Min(minZ,6+z);}
                Integral(horizontal,vertical,at,length,loads,out x,out z);minZ=Math.Min(minZ,6+z);
                s0=at;if(i<loads.Count)mass+=loads[i].weight;
            }
            List<double> samples=new List<double>();for(int i=0;i<65;i++)samples.Add(i*length/64);
            foreach(Load load in loads)samples.Add(load.at);samples.Sort();
            Solution result=new Solution{height=(float)(10*(6-minZ)),length=length,nodes=new Vector3[samples.Count]};
            for(int i=0;i<samples.Count;i++)result.nodes[i]=Point(span,horizontal,vertical,samples[i],length,loads);
            foreach(Load load in loads)result.loadPoints[load.id]=Point(span,horizontal,vertical,load.at,length,loads);
            return result;
        }
    }
}
