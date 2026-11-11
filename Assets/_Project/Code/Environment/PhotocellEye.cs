using UnityEngine;

namespace Deeploration.Environment
{
    /// <summary>
    /// Occhio della fotocellula: ruota un pivot verso la luce più forte che arriva alla <see cref="Photocell"/>
    /// (anche sotto soglia, così la torcia accesa basta a farsi guardare), entro un cono attorno alla posa di
    /// riposo, a velocità limitata. Senza luce scansiona lento. Accende lo schermo in ambra con la luce ricevuta.
    /// Solo estetica: lo scatto resta tutto in <see cref="Photocell"/>, disattivare questo componente non lo tocca.
    /// </summary>
    public sealed class PhotocellEye : MonoBehaviour
    {
        [Tooltip("Fotocellula da cui leggere la luce. Se vuoto, cercata sui genitori.")]
        [SerializeField] private Photocell photocell;

        [Tooltip("Pivot che ruota: vuoto centrato nella sfera, +Z è la direzione dello sguardo a riposo.")]
        [SerializeField] private Transform eyePivot;

        [Tooltip("Angolo massimo dalla posa di riposo, in gradi.")]
        [SerializeField, Range(0f, 180f)] private float maxAngle = 60f;

        [Tooltip("Velocità massima di rotazione verso la luce, in gradi al secondo.")]
        [SerializeField, Min(0f)] private float turnSpeed = 90f;

        [Header("Scansione a vuoto")]
        [SerializeField, Min(0f)] private float scanAmplitude = 25f;
        [SerializeField, Min(0f)] private float scanSpeed = 0.35f;
        [SerializeField, Min(0f)] private float scanTurnSpeed = 30f;

        [Header("Schermo")]
        [Tooltip("Renderer dello schermo CRT dell'occhio. Opzionale. Il materiale deve avere l'emissione attiva.")]
        [SerializeField] private Renderer screenRenderer;
        [SerializeField, ColorUsage(false, true)] private Color screenColor = new Color(1f, 0.55f, 0.1f);
        [Tooltip("Emissione a vuoto, frazione del pieno.")]
        [SerializeField, Range(0f, 1f)] private float idleGlow = 0.15f;
        [Tooltip("Punteggio di luce ricevuta che porta lo schermo al pieno. La torcia vicina fa qualche decina.")]
        [SerializeField, Min(0.01f)] private float fullGlowScore = 30f;
        [SerializeField, Min(0f)] private float maxEmission = 4f;
        [Tooltip("Velocità con cui l'emissione segue la luce, frazione al secondo.")]
        [SerializeField, Min(0f)] private float glowResponse = 6f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Quaternion restLocalRotation;
        private MaterialPropertyBlock block;
        private float glow;
        private float scanPhase;

        private void Awake()
        {
            if (photocell == null) photocell = GetComponentInParent<Photocell>();
            if (eyePivot == null) eyePivot = transform;
            restLocalRotation = eyePivot.localRotation;
            scanPhase = Random.value * 100f;
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            Light target = photocell != null ? photocell.BrightestLight : null;
            float score = photocell != null ? photocell.BrightestScore : 0f;

            Quaternion desired;
            float speed;
            if (target != null)
            {
                desired = LookRotationClamped(target.transform.position);
                speed = turnSpeed;
            }
            else
            {
                float t = Time.time * scanSpeed + scanPhase;
                desired = restLocalRotation * Quaternion.Euler(
                    Mathf.Sin(t * 0.7f) * scanAmplitude * 0.4f, Mathf.Sin(t) * scanAmplitude, 0f);
                speed = scanTurnSpeed;
            }
            eyePivot.localRotation = Quaternion.RotateTowards(eyePivot.localRotation, desired, speed * Time.deltaTime);

            float targetGlow = Mathf.Max(idleGlow, Mathf.Clamp01(score / fullGlowScore));
            glow = Mathf.MoveTowards(glow, targetGlow, glowResponse * Time.deltaTime);
            if (screenRenderer != null)
            {
                screenRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, screenColor * (glow * maxEmission));
                screenRenderer.SetPropertyBlock(block);
            }
        }

        // Rotazione locale che guarda il punto, limitata a maxAngle dalla posa di riposo: un bersaglio
        // alle spalle porta l'occhio al bordo del cono, senza ribaltarlo.
        private Quaternion LookRotationClamped(Vector3 worldPoint)
        {
            Transform parent = eyePivot.parent;
            Vector3 toTarget = worldPoint - eyePivot.position;
            if (parent != null) toTarget = parent.InverseTransformDirection(toTarget);
            if (toTarget.sqrMagnitude < 0.0001f) return eyePivot.localRotation;

            Vector3 restForward = restLocalRotation * Vector3.forward;
            Vector3 dir = Vector3.RotateTowards(restForward, toTarget.normalized, maxAngle * Mathf.Deg2Rad, 0f);
            return Quaternion.LookRotation(dir, restLocalRotation * Vector3.up);
        }
    }
}
