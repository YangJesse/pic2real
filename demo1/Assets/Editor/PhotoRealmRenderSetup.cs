using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PhotoRealmRenderSetup
{
  public static void SetupProject() {
    Directory.CreateDirectory("Assets/Generated");
    PlayerSettings.companyName = "Rotating Architecture";
    PlayerSettings.productName = "Architecture of the Other Side";
    PlayerSettings.colorSpace = ColorSpace.Linear;
    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.rotatingarchitecture.prototype");
    PlayerSettings.defaultScreenWidth = 1440;
    PlayerSettings.defaultScreenHeight = 900;
    PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
    PlayerSettings.runInBackground = true;
    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
    UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;

    const string rendererPath = "Assets/Generated/SpinRenderer.asset";
    const string pipelinePath = "Assets/Generated/SpinURP.asset";
    var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
    if (!renderer) {
      renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(renderer, rendererPath);
    }
    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
    if (!pipeline) {
      pipeline = UniversalRenderPipelineAsset.Create(renderer);
      AssetDatabase.CreateAsset(pipeline, pipelinePath);
    }
    pipeline.supportsHDR = true;
    pipeline.msaaSampleCount = 4;
    pipeline.renderScale = 1f;
    pipeline.shadowDistance = 42f;
    pipeline.mainLightShadowmapResolution = 2048;
    pipeline.shadowCascadeCount = 4;
    renderer.renderingMode = RenderingMode.ForwardPlus;
    if (!renderer.postProcessData)
      renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
    GraphicsSettings.defaultRenderPipeline = pipeline;
    QualitySettings.renderPipeline = pipeline;
    QualitySettings.vSyncCount = 1;
    EditorUtility.SetDirty(renderer);
    EditorUtility.SetDirty(pipeline);
  }

  public static void SetupLook() {
    const string profilePath = "Assets/Generated/SpinLook.asset";
    var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
    if (!profile) {
      profile = ScriptableObject.CreateInstance<VolumeProfile>();
      AssetDatabase.CreateAsset(profile, profilePath);
    }
    var tone = ProfileComponent<Tonemapping>(profile);
    tone.mode.Override(TonemappingMode.ACES);
    var bloom = ProfileComponent<Bloom>(profile);
    bloom.intensity.Override(0.22f);
    bloom.threshold.Override(1.1f);
    bloom.scatter.Override(0.53f);
    var color = ProfileComponent<ColorAdjustments>(profile);
    color.postExposure.Override(0.05f);
    color.contrast.Override(8f);
    color.saturation.Override(-7f);
    var vignette = ProfileComponent<Vignette>(profile);
    vignette.intensity.Override(0.10f);
    vignette.smoothness.Override(0.62f);
    foreach (var component in profile.components) {
      if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
      EditorUtility.SetDirty(component);
    }
    EditorUtility.SetDirty(profile);
    var look = new GameObject("Soft contrast and bloom").AddComponent<Volume>();
    look.isGlobal = true;
    look.priority = 10;
    look.sharedProfile = profile;
    var camera = Camera.main;
    if (camera) camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
  }

  static T ProfileComponent<T>(VolumeProfile profile) where T : VolumeComponent {
    return profile.TryGet<T>(out var component) ? component : profile.Add<T>();
  }

}
