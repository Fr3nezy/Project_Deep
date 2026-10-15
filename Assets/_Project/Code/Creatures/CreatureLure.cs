using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Esca sulla spina della pinna dorsale: a riposo la catena di ossa resta piegata all'indietro come nel modello;
    /// con l'aggressività si alza e si piega in avanti fino a portare l'esca davanti alla bocca, del tutto mentre la
    /// creatura caccia o si lancia sul sub. Un ondeggiamento lento la fa penzolare, più ampio quando è distesa.
    /// La piega avviene attorno all'asse laterale della creatura, nel verso che porta ogni osso verso il muso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureLure : MonoBehaviour
    {
        [Tooltip("Ossa dell'esca dalla base alla punta (Lure_01 … Lure_04). L'asse Y locale è la direzione dell'osso.")]
        [SerializeField] private Transform[] bones;

        [Tooltip("Piega in avanti di ogni osso (gradi) a esca distesa, nello stesso ordine delle ossa. La prima alza la " +
                 "pinna dorsale e porta avanti tutta la spina, le altre la curvano sopra la testa.")]
        [SerializeField] private float[] forwardAngles = { 60f, 45f, 40f, 30f };

        [Tooltip("Velocità con cui l'esca insegue la posa voluta (1/s).")]
        [SerializeField, Min(0.1f)] private float responsiveness = 1.5f;

        [Tooltip("Ampiezza dell'ondeggiamento (gradi per osso) a esca distesa; a riposo il 30%.")]
        [SerializeField, Range(0f, 30f)] private float swayAngle = 6f;

        [Tooltip("Ondeggiamenti al secondo.")]
        [SerializeField, Min(0f)] private float swayFrequency = 0.5f;

        [Tooltip("Renderer della creatura (opzionale): se assegnato, le ossa vengono scritte solo quando è visibile.")]
        [SerializeField] private Renderer visibilityRenderer;

        private Creature creature;
        private Quaternion[] restLocal;
        private float bendSign = 1f;
        // Asse laterale nello spazio dell'osso a cui è attaccata l'esca (Spine_01): segue il corpo che ondeggia.
        private Vector3 lateralInBase;
        private Transform baseParent;
        private float extension;
        private float swayPhase;

        /// <summary>Quanto è distesa l'esca, da 0 (riposo, all'indietro) a 1 (davanti alla bocca).</summary>
        public float Extension => extension;

        private void Awake()
        {
            creature = GetComponent<Creature>();
            int count = bones != null ? bones.Length : 0;
            restLocal = new Quaternion[count];
            for (int i = 0; i < count; i++) restLocal[i] = bones[i].localRotation;
            // Verso della piega: ruotare la prima osso attorno all'asse laterale deve portarla verso il muso.
            if (count > 0)
            {
                Vector3 along = bones[0].up;
                bendSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(along, transform.forward), transform.right));
                if (bendSign == 0f) bendSign = 1f;
                baseParent = bones[0].parent;
                lateralInBase = baseParent != null
                    ? Quaternion.Inverse(baseParent.rotation) * transform.right
                    : transform.right;
            }
        }

        private void OnEnable()
        {
            extension = 0f;
            swayPhase = Random.value * Mathf.PI * 2f;
        }

        private void LateUpdate()
        {
            if (restLocal == null || restLocal.Length == 0) return;
            float target = 0f;
            if (creature != null)
            {
                switch (creature.State)
                {
                    case CreatureState.Strike:
                    case CreatureState.Hunt:
                        target = 1f;
                        break;
                    case CreatureState.Flee:
                        target = 0f;
                        break;
                    default:
                        target = creature.Aggression;
                        break;
                }
            }
            extension = Mathf.Lerp(extension, target, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));
            swayPhase += swayFrequency * 2f * Mathf.PI * Time.deltaTime;
            if (swayPhase > 1000f) swayPhase -= 2f * Mathf.PI * 159f;

            if (visibilityRenderer != null && !visibilityRenderer.isVisible) return;

            Vector3 lateral = (baseParent != null ? baseParent.rotation * lateralInBase : transform.right) * bendSign;
            float swayScale = Mathf.Lerp(0.3f, 1f, extension);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                bone.localRotation = restLocal[i];
                float angle = (i < forwardAngles.Length ? forwardAngles[i] : 0f) * extension +
                              swayAngle * swayScale * Mathf.Sin(swayPhase - i * 0.6f);
                Vector3 axisLocal = Quaternion.Inverse(bone.rotation) * lateral;
                bone.localRotation = restLocal[i] * Quaternion.AngleAxis(angle, axisLocal);
            }
        }
    }
}
