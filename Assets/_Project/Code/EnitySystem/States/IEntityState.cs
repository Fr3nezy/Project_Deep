using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Interfaccia per gli stati comportamentali dell'entità.
    /// Implementa il State Pattern per gestire transizioni e logiche complesse.
    /// </summary>
    public interface IEntityState
    {
        /// <summary>
        /// Tipo di stato (per identificazione e debug).
        /// </summary>
        EntityState StateType { get; }

        /// <summary>
        /// Chiamato quando l'entità entra in questo stato.
        /// </summary>
        /// <param name="context">Contesto contenente tutti i componenti necessari</param>
        void OnEnter(StateContext context);

        /// <summary>
        /// Chiamato ogni frame mentre l'entità è in questo stato.
        /// </summary>
        /// <param name="context">Contesto contenente tutti i componenti necessari</param>
        void OnUpdate(StateContext context);

        /// <summary>
        /// Chiamato ogni FixedUpdate mentre l'entità è in questo stato.
        /// </summary>
        /// <param name="context">Contesto contenente tutti i componenti necessari</param>
        void OnFixedUpdate(StateContext context);

        /// <summary>
        /// Chiamato quando l'entità esce da questo stato.
        /// </summary>
        /// <param name="context">Contesto contenente tutti i componenti necessari</param>
        void OnExit(StateContext context);

        /// <summary>
        /// Verifica se lo stato deve transizionare a un altro stato.
        /// </summary>
        /// <param name="context">Contesto contenente tutti i componenti necessari</param>
        /// <returns>Il nuovo stato se deve cambiare, null altrimenti</returns>
        IEntityState CheckTransitions(StateContext context);
    }

    /// <summary>
    /// Contesto condiviso tra tutti gli stati, contiene riferimenti ai componenti.
    /// Evita che ogni stato debba ottenere i componenti autonomamente.
    /// </summary>
    public class StateContext
    {
        // Componenti principali
        public EntityStatus EntityStatus { get; private set; }
        public MovementController MovementController { get; private set; }
        public SenseController SenseController { get; private set; }
        public EntityDebugGizmos DebugGizmos { get; private set; }
        public Transform Transform { get; private set; }

        // Stato condiviso
        public Transform CurrentTarget { get; set; }
        public EntityType CurrentTargetType { get; set; }
        public float StateTimer { get; set; }

        /// <summary>
        /// Costruttore del contesto.
        /// </summary>
        public StateContext(
            EntityStatus entityStatus,
            MovementController movementController,
            SenseController senseController,
            EntityDebugGizmos debugGizmos,
            Transform transform)
        {
            EntityStatus = entityStatus;
            MovementController = movementController;
            SenseController = senseController;
            DebugGizmos = debugGizmos;
            Transform = transform;
            StateTimer = 0f;
        }

        /// <summary>
        /// Resetta il timer dello stato.
        /// </summary>
        public void ResetTimer()
        {
            StateTimer = 0f;
        }

        /// <summary>
        /// Incrementa il timer dello stato.
        /// </summary>
        public void IncrementTimer(float deltaTime)
        {
            StateTimer += deltaTime;
        }

        /// <summary>
        /// Pulisce il target corrente.
        /// </summary>
        public void ClearTarget()
        {
            CurrentTarget = null;
            CurrentTargetType = EntityType.None;
        }

        /// <summary>
        /// Imposta un nuovo target.
        /// </summary>
        public void SetTarget(Transform target, EntityType targetType)
        {
            CurrentTarget = target;
            CurrentTargetType = targetType;
        }
    }
}
