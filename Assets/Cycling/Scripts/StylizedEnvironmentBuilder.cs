using UnityEngine;

namespace CalgaryCycling
{
    public enum StylizedZone { Riverfront, Downtown, Evergreen }

    // Builds near-field dressing around the real route centreline. Imported
    // prefabs provide visual detail; generated lines preserve route geometry.
    public sealed class StylizedEnvironmentBuilder
    {
        readonly RideSettings settings;
        readonly StylizedQualityProfile quality;
        readonly StylizedAssetLibrary assets;
        readonly Transform root;
        readonly Material ground, road, sidewalk, routeEdge, routeArrow, water;
        readonly System.Collections.Generic.List<Material> ownedMaterials = new System.Collections.Generic.List<Material>();
        readonly System.Collections.Generic.List<Mesh> ownedMeshes = new System.Collections.Generic.List<Mesh>();
        RouteClearance clearance;

        public StylizedEnvironmentBuilder(RideSettings config, Transform parent)
        {
            settings = config;
            quality = config.stylizedQuality;
            assets = config.stylizedAssets;
            root = new GameObject("Imported Stylized Calgary Environment").transform;
            root.SetParent(parent, false);
            ground = Mat("Near-field Bow River ground", new Color(.34f, .48f, .29f));
            road = Mat("Riverfront cycle path", new Color(.73f, .48f, .31f));
            sidewalk = Mat("Warm sandstone sidewalk", new Color(.82f, .65f, .43f));
            routeEdge = Mat("Route edge", new Color(.16f, .82f, .72f));
            routeArrow = Mat("Route arrows", new Color(.72f, 1f, .39f));
            water = Mat("Bow River", new Color(.08f, .38f, .48f));
        }

        public Transform Build(RoutePath route, StylizedZone zone, bool includeRoute)
        {
            ClearChildren();
            clearance = new RouteClearance(route, settings.corridorHalfWidth + 1f);
            if (includeRoute) BuildRouteRibbon(route);
            if (assets == null)
            {
                Debug.LogWarning("[StylizedEnvironment] No imported asset library is assigned. Run Calgary Cycling > Assets > Create Imported Stylized Library.");
                return root;
            }
            if (zone == StylizedZone.Riverfront) BuildRiverfront(route);
            else if (zone == StylizedZone.Downtown) BuildDowntown(route);
            else BuildEvergreen(route);
            return root;
        }

        void BuildRouteRibbon(RoutePath route)
        {
            // Keep the returned route centreline unchanged, but give it a broad
            // near-field ground bed so the ride never reads as a floating ribbon
            // when Cesium terrain is still streaming or has small height error.
            StripMesh("Near-field route ground", ground, route, 30f, 0, 4f, -.10f);
            StripMesh("Real route cycle path", road, route, settings.corridorHalfWidth * 2f + .8f, 0, 2.5f, .025f);
            StripMesh("Warm path shoulder left", sidewalk, route, .42f, settings.corridorHalfWidth + .48f, 2.5f, .025f);
            StripMesh("Warm path shoulder right", sidewalk, route, .42f, -(settings.corridorHalfWidth + .48f), 2.5f, .025f);
            float markerStep = quality == null ? 22f : quality.routeMarkerStep;
            for (float d = 8; d < route.Length; d += markerStep)
            {
                Vector3 p = route.Position(d) + Vector3.up * .05f;
                Vector3 f = route.Forward(d);
                Part("Mint route marker", PrimitiveType.Cube, p + f * .25f, new Vector3(.18f, .035f, .8f), Quaternion.LookRotation(f, Vector3.up), routeArrow);
                Part("Route edge", PrimitiveType.Cube, route.Position(d) + route.Right(d) * (settings.corridorHalfWidth + .18f) + Vector3.up * .035f, new Vector3(.05f, .025f, 1.1f), Quaternion.LookRotation(f, Vector3.up), routeEdge);
                Part("Route edge", PrimitiveType.Cube, route.Position(d) - route.Right(d) * (settings.corridorHalfWidth + .18f) + Vector3.up * .035f, new Vector3(.05f, .025f, 1.1f), Quaternion.LookRotation(f, Vector3.up), routeEdge);
            }
        }

