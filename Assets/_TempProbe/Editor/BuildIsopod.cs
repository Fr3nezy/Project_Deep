using Deeploration.Creatures;
using UnityEditor;
using UnityEngine;

public static class BuildIsopod
{
    public static string Run()
    {
        string dir = "Assets/_Project/Entities";
        string matPath = dir + "/Materials/M_Isopod_Placeholder.mat";
        if (!AssetDatabase.IsValidFolder(dir + "/Materials")) AssetDatabase.CreateFolder(dir, "Materials");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.45f, 0.38f, 0.32f));
            mat.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        int entity = LayerMask.NameToLayer("Entity");

        var root = new GameObject("Creature_Isopod");
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0, -0.09f, 0);

        GameObject Prim(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = n;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        Prim(PrimitiveType.Sphere, "Head", visual.transform, new Vector3(0, 0, 0.17f), new Vector3(0.14f, 0.07f, 0.09f), Vector3.zero);
        Renderer vis = null;
        for (int i = 0; i < 7; i++)
        {
            float w = 0.17f - i * 0.008f;
            var s = Prim(PrimitiveType.Sphere, "Seg_" + i, visual.transform, new Vector3(0, 0.005f, 0.105f - i * 0.05f),
                new Vector3(w, 0.085f, 0.075f), Vector3.zero);
            if (i == 3) vis = s.GetComponent<Renderer>();
        }
        Prim(PrimitiveType.Sphere, "Tail", visual.transform, new Vector3(0, -0.005f, -0.235f), new Vector3(0.09f, 0.05f, 0.08f), Vector3.zero);

        var left = new Transform[7];
        var right = new Transform[7];
        for (int i = 0; i < 7; i++)
        {
            float z = 0.10f - i * 0.05f;
            float half = 0.075f - i * 0.003f;
            for (int s = -1; s <= 1; s += 2)
            {
                var pivot = new GameObject((s < 0 ? "LegL_" : "LegR_") + i).transform;
                pivot.SetParent(visual.transform, false);
                pivot.localPosition = new Vector3(s * half, -0.02f, z);
                pivot.localEulerAngles = new Vector3(0, 0, -35f * s);
                Prim(PrimitiveType.Cylinder, "Leg", pivot, new Vector3(s * 0.04f, 0, 0), new Vector3(0.012f, 0.04f, 0.012f), new Vector3(0, 0, 90));
                if (s < 0) left[i] = pivot; else right[i] = pivot;
            }
        }

        Transform[] Antenna(string n, float s)
        {
            var segs = new Transform[3];
            Transform parent = visual.transform;
            for (int k = 0; k < 3; k++)
            {
                var seg = new GameObject(n + "_" + k).transform;
                seg.SetParent(parent, false);
                seg.localPosition = k == 0 ? new Vector3(s * 0.04f, 0.015f, 0.21f) : new Vector3(0, 0, 0.05f);
                seg.localEulerAngles = k == 0 ? new Vector3(-10f, s * 25f, 0) : Vector3.zero;
                Prim(PrimitiveType.Cylinder, "Mesh", seg, new Vector3(0, 0, 0.025f), new Vector3(0.008f, 0.025f, 0.008f), new Vector3(90, 0, 0));
                segs[k] = seg;
                parent = seg;
            }
            return segs;
        }
        var aL = Antenna("AntL", -1f);
        var aR = Antenna("AntR", 1f);

        if (entity >= 0)
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = entity;
        root.AddComponent<Creature>();
        var crawl = root.AddComponent<CreatureCrawl>();
        var so = new SerializedObject(crawl);
        void SetArr(string name, Transform[] arr)
        {
            var p = so.FindProperty(name);
            p.arraySize = arr.Length;
            for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
        }
        SetArr("legsLeft", left);
        SetArr("legsRight", right);
        SetArr("antennaLeft", aL);
        SetArr("antennaRight", aR);
        so.FindProperty("visibilityRenderer").objectReferenceValue = vis;
        so.ApplyModifiedPropertiesWithoutUndo();

        string prefabPath = dir + "/Prefabs/Creature_Isopod.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        string profPath = dir + "/Profiles/Creature_Isopod.asset";
        var prof = ScriptableObject.CreateInstance<CreatureProfile>();
        prof.prefab = prefab.GetComponent<Creature>();
        prof.cruiseSpeed = 0.25f;
        prof.fleeSpeed = 1.2f;
        prof.acceleration = 1.5f;
        prof.turnRate = 100f;
        prof.heightAboveFloor = 0.15f;
        prof.heightTolerance = 0.05f;
        prof.crawlsOnFloor = true;
        prof.wanderTurn = 60f;
        prof.wanderFrequency = 0.25f;
        prof.lightSensitivity = 0.8f;
        prof.hearingSensitivity = 1.5f;
        prof.weakLightReaction = StimulusReaction.Ignore;
        prof.strongLightReaction = StimulusReaction.Flee;
        prof.flashReaction = StimulusReaction.Flee;
        prof.noiseReaction = StimulusReaction.Flee;
        prof.calmDelay = 4f;
        prof.startleDuration = 0.25f;
        prof.diverClearance = 0.6f;
        prof.bodyRadius = 0.12f;
        prof.lookAhead = 1f;
        AssetDatabase.CreateAsset(prof, profPath);
        AssetDatabase.SaveAssets();
        return "ok " + prefabPath + " entityLayer=" + entity;
    }
}
