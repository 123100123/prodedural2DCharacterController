using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// One-time setup of the Universal Render Pipeline with the 2D Renderer, which the 2D lights need: creates the renderer
// and pipeline assets under Assets/Settings and makes them the default pipeline and the pipeline of every quality
// level. Run it from Tools > Procedural Character > Set Up URP 2D Lighting once the URP package has been installed
// (Package Manager adds it from Packages/manifest.json). Safe to run again: existing assets are reused. The URP types
// are reached by name, so this compiles whether or not the package is installed yet.
public static class UrpLightingSetup {
    private const string SettingsFolder = "Assets/Settings";
    private const string RendererPath = SettingsFolder + "/Renderer2D.asset";
    private const string PipelinePath = SettingsFolder + "/UniversalRP.asset";
    private const string PackagePath = "Packages/com.unity.render-pipelines.universal";
    private const string PipelineType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime";
    private const string RendererType = "UnityEngine.Rendering.Universal.Renderer2DData, Unity.RenderPipelines.Universal.2D.Runtime";
    private const string PostProcessType = "UnityEngine.Rendering.Universal.PostProcessData, Unity.RenderPipelines.Universal.Runtime";
    private const string ReloaderType = "UnityEngine.Rendering.ResourceReloader, Unity.RenderPipelines.Core.Runtime";
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/Procedural Character/Set Up URP 2D Lighting")]
    public static void SetUpFromMenu() => Debug.Log(SetUp());

    // Returns what it did (or why it could not), for the menu and for tools driving the Editor.
    public static string SetUp() {
        Type pipelineType = Type.GetType(PipelineType);
        Type rendererType = Type.GetType(RendererType);
        if (pipelineType == null || rendererType == null) {
            return "The Universal Render Pipeline package is not installed yet. It is listed in Packages/manifest.json; " +
                   "wait for the Package Manager to finish resolving it, then run this again.";
        }

        EnsureFolder();
        ScriptableObject renderer = LoadOrCreateRenderer(rendererType);
        RenderPipelineAsset pipeline = LoadOrCreatePipeline((pipelineType, renderer));
        if (pipeline == null) {
            return "Could not create the Universal Render Pipeline asset.";
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        AssignToEveryQualityLevel(pipeline);
        AssetDatabase.SaveAssets();
        return $"Universal Render Pipeline set up: {PipelinePath} with the 2D Renderer {RendererPath}, assigned in Graphics and all " +
               $"{QualitySettings.names.Length} quality levels. Colour space stays {PlayerSettings.colorSpace}.";
    }

    // Shadows are drawn into a texture the size of the light texture, which defaults to half the screen: shadow edges are
    // soft and blocky. Full size gives crisp edges. Returns what it did.
    public static string UseCrispShadows() {
        Type rendererType = Type.GetType(RendererType);
        ScriptableObject renderer = rendererType != null ? AssetDatabase.LoadAssetAtPath(RendererPath, rendererType) as ScriptableObject : null;
        if (renderer == null) {
            return "Crisp shadows: the 2D Renderer asset does not exist yet; run Set Up URP 2D Lighting first.";
        }
        SerializedObject serialized = new SerializedObject(renderer);
        SerializedProperty scale = serialized.FindProperty("m_LightRenderTextureScale");
        if (scale == null) {
            return "Crisp shadows: this pipeline version has no light render texture scale.";
        }
        scale.floatValue = 1f;
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        return "Crisp shadows: the 2D Renderer draws its light and shadow textures at full size.";
    }

    private static void EnsureFolder() {
        if (!AssetDatabase.IsValidFolder(SettingsFolder)) {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }
    }

    private static ScriptableObject LoadOrCreateRenderer(Type rendererType) {
        ScriptableObject existing = AssetDatabase.LoadAssetAtPath(RendererPath, rendererType) as ScriptableObject;
        if (existing != null) {
            return existing;
        }

        ScriptableObject created = ScriptableObject.CreateInstance(rendererType);
        SetDefaultPostProcessData(created);
        AssetDatabase.CreateAsset(created, RendererPath);
        ReloadMissingResources(created);
        return created;
    }

    private static RenderPipelineAsset LoadOrCreatePipeline((Type pipelineType, ScriptableObject renderer) parts) {
        RenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath(PipelinePath, parts.pipelineType) as RenderPipelineAsset;
        if (existing != null) {
            return existing;
        }

        MethodInfo create = parts.pipelineType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
        RenderPipelineAsset created = create?.Invoke(null, new object[] { parts.renderer }) as RenderPipelineAsset;
        if (created != null) {
            AssetDatabase.CreateAsset(created, PipelinePath);
        }
        return created;
    }

    // The post-processing defaults the menu's own 2D renderer is made with (an internal lookup, so by reflection).
    private static void SetDefaultPostProcessData(ScriptableObject renderer) {
        Type postProcessType = Type.GetType(PostProcessType);
        object data = postProcessType?.GetMethod("GetDefaultPostProcessData", AnyStatic)?.Invoke(null, null);
        if (data == null) {
            return;
        }
        Type rendererType = renderer.GetType();
        FieldInfo field = rendererType.GetField("postProcessData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo property = rendererType.GetProperty("postProcessData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(renderer, data);
        property?.SetValue(renderer, data);
    }

    // Fills the shaders and materials the renderer data references from the package.
    private static void ReloadMissingResources(ScriptableObject renderer) {
        MethodInfo reload = Type.GetType(ReloaderType)?.GetMethod("ReloadAllNullIn", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(object), typeof(string) }, null);
        reload?.Invoke(null, new object[] { renderer, PackagePath });
        EditorUtility.SetDirty(renderer);
    }

    private static void AssignToEveryQualityLevel(RenderPipelineAsset pipeline) {
        int original = QualitySettings.GetQualityLevel();
        for (int level = 0; level < QualitySettings.names.Length; level++) {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(original, false);
    }
}
