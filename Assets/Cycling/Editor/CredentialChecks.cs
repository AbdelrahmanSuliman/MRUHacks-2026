using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CalgaryCycling.Editor
{
    public static class CredentialChecks
    {
        [MenuItem("Calgary Cycling/Validation/Check Android Credentials %#F8")]
        public static void Run()
        {
            string path=Path.Combine(Application.streamingAssetsPath,"cycling-secrets.json");
            if(!File.Exists(path))throw new InvalidOperationException("Assets/StreamingAssets/cycling-secrets.json is missing.");
            var request=UnityWebRequest.Get(new Uri(Path.GetFullPath(path)).AbsoluteUri);
            request.SendWebRequest().completed+=operation=>
            {
                try
                {
                    var lines=new List<string>();
                    if(request.result!=UnityWebRequest.Result.Success)
                        throw new InvalidOperationException("Credential request failed: "+request.result);
                    string text=request.downloadHandler.text;
                    var parser=typeof(SceneryService).GetMethod("TryReadSecrets",BindingFlags.NonPublic|BindingFlags.Static);
                    object[] args={text,null,1L,2L};
                    bool parsed=(bool)parser.Invoke(null,args);
                    bool tokenLoaded=parsed && !string.IsNullOrWhiteSpace(args[1] as string);
                    lines.Add((tokenLoaded?"PASS":"FAIL")+": Cesium token parsed from UnityWebRequest text");
                    var orsParser=typeof(RoutePlannerService).GetMethod("ParseKey",BindingFlags.NonPublic|BindingFlags.Static);
                    bool orsLoaded=false;
                    try
                    {
                        orsLoaded=!string.IsNullOrWhiteSpace(orsParser.Invoke(null,new object[]{text}) as string);
                    }
                    catch(TargetInvocationException) { }
                    lines.Add((orsLoaded?"PASS":"FAIL")+": Route key parsed from UnityWebRequest text");
                    lines.Add("Leading UTF-8 marker in web response: "+(text.Length>0 && text[0]=='\uFEFF'));
                    Directory.CreateDirectory("Temp");
                    File.WriteAllLines("Temp/CyclingCredentialChecks.txt",lines);
                    Debug.Log("[CredentialChecks] "+string.Join("\n",lines));
                }
                finally { request.Dispose(); }
            };
        }
    }
}
