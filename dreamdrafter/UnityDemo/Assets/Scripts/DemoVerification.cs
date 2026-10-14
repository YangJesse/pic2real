using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dashed
{
    public partial class DemoDirector
    {
        readonly List<string> verification=new List<string>();
        string verificationFolder;
        bool verificationFailed;
        sealed class GeometrySnapshot
        {
            public int meshCount,vertexCount,enabledRenderers,meshHash;
            public long indexCount;
            public Bounds bounds;
        }
        GeometrySnapshot CologneGeometry()
        {
            GameObject tower=GameObject.Find("COL_complete");if(tower==null)return null;
            var result=new GeometrySnapshot{meshHash=17};
            foreach(var filter in tower.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;if(mesh==null)continue;
                result.meshCount++;result.vertexCount+=mesh.vertexCount;
                for(int sub=0;sub<mesh.subMeshCount;sub++)result.indexCount+=mesh.GetIndexCount(sub);
                result.meshHash=unchecked(result.meshHash*31+mesh.GetEntityId().GetHashCode());
            }
            foreach(var renderer in tower.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                if(result.enabledRenderers==0)result.bounds=renderer.bounds;else result.bounds.Encapsulate(renderer.bounds);
                result.enabledRenderers++;
            }
            return result;
        }
        bool SameGeometry(GeometrySnapshot baseline,GeometrySnapshot sample)
        {
            return baseline!=null&&sample!=null&&baseline.meshCount>0&&baseline.enabledRenderers>0&&
                baseline.meshCount==sample.meshCount&&baseline.vertexCount==sample.vertexCount&&
                baseline.indexCount==sample.indexCount&&baseline.meshHash==sample.meshHash&&
                baseline.enabledRenderers==sample.enabledRenderers&&
                Vector3.Distance(baseline.bounds.center,sample.bounds.center)<.001f&&
                Vector3.Distance(baseline.bounds.size,sample.bounds.size)<.001f;
        }
        bool CologneSurfaceMatches(string expected)
        {
            GameObject tower=GameObject.Find("COL_complete");if(tower==null)return false;
            Texture texture=Resources.Load<Texture2D>("Surfaces/"+expected);if(texture==null)return false;
            int matches=0;
            foreach(var renderer in tower.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach(Material material in renderer.sharedMaterials)
                {
                    Texture main=material!=null?material.mainTexture:null;if(main==null)continue;
                    if(main.name!="COL_Stone"&&main.name!="COL_Crayon30"&&main.name!="COL_Crayon60")continue;
                    if(main!=texture)return false;
                    matches++;
                }
            }
            return matches>0;
        }
        void Verify(bool condition,string name)
        {
            verification.Add((condition?"PASS ":"FAIL ")+name);
            if(!condition){verificationFailed=true;Debug.LogError("[DemoVerify] "+name);}else Debug.Log("[DemoVerify] "+name);
        }
        IEnumerator Snapshot(string name,bool book=false)
        {
            notebook=book;
            yield return new WaitForSeconds(.4f);
            yield return new WaitForEndOfFrame();
            Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
            if(frame!=null)
            {
                string path=Path.Combine(verificationFolder,name+".png");
                File.WriteAllBytes(path,frame.EncodeToPNG());
                Verify(File.Exists(path)&&new FileInfo(path).Length>1000,"rendered screenshot "+name);
                Color32[] pixels=frame.GetPixels32();
                var colors=new HashSet<int>();long sumRGB=0;int samples=0,nonBlack=0;
                int stride=Math.Max(1,pixels.Length/4096);
                for(int pixel=0;pixel<pixels.Length;pixel+=stride)
                {
                    Color32 c=pixels[pixel];int rgb=c.r+c.g+c.b;
                    sumRGB+=rgb;samples++;if(rgb>30)nonBlack++;
                    colors.Add((c.r<<16)|(c.g<<8)|c.b);
                }
                float litFraction=samples>0?nonBlack/(float)samples:0;
                double meanRGB=samples>0?sumRGB/(double)samples:0;
                Verify(colors.Count>=20&&litFraction>.10f&&meanRGB>30,
                    "screenshot pixels "+name+": "+colors.Count+" colors, "+(litFraction*100).ToString("F1")+"% nonblack, RGB mean "+meanRGB.ToString("F1"));
                Destroy(frame);
            }
            else Verify(false,"screenshot capture "+name);
            yield return new WaitForSeconds(.35f);
            notebook=false;
        }
        IEnumerator VerifyWalkthrough()
        {
            string[] args=Environment.GetCommandLineArgs();
            int idx=Array.IndexOf(args,"--verification-output");
            verificationFolder=idx>=0&&idx+1<args.Length?args[idx+1]:Path.Combine(Application.persistentDataPath,"Verification");
            Directory.CreateDirectory(verificationFolder);
            Application.logMessageReceived+=VerificationLog;
            yield return new WaitForSeconds(1);
            NewGame();yield return new WaitForSeconds(.6f);
            Verify(chapter==0,"starts in workshop");
            ContextAction();Prepare();SnapView();Raise();Capture();
            Verify(page.placed&&page.closed,"shelf placed only after framing");
            ContextAction();ContextAction();Verify(data.pencil&&data.crayonPiece,"inherited pencil and crayon half collected");
            yield return Snapshot("00_Workshop");
            DrawPortal();Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[1],"loads Pisa scene");
            Verify(world.MissingModels.Count==0,"Pisa authored models loaded");
            yield return Snapshot("01_Pisa_Initial");
            Prepare();SelectCandidate(0);SnapView();Raise();Capture();
            Verify(!page.closed,"unlevel Pisa paper rejected");
            SelectCandidate(1);SnapView();Raise();Capture();
            Verify(page.closed,"Pisa good paper completes curved tower");
            yield return Snapshot("01_Pisa_Complete");
            notebook=true;yield return Snapshot("01_Pisa_Notebook",true);
            DrawPortal();Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[2],"loads Parthenon scene");
            Verify(world.MissingModels.Count==0,"Parthenon authored models loaded");
            Prepare();Verify(!page.prepared,"drawing waits for old drums");
            PushDrums();Prepare();SelectCandidate(1);SnapView();Raise();Capture();
            Verify(page.columns==5&&!page.closed,"first Parthenon round max five columns");
            yield return Snapshot("02_Parthenon_FiveColumns");
            PushDrums();Prepare();SnapView();Raise();Capture();
            Verify(page.columns==8&&page.drums==16&&page.closed,"second round uses all old drums and restores eight columns");
            yield return Snapshot("02_Parthenon_Complete");
            DrawPortal();Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[3],"loads Pantheon scene");
            Verify(world.MissingModels.Count==0,"Pantheon authored models loaded");
            Prepare();SelectCandidate(2);SnapView();Raise();Capture();Verify(!page.placed,"oversized aperture rejected");
            SelectCandidate(1);SnapView();Raise();Capture();Verify(page.placed&&!page.closed,"opening oculus starts rain without closing page");
            yield return Snapshot("03_Pantheon_Rain");
            SetSun(-180);Drain();Drain();Drain();
            yield return new WaitForSeconds(1.2f);Verify(page.water>0&&!page.closed,"three drains cannot defeat incoming rain");
            Drain();yield return new WaitForSeconds(4.2f);
            Verify(page.water<=0&&!page.closed,"drained water with wrong sun still does not close page");
            SetSun(0);Verify(page.closed,"noon light on threshold completes Pantheon");
            yield return Snapshot("03_Pantheon_Complete");
            DrawPortal();Verify(!page.portal&&!data.pages[4].joined,"Pantheon exit waits for next-page crayon half");
            JoinPage();Verify(data.pages[4].joined,"crayon half joins next page while still in Pantheon");
            DrawPortal();Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[4],"loads Cologne scene");
            Verify(world.MissingModels.Count==0,"Cologne authored models loaded");
            Verify(page.joined,"Cologne is entered with the completed joined page");
            page.joined=false;Prepare();Verify(!page.prepared,"Cologne still protects an unjoined page");page.joined=true;
            Prepare();page.openness=.44f;SnapView();Raise();Capture();Verify(!page.closed,"solid spire rejected");
            EraseSpire();SelectCandidate(1);SnapView();Raise();Capture();Verify(page.closed,"52 percent openwork spire completes Cologne");
            GeometrySnapshot cologneSkeleton=CologneGeometry();
            SelectCandidate(0);Verify(SameGeometry(cologneSkeleton,CologneGeometry()),"Cologne crayon 0 percent retains meshes, enabled totals and bounds");
            Verify(CologneSurfaceMatches("COL_Stone"),"Cologne 0 percent selects the authored stone texture");
            yield return Snapshot("04_Cologne_Crayon0");
            SelectCandidate(2);Verify(SameGeometry(cologneSkeleton,CologneGeometry()),"Cologne crayon 60 percent retains meshes, enabled totals and bounds");
            Verify(CologneSurfaceMatches("COL_Crayon60"),"Cologne 60 percent selects a distinct authored crayon texture");
            yield return Snapshot("04_Cologne_Crayon60");
            SelectCandidate(1);Verify(SameGeometry(cologneSkeleton,CologneGeometry()),"Cologne crayon 30 percent restores style without changing skeleton");
            Verify(CologneSurfaceMatches("COL_Crayon30"),"Cologne returns to authored 30 percent texture without accumulating earlier styles");
            yield return Snapshot("04_Cologne_Complete");
            yield return Snapshot("04_Cologne_Notebook",true);
            DrawPortal();Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[5],"loads Sagrada scene");
            Verify(world.MissingModels.Count==0,"Sagrada authored models loaded");
            EraseMirror();EraseMirror();Verify(!page.mirror,"partial dust erasing does not clear central mirror");EraseMirror();
            SelectCandidate(1);selectedSpan=0;HangChain();selectedSpan=1;HangChain();
            SnapView();Raise();Capture();Verify(!page.nave,"missing third span prevents mirror entry");
            selectedSpan=2;HangChain();
            Verify(Rules.CheckSagrada(new[]{SpanHeight(0),SpanHeight(1),SpanHeight(2)},new[]{1,1,1},page.mirror).success,"three selected loaded chains match structural thresholds");
            yield return Snapshot("05_Sagrada_LiveMirror");
            world.Inspect("room");yield return Snapshot("05_Sagrada_ModelRoom");SnapView();
            Raise();Capture();Verify(page.nave,"valid chains and clear mirror enter upright nave");
            world.Inspect("nave");yield return Snapshot("05_Sagrada_Nave");SnapView();
            Prepare();yield return Snapshot("05_Sagrada_FinalNotebook",true);
            SnapView();Raise();Capture();Verify(page.portal,"final page has one result and opens return door");
            yield return Snapshot("05_Sagrada_FinalFacade");
            Next();yield return new WaitForSeconds(.7f);
            Verify(SceneManager.GetActiveScene().name==SceneNames[6],"returns to dawn workshop scene");
            ContextAction();Prepare();yield return Snapshot("06_Dawn_Notebook",true);
            SnapView();Raise();Capture();Verify(data.bridge,"bridge half is drawn on window glass");
            SnapView();
            GameObject inkObject=GameObject.Find("BridgeOnWindowGlass"),stoneObject=GameObject.Find("ExistingHalfBridge");
            LineRenderer inkBridge=inkObject!=null?inkObject.GetComponent<LineRenderer>():null;
            LineRenderer stoneBridge=stoneObject!=null?stoneObject.GetComponent<LineRenderer>():null;
            Verify(inkBridge!=null&&stoneBridge!=null&&inkBridge.positionCount>=2&&stoneBridge.positionCount>=2,
                "both glass drawing and distant original bridge exist");
            if(inkBridge!=null&&stoneBridge!=null&&inkBridge.positionCount>=2&&stoneBridge.positionCount>=2)
            {
                // The common crown is the last point of the new 0..PI/2 half and the
                // first point of the original PI/2..PI half, rather than their outer feet.
                Vector3 inkCrown=inkBridge.GetPosition(inkBridge.positionCount-1),stoneCrown=stoneBridge.GetPosition(0);
                Vector3 inkScreen=viewCamera.WorldToScreenPoint(inkCrown),stoneScreen=viewCamera.WorldToScreenPoint(stoneCrown);
                float alignedPixels=Vector2.Distance(new Vector2(inkScreen.x,inkScreen.y),new Vector2(stoneScreen.x,stoneScreen.y));
                Verify(Mathf.Abs(inkCrown.x-4.91f)<.02f&&Mathf.Abs(stoneCrown.x-9f)<.02f,
                    "bridge uses separate glass x=4.91 and original x=9 planes");
                Verify(inkScreen.z>0&&stoneScreen.z>0&&alignedPixels<3f,
                    "bridge crowns coincide at fixed view: "+alignedPixels.ToString("F2")+" px");
                Vector3 inkRay=(inkCrown-viewCamera.transform.position).normalized,stoneRay=(stoneCrown-viewCamera.transform.position).normalized;
                yield return Snapshot("06_Dawn_Aligned");
                world.SetPlayerPose(viewEye+Vector3.forward*.55f,Quaternion.LookRotation(viewAim-viewEye));
                inkScreen=viewCamera.WorldToScreenPoint(inkCrown);stoneScreen=viewCamera.WorldToScreenPoint(stoneCrown);
                float parallaxPixels=Vector2.Distance(new Vector2(inkScreen.x,inkScreen.y),new Vector2(stoneScreen.x,stoneScreen.y));
                float inkShift=Vector3.Angle(inkRay,(inkCrown-viewCamera.transform.position).normalized);
                float stoneShift=Vector3.Angle(stoneRay,(stoneCrown-viewCamera.transform.position).normalized);
                Verify(parallaxPixels>20f&&Mathf.Abs(inkShift-stoneShift)>.1f,
                    "side-step separates glass drawing from original: "+parallaxPixels.ToString("F2")+" px; angles "+inkShift.ToString("F2")+" / "+stoneShift.ToString("F2")+" deg");
                yield return Snapshot("06_Dawn_Parallax");
                SnapView();
            }
            Next();Verify(data.finished,"story closes with grandmother and signed sketchbook");
            yield return Snapshot("06_Dawn_End");
            var roundTrip=JsonUtility.FromJson<DemoSave>(JsonUtility.ToJson(data));
            Verify(roundTrip.finished&&roundTrip.pages[2].columns==8&&roundTrip.pages[5].spans[2]==1,"Unity JSON save preserves full walkthrough");
            File.WriteAllLines(Path.Combine(verificationFolder,"runtime-walkthrough.txt"),verification);
            File.WriteAllText(Path.Combine(verificationFolder,"runtime-save.json"),JsonUtility.ToJson(data,true));
            File.WriteAllText(Path.Combine(verificationFolder,"runtime-result.json"),"{\"passed\":"+(!verificationFailed?"true":"false")+",\"checks\":"+verification.Count+",\"unity\":\""+Application.unityVersion+"\"}");
            Application.logMessageReceived-=VerificationLog;
            Debug.Log("[DemoVerify] COMPLETE "+(!verificationFailed?"PASS":"FAIL"));
            yield return new WaitForSeconds(.5f);Application.Quit(verificationFailed?1:0);
        }
        void VerificationLog(string condition,string stack,LogType type)
        {
            if(type==LogType.Exception){verificationFailed=true;verification.Add("EXCEPTION "+condition+" "+stack);}
            if(type==LogType.Error&&!condition.StartsWith("[DemoVerify]")){verificationFailed=true;verification.Add("ERROR "+condition);}
        }
    }
}
