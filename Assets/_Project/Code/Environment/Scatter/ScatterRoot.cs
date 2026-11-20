using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Deeploration.Environment
{
    /// <summary>
    /// Radice degli oggetti generati dallo scatter di Geometry Nodes. I figli si ricreano da zero
    /// dal JSON esportato da Blender: non vanno ritoccati a mano, si perdono alla rigenerazione.
    /// La radice va tenuta con la stessa trasformazione dell'istanza della mappa (di solito l'origine).
    /// </summary>
    [DisallowMultipleComponent]
    public class ScatterRoot : MonoBehaviour
    {
        [Tooltip("<blend>.scatter.json scritto da export_scatter.py.")]
        public TextAsset instances;
        public ScatterPalette palette;
        [Tooltip("Rigenera da solo quando il JSON viene reimportato.")]
        public bool autoRegenerate = true;

        [Serializable] private class Item { public string asset; public string host; public float[] p; public float[] q; public float[] s; }
        [Serializable] private class Data { public string source; public Item[] instances; }

        /// <summary>
        /// Blender (destrorso, Z su) → Unity (sinistrorso, Y su): (x, y, z) → (-x, z, -y).
        /// Verificato sulla mappa e sulla prova ScatterSpike il 2026-11-17.
        /// </summary>
        public static Matrix4x4 BlenderToUnity(Vector3 p, Quaternion q, Vector3 s)
        {
            var c = new Matrix4x4(
                new Vector4(-1f, 0f, 0f, 0f),
                new Vector4(0f, 0f, -1f, 0f),
                new Vector4(0f, 1f, 0f, 0f),
                new Vector4(0f, 0f, 0f, 1f)); // colonne: x→-x, y→-z, z→y
            return c * Matrix4x4.TRS(p, q, s) * c.inverse;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Svuota e ricrea i figli in un unico passo di Undo. Ritorna il numero di oggetti creati,
        /// -1 se JSON o palette mancano o non si leggono (in quel caso i figli restano come sono).
        /// </summary>
        public int Regenerate()
        {
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                Debug.LogError($"[Scatter] {name}: la radice non può stare dentro un'istanza di prefab, niente rigenerato.", this);
                return -1;
            }
            if (instances == null || palette == null)
            {
                Debug.LogError($"[Scatter] {name}: manca il JSON o la palette, niente rigenerato.", this);
                return -1;
            }

            Data data;
            try { data = JsonUtility.FromJson<Data>(instances.text); }
            catch (Exception e)
            {
                Debug.LogError($"[Scatter] {name}: JSON illeggibile ({e.Message}), niente rigenerato.", this);
                return -1;
            }
            if (data?.instances == null)
            {
                Debug.LogError($"[Scatter] {name}: JSON senza istanze, niente rigenerato.", this);
                return -1;
            }

            Undo.SetCurrentGroupName("Rigenera scatter");
            int group = Undo.GetCurrentGroup();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);

            var missing = new Dictionary<string, int>();
            int created = 0;
            foreach (var it in data.instances)
            {
                var entry = palette.Find(it.asset);
                if (entry == null)
                {
                    missing.TryGetValue(it.asset, out int n);
                    missing[it.asset] = n + 1;
                    continue;
                }
                if (it.p == null || it.p.Length < 3 || it.q == null || it.q.Length < 4 || it.s == null || it.s.Length < 3)
                    continue;

                var m = BlenderToUnity(
                    new Vector3(it.p[0], it.p[1], it.p[2]),
                    new Quaternion(it.q[1], it.q[2], it.q[3], it.q[0]),
                    new Vector3(it.s[0], it.s[1], it.s[2]));

                var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, transform);
                var t = go.transform;
                Quaternion baseRot = entry.prefab.transform.localRotation;
                Vector3 baseScale = entry.prefab.transform.localScale;
                // shortcut: con scala non uniforme e prefab ruotato alla radice la composizione è approssimata; basta per flora e rocce.
                t.localPosition = m.GetColumn(3);
                t.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)) * Quaternion.Euler(entry.rotationOffset) * baseRot;
                t.localScale = Vector3.Scale(new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude), baseScale);
                Undo.RegisterCreatedObjectUndo(go, "Rigenera scatter");
                created++;
            }
            Undo.CollapseUndoOperations(group);

            foreach (var kv in missing)
                Debug.LogWarning($"[Scatter] {name}: sorgente '{kv.Key}' senza voce nella palette, saltate {kv.Value} istanze.", this);
            Debug.Log($"[Scatter] {name}: {created} oggetti da {instances.name}.", this);
            return created;
        }
#endif
    }
}
