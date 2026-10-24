using System;
using System.IO;
using CesiumForUnity;
using UnityEngine;
namespace CalgaryCycling
{
    public sealed class SceneryService : MonoBehaviour
    {
        [Serializable] sealed class Secrets { public string cesiumIonAccessToken; public long cesiumIonAssetId = 1; }
        public Cesium3DTileset Tiles { get; private set; }
        public Cesium3DTileset Buildings { get; private set; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public string Status { get; private set; } = "Not initialized";
        public float Progress { get; private set; }
        public event Action Failure;
        float elapsed,stable,poll,unready;
        bool sawTile;
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
            Tiles.preloadAncestors=false;Tiles.preloadSiblings=false;
            Tiles.createPhysicsMeshes=false;Tiles.showCreditsOnScreen=true;
            Tiles.OnTileGameObjectCreated+=TileCreated;
            var buildingGO=new GameObject("Cesium ion OSM Buildings");buildingGO.SetActive(false);buildingGO.transform.SetParent(georeference,false);
            Buildings=buildingGO.AddComponent<Cesium3DTileset>();
            Buildings.tilesetSource=CesiumDataSource.FromCesiumIon;
            Buildings.maximumScreenSpaceError=config.screenSpaceError;
            Buildings.maximumCachedBytes=(long)config.cacheMiB*1024*1024;
            Buildings.maximumSimultaneousTileLoads=(uint)config.simultaneousLoads;
            Buildings.preloadAncestors=false;Buildings.preloadSiblings=false;
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
            Fail("Tiles request failed (HTTP "+details.httpStatusCode+"). Check key, billing, quota and connection, then Retry.");
        }
        public void Retry()
        {
            Ready=Failed=sawTile=false;elapsed=stable=poll=unready=0;Progress=0;
            if(Tiles==null)return;
            Tiles.gameObject.SetActive(false);
            string token=Environment.GetEnvironmentVariable("CALGARY_CESIUM_ION_TOKEN");
            long assetId=1;
            try
            {
                if(string.IsNullOrWhiteSpace(token))
                {
                    string path=Path.Combine(Application.persistentDataPath,"cycling-secrets.json");
                    if(File.Exists(path))
                    {
                        var secrets=JsonUtility.FromJson<Secrets>(File.ReadAllText(path));
                        token=secrets?.cesiumIonAccessToken;
                        assetId=secrets == null || secrets.cesiumIonAssetId <= 0 ? 1 : secrets.cesiumIonAssetId;
                    }
                }
            }
            catch { Fail("Could not read cycling-secrets.json. Check JSON and file permissions.");return; }
            if(string.IsNullOrWhiteSpace(token)) { Fail("Missing Cesium ion token. Configure cycling-secrets.json, then Retry.");return; }
            Tiles.ionAccessToken=token.Trim();
            Tiles.ionAssetID=assetId;
            Buildings.ionAccessToken=token.Trim();
            Buildings.ionAssetID=96188; // Cesium ion OSM Buildings asset.
            Status="Connecting to Cesium ion...";
            Tiles.gameObject.SetActive(true);
            Buildings.gameObject.SetActive(true);
        }
        void Update()
        {
            if(Tiles==null || Failed || !Tiles.gameObject.activeSelf)return;
            float dt=Time.unscaledDeltaTime;elapsed+=dt;poll+=dt;
            if(Application.internetReachability==NetworkReachability.NotReachable)
            { Fail("No network connection. Reconnect and choose Retry.");return; }
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
        void OnDestroy()
        {
            Cesium3DTileset.OnCesium3DTilesetLoadFailure-=LoadFailed;
            if(Tiles!=null)Tiles.OnTileGameObjectCreated-=TileCreated;
            if(Buildings!=null)Buildings.OnTileGameObjectCreated-=TileCreated;
        }
    }
}
