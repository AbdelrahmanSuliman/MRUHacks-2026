using UnityEngine;

namespace CalgaryCycling
{
    [CreateAssetMenu(menuName = "Calgary Cycling/Stylized Quality Profile")]
    public sealed class StylizedQualityProfile : ScriptableObject
    {
        [Header("Environment budgets")]
        [Range(4,20)] public float dressingStep = 12;
        [Range(6,30)] public float routeMarkerStep = 22;
        [Range(50,400)] public float nearFieldDistance = 220;
        [Range(8,64)] public int treeCountLimit = 48;
        [Range(8,64)] public int buildingCountLimit = 32;
        [Header("Quest tuning")]
        [Range(.6f,1.2f)] public float renderScale = .85f;
        public bool enableWater = true;
        public bool enableFlowers = true;
    }
}
