using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Dati di una specie: quale prefab usare, come si muove, cosa percepisce e quando scappa. Un profilo per
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
        [Tooltip("Distanza massima a cui vede il sub dentro il cono visivo (m)")]
        [Range(0f, 60f)]
        public float sightRange = 12f;

        [Tooltip("Ampiezza del cono visivo (gradi)")]
        [Range(10f, 360f)]
        public float fovAngle = 150f;

        [Tooltip("Distanza a cui sente il sub in ogni direzione, anche senza vederlo (m)")]
        [Range(0f, 30f)]
        public float hearingRange = 3f;

        [Header("Paura")]
        [Tooltip("Se percepisce il sub più vicino di così, scappa (m)")]
        [Range(0f, 30f)]
        public float fleeDistance = 5f;

        [Tooltip("Secondi senza percepire il sub prima di tornare a vagare")]
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
