using UnityEngine;
namespace CalgaryCycling
{
    [CreateAssetMenu(menuName = "Calgary Cycling/Ride Settings")]
    public sealed class RideSettings : ScriptableObject
    {
        [Header("WGS84 origin: Peace Bridge, Calgary")]
        public double longitude = -114.07883;
        public double latitude = 51.05389;
        [Tooltip("ELLIPSOID metres, not sea-level height. Initial estimate; calibrate before presenting.")]
        public double ellipsoidHeight = 1040;
        [Header("Authored demonstration path, metres east/up/north from origin")]
        public Vector3[] waypoints = { new Vector3(0,0,-45), new Vector3(0,0,-20),
            new Vector3(2,0,5), new Vector3(6,0,30), new Vector3(12,0,55) };
        [Range(.25f, 2f)] public float corridorHalfWidth = 1.2f;
        [Range(1, 6)] public float cruisingSpeed = 3;
        [Range(.2f, 2)] public float steeringSensitivity = 1;
        [Range(0, .5f)] public float deadZone = .15f;
        [Min(.1f)] public float acceleration = .6f;
        [Min(.1f)] public float steeringSmoothTime = .35f;
        [Range(5, 30)] public float maxSteeringAngle = 12;
        [Range(1, 30)] public float maxYawDegreesPerSecond = 12;
        [Range(1, 2.2f)] public float eyeHeight = 1.65f;
        public bool useRightStick;
        [Header("Optional external stylized bicycle prefab")]
        public GameObject bicyclePrefab;
        public StylizedQualityProfile stylizedQuality;
        public StylizedAssetLibrary stylizedAssets;
        public StylizedZone stylizedZone = StylizedZone.Riverfront;
        [Tooltip("Small local offset used to keep imported near-field art on the Cesium route surface.")]
        [Range(-2f, 2f)] public float stylizedGroundOffset = .08f;
        [Header("Streaming budgets: starting points, not performance guarantees")]
        [Range(8, 64)] public float screenSpaceError = 24;
        [Range(64, 512)] public int cacheMiB = 128;
        [Range(1, 16)] public int simultaneousLoads = 4;
        [Range(15, 180)] public float loadTimeout = 90;
        [Header("Synthetic route surface, independent of Google geometry")]
        public bool showDemoDeck = true;
    }
}