        void BuildRiverfront(RoutePath route)
        {
            // River geography comes from Cesium. A river following every turn of
            // an arbitrary address route would cross the trail at return bends.
            float step = quality == null ? 15f : quality.dressingStep;
            int limit = quality == null ? 48 : quality.treeCountLimit;
            int created = 0;
            for (float d = 8; d < route.Length && created < limit; d += step, created++)
            {
                Vector3 c = route.Position(d);
                Vector3 r = route.Right(d);
                SpawnNature(Pick(assets.trees, d), c + r * 8.5f, route.Forward(d), .9f, d);
                SpawnNature(Pick(assets.bushes, d + 3), c - r * 6.5f, route.Forward(d), .75f, d + 3);
                SpawnNature(Pick(assets.grass, d + 5), c + r * 6.5f, route.Forward(d), .7f, d + 5);
                SpawnNature(Pick(assets.rocks, d + 7), c + r * 7.5f + Vector3.up * .05f, route.Forward(d), .45f, d + 7);
                if (quality == null || quality.enableFlowers) SpawnNature(Pick(assets.flowers, d + 11), c - r * 6f, route.Forward(d), .65f, d + 11);
                if (Mathf.RoundToInt(d) % 45 == 0) SpawnProp(Pick(assets.streetLights, d), c + r * 5.5f, route.Forward(d), .9f, d);
            }
            for (float d = 28; d < Mathf.Min(route.Length, 190f); d += 64f)
            {
                Vector3 c = route.Position(d); Vector3 r = route.Right(d); Vector3 f = route.Forward(d);
                SpawnBuilding(Pick(assets.buildings, d + 2), c + r * (settings.corridorHalfWidth + 13.5f), f, .9f, d + 2);
                SpawnBuilding(Pick(assets.buildings, d + 5), c - r * (settings.corridorHalfWidth + 13.5f), f, .9f, d + 5);
            }
            if (route.Length >= 20)
            {
                float stopDistance = Mathf.Min(32, route.Length - 4);
                SpawnProp(Pick(assets.transitStops, 1), route.Position(stopDistance) - route.Right(stopDistance) * 6.5f, route.Forward(stopDistance), .9f, stopDistance);
                float fenceDistance = Mathf.Min(62, route.Length - 4);
                SpawnProp(Pick(assets.fences, 2), route.Position(fenceDistance) + route.Right(fenceDistance) * 6.5f, route.Forward(fenceDistance), .9f, fenceDistance);
            }
        }

        void BuildDowntown(RoutePath route)
        {
            float step = quality == null ? 24f : quality.dressingStep * 2f;
            int limit = quality == null ? 32 : quality.buildingCountLimit;
            int created = 0;
            for (float d = 12; d < route.Length && created < limit; d += step, created++)
            {
                Vector3 c = route.Position(d); Vector3 r = route.Right(d); Vector3 f = route.Forward(d);
                SpawnBuilding(Pick(assets.buildings, d), c + r * (settings.corridorHalfWidth + 8f), f, 1f, d);
                SpawnBuilding(Pick(assets.buildings, d + 1), c - r * (settings.corridorHalfWidth + 8f), f, 1f, d + 1);
                if (Mathf.RoundToInt(d) % 32 == 0) SpawnProp(Pick(assets.streetLights, d), c + r * 3.8f, f, 1f, d);
                if (Mathf.RoundToInt(d) % 48 == 0) SpawnProp(Pick(assets.smallProps, d), c - r * 4.2f, f, 1f, d);
            }
        }

        void BuildEvergreen(RoutePath route)
        {
            float step = quality == null ? 14f : quality.dressingStep;
            int limit = quality == null ? 48 : quality.treeCountLimit;
            int created = 0;
            for (float d = 8; d < route.Length && created < limit; d += step, created++)
            {
                Vector3 c = route.Position(d); Vector3 r = route.Right(d);
                SpawnNature(Pick(assets.trees, d * .23f), c + r * (settings.corridorHalfWidth + 4f), route.Forward(d), 1f, d);
                SpawnNature(Pick(assets.trees, d * .31f + 2), c - r * (settings.corridorHalfWidth + 5.5f), route.Forward(d), 1.15f, d + 2);
                SpawnNature(Pick(assets.grass, d + 3), c + r * 3.1f, route.Forward(d), .85f, d + 3);
                SpawnNature(Pick(assets.rocks, d), c + r * 3.5f, route.Forward(d), .8f, d);
                if (quality == null || quality.enableFlowers) SpawnNature(Pick(assets.flowers, d + 4), c - r * 3.8f, route.Forward(d), .8f, d + 4);
            }
        }

        void SpawnBuilding(GameObject prefab, Vector3 position, Vector3 forward, float scale, float seed)
        {
            Spawn(prefab, "Imported stylized building", position + Vector3.up * .02f, Quaternion.LookRotation(forward, Vector3.up), scale, seed);
        }

        void SpawnNature(GameObject prefab, Vector3 position, Vector3 forward, float scale, float seed)
        {
            Spawn(prefab, "Imported Riverfront nature", position, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0, seed * 37f % 360f, 0), scale, seed);
        }

        void SpawnProp(GameObject prefab, Vector3 position, Vector3 forward, float scale, float seed)
        {
            Spawn(prefab, "Imported Calgary street prop", position, Quaternion.LookRotation(forward, Vector3.up), scale, seed);
        }

