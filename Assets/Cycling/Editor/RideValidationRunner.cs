using System;
using System.IO;
using UnityEditor;

namespace CalgaryCycling.Editor
{
    // Runs the existing Editor commands without intercepting the user's keyboard.
    // Write one allowed command to Temp/CyclingEditorCommand.txt; results use the normal reports.
    [InitializeOnLoad]
    public static class RideValidationRunner
    {
        static double nextPoll;
        static bool refreshed;
        static RideValidationRunner() { EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            const string request="Temp/CyclingEditorCommand.txt";
            if(!File.Exists(request))return;
            if(!refreshed)
            {
                refreshed=true;
                AssetDatabase.Refresh();
                return; // Allow script compilation/domain reload before executing the command.
            }
            refreshed=false;
            string command=File.ReadAllText(request).Trim();
            File.Delete(request);
            try
            {
                switch(command)
                {
                    case "vr-menu":VRMenuChecks.Run();break;
                    case "product":RideProductChecks.Run();break;
                    case "credentials":CredentialChecks.Run();break;
                    case "build-quest":QuestBuild.Build();break;
                    default:throw new InvalidOperationException("Unknown cycling validation command.");
                }
                File.WriteAllText("Temp/CyclingEditorCommand.result.txt","Completed: "+command);
            }
            catch(Exception ex){File.WriteAllText("Temp/CyclingEditorCommand.result.txt","FAILED: "+ex.GetBaseException().Message);}
        }
    }
}
