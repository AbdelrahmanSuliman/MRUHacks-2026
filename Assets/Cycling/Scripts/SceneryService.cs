using System;
using System.Collections;
using System.IO;
using CesiumForUnity;
using UnityEngine;
using UnityEngine.Networking;
namespace CalgaryCycling
{
    public sealed class SceneryService : MonoBehaviour
    {
        [Serializable] sealed class Secrets
        {
            public string cesiumIonAccessToken;
            public long cesiumIonAssetId = 1;
            public long cesiumIonImageryAssetId = 2;
        }
        public Cesium3DTileset Tiles { get; private set; }
        public Cesium3DTileset Buildings { get; private set; }
        public CesiumIonRasterOverlay Imagery { get; private set; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public string Status { get; private set; } = "Not initialized";
        public float Progress { get; private set; }
        public event Action Failure;
        float elapsed,stable,poll,unready,nextNetworkCheck;
        bool sawTile;
        Coroutine credentialRoutine;
        RideSettings settings;
        public void Initialize(RideSettings config, Transform georeference)
        {
            settings=config;
            var go=new GameObject("Cesium ion World Terrain");go.SetActive(false);
            go.transform.SetParent(georeference,false);
            Tiles=go.AddComponent<Cesium3DTileset>();
            Tiles.tilesetSource=CesiumDataSource.FromCesiumIon;
            Tiles.maximumScreenSpaceError=config.screenSpaceError;
            Tiles.maximumCachedBytes=(long)config.cacheMiB*1024*1024;
            Tiles.maximumSimultaneousTileLoads=(uint)config.simultaneousLoads;
            Tiles.preloadAncestors=true;Tiles.preloadSiblings=false;
            Tiles.createPhysicsMeshes=false;Tiles.showCreditsOnScreen=true;
            Tiles.OnTileGameObjectCreated+=TileCreated;
            Imagery=go.AddComponent<CesiumIonRasterOverlay>();
            Imagery.showCreditsOnScreen=true;
            var buildingGO=new GameObject("Cesium ion OSM Buildings");buildingGO.SetActive(false);buildingGO.transform.SetParent(georeference,false);
            Buildings=buildingGO.AddComponent<Cesium3DTileset>();
            Buildings.tilesetSource=CesiumDataSource.FromCesiumIon;
            Buildings.maximumScreenSpaceError=config.screenSpaceError;
            Buildings.maximumCachedBytes=(long)config.cacheMiB*1024*1024;
            Buildings.maximumSimultaneousTileLoads=(uint)config.simultaneousLoads;
            Buildings.preloadAncestors=true;Buildings.preloadSiblings=false;
            Buildings.createPhysicsMeshes=false;Buildings.showCreditsOnScreen=true;
            Buildings.OnTileGameObjectCreated+=TileCreated;
            Cesium3DTileset.OnCesium3DTilesetLoadFailure+=LoadFailed;
            Retry();
        }
        void TileCreated(GameObject go) { sawTile=true; }
        void LoadFailed(Cesium3DTilesetLoadFailureDetails details)
        {
            if(details.tileset!=Tiles && details.tileset!=Buildings)return;
            // Never display the SDK message: it may contain the credential-bearing URL.
            Fail("Cesium ion request failed (HTTP "+details.httpStatusCode+"). Check token access to terrain, imagery and buildings, account limits and connection, then Retry.");
        }
        public void Retry()
        {
            Ready=Failed=sawTile=false;elapsed=stable=poll=unready=nextNetworkCheck=0;Progress=0;
            if(Tiles==null)return;
            if(credentialRoutine!=null)StopCoroutine(credentialRoutine);
            Tiles.gameObject.SetActive(false);
            if(Buildings!=null)Buildings.gameObject.SetActive(false);
            string networkIssue=SceneryNetwork.BlockingIssue();
            if(networkIssue!=null){Fail(networkIssue);return;}
            Status="Loading scenery credentials...";
            credentialRoutine=StartCoroutine(LoadCredentialsAndStart());
        }

        IEnumerator LoadCredentialsAndStart()
        {
            string token=Environment.GetEnvironmentVariable("CALGARY_CESIUM_ION_TOKEN");
            long assetId=1;
            long imageryAssetId=2;
            if(!UsableToken(token))
            {
                var paths=new System.Collections.Generic.List<string>
                {
                    Path.Combine(Application.persistentDataPath,"cycling-secrets.json")
                };
#if UNITY_EDITOR
                paths.Add(Path.GetFullPath(Path.Combine(Application.dataPath,"..","cycling-secrets.json")));
#endif
                foreach(string path in paths)
                {
                    if(!File.Exists(path))continue;
                    if(TryReadSecrets(File.ReadAllText(path),out token,out assetId,out imageryAssetId) && UsableToken(token))break;
                }

                if(!UsableToken(token))
                {
                    string streamingPath=Path.Combine(Application.streamingAssetsPath,"cycling-secrets.json");
                    if(streamingPath.StartsWith("jar:",StringComparison.OrdinalIgnoreCase) ||
                       streamingPath.StartsWith("http:",StringComparison.OrdinalIgnoreCase) ||
                       streamingPath.StartsWith("https:",StringComparison.OrdinalIgnoreCase))
                    {
                        using(var request=UnityWebRequest.Get(streamingPath))
                        {
                            request.timeout=10;
                            yield return request.SendWebRequest();
                            if(request.result==UnityWebRequest.Result.Success)
                                TryReadSecrets(request.downloadHandler.text,out token,out assetId,out imageryAssetId);
                        }
                    }
                    else if(File.Exists(streamingPath))
                    {
                        TryReadSecrets(File.ReadAllText(streamingPath),out token,out assetId,out imageryAssetId);
                    }
                }
            }
            if(!UsableToken(token)) { credentialRoutine=null;Fail("Missing Cesium ion token. Put cycling-secrets.json in the project root, StreamingAssets, or persistent data folder, then Retry.");yield break; }
            Tiles.ionAccessToken=token.Trim();
            Tiles.ionAssetID=assetId;
            Imagery.ionAccessToken=token.Trim();
            Imagery.ionAssetID=imageryAssetId;
            Buildings.ionAccessToken=token.Trim();
            Buildings.ionAssetID=96188; // Cesium ion OSM Buildings asset.
            Status="Connecting to Cesium ion...";
            Debug.Log("[SceneryService] Cesium credentials loaded. Connecting to terrain, imagery and buildings.");
            Tiles.gameObject.SetActive(true);
            Buildings.gameObject.SetActive(true);
            credentialRoutine=null;
        }

        static bool TryReadSecrets(string json,out string token,out long assetId,out long imageryAssetId)
        {
            token=null;assetId=1;imageryAssetId=2;
            try
            {
                // File.ReadAllText removes a UTF-8 BOM; DownloadHandler.text on Quest preserves it.
                // JsonUtility rejects that invisible marker unless it is removed first.
                var secrets=JsonUtility.FromJson<Secrets>(json?.TrimStart('\uFEFF',' ','\t','\r','\n'));
                if(secrets==null)return false;
                token=secrets.cesiumIonAccessToken;
                assetId=secrets.cesiumIonAssetId<=0?1:secrets.cesiumIonAssetId;
                imageryAssetId=secrets.cesiumIonImageryAssetId<0?2:secrets.cesiumIonImageryAssetId;
                return true;
            }
            catch { return false; }
        }
        void Update()
        {
            if(Tiles==null || Failed || !Tiles.gameObject.activeSelf)return;
            float dt=Time.unscaledDeltaTime;elapsed+=dt;poll+=dt;
            if(elapsed>=nextNetworkCheck)
            {
                nextNetworkCheck=elapsed+3;
                string networkIssue=SceneryNetwork.BlockingIssue();
                if(networkIssue!=null){Fail(networkIssue);return;}
            }
            if(poll<.25f)return;
            float step=poll;poll=0;
            Progress=Mathf.Clamp(Tiles.ComputeLoadProgress()/100f,0,1);
            if(!Ready)
            {
                stable=sawTile && Progress>=.99f ? stable+step : 0;
                Status="Loading current view: "+Mathf.RoundToInt(Progress*100)+"%";
                if(stable>=2) { Ready=true;Status="Scenery ready. Inspect route alignment before presenting."; }
                else if(elapsed>settings.loadTimeout)Fail("Scenery loading timed out. Check network, key, quota or coverage, then Retry.");
            }
            else
            {
                unready=Progress<.90f?unready+step:0;
                Status=Progress<.99f?"Streaming scenery: "+Mathf.RoundToInt(Progress*100)+"%":"Scenery ready";
                if(unready>settings.loadTimeout)Fail("Streaming stalled. Ride paused; reconnect and Retry.");
            }
        }
        void Fail(string message)
        {
            Ready=false;
            Failed=true;
            Status=message;
            // Keep handled runtime failures visible in the Unity Editor log and
            // Android logcat without logging the credential-bearing tiles URL.
            Debug.LogError("[SceneryService] " + message);
            Failure?.Invoke();
        }
        static bool UsableToken(string token)
        {
            if(string.IsNullOrWhiteSpace(token))return false;
            string value=token.Trim();
            return value!="=" && !value.Equals("YOUR_CESIUM_ION_TOKEN",StringComparison.OrdinalIgnoreCase);
        }
        void OnDestroy()
        {
            if(credentialRoutine!=null)StopCoroutine(credentialRoutine);
            Cesium3DTileset.OnCesium3DTilesetLoadFailure-=LoadFailed;
            if(Tiles!=null)Tiles.OnTileGameObjectCreated-=TileCreated;
            if(Buildings!=null)Buildings.OnTileGameObjectCreated-=TileCreated;
        }
    }
}
