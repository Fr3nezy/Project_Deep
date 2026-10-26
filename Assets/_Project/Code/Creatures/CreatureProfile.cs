using UnityEngine;

namespace Deeploration.Creatures
{
    /// <summary>
    /// Dati di una specie: quale prefab usare, come si muove e quanto è sensibile a luce e rumore e come reagisce a ogni stimolo. Un profilo per
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

        [Tooltip("Vive sul fondale invece di nuotare: resta a heightAboveFloor dal terreno senza salire né scendere " +
                 "di rotta, e il corpo si inclina sulla pendenza. heightAboveFloor deve superare bodyRadius, " +
                 "altrimenti il corpo tocca il fondale e la creatura si blocca.")]
        public bool crawlsOnFloor;

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

        [Header("Reazioni")]
        [Tooltip("Reazione alla luce debole: torcia bassa, scia del prato di penne, creature luminose")]
        public StimulusReaction weakLightReaction = StimulusReaction.Approach;

        [Tooltip("Reazione alla luce forte e stabile: torcia piena")]
        public StimulusReaction strongLightReaction = StimulusReaction.Flee;

        [Tooltip("Reazione al flash: l'istante in cui si accende una luce forte")]
        public StimulusReaction flashReaction = StimulusReaction.Flee;

        [Tooltip("Reazione al rumore: passi, atterraggi")]
        public StimulusReaction noiseReaction = StimulusReaction.Flee;

        [Tooltip("Distanza preferita (m) da una luce debole quando si avvicina: ogni creatura sceglie il suo posto " +
                 "fra il 75% e il 130% di questa distanza")]
        [Range(0.5f, 15f)]
        public float inspectDistance = 2.5f;

        [Tooltip("Moltiplica la distanza preferita per la luce forte e il flash: una luce più intensa si osserva da " +
                 "più lontano, dove la luminosità è quella preferita")]
        [Range(1f, 5f)]
        public float strongLightDistanceScale = 2.2f;

        [Tooltip("Ogni quanti secondi, in media, cambia posto mentre osserva (±50%)")]
        [Range(0.5f, 30f)]
        public float inspectRepositionInterval = 5f;

        [Tooltip("Secondi senza percepire stimoli prima di calmarsi (da Flee a Inspect o Wander, da Inspect a Wander)")]
        [Range(0f, 30f)]
        public float calmDelay = 3f;

        [Header("Banco")]
        [Tooltip("Raggio (m) entro cui vede i compagni della stessa specie. 0 = solitaria, niente regole di banco. " +
                 "Arriva al massimo a 3 celle della griglia del CreatureManager (neighbourCellSize)")]
        [Range(0f, 10f)]
        public float schoolRadius = 0f;

        [Tooltip("Sotto questa distanza (m) da un compagno se ne allontana (separazione), in ogni stato")]
        [Range(0.05f, 5f)]
        public float separationDistance = 0.6f;

        [Tooltip("Quanto piega verso il baricentro dei compagni mentre vaga (coesione)")]
        [Range(0f, 3f)]
        public float cohesionWeight = 0.5f;

        [Tooltip("Quanto allinea la rotta a quella dei compagni mentre vaga o scappa (allineamento)")]
        [Range(0f, 3f)]
        public float alignmentWeight = 0.5f;

        [Header("Sub")]
        [Tooltip("Spazio personale del sub (m) attorno al corpo, dai piedi alla testa: la creatura non ci entra. " +
                 "0 = nessuno (per un predatore che deve arrivare a contatto)")]
        [Range(0f, 5f)]
        public float diverClearance = 1f;

        [Header("Predatore")]
        [Tooltip("Entro questa distanza (m) dal sub gli gira attorno (stato Stalk), anche al buio: lo sente senza " +
                 "vederlo. 0 = non è un predatore del sub")]
        [Range(0f, 60f)]
        public float stalkRange = 0f;

        [Tooltip("Raggio dell'orbita (m) attorno al sub ad aggressività 0, al buio")]
        [Range(1f, 30f)]
        public float stalkRadius = 8f;

