#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deeploration.Editor
{
    /// <summary>
    /// Controlla che il prefab Player resti pulito e che le sue istanze nelle scene non accumulino correzioni:
    /// radice dritta e a scala 1, figli diretti centrati sui piedi, nessun prefab annidato tranne i modelli,
    /// nessuno script mancante, nessuna scala non uniforme; nelle scene radice non inclinata e override
    /// limitati alla posa della radice. Solo lettura: non modifica niente, scrive il resoconto in console.
    /// </summary>
    public static class PlayerPrefabValidator
    {
        private const string MenuPath = "Deeplonauts/Validate Player";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ScenesFolder = "Assets/_Project/Scenes";

        // Oltre questo numero di override (esclusa la posa della radice) un'istanza merita un'occhiata.
        private const int OverrideWarningThreshold = 6;
        private const float Tolerance = 0.001f;

        [MenuItem(MenuPath)]
        private static void Validate()
        {
            var report = new StringBuilder();
            int problems = 0;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Validate Player] Prefab non trovato: {PrefabPath}");
                return;
            }

            report.AppendLine($"== Prefab {PrefabPath}");
            problems += CheckPrefab(prefab, report);

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }))
                problems += CheckScene(AssetDatabase.GUIDToAssetPath(guid), prefab, report);

            string summary = problems == 0 ? "nessun problema" : $"{problems} problemi";
            string message = $"[Validate Player] {summary}\n{report}";
            if (problems == 0) Debug.Log(message);
            else Debug.LogWarning(message);
            EditorUtility.DisplayDialog("Validate Player", $"{summary}. Dettagli in console.", "OK");
        }

        private static int CheckPrefab(GameObject prefab, StringBuilder report)
        {
            int problems = 0;
            Transform root = prefab.transform;

            if (root.localRotation != Quaternion.identity || !IsOne(root.localScale))
                problems += Problem(report, "la radice deve avere rotazione zero e scala 1");

            foreach (Transform child in root)
            {
                Vector3 p = child.localPosition;
                if (Mathf.Abs(p.x) > Tolerance || Mathf.Abs(p.z) > Tolerance)
                    problems += Problem(report, $"{child.name} è spostato in orizzontale dalla radice ({p.x:F2}, {p.z:F2}): i figli diretti stanno sopra i piedi");
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = PathOf(t, root);

                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    problems += Problem(report, $"{path}: script mancante");

                if (t != root && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                    PrefabUtility.GetPrefabAssetType(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) !=
                    PrefabAssetType.Model)
                    problems += Problem(report, $"{path}: prefab annidato ({AssetPathOf(t.gameObject)}); il Player deve essere autonomo, solo i modelli restano collegati");

                if (!IsUniform(t.localScale))
                    report.AppendLine($"  avviso: {path} ha scala non uniforme {t.localScale}");
            }

            return problems;
        }

        private static int CheckScene(string scenePath, GameObject prefab, StringBuilder report)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            int problems = 0;
            try
            {
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) ||
                        PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != prefab)
                        continue;

                    report.AppendLine($"== {Path.GetFileNameWithoutExtension(scenePath)} / {t.name}");
                    problems += CheckInstance(t.gameObject, report);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
            return problems;
        }

        private static int CheckInstance(GameObject instance, StringBuilder report)
        {
            int problems = 0;
            Vector3 euler = instance.transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)) > 0.01f)
                problems += Problem(report, $"radice inclinata ({euler.x:F1}, {euler.z:F1}): solo la rotazione sull'asse verticale; le pose (per esempio da svenuto) vanno nei loro componenti");
            if (!IsOne(instance.transform.localScale))
                problems += Problem(report, "radice con scala diversa da 1");

            var others = new List<string>();
            foreach (PropertyModification m in PrefabUtility.GetPropertyModifications(instance))
            {
                if (m.target == null) continue;
                bool rootPose = (m.target == PrefabUtility.GetCorrespondingObjectFromSource(instance.transform) ||
                                 m.target == PrefabUtility.GetCorrespondingObjectFromSource(instance)) &&
                                (m.propertyPath.StartsWith("m_LocalPosition") || m.propertyPath.StartsWith("m_LocalRotation") ||
                                 m.propertyPath.StartsWith("m_LocalEulerAnglesHint") || m.propertyPath == "m_Name");
                if (rootPose) continue;
                string owner = m.target is Component c ? $"{c.gameObject.name}/{c.GetType().Name}" : m.target.name;
                others.Add($"{owner}.{m.propertyPath}");
            }

            report.AppendLine($"  override oltre la posa della radice: {others.Count}");
            foreach (string o in others) report.AppendLine($"    {o}");
            if (others.Count > OverrideWarningThreshold)
                problems += Problem(report, $"troppi override ({others.Count}): portare nel prefab quelli validi per tutte le scene");

            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(instance))
                problems += Problem(report, $"componente aggiunto solo in scena: {added.instanceComponent.GetType().Name} su {added.instanceComponent.gameObject.name}");
            foreach (RemovedComponent removed in PrefabUtility.GetRemovedComponents(instance))
                problems += Problem(report, $"componente tolto solo in scena: {removed.assetComponent.GetType().Name} su {removed.containingInstanceGameObject.name}");
            foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(instance))
                problems += Problem(report, $"oggetto aggiunto solo in scena: {added.instanceGameObject.name}");

            return problems;
        }

        private static int Problem(StringBuilder report, string message)
        {
            report.AppendLine($"  PROBLEMA: {message}");
            return 1;
        }

        private static bool IsOne(Vector3 scale) => (scale - Vector3.one).sqrMagnitude < Tolerance * Tolerance;

        private static bool IsUniform(Vector3 scale) =>
            Mathf.Abs(scale.x - scale.y) < Tolerance * Mathf.Abs(scale.x) + Tolerance &&
            Mathf.Abs(scale.x - scale.z) < Tolerance * Mathf.Abs(scale.x) + Tolerance;

        private static string PathOf(Transform t, Transform root) =>
            t == root ? root.name : $"{root.name}/{AnimationUtility.CalculateTransformPath(t, root)}";

        private static string AssetPathOf(GameObject instance) =>
            AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(instance));
    }
}
#endif
