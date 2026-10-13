using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Dati di una specie: quale prefab usare, come si muove e quanto è sensibile a luce e rumore. Un profilo per
    /// specie, condiviso da tutte le istanze; il pool del <see cref="CreatureManager"/> è indicizzato per profilo.
    /// </summary>
    [CreateAssetMenu(fileName = "Creature Profile", menuName = "Deeploration/Creature Profile")]
    public class CreatureProfile : ScriptableObject
    {
        [Header("Specie")]
        [Tooltip("Prefab istanziato dal pool. Deve avere il componente Creature alla radice.")]
        public Creature prefab;

        [Header("Movimento")]
        [Tooltip("Velocità di crociera mentre vaga (m/s)")]
        [Range(0.1f, 10f)]
        public float cruiseSpeed = 1.2f;

        [Tooltip("Velocità di fuga (m/s)")]
        [Range(0.5f, 20f)]
        public float fleeSpeed = 4f;

        [Tooltip("Quanto in fretta cambia velocità (m/s²)")]
        [Range(0.1f, 20f)]
        public float acceleration = 3f;

        [Tooltip("Velocità massima di virata (gradi al secondo)")]
        [Range(10f, 720f)]
        public float turnRate = 90f;

        [Tooltip("Altezza preferita sopra il fondale (m)")]
        [Range(0f, 30f)]
        public float heightAboveFloor = 2.5f;

        [Tooltip("Scarto dall'altezza preferita entro cui non corregge la quota (m)")]
        [Range(0f, 10f)]
        public float heightTolerance = 1f;

        [Header("Vagare")]
        [Tooltip("Virata massima del vagare (gradi al secondo): più alta, traiettoria più nervosa")]
        [Range(0f, 360f)]
        public float wanderTurn = 45f;

        [Tooltip("Frequenza dei cambi di direzione del vagare (Hz)")]
        [Range(0.01f, 2f)]
        public float wanderFrequency = 0.15f;

        [Header("Percezione")]
        [Tooltip("Moltiplica la portata delle luci percepite (torcia, prato di penne, creature luminose). 0 = cieca alla luce")]
        [Range(0f, 3f)]
        public float lightSensitivity = 1f;

        [Tooltip("Moltiplica la portata dei rumori percepiti (passi, atterraggi). 0 = sorda")]
        [Range(0f, 3f)]
        public float hearingSensitivity = 1f;

        [Header("Reazione")]
        [Tooltip("Secondi senza percepire stimoli prima di tornare a vagare")]
        [Range(0f, 30f)]
        public float calmDelay = 3f;

        [Header("Corpo")]
        [Tooltip("Raggio d'ingombro usato per evitare gli ostacoli (m)")]
        [Range(0.05f, 5f)]
        public float bodyRadius = 0.3f;

        [Tooltip("Quanti secondi di strada guarda avanti per gli ostacoli")]
        [Range(0.2f, 5f)]
        public float lookAhead = 1.5f;
    }
}
