#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Deeploration.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Deeploration.Editor
{
    /// <summary>
    /// Lancia Blender in background per esportare le istanze di Geometry Nodes di un .blend in
    /// &lt;nome&gt;.scatter.json (export_scatter.py). Il percorso di Blender sta in EditorPrefs.
    /// </summary>
    public static class ScatterExport
    {
        public const string BlenderPathKey = "Deeplonauts.Scatter.BlenderPath";
        // shortcut: default sulla macchina di Manu; su un'altra macchina si imposta dal menu.
        private const string DefaultBlender = @"Z:\Blender\stable\blender-5.3.0-stable.02f0899d78e3\blender.exe";
        private const string ScriptPath = "Assets/_Project/Code/Editor/Scatter/export_scatter.py";
        private const int TimeoutMs = 120000;

        public static string BlenderPath
        {
            get => EditorPrefs.GetString(BlenderPathKey, DefaultBlender);
            set => EditorPrefs.SetString(BlenderPathKey, value);
        }

        public static string JsonPathFor(string blendAssetPath) =>
            Path.ChangeExtension(blendAssetPath, null) + ".scatter.json";

        /// <summary>
        /// Esporta e importa il JSON. False (con errore in console) se Blender manca, fallisce o va oltre 120 s:
        /// in quel caso il JSON precedente resta com'era.
        /// </summary>
        public static bool Run(string blendAssetPath)
        {
            string blender = BlenderPath;
            if (!File.Exists(blender))
            {
                Debug.LogError($"[Scatter] Blender non trovato in '{blender}'. Menu Deeplonauts/Scatter/Imposta percorso Blender.");
                return false;
            }

            var psi = new ProcessStartInfo(blender,
                // --factory-startup: niente add-on dell'utente, più veloce e uguale su ogni macchina.
                $"--background --factory-startup \"{Path.GetFullPath(blendAssetPath)}\" --python \"{Path.GetFullPath(ScriptPath)}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            string output;
            int exitCode;
            try
            {
                EditorUtility.DisplayProgressBar("Scatter", $"Export delle istanze da {Path.GetFileName(blendAssetPath)}", 0.5f);
                using var proc = Process.Start(psi);
                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit(TimeoutMs))
                {
                    proc.Kill();
                    Debug.LogError($"[Scatter] Blender oltre {TimeoutMs / 1000} s su {blendAssetPath}: interrotto, JSON invariato.");
                    return false;
                }
                output = stdout.Result + stderr.Result;
                exitCode = proc.ExitCode;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Scatter] Avvio di Blender fallito: {e.Message}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (exitCode != 0 || !output.Contains("SCATTER_OK"))
            {
                Debug.LogError($"[Scatter] Export fallito su {blendAssetPath} (exit {exitCode}), JSON invariato.\n{Tail(output)}");
                return false;
            }

            bool unchanged = false;
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("SCATTER_WARN"))
                    Debug.LogWarning($"[Scatter] {line}");
                else if (line.StartsWith("SCATTER_OK") && line.EndsWith(" invariato"))
                    unchanged = true;
            }

            if (!unchanged)
                AssetDatabase.ImportAsset(JsonPathFor(blendAssetPath), ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static string Tail(string s) => s.Length > 2000 ? s.Substring(s.Length - 2000) : s;

        [MenuItem("Deeplonauts/Scatter/Esporta dal .blend selezionato")]
        private static void ExportSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".blend")) { Debug.LogWarning("[Scatter] Seleziona un file .blend nel Project."); return; }
            Run(path);
        }

        [MenuItem("Deeplonauts/Scatter/Imposta percorso Blender")]
        private static void SetBlenderPath()
        {
            string p = EditorUtility.OpenFilePanel("blender.exe", Path.GetDirectoryName(BlenderPath), "exe");
            if (!string.IsNullOrEmpty(p)) BlenderPath = p;
        }

        [MenuItem("Deeplonauts/Scatter/Rigenera tutto")]
        public static void RegenerateAll()
        {
            foreach (var root in ScatterPostprocessor.LoadedRoots())
                if (root.Regenerate() >= 0) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            ScatterPostprocessor.RefreshGroundProbes();
        }
    }

    /// <summary>
    /// Reimport di un .blend con accanto un .scatter.json → nuovo export (il primo JSON si crea dal menu).
    /// Reimport di un .scatter.json → rigenera le ScatterRoot che lo usano nelle scene aperte, senza salvarle.
    /// Il lavoro va in delayCall: dentro OnPostprocessAllAssets non si lanciano processi né import.
    /// </summary>
    public class ScatterPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingBlends = new HashSet<string>();
        private static readonly HashSet<string> PendingJsons = new HashSet<string>();
        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (path.EndsWith(".blend") && File.Exists(ScatterExport.JsonPathFor(path))) PendingBlends.Add(path);
                else if (path.EndsWith(".scatter.json")) PendingJsons.Add(path);
            }
            if ((PendingBlends.Count > 0 || PendingJsons.Count > 0) && !scheduled)
            {
                scheduled = true;
                EditorApplication.delayCall += Flush;
            }
        }

        private static void Flush()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Flush;
                return;
            }
            scheduled = false;

            var blends = new List<string>(PendingBlends); PendingBlends.Clear();
            foreach (var b in blends) ScatterExport.Run(b); // riempie PendingJsons tramite l'import del JSON

            var jsons = new List<string>(PendingJsons); PendingJsons.Clear();
            bool regenerated = false;
            foreach (var j in jsons)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(j);
                if (asset == null) continue;
                foreach (var root in LoadedRoots())
                    if (root.autoRegenerate && root.instances == asset && root.Regenerate() >= 0)
                    {
                        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                        regenerated = true;
                    }
            }
            if (blends.Count > 0 || regenerated) RefreshGroundProbes(); // il terreno può essere cambiato sotto rocce ferme o nuove
        }

        /// <summary>GroundBlendProbe rifà il raycast solo quando la roccia si muove: dopo un cambio di mappa va forzato.</summary>
        public static void RefreshGroundProbes()
        {
            Physics.SyncTransforms();
            foreach (var probe in Object.FindObjectsByType<GroundBlendProbe>(FindObjectsSortMode.None))
                probe.Probe();
        }

        public static List<ScatterRoot> LoadedRoots()
        {
            var list = new List<ScatterRoot>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                    list.AddRange(go.GetComponentsInChildren<ScatterRoot>(true));
            }
            return list;
        }
    }
}
#endif