        void Spawn(GameObject prefab, string name, Vector3 position, Quaternion rotation, float scale, float seed)
        {
            if (prefab == null) return;
            var instance = Object.Instantiate(prefab, root);
            instance.name = name;
            instance.transform.localPosition = position + Vector3.up * settings.stylizedGroundOffset;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * scale;
            if (!RouteClearance.TryFootprint(instance.transform, root, out var bounds))
            {
                instance.SetActive(false); Release(instance); return;
            }
            // Seat the actual mesh base on the ground; imported pivots vary.
            instance.transform.localPosition += Vector3.up *
                (position.y + settings.stylizedGroundOffset - .10f - bounds.min.y);
            bool placed = false;
            // Use the requested prop facing to find the roadside, not the origin.
            Vector3 side = Vector3.Cross(Vector3.up, rotation * Vector3.forward).normalized;
            if (clearance != null)
            {
                // Try both sides deterministically. Every attempt checks all route segments.
                Vector3 original = instance.transform.localPosition;
                for (int attempt=0; attempt<9; attempt++)
                {
                    float shift=attempt==0 ? 0 : ((attempt+1)/2)*3f*(attempt%2==1?1:-1);
                    instance.transform.localPosition=original+side*shift;
                    RouteClearance.TryFootprint(instance.transform,root,out bounds);
                    if(!clearance.Overlaps(bounds)){placed=true;break;}
                }
            }
            else placed=true;
            if(!placed){instance.SetActive(false);Release(instance);return;}
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            var block = new MaterialPropertyBlock();
            float brightness = .94f + Mathf.Abs(Mathf.Sin(seed * .17f)) * .12f;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                NormalizeMaterials(renderer);
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Color.white * brightness);
                block.SetColor("_Color", Color.white * brightness);
                renderer.SetPropertyBlock(block);
            }
        }

        void NormalizeMaterials(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.shader == null || source.shader.name.Contains("Universal Render Pipeline")) continue;
                var replacement = new Material(lit) { name = source.name + " Quest Lit" };
                if (source.HasProperty("_MainTex")) replacement.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                if (source.HasProperty("_Color")) replacement.SetColor("_BaseColor", source.GetColor("_Color"));
                replacement.enableInstancing = true;
                ownedMaterials.Add(replacement);
                materials[i] = replacement;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }

        static GameObject Pick(GameObject[] prefabs, float seed)
        {
            if (prefabs == null || prefabs.Length == 0) return null;
            return prefabs[Mathf.Abs(Mathf.RoundToInt(seed * 17.31f)) % prefabs.Length];
        }

        void ClearChildren()
        {
            if(root==null)return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child=root.GetChild(i).gameObject;
                child.SetActive(false);
                Release(child);
            }
            foreach(var mesh in ownedMeshes)Release(mesh);
            ownedMeshes.Clear();
            foreach(var material in ownedMaterials)Release(material);
            ownedMaterials.Clear();
        }

        static void Release(Object value)
        {
            if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            ClearChildren();
            Release(ground);Release(road);Release(sidewalk);Release(routeEdge);Release(routeArrow);Release(water);
            if(root!=null)Release(root.gameObject);
        }

        LineRenderer Line(string name, Material material, float width, RoutePath route, float step, Vector3 offset)
        {
            var go = new GameObject(name, typeof(LineRenderer)); go.transform.SetParent(root, false);
            var line = go.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.alignment = LineAlignment.TransformZ; line.material = material; line.widthMultiplier = width;
            int count = Mathf.Max(2, Mathf.CeilToInt(route.Length / step) + 1); line.positionCount = count;
            for (int i = 0; i < count; i++) line.SetPosition(i, route.Position(Mathf.Min(route.Length, i * step)) + offset + Vector3.up * settings.stylizedGroundOffset);
            return line;
        }

        LineRenderer OffsetLine(string name, Material material, float width, RoutePath route, float step, float lateral, Vector3 offset)
        {
            var go = new GameObject(name, typeof(LineRenderer)); go.transform.SetParent(root, false);
            var line = go.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.alignment = LineAlignment.TransformZ; line.material = material; line.widthMultiplier = width;
            int count = Mathf.Max(2, Mathf.CeilToInt(route.Length / step) + 1); line.positionCount = count;
            for (int i = 0; i < count; i++) { float d = Mathf.Min(route.Length, i * step); line.SetPosition(i, route.Position(d) + route.Right(d) * lateral + offset + Vector3.up * settings.stylizedGroundOffset); }
            return line;
        }

        void StripMesh(string name, Material material, RoutePath route, float width, float lateral, float step)
        {
            StripMesh(name, material, route, width, lateral, step, .025f);
        }

        void StripMesh(string name, Material material, RoutePath route, float width, float lateral, float step, float verticalOffset)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(route.Length / step) + 1);
            var vertices = new Vector3[count * 2];
            var triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                float d = Mathf.Min(route.Length, i * step);
                Vector3 centre = route.Position(d) + route.Right(d) * lateral + Vector3.up * (verticalOffset + settings.stylizedGroundOffset);
                Vector3 right = route.Right(d) * (width * .5f);
                vertices[i * 2] = centre - right;
                vertices[i * 2 + 1] = centre + right;
            }
            int t = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2;
                triangles[t++] = a; triangles[t++] = a + 2; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = a + 2; triangles[t++] = a + 3;
            }
            var mesh = new Mesh { name = name + " Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ownedMeshes.Add(mesh);
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        GameObject Part(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(root, false); go.transform.localPosition = position; go.transform.localRotation = rotation; go.transform.localScale = scale; Release(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        static Material Mat(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = name, color = color }; material.enableInstancing = true; return material;
        }
    }
}
