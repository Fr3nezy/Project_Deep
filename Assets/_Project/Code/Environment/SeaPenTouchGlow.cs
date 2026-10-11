using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Pubblica come dati globali degli shader gli ultimi punti in cui il Diver è passato sul fondale
    /// e la posizione attuale dei suoi piedi. I materiali <c>Deeplonauts/Coral</c> con Touch Glow attivo
    /// (le penne di mare) fanno partire da ogni punto un'onda di bioluminescenza; quelli con Push si
    /// piegano via dai piedi. Solo visivo: nessun collider, nessun effetto sul gameplay.
    /// Va sul GameObject che si muove con il Diver e ha il pivot ai piedi (<c>PlayerCapsule</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaPenTouchGlow : MonoBehaviour
    {
        /// <summary>Punti ricordati; deve coincidere con CORAL_TOUCH_POINTS in Coral.shader.</summary>
        public const int MaxPoints = 12;

        private static readonly int PointsId = Shader.PropertyToID("_SeaPenTouchPoints");
        private static readonly int CountId = Shader.PropertyToID("_SeaPenTouchCount");
        private static readonly int TimeId = Shader.PropertyToID("_SeaPenTouchTime");
        private static readonly int PlayerId = Shader.PropertyToID("_SeaPenPlayer");

        [Tooltip("Distanza percorsa (m) tra due punti di tocco: a passo normale ne nasce uno ogni ~0.4 s.")]
        [SerializeField, Min(0.05f)] private float stampSpacing = 0.4f;

        [Tooltip("Di quanto (m) il punto di tocco precede i piedi nella direzione di marcia, così le penne " +
                 "si accendono poco prima che il Diver ci arrivi (licenza stilistica).")]
        [SerializeField, Min(0f)] private float lookAhead = 0.8f;

        private readonly Vector4[] points = new Vector4[MaxPoints];
        private int next;
        private int count;
        private Vector3 lastStamp;

        private void OnEnable()
        {
            next = 0;
            count = 0;
            lastStamp = transform.position;
            Shader.SetGlobalVectorArray(PointsId, points);
            Shader.SetGlobalFloat(CountId, 0f);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(CountId, 0f);
            Shader.SetGlobalVector(PlayerId, Vector4.zero);
        }

        private void Update()
        {
            Vector3 feet = transform.position;
            Vector3 step = feet - lastStamp;
            step.y = 0f;
            if (step.sqrMagnitude >= stampSpacing * stampSpacing)
            {
                lastStamp = feet;
                Touch(feet + step.normalized * lookAhead);
            }

            Shader.SetGlobalVector(PlayerId, new Vector4(feet.x, feet.y, feet.z, 1f));
            Shader.SetGlobalFloat(TimeId, Time.time);
        }

        /// <summary>Registra un tocco in <paramref name="position"/> adesso; sostituisce il più vecchio quando sono pieni.</summary>
        public void Touch(Vector3 position)
        {
            points[next] = new Vector4(position.x, position.y, position.z, Time.time);
            next = (next + 1) % MaxPoints;
            count = Mathf.Min(count + 1, MaxPoints);
            Shader.SetGlobalVectorArray(PointsId, points);
            Shader.SetGlobalFloat(CountId, count);
        }
    }
}
