using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CalgaryCycling.Editor
{
    public static class CreateImportedStylizedLibrary
    {
        const string LibraryPath = "Assets/Cycling/Settings/ImportedStylizedAssets.asset";
        const string SettingsPath = "Assets/Cycling/Settings/Calgary.asset";
        const string QualityPath = "Assets/Cycling/Settings/Quest Stylized Quality.asset";

        [MenuItem("Calgary Cycling/Assets/Create Imported Stylized Library")]
        public static void CreateOrRefresh()
        {
            var library = AssetDatabase.LoadAssetAtPath<StylizedAssetLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<StylizedAssetLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.trees = Find("Assets/EmbersStorm -  Free Nature Pack/Prefabs/Trees/Pine tree", "*.prefab", 6);
            library.bushes = Find("Assets/EmbersStorm -  Free Nature Pack/Prefabs/Trees/Bush", "*.prefab", 5);
            library.grass = Find("Assets/EmbersStorm -  Free Nature Pack/Prefabs/Plants/Grass", "*.prefab", 6);
            library.flowers = Find("Assets/EmbersStorm -  Free Nature Pack/Prefabs/Plants/Flowers", "*.prefab", 8);
            library.rocks = Find("Assets/EmbersStorm -  Free Nature Pack/Prefabs/Rocks", "*.prefab", 8);
            library.buildings = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Buildings", "*.prefab", 9);
            library.roadDetails = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Roads", "Road_Manhole_*.prefab", 2);
            library.streetLights = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Road_Signs", "Light.prefab", 1);
            library.fences = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Other", "Fence.prefab", 1);
            library.transitStops = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Other", "*Stop.prefab", 2);
            library.smallProps = Find("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Other", "*.prefab", 7,
                "Fence.prefab", "Bus Stop.prefab", "Tram_Stop.prefab", "Tree_1.prefab", "Tree_2.prefab");

            EditorUtility.SetDirty(library);
            AssignToRideSettings(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StylizedAssets] Refreshed {LibraryPath}: {Count(library.trees)} trees, {Count(library.flowers)} flowers, {Count(library.rocks)} rocks, {Count(library.buildings)} buildings and {Count(library.smallProps)} city props.");
        }

        static void AssignToRideSettings(StylizedAssetLibrary library)
        {
            var settings = AssetDatabase.LoadAssetAtPath<RideSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"[StylizedAssets] Could not find {SettingsPath}; create the library, then assign it to RideSettings.stylizedAssets.");
                return;
            }

            settings.stylizedAssets = library;
            var quality = AssetDatabase.LoadAssetAtPath<StylizedQualityProfile>(QualityPath);
            if (quality == null)
            {
                quality = ScriptableObject.CreateInstance<StylizedQualityProfile>();
                quality.name = "Quest Stylized Quality";
                quality.dressingStep = 16;
                quality.routeMarkerStep = 24;
                quality.nearFieldDistance = 180;
                quality.treeCountLimit = 32;
                quality.buildingCountLimit = 20;
                quality.renderScale = .85f;
                quality.enableWater = true;
                quality.enableFlowers = true;
                AssetDatabase.CreateAsset(quality, QualityPath);
            }
            settings.stylizedQuality = quality;
            settings.stylizedZone = StylizedZone.Riverfront;
            EditorUtility.SetDirty(settings);
        }

        static GameObject[] Find(string folder, string filter, int max, params string[] excludedNames)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<GameObject>();
            var excluded = new HashSet<string>(excludedNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Matches(Path.GetFileName(path), filter))
                .Where(path => !excluded.Contains(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .Where(prefab => prefab != null)
                .ToArray();
        }

        static bool Matches(string fileName, string filter)
        {
            if (filter == "*.prefab") return true;
            if (!filter.Contains("*")) return fileName.Equals(filter, StringComparison.OrdinalIgnoreCase);
            var parts = filter.Split('*');
            return fileName.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(parts[parts.Length - 1], StringComparison.OrdinalIgnoreCase);
        }

        static int Count(GameObject[] prefabs) => prefabs == null ? 0 : prefabs.Length;
    }
}
