using UnityEngine;

namespace CalgaryCycling
{
    public static class SceneryNetwork
    {
        public static string BlockingIssue()
        {
            if(Application.internetReachability==NetworkReachability.NotReachable)
                return "No internet connection. Connect the headset to Wi-Fi, then Retry.";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var connectivity=activity.Call<AndroidJavaObject>("getSystemService","connectivity"))
                using(var network=connectivity.Call<AndroidJavaObject>("getActiveNetwork"))
                {
                    if(network==null)return "No internet connection. Connect the headset to Wi-Fi, then Retry.";
                    using(var capabilities=connectivity.Call<AndroidJavaObject>("getNetworkCapabilities",network))
                    {
                        // NET_CAPABILITY_CAPTIVE_PORTAL: Wi-Fi is connected but requires web sign-in.
                        if(capabilities!=null && capabilities.Call<bool>("hasCapability",17))
                            return "Wi-Fi sign-in required. Complete guest Wi-Fi sign-in on the headset or use a phone hotspot, then Retry.";
                    }
                }
            }
            catch(AndroidJavaException)
            {
                // If a device cannot expose network capabilities, let the actual Cesium request decide.
            }
#endif
            return null;
        }
    }
}
