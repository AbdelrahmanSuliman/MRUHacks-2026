using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace CalgaryCycling.Editor
{
    public static class QuestBuild
    {
        public static void ValidateControllers()
        {
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var touch=settings==null?null:settings.GetFeature<OculusTouchControllerProfile>();
            if(touch==null || !touch.enabled)
                throw new BuildFailedException("Quest 2 controllers are disabled. Enable Oculus Touch Controller Profile under Android OpenXR settings before building.");
        }
        [MenuItem("Calgary Cycling/Build/Build Quest APK %#F7")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before building.");
            ValidateControllers();
            if(!File.Exists("Assets/StreamingAssets/cycling-secrets.json"))
                throw new InvalidOperationException("Add cycling-secrets.json to Assets/StreamingAssets before building for Quest.");
            string[] scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new InvalidOperationException("No enabled build scenes.");
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=scenes,
                locationPathName="Builds/CalgaryCycling-Quest.apk",
                target=BuildTarget.Android,
                options=BuildOptions.None
            });
            string summary=report.summary.result+": "+report.summary.outputPath+
                "\nErrors: "+report.summary.totalErrors+"; warnings: "+report.summary.totalWarnings;
            File.WriteAllText("Temp/CyclingQuestBuild.txt",summary);
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(summary);
            Debug.Log("[QuestBuild] "+summary);
        }
    }

    public sealed class QuestControllerBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.Android)QuestBuild.ValidateControllers();
        }
    }
}
