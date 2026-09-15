using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di ispezione/avvicinamento lento.
    /// L'entità si avvicina gradualmente a un'entità rilevata nella Detection Zone
    /// per classificarla meglio prima di decidere il comportamento.
    /// </summary>
    public class InspectState : IEntityState
    {
        public EntityState StateType => EntityState.Inspect;

        private const float INSPECT_SPEED_MULTIPLIER = 0.4f; // 40% della velocità base

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();

            if (context.CurrentTarget != null)
            {
                context.DebugGizmos?.SetDebugTarget(context.CurrentTarget, false);
                Debug.Log($"[InspectState] {context.EntityStatus.Profile.creatureName} inizia ispezione di {context.CurrentTarget.gameObject.name}");
            }
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);
        }

        public void OnFixedUpdate(StateContext context)
        {
            if (context.CurrentTarget == null) return;

            // Movimento lento di avvicinamento
            Vector3 targetPos = context.CurrentTarget.position;
            float inspectSpeed = context.EntityStatus.Profile.speedBase * INSPECT_SPEED_MULTIPLIER;

            // Usa movimento normale ma con velocità ridotta
            context.MovementController.MoveTowards(targetPos, useAcceleration: false);

            // Override della velocità nel MovementController se possibile
            if (context.MovementController is MovementController movement)
            {
                // Riduci la velocità target
                Vector3 currentVelocity = movement.CurrentVelocity;
                if (currentVelocity.magnitude > inspectSpeed)
                {
                    movement.GetComponent<Rigidbody>().linearVelocity = currentVelocity.normalized * inspectSpeed;
                }
            }
        }

        public void OnExit(StateContext context)
        {
            context.DebugGizmos?.ClearDebugTarget();
            Debug.Log($"[InspectState] {context.EntityStatus.Profile.creatureName} termina ispezione");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            if (context.CurrentTarget == null) return new IdleState();

            // Priorità massima: pericolo immediato
            if (context.SenseController.HasThreats)
            {
                Transform predator = context.SenseController.ClosestPredator;
                if (predator != null && context.SenseController.IsPredatorTooClose(predator))
                {
                    return new FleeState();
                }
            }

            // Se target entra in zona di decisione, prendiamo una decisione
            FovZone zone = context.SenseController.GetFovZone(context.CurrentTarget.position);
            switch (zone)
            {
                case FovZone.Decision4:
                    // Classifica il target e prendi decisione
                    return ClassifyTargetAndDecide(context);

                case FovZone.Bite1:
                    // Target molto vicino - attacco diretto
                    return new BiteState();

                case FovZone.Detection5:
                    // Continua ispezione
                    return null;

                default:
                    // Target uscito da tutte le zone
                    context.ClearTarget();
                    return new IdleState();
            }
        }

        /// <summary>
        /// Classifica il target e decide il comportamento appropriato.
        /// </summary>
        private IEntityState ClassifyTargetAndDecide(StateContext context)
        {
            EntityStatus targetStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            PlayerStatus playerStatus = context.CurrentTarget.GetComponent<PlayerStatus>();

            bool isTargetAlive = false;
            EntityType targetType = EntityType.None;

            if (targetStatus != null)
            {
                isTargetAlive = targetStatus.IsAlive;
                targetType = targetStatus.EntityType;
            }
            else if (playerStatus != null)
            {
                isTargetAlive = playerStatus.IsAlive;
                targetType = EntityType.Player;
            }

            if (!isTargetAlive)
            {
                // Target morto - torna normale
                context.ClearTarget();
                return new IdleState();
            }

            // Decisione basata sulla relazione predatore-preda
            bool isPrey = context.EntityStatus.CanHunt(targetType);
            bool isPredator = context.EntityStatus.IsPreyOf(targetType);

            if (isPredator)
            {
                // Il target è un predatore - FUGA!
                return new FleeState();
            }
            else if (isPrey && context.EntityStatus.IsHungry())
            {
                // Il target è una preda e siamo affamati - CACCIA!
                return new HuntState();
            }
            else
            {
                // Neutrale o non affamato - torna normale
                context.ClearTarget();
                return new IdleState();
            }
        }
    }
}
