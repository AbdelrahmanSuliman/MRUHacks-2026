using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace CalgaryCycling.Editor
{
    public static class CreateDemoScene
    {
        [MenuItem("Calgary Cycling/Create Demo Scene")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Assets/Cycling/Settings");Directory.CreateDirectory("Assets/Cycling/Scenes");
            var settings=AssetDatabase.LoadAssetAtPath<RideSettings>("Assets/Cycling/Settings/Calgary.asset");
            if(settings==null) {settings=ScriptableObject.CreateInstance<RideSettings>();AssetDatabase.CreateAsset(settings,"Assets/Cycling/Settings/Calgary.asset");}
            string rendererPath="Assets/Cycling/Settings/CyclingRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(renderer==null) {renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,rendererPath);}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Cycling/Settings/CyclingURP.asset");
            if(pipeline==null)
            {
                pipeline=UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount=2;pipeline.renderScale=.85f;pipeline.supportsHDR=false;
                AssetDatabase.CreateAsset(pipeline,"Assets/Cycling/Settings/CyclingURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;
            // Runtime-created materials still require a retained shader in player builds.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included=graphics.FindProperty("m_AlwaysIncludedShaders");
            var unlit=Shader.Find("Universal Render Pipeline/Unlit");
            bool found=false;
            for(int j=0;j<included.arraySize;j++)found|=included.GetArrayElementAtIndex(j).objectReferenceValue==unlit;
            if(!found && unlit!=null) {included.InsertArrayElementAtIndex(included.arraySize);included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue=unlit;graphics.ApplyModifiedProperties();}
            for(int i=0;i<QualitySettings.names.Length;i++)
            {QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            PlayerSettings.companyName="CalgaryCycling";PlayerSettings.productName="Calgary Cycling Simulator";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"ca.calgarycycling.simulator");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Calgary Cycling Simulator");root.AddComponent<RideApp>().SetSettings(settings);
            EditorSceneManager.SaveScene(scene,"Assets/Cycling/Scenes/Calgary.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Cycling/Scenes/Calgary.unity",true)};
            AssetDatabase.SaveAssets();Selection.activeObject=settings;
            Debug.Log("Scene created. Next: Input System + OpenXR settings, credentials, then Play. Read README.md.");
        }
        [MenuItem("Calgary Cycling/Show Credential Folder")]
        public static void Credentials() { Directory.CreateDirectory(Application.persistentDataPath);EditorUtility.RevealInFinder(Application.persistentDataPath); }
    }
}
