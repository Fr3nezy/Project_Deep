using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Misura il costo per frame del sistema creature in Play: tempo del main thread, script di Update e
    /// FixedUpdate, memoria allocata dalla GC. Dopo un riscaldamento campiona per qualche secondo e scrive le
    /// medie nella console e in <c>Logs/CreatureBenchmark.log</c> (fuori da Assets), così il risultato resta
    /// leggibile anche se la console è sommersa di log. Strumento di sviluppo: non serve in gioco.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureBenchmark : MonoBehaviour
    {
        [Tooltip("Secondi di attesa prima di campionare, per far assestare spawn e caricamenti.")]
        [SerializeField, Min(0f)] private float warmup = 3f;

        [Tooltip("Secondi di campionamento.")]
        [SerializeField, Min(0.5f)] private float duration = 10f;

        [Tooltip("Etichetta della misura nel file di log (es. \"vecchio sistema, 12 cubi\").")]
        [SerializeField] private string label = "creature";

        [Tooltip("Avvia la misura appena il componente si abilita.")]
        [SerializeField] private bool runOnEnable;

        private ProfilerRecorder mainThread;
        private ProfilerRecorder behaviourUpdate;
        private ProfilerRecorder fixedBehaviourUpdate;
        private ProfilerRecorder gcAlloc;

        private bool running;
        private float startTime;
        private int frames;
        private double sumMain, sumUpdate, sumFixed, sumGc, maxMain;

        /// <summary>True mentre la misura è in corso (riscaldamento compreso).</summary>
        public bool IsRunning => running;

        /// <summary>Ultimo risultato in forma di riga di testo, vuoto finché non c'è una misura conclusa.</summary>
        public string LastResult { get; private set; } = "";

        private void OnEnable()
        {
            if (runOnEnable) Run(label);
        }

        private void OnDisable()
        {
            DisposeRecorders();
            running = false;
        }

        /// <summary>Avvia una misura con l'etichetta indicata; se ne è in corso un'altra la riavvia.</summary>
        public void Run(string measureLabel)
        {
            label = measureLabel;
            DisposeRecorders();
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            behaviourUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate", 1);
            fixedBehaviourUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FixedBehaviourUpdate", 1);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            frames = 0;
            sumMain = sumUpdate = sumFixed = sumGc = maxMain = 0;
            startTime = Time.unscaledTime;
            running = true;
        }

        [ContextMenu("Run")]
        private void RunFromMenu() => Run(label);

        private void Update()
        {
            if (!running) return;
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < warmup) return;

            // I recorder restituiscono l'ultimo frame concluso; i tempi sono in nanosecondi.
            double main = mainThread.LastValue * 1e-6;
            sumMain += main;
            if (main > maxMain) maxMain = main;
            sumUpdate += behaviourUpdate.LastValue * 1e-6;
            sumFixed += fixedBehaviourUpdate.LastValue * 1e-6;
            sumGc += gcAlloc.LastValue / 1024.0;
            frames++;

            if (elapsed >= warmup + duration) Finish();
        }

        private void Finish()
        {
            running = false;
            DisposeRecorders();
            if (frames == 0) return;

            LastResult = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm} | {1} | {2} frame | main {3:F2} ms (max {4:F2}) | Update {5:F3} ms | FixedUpdate {6:F3} ms | GC {7:F1} KB/frame | {8}",
                System.DateTime.Now, label, frames, sumMain / frames, maxMain, sumUpdate / frames, sumFixed / frames,
                sumGc / frames, Application.isEditor ? "editor" : "player");
            Debug.Log("[CreatureBenchmark] " + LastResult);

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "CreatureBenchmark.log"), LastResult + "\n");
        }

        private void DisposeRecorders()
        {
            mainThread.Dispose();
            behaviourUpdate.Dispose();
            fixedBehaviourUpdate.Dispose();
            gcAlloc.Dispose();
        }
    }
}
