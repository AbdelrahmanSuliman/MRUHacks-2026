using UnityEngine;

namespace CalgaryCycling
{
    // Editor-assigned imported art used by the runtime environment builder.
    // Keeping these references in one asset makes the visual kit swappable without
    // coupling route, Cesium, or bicycle code to a particular asset pack.
    [CreateAssetMenu(menuName = "Calgary Cycling/Imported Stylized Asset Library")]
    public sealed class StylizedAssetLibrary : ScriptableObject
    {
        [Header("Nature")]
        public GameObject[] trees;
        public GameObject[] bushes;
        public GameObject[] grass;
        public GameObject[] flowers;
        public GameObject[] rocks;

        [Header("Riverfront / city dressing")]
        public GameObject[] buildings;
        public GameObject[] roadDetails;
        public GameObject[] streetLights;
        public GameObject[] fences;
        public GameObject[] transitStops;
        public GameObject[] smallProps;

        public bool HasNature => Has(trees) || Has(bushes) || Has(grass) || Has(flowers) || Has(rocks);
        public bool HasCity => Has(buildings) || Has(streetLights) || Has(fences) || Has(transitStops) || Has(smallProps);

        static bool Has(GameObject[] prefabs) => prefabs != null && prefabs.Length > 0;
    }
}
