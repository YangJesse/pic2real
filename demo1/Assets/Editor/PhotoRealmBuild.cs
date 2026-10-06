using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PhotoRealmBuild
{
    public const string ScenePath="Assets/Scenes/PhotoRealm.unity";
    [MenuItem("Photo Realm/Build playable Mac app")]
    public static void BuildMac()
    {
        PhotoRealmRenderSetup.SetupProject();
        PlayerSettings.productName="Photo Realm";PlayerSettings.companyName="Architectural Photo Lab";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.architecture.photorealm");
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.resizableWindow=true;
        var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
        pipeline.shadowDistance=70;pipeline.mainLightShadowmapResolution=4096;
        var pipelineSettings=new SerializedObject(pipeline);
        pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
        pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
        pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("Photo Realm runtime world").AddComponent<PhotoRealmGame>();
        game.chineseFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        PhotoRealmRenderSetup.SetupLook();
        foreach(string file in Directory.GetFiles("Assets/Resources/PhotoRealm","*-normal.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(file);
            if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
        }
        // Preserve the shader used by runtime-authored photographic geometry.
        var warmup=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        const string matPath="Assets/Generated/PhotoRealmShader.mat";
        if(!AssetDatabase.LoadAssetAtPath<Material>(matPath))AssetDatabase.CreateAsset(warmup,matPath);
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Shader reference";cube.transform.position=Vector3.one*-1000;
        cube.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var warm=AssetDatabase.LoadAssetAtPath<Material>(matPath);warm.EnableKeyword("_NORMALMAP");warm.SetTexture("_BumpMap",Resources.Load<Texture2D>("PhotoRealm/Stone-normal"));EditorUtility.SetDirty(warm);
        warm.EnableKeyword("_EMISSION");warm.SetColor("_EmissionColor",Color.white);
        const string waterPath="Assets/Generated/PhotoRealmWater.mat";
        if(!AssetDatabase.LoadAssetAtPath<Material>(waterPath))AssetDatabase.CreateAsset(new Material(Shader.Find("PhotoRealm/Water")),waterPath);
        var waterRef=GameObject.CreatePrimitive(PrimitiveType.Cube);waterRef.transform.position=Vector3.one*-1001;waterRef.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
        const string shaftPath="Assets/Generated/PhotoRealmLightShaft.mat";
        if(!AssetDatabase.LoadAssetAtPath<Material>(shaftPath))AssetDatabase.CreateAsset(new Material(Shader.Find("PhotoRealm/LightShaft")),shaftPath);
        var shaftRef=GameObject.CreatePrimitive(PrimitiveType.Cube);shaftRef.transform.position=Vector3.one*-1003;shaftRef.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(shaftPath);
        PreserveTransparency("Universal Render Pipeline/Lit","Glass");
        PreserveTransparency("Universal Render Pipeline/Unlit","Mist");
        // Skybox shader must be referenced in a build as well.
        var sky=new Material(Shader.Find("Skybox/Procedural"));RenderSettings.skybox=sky;
        const string skyPath="Assets/Generated/PhotoRealmSky.mat";
        if(!AssetDatabase.LoadAssetAtPath<Material>(skyPath))AssetDatabase.CreateAsset(sky,skyPath);
        RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds");Directory.CreateDirectory("artifacts");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName="Builds/PhotoRealm.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
        File.WriteAllText("artifacts/photo-realm-build.txt",result.summary.result+"\nErrors: "+result.summary.totalErrors+"\nWarnings: "+result.summary.totalWarnings+"\nSeconds: "+result.summary.totalTime.TotalSeconds);
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Photo Realm build failed");
        Debug.Log("PHOTO_REALM_BUILD_SUCCESS");
    }
    static void PreserveTransparency(string shader,string name)
    {
        string path="Assets/Generated/PhotoRealm"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,path);}
        mat.SetFloat("_Surface",1);mat.SetFloat("_ZWrite",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;EditorUtility.SetDirty(mat);
        var reference=GameObject.CreatePrimitive(PrimitiveType.Cube);reference.transform.position=Vector3.one*-1002;reference.GetComponent<Renderer>().sharedMaterial=mat;
    }
}
