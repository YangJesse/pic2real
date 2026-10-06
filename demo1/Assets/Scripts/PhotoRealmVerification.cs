using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Drives the same movement controller and interaction methods as keyboard play.
// Route traversal never teleports. A separate preflight intentionally tests fall recovery.
public class PhotoRealmVerification : MonoBehaviour
{
    public PhotoRealmGame game;
    [Serializable] class Report {public string result;public string detail;public List<string> checks=new List<string>();public float elapsed;public int falls;public string finalPosition;}
    Report report=new Report();float begun;bool failed;
    IEnumerator Start()
    {
        begun=Time.realtimeSinceStartup;yield return null;yield return new WaitForSeconds(1);
        Check(!game.TryPlace(),"Cannot place a photograph before pickup and alignment");
        Check(!game.TryAim(),"Cannot aim without a photograph");
        int falls=game.falls;game.VerificationTeleport(new Vector3(0,-8,0));yield return null;
        Check(game.falls==falls+1&&game.Position.y>-.5f,"Fall recovery restores player to safe start");
        yield return Move(new Vector3(-1.5f,0,2.5f));
        Check(game.Interact(),"Bridge photograph pickup");
        yield return Move(new Vector3(0,0,-2));Check(!game.TryAim(),"Photograph rejected outside authored anchor");
        yield return Move(game.sites[0].anchor);Check(game.TryAim(),"Bridge aim entered");yield return WaitReady();
        game.SetLook(90,0);Check(!game.TryPlace(),"Misaligned photograph cannot commit");game.CancelAim();
        Check(!game.sites[0].placed&&!game.sites[0].objectRoot.activeSelf,"Cancelling preview leaves bridge absent");
        yield return Photo(0);yield return Shot("01-bridge");
        Check(!game.TryPlace(),"Duplicate placement rejected");
        yield return Move(new Vector3(0,0,18.8f));
        yield return Move(new Vector3(-1.2f,0,20));Check(game.Interact(),"Door photograph pickup");
        yield return Move(game.sites[1].anchor);yield return Photo(1);yield return Shot("02-door");
        Check(game.Interact(),"Door opens through normal interaction");yield return new WaitForSeconds(1.5f);
        yield return Move(new Vector3(0,0,25.2f));
        yield return Move(game.sites[2].anchor);Check(game.Interact(),"Lamp photograph pickup");
        game.LookAt(new Vector3(0,1.4f,33));yield return Shot("03-room-before-light");yield return Photo(2);game.LookAt(new Vector3(0,1.4f,33));yield return Shot("04-room-after-light");
        Check(game.sites[2].objectRoot.GetComponentInChildren<Light>().enabled,"Photo lamp is an actual scene light");
        yield return Move(new Vector3(2,0,28.8f));Check(game.Interact(),"Spiral stair photograph pickup");
        yield return Move(game.sites[3].anchor);yield return Photo(3);yield return Shot("05-spiral-stair");
        yield return Move(new Vector3(0,0,30.7f));
        for(int i=0;i<48;i++)
        {
            Vector3 p=PhotoRealmWorld.StairCenter+PhotoRealmWorld.Polar(3,-86.25f+i*7.5f)+Vector3.up*((i+1)*.125f);
            yield return Move(p,.13f,5);
        }
        Check(game.Position.y>5.7f,"Walked up all 48 spiral treads with CharacterController");
        yield return Move(new Vector3(0,6,28));yield return Move(game.sites[4].anchor);
        Check(game.Interact(),"Wind screen photograph pickup");
        yield return Move(new Vector3(9.2f,6,27.5f));game.SetVerificationMovement(Vector2.zero);
        float z=game.Position.z;yield return new WaitForSeconds(.55f);
        Check(z-game.Position.z>.4f,"Unprotected crosswind physically displaces the player");
        yield return Move(game.sites[4].anchor);yield return Photo(4);
        yield return Move(new Vector3(9.2f,6,27.5f));z=game.Position.z;yield return new WaitForSeconds(.55f);
        Check(Mathf.Abs(z-game.Position.z)<.10f,"Screen reduces player drift inside the protected passage");
        game.LookAt(new Vector3(20,7,28.7f));yield return Shot("06-wind-screen");
        yield return Move(new Vector3(26,6,27.5f));yield return Move(game.sites[5].anchor);
        Check(game.Interact(),"Drain photograph pickup");yield return Photo(5);
        game.LookAt(new Vector3(30,4.8f,39));yield return Shot("07-flooded-court");
        Check(game.Interact(),"Drain pump starts with normal interaction");
        yield return new WaitForSeconds(4.5f);Check(game.waterY<5.5f&&game.waterY>2.7f,"Water level descends progressively");
        yield return new WaitForSeconds(5);Check(game.drained&&game.stage==6&&game.waterY<2.7f,"Drain completes and opens the submerged route");
        yield return Shot("08-drained-court");
        yield return Move(new Vector3(26,6,30.7f));yield return Move(new Vector3(26,3.3f,36));
        yield return Move(new Vector3(26,3.3f,41));yield return Move(new Vector3(36,3.3f,41));
        yield return Move(new Vector3(36,3.3f,45));yield return Move(new Vector3(36,6,50));
        yield return Move(game.goal);yield return new WaitForSeconds(.5f);
        Check(game.completed,"Full playable route reaches completion trigger");
        Check(game.falls==1,"No accidental falls or resets during route traversal");
        game.LookAt(new Vector3(0,7,30));yield return Shot("09-completed-route");
        Finish(true,"All gameplay checks passed. Route used regular controller movement; one intentional preflight fall.");
    }
    IEnumerator Move(Vector3 target,float tolerance=.20f,float timeout=20)
    {
        float start=Time.realtimeSinceStartup;
        while(PhotoRealmGame.HorizontalDistance(game.Position,target)>tolerance)
        {
            Vector3 delta=target-game.Position;float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            game.SetLook(yaw,0);game.SetVerificationMovement(Vector2.up);
            if(Time.realtimeSinceStartup-start>timeout){Fail("Traversal blocked at "+game.Position+" aiming for "+target);yield break;}
            yield return null;
        }
        game.SetVerificationMovement(Vector2.zero);yield return new WaitForSeconds(.08f);
    }
    IEnumerator Photo(int index)
    {
        Check(game.stage==index,"Correct stage for photograph "+index);
        Check(game.TryAim(),"Photo "+index+" enters preview");yield return WaitReady();
        Check(game.IsAligned,"Photo "+index+" aligns with authored viewpoint");
        yield return Shot("preview-"+index);
        Check(game.TryPlace(),"Photo "+index+" commits");yield return WaitReady();
        Check(game.sites[index].placed&&game.sites[index].objectRoot.activeSelf,"Photo "+index+" has real scene geometry");
    }
    IEnumerator WaitReady(){float start=Time.realtimeSinceStartup;while(game.busy){if(Time.realtimeSinceStartup-start>6){Fail("Photo transition timed out");yield break;}yield return null;}yield return new WaitForSeconds(.1f);}
    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(game.EvidenceDirectory,name+".png"));yield return new WaitForSeconds(.12f);
    }
    void Check(bool condition,string name){if(!condition){Fail(name);return;}report.checks.Add(name);Debug.Log("PHOTO_QA PASS "+name);}
    void Fail(string why)
    {
        if(failed)return;failed=true;game.SetVerificationMovement(Vector2.zero);ScreenCapture.CaptureScreenshot(Path.Combine(game.EvidenceDirectory,"failure.png"));
        Debug.LogError("PHOTO_QA FAIL "+why);Finish(false,why);StopAllCoroutines();
    }
    void Finish(bool success,string detail)
    {
        report.result=success?"PASS":"FAIL";report.detail=detail;report.elapsed=Time.realtimeSinceStartup-begun;
        report.falls=game.falls;report.finalPosition=game.Position.ToString("F3");
        File.WriteAllText(Path.Combine(game.EvidenceDirectory,"report.json"),JsonUtility.ToJson(report,true));
        Debug.Log("PHOTO_QA "+report.result+" "+detail);
        if(!Application.isEditor)Application.Quit(success?0:1);
    }
}
