using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dashed.Editor
{
    public static class DemoBuilder
    {
        static string Root {get{return Directory.GetParent(Application.dataPath).FullName;}}
        public static readonly string[] SceneNames={"00_Workshop","01_Pisa","02_Parthenon","03_Pantheon","04_Cologne","05_Sagrada","06_WorkshopDawn"};

        [MenuItem("虚线/1. 生成全部互动场景")]
        public static void Setup()
        {
            DottedLine.Editor.ModelImporter.ImportAll();
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Art"}))
            {
                var ti=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(path)) as TextureImporter;
                if(ti!=null){ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Compressed;ti.SaveAndReimport();}
            }
            Configure();
            var scenes=new EditorBuildSettingsScene[7];
            for(int i=0;i<7;i++)
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var director=new GameObject("DashedDemoDirector").AddComponent<DemoDirector>();director.startChapter=i;
                var preview=new GameObject("EditorStagePreview");
                string key=new[]{"SAG_wks_return_preview","PIS_initial_preview","PAR_initial_preview","PAN_initial","COL_initial","SAG_room_initial","SAG_wks_return_preview"}[i];
                var prefab=Resources.Load<GameObject>("Models/"+key);
                if(prefab!=null)PrefabUtility.InstantiatePrefab(prefab,preview.transform);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.SetParent(preview.transform);
                Vector3[] eyes={DemoWorld.B(2,-1.8f,1.65f),DemoWorld.B(-50,0,1.65f),DemoWorld.B(0,48,1.65f),DemoWorld.B(0,19.8f,1.65f),DemoWorld.B(-150,0,1.65f),DemoWorld.B(4.6f,-4.2f,2.4f),DemoWorld.B(2,-1.8f,1.65f)};
                Vector3[] aims={DemoWorld.B(-.7f,-1.5f,1.1f),DemoWorld.B(0,-2,27),DemoWorld.B(0,14,6.5f),DemoWorld.B(0,0,26),DemoWorld.B(0,0,74),DemoWorld.B(-1,.25f,3.5f),DemoWorld.B(-.7f,-1.5f,1.1f)};
                camera.transform.position=eyes[i];camera.transform.LookAt(aims[i]);camera.fieldOfView=new[]{65,64,64,80,68,67,65}[i];camera.farClipPlane=1500;
                camera.backgroundColor=new Color(.89f,.88f,.84f);camera.clearFlags=CameraClearFlags.SolidColor;
                var light=new GameObject("PreviewLight").AddComponent<Light>();light.transform.SetParent(preview.transform);light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(42,-35,0);
                string path="Assets/Scenes/"+SceneNames[i]+".unity";EditorSceneManager.SaveScene(scene,path);scenes[i]=new EditorBuildSettingsScene(path,true);
            }
            EditorBuildSettings.scenes=scenes;AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(scenes[0].path);
            Debug.Log("[Dashed] 7 scene files ready: workshop + five buildings + dawn. Imported delivery models are native prefabs.");
        }
        static void Configure()
        {
            PlayerSettings.companyName="DashedDemo";PlayerSettings.productName="虚线 - 把别人没画完的画完";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            PlayerSettings.colorSpace=ColorSpace.Gamma;PlayerSettings.resizableWindow=true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.stripEngineCode=false;
            QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=150;QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High;
            var playerAssets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if(playerAssets.Length>0)
            {
                var ps=new SerializedObject(playerAssets[0]);
                var input=ps.FindProperty("activeInputHandler");if(input!=null)input.intValue=0;ps.ApplyModifiedPropertiesWithoutUndo();
            }
            // Include the mirror shader even though its material is created after entering Play mode.
            var graphicsAssets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if(graphicsAssets.Length==0)return;
            var graphics=new SerializedObject(graphicsAssets[0]);
            var included=graphics.FindProperty("m_AlwaysIncludedShaders");
            if(included!=null)
            {
                foreach(string name in new[]{"Standard","Sprites/Default","Dashed/LiveMirror","VirtualLine/Delivered Surface","VirtualLine/Delivered Transparent"})
                {
                    Shader shader=Shader.Find(name);if(shader==null)continue;bool found=false;
                    for(int i=0;i<included.arraySize;i++)if(included.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;
                    if(!found){int n=included.arraySize;included.InsertArrayElementAtIndex(n);included.GetArrayElementAtIndex(n).objectReferenceValue=shader;}
                }
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        [MenuItem("虚线/2. 导出 Windows 演示程序")]
        public static void BuildWindows()
        {
            if(!File.Exists(Path.Combine(Root,"Assets/Scenes/00_Workshop.unity")))Setup();
            Configure();
            string directory=Path.Combine(Root,"Builds/Windows");Directory.CreateDirectory(directory);
            string[] paths=new string[7];for(int i=0;i<7;i++)paths[i]="Assets/Scenes/"+SceneNames[i]+".unity";
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=paths,locationPathName=Path.Combine(directory,"DashedDemo.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Directory.CreateDirectory(Path.Combine(Root,"TestResults"));
            File.WriteAllText(Path.Combine(Root,"TestResults/build-result.json"),JsonUtility.ToJson(new BuildSummaryData {result=report.summary.result.ToString(),errors=(int)report.summary.totalErrors,warnings=(int)report.summary.totalWarnings,bytes=(long)report.summary.totalSize,unity=Application.unityVersion},true));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("[Dashed] Windows player built: "+directory);
        }
        [MenuItem("虚线/生成工程并导出 Windows")]
        public static void SetupAndBuild(){Setup();BuildWindows();}
        [Serializable] class BuildSummaryData {public string result,unity;public int errors,warnings;public long bytes;}
    }
    [CustomEditor(typeof(DemoDirector))]
    public class DemoDirectorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("《虚线》固定演示画互动 Demo。点击 Play 开始本场，B 打开速写本，F 举页，左键落笔，E 互动。场景模型来自交付文件夹，运行时保留逐页进度与结构判定。",MessageType.Info);
            DrawDefaultInspector();
            if(GUILayout.Button("打开序章"))EditorSceneManager.OpenScene("Assets/Scenes/00_Workshop.unity");
        }
    }
}
