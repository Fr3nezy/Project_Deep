using System.Collections.Generic;
using Deeploration.Player;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class WireHelmet
{
    const string FbxPath = "Assets/_Project/Assets/Models/Helmet/Helmet.fbx";
    const string MatDir = "Assets/_Project/Assets/Models/Helmet/Materials_Placeholder";

    public static string ExtractMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Assets/Models/Helmet", "Materials_Placeholder");
        var log = new List<string>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
        {
            if (o is Material m && m.name.StartsWith("PH_"))
            {
                string err = AssetDatabase.ExtractAsset(m, MatDir + "/" + m.name + ".mat");
                log.Add(m.name + (string.IsNullOrEmpty(err) ? " ok" : " ERR " + err));
            }
        }
        AssetDatabase.WriteImportSettingsIfDirty(FbxPath);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        return string.Join("\n", log);
    }

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");

    static void Style(string name, Color baseColor, float metallic, float smooth, Color emission)
    {
        var m = Mat(name);
        if (m == null) return;
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        m.SetColor("_EmissionColor", emission);
        if (emission.maxColorComponent > 0f || name.StartsWith("PH_Lamp") || name == "PH_CRT_Bar") m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = emission.maxColorComponent > 0f || name.StartsWith("PH_Lamp") || name == "PH_CRT_Bar" ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(m);
    }

    public static string StyleMaterials()
    {
        Style("PH_Metal", new Color(0.5f, 0.48f, 0.45f), 0.6f, 0.4f, Color.black);
        Style("PH_Black", new Color(0.02f, 0.02f, 0.02f), 0f, 0.3f, Color.black);
        Style("PH_Face_Dark", new Color(0.03f, 0.03f, 0.03f), 0f, 0.2f, Color.black);
        Style("PH_Seg_Green", new Color(0.05f, 0.5f, 0.1f), 0f, 0.3f, new Color(0.05f, 0.5f, 0.1f) * 1.0f);
        Style("PH_Seg_Orange", new Color(0.8f, 0.3f, 0f), 0f, 0.3f, new Color(0.8f, 0.3f, 0f) * 1.0f);
        Style("PH_Seg_Red", new Color(0.7f, 0.05f, 0.03f), 0f, 0.3f, new Color(0.7f, 0.05f, 0.03f) * 1.0f);
        Style("PH_Needle", new Color(0.9f, 0.85f, 0.75f), 0f, 0.4f, new Color(0.9f, 0.85f, 0.75f) * 0.8f);
        Style("PH_LampO2", new Color(0.05f, 0.02f, 0f), 0f, 0.5f, Color.black);
        Style("PH_LampPress", new Color(0.05f, 0.02f, 0f), 0f, 0.5f, Color.black);
        Style("PH_Screen", new Color(0.02f, 0.012f, 0.004f), 0f, 0.1f, new Color(0.24f, 0.13f, 0.01f) * 0.45f);
        Style("PH_Decal", new Color(0.8f, 0.8f, 0.75f), 0f, 0.3f, Color.black);
        Style("PH_Compass", new Color(0.04f, 0.03f, 0.02f), 0f, 0.2f, new Color(1f, 0.69f, 0.25f) * 1f);

        var glass = Mat("PH_Glass");
        glass.shader = Shader.Find("Universal Render Pipeline/Lit");
        glass.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.03f, 0.10f));
        glass.SetFloat("_Smoothness", 0.95f);
        glass.SetFloat("_Surface", 1f);
        glass.SetFloat("_Blend", 0f);
        glass.SetFloat("_SrcBlend", 5f);
        glass.SetFloat("_DstBlend", 10f);
        glass.SetFloat("_ZWrite", 0f);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.renderQueue = 3000;
        EditorUtility.SetDirty(glass);

        string barPath = MatDir + "/PH_CRT_Bar.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(barPath) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), barPath);
        Style("PH_CRT_Bar", new Color(0.02f, 0.012f, 0.004f), 0f, 0.1f, Color.black);
        AssetDatabase.SaveAssets();
        return "stili ok";
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static TextMeshPro MakeText(Transform parent, string name, string text, float capHeight, Vector2 anchor, Quaternion rot, Vector3 origin,
        Vector3 gx, Vector3 gy)
    {
        var go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.position = origin + gx * anchor.x + gy * anchor.y;
        go.transform.rotation = rot;
        var t = go.GetComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 1f;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.color = new Color(1f, 0.69f, 0.25f);
        t.rectTransform.sizeDelta = new Vector2(0.03f, 0.01f);
        t.rectTransform.pivot = new Vector2(0f, 1f);
        t.ForceMeshUpdate();
        float lineHeight = t.GetPreferredValues("H").y; // altezza della riga a fontSize 1
        t.fontSize = capHeight / Mathf.Max(lineHeight, 1e-6f);
        return t;
    }

    public static string WirePrefab()
    {
        const string path = "Assets/_Project/Prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var helmet = Find(root.transform, "HelmetVisualPivot").Find("Helmet");
            var comp = helmet.GetComponent<HelmetInstruments>() ?? helmet.gameObject.AddComponent<HelmetInstruments>();

            // Il vecchio CTR_Texts, se c'è, si rifà da capo.
            var old = helmet.Find("CTR_Texts");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Telaio dello schermo del CRT in spazio Unity (da Blender: (x, y, z) -> (-x, z, -y)).
            Vector3 gn = new Vector3(0.4130f, 0.5050f, -0.7579f).normalized;
            Vector3 gx = new Vector3(0.8260f, 0.1427f, 0.5453f).normalized;
            Vector3 gy = new Vector3(-0.3836f, 0.8512f, 0.3582f).normalized;
            Vector3 ctr = helmet.InverseTransformPoint(Find(helmet, "MSH_CTR_Screen").GetComponent<Renderer>().bounds.center);
            // Coordinate nello spazio dell'FBX, con la radice Helmet come origine (stessa del modello).
            Quaternion rot = helmet.rotation * Quaternion.LookRotation(-gn, gy);
            Vector3 O(Vector3 local) => helmet.TransformPoint(local);
            Vector3 origin = O(ctr + gn * 0.0005f);
            Vector3 wx = helmet.TransformDirection(gx), wy = helmet.TransformDirection(gy);

            var group = new GameObject("CTR_Texts").transform;
            group.SetParent(helmet, false);

            float cap = 0.0042f;
            var lab = MakeText(group, "DepthLabel", "PROFONDITÀ", cap, new Vector2(-0.0148f, 0.0160f), rot, origin, wx, wy);
            var val = MakeText(group, "DepthValue", "2040 M", 0.0090f, new Vector2(-0.0148f, 0.0108f), rot, origin, wx, wy);
            var ll = MakeText(group, "LightLabel", "LUCE", cap, new Vector2(-0.0148f, -0.0010f), rot, origin, wx, wy);
            var lv = MakeText(group, "LightValue", "PIENA", cap, new Vector2(-0.0030f, -0.0010f), rot, origin, wx, wy);

            var bars = new List<Renderer>();
            var barMat = Mat("PH_CRT_Bar");
            for (int i = 0; i < 3; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "LightBar_" + i;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(group, false);
                q.transform.position = origin + wx * (-0.0128f + i * 0.0050f) + wy * -0.0078f;
                q.transform.rotation = rot;
                q.transform.localScale = new Vector3(0.0040f, 0.0022f, 1f);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = barMat;
                bars.Add(r);
            }

            var so = new SerializedObject(comp);
            so.FindProperty("oxygenNeedle").objectReferenceValue = Find(helmet, "Needle_O2_Pivot");
            so.FindProperty("pressureNeedle").objectReferenceValue = Find(helmet, "Needle_Press_Pivot");
            so.FindProperty("oxygenLamp").objectReferenceValue = Find(helmet, "MSH_Lamp_O2").GetComponent<Renderer>();
            so.FindProperty("pressureLamp").objectReferenceValue = Find(helmet, "MSH_Lamp_Press").GetComponent<Renderer>();
            so.FindProperty("depthLabel").objectReferenceValue = lab;
            so.FindProperty("depthValue").objectReferenceValue = val;
            so.FindProperty("lightLabel").objectReferenceValue = ll;
            so.FindProperty("lightValue").objectReferenceValue = lv;
            var barsProp = so.FindProperty("lightBars");
            barsProp.arraySize = bars.Count;
            for (int i = 0; i < bars.Count; i++) barsProp.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "prefab ok, testi: label=" + lab.fontSize.ToString("0.000") + " value=" + val.fontSize.ToString("0.000");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