        [Tooltip("Raggio dell'orbita (m) ad aggressività piena: la distanza da cui colpirà")]
        [Range(0.5f, 10f)]
        public float strikeRadius = 2.5f;

        [Tooltip("Aggressività guadagnata al secondo sotto la luce forte del sub; la luce debole e il rumore valgono metà")]
        [Range(0f, 2f)]
        public float aggressionRate = 0.15f;

        [Tooltip("Aggressività persa al secondo quando non percepisce né luce né rumore del sub")]
        [Range(0f, 2f)]
        public float aggressionDecay = 0.1f;

        [Tooltip("Velocità dello slancio (m/s), verso il sub nel morso e verso la preda nell'ultimo tratto della caccia")]
        [Range(0.5f, 20f)]
        public float strikeSpeed = 5f;

        [Tooltip("Distanza (m) fra la testa e il bersaglio a cui il morso va a segno")]
        [Range(0.1f, 3f)]
        public float biteReach = 0.5f;

        [Tooltip("Secondi dopo un morso prima che possa lanciarsi di nuovo sul sub")]
        [Range(0f, 120f)]
        public float strikeCooldown = 20f;

        [Header("Caccia")]
        [Tooltip("Specie che caccia. Vuoto = non caccia")]
        public CreatureProfile prey;

        [Tooltip("Entro questa distanza (m) nota una preda e la insegue")]
        [Range(0f, 30f)]
        public float huntRange = 5f;

        [Tooltip("Caccia solo se l'aggressività verso il sub non supera questo valore: quando il sub lo provoca, il sub ha la precedenza")]
        [Range(0f, 1f)]
        public float huntMaxAggression = 0.6f;

        [Tooltip("Sotto questa distanza (m) dalla preda passa dall'inseguimento allo slancio")]
        [Range(0.2f, 10f)]
        public float lungeDistance = 1.5f;

        [Tooltip("Portata (m) entro cui le prede lo sentono arrivare: un rumore che le fa scappare")]
        [Range(0f, 10f)]
        public float preyAlertRadius = 1.2f;

        [Tooltip("Secondi di sazietà dopo una preda presa, in cui non caccia")]
        [Range(0f, 300f)]
        public float satiation = 30f;

        [Header("Spavento")]
        [Tooltip("Secondi di scatto all'inizio della fuga: virata e accelerazione moltiplicate per 6, corpo piegato a C")]
        [Range(0f, 2f)]
        public float startleDuration = 0.35f;

        [Tooltip("Lampo dei fotofori all'inizio della fuga: l'emissione arriva a (1 + questo valore) volte la normale. " +
                 "0 = nessun lampo")]
        [Range(0f, 10f)]
        public float startleGlow = 0f;

        [Tooltip("Secondi in cui il lampo si spegne")]
        [Range(0.05f, 5f)]
        public float startleGlowDuration = 1.2f;

        [Tooltip("Portata (m) del lampo come stimolo Flash per le altre creature: l'allarme che si propaga nel banco. " +
                 "0 = il lampo non è uno stimolo")]
        [Range(0f, 20f)]
        public float alarmRadius = 0f;

        [Header("Corpo")]
        [Tooltip("Raggio d'ingombro usato per evitare gli ostacoli (m)")]
        [Range(0.05f, 5f)]
        public float bodyRadius = 0.3f;

        [Tooltip("Quanti secondi di strada guarda avanti per gli ostacoli")]
        [Range(0.2f, 5f)]
        public float lookAhead = 1.5f;

        /// <summary>Reazione della specie alla categoria di stimolo indicata.</summary>
        public StimulusReaction ReactionTo(StimulusCategory category) => category switch
        {
            StimulusCategory.WeakLight => weakLightReaction,
            StimulusCategory.StrongLight => strongLightReaction,
            StimulusCategory.Flash => flashReaction,
            StimulusCategory.Noise => noiseReaction,
            _ => StimulusReaction.Ignore
        };
    }
}
