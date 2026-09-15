using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di fuga da predatori.
    /// L'entità fugge usando tutta la stamina disponibile fino a mettersi in salvo.
    /// </summary>
    public class FleeState : IEntityState
    {
        public EntityState StateType => EntityState.Flee;

        private const float SAFE_DISTANCE = 20f; // Distanza considerata sicura dal predatore
        private const float PANIC_TIME = 5f; // Tempo minimo di fuga anche dopo aver perso il predatore

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();
            
            // Imposta il predatore più vicino come target (da cui fuggire)
            Transform predator = context.SenseController.ClosestPredator;
            if (predator != null)
            {
                EntityStatus predatorStatus = predator.GetComponent<EntityStatus>();
                context.SetTarget(predator, predatorStatus != null ? predatorStatus.EntityType : EntityType.None);
                
                // Notifica debug gizmos
                context.DebugGizmos?.SetDebugTarget(predator, false);
            }

            Debug.Log($"[FleeState] {context.EntityStatus.Profile.creatureName} inizia fuga!");
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);

            // Aggiorna il predatore più vicino
            Transform nearestPredator = context.SenseController.ClosestPredator;
            if (nearestPredator != null && nearestPredator != context.CurrentTarget)
            {
                EntityStatus predatorStatus = nearestPredator.GetComponent<EntityStatus>();
                context.SetTarget(nearestPredator, predatorStatus != null ? predatorStatus.EntityType : EntityType.None);
                context.DebugGizmos?.SetDebugTarget(nearestPredator, false);
            }
        }

        public void OnFixedUpdate(StateContext context)
        {
            // Se c'è un predatore specifico, fuggi da lui
            if (context.CurrentTarget != null)
            {
                context.MovementController.FleeFrom(context.CurrentTarget.position, useAcceleration: true);
            }
            // Altrimenti usa la direzione media di fuga da tutti i predatori
            else if (context.SenseController.HasThreats)
            {
                Vector3 fleeDirection = context.SenseController.GetAverageFleeDirection();
                if (fleeDirection != Vector3.zero)
                {
                    Vector3 fleeTarget = context.Transform.position + fleeDirection * 10f;
                    context.MovementController.MoveTowards(fleeTarget, useAcceleration: true);
                }
            }
            else
            {
                // Nessun predatore, muoviti velocemente in avanti
                Vector3 escapeTarget = context.Transform.position + context.Transform.forward * 10f;
                context.MovementController.MoveTowards(escapeTarget, useAcceleration: true);
            }
        }

        public void OnExit(StateContext context)
        {
            context.DebugGizmos?.ClearDebugTarget();
            Debug.Log($"[FleeState] {context.EntityStatus.Profile.creatureName} è al sicuro");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            // PRIORITÀ 1: Stamina completamente esaurita -> riposo (anche se ancora in pericolo)
            if (context.EntityStatus.GetStatusValue(StatusType.Stamina) <= 0f)
            {
                return new RestState();
            }

            // PRIORITÀ 2: Al sicuro per abbastanza tempo -> idle o rest
            if (!context.SenseController.HasThreats || IsSafe(context))
            {
                if (context.StateTimer >= PANIC_TIME)
                {
                    // Se stamina bassa, riposa, altrimenti idle
                    if (context.EntityStatus.IsExhausted())
                    {
                        return new RestState();
                    }
                    else
                    {
                        return new IdleState();
                    }
                }
            }

            return null; // Continua a fuggire
        }

        /// <summary>
        /// Verifica se l'entità è a distanza di sicurezza da tutti i predatori.
        /// </summary>
        private bool IsSafe(StateContext context)
        {
            if (!context.SenseController.HasThreats) return true;

            // Controlla se tutti i predatori sono oltre la distanza di sicurezza
            foreach (Transform predator in context.SenseController.GetAllPredators())
            {
                if (predator == null) continue;

                float distance = Vector3.Distance(context.Transform.position, predator.position);
                if (distance < SAFE_DISTANCE)
                {
                    return false; // Ancora troppo vicino
                }
            }

            return true; // Tutti i predatori sono lontani
        }
    }
}
