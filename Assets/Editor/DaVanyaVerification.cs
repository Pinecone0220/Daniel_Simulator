using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DaVanyaVerification
{
    static int frames, stage;
    static DanielSimulator sim;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Output = "C:/Users/PC/Desktop/Game_Project/3D/Daniel_Simulator/output/model-import";
    public static void Run() {
        var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Resources/Characters/DaVanya/DaVanya.fbx");
        importer.optimizeGameObjects=false; importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
        importer.SaveAndReimport();
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update+=Tick; EditorApplication.isPlaying=true;
    }
    static void Tick() {
        if(!EditorApplication.isPlaying || ++frames<30)return;
        try {
            if(stage==0) {
                sim=UnityEngine.Object.FindFirstObjectByType<DanielSimulator>();
                var mode=typeof(DanielSimulator).GetNestedType("Mode",BindingFlags.NonPublic);
                typeof(DanielSimulator).GetMethod("ResetGame",F).Invoke(sim,new object[]{Enum.Parse(mode,"Free")});
                sim.enabled=false; stage=1; frames=27; return;
            }
            EditorApplication.update-=Tick;
            var player=(GameObject)typeof(DanielSimulator).GetField("player",F).GetValue(sim);
            var anim=player.GetComponentInChildren<DanielCharacterAnimation>();
            if(!anim || !anim.HasValidRig || anim.name!="DaVanya")throw new Exception("DaVanya rig not active");
            anim.enabled=false;
            var skin=anim.GetComponentInChildren<SkinnedMeshRenderer>();
            if(!skin || skin.sharedMesh.vertexCount<50000)throw new Exception("Skinned mesh missing");
            skin.updateWhenOffscreen=true;
            foreach(var mat in skin.sharedMaterials)if(!mat.mainTexture)throw new Exception("Missing texture "+mat.name);
            player.transform.position=Vector3.zero; player.transform.rotation=Quaternion.identity;
            var arena=(GameObject)typeof(DanielSimulator).GetField("arena",F).GetValue(sim); arena.SetActive(false);
            var cam=(Camera)typeof(DanielSimulator).GetField("cam",F).GetValue(sim);
            cam.orthographic=true; cam.orthographicSize=1.30f;
            cam.transform.position=new Vector3(2.2f,1.55f,4.2f);cam.transform.LookAt(new Vector3(0,1f,0));
            var foot=anim.transform.Find("DaVanya_Rig"); 
            foreach(var bone in anim.GetComponentsInChildren<Transform>())
                if(bone.name=="LeftUpLeg" || bone.name=="RightUpLeg")Debug.Log("BONE_POSITION "+bone.name+" "+anim.transform.InverseTransformPoint(bone.position));
            Capture(cam,anim,"Idle",0,"DaVanya-idle");
            Capture(cam,anim,"Walk",.15f,"DaVanya-walk");
            Capture(cam,anim,"Empowered",.4f,"DaVanya-E2");
            anim.SetStealth(true); Capture(cam,anim,"Walk",.15f,"DaVanya-stealth"); anim.SetStealth(false);
            anim.PlayUltimateLatch();anim.Sample(.2f);anim.SetHidden(true);
            if(skin.enabled)throw new Exception("R hiding failed");
            anim.PlayUltimateExit();anim.Sample(.15f);
            if(!skin.enabled)throw new Exception("R exit visibility failed");
            Debug.Log("DAVANYA_VERIFIED: active model, valid rig, texture coverage, walk, E2, stealth, R visibility");
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
    }
    static void Capture(Camera c,DanielCharacterAnimation a,string pose,float phase,string name) {
        a.PreviewPose(pose,phase);
        var rt=new RenderTexture(900,1000,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
        var tex=new Texture2D(900,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,900,1000),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());
        c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
    }
}
