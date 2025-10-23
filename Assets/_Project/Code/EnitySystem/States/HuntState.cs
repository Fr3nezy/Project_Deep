using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di caccia attiva.
    /// L'entità insegue una preda fino a raggiungerla o perderla.
    /// </summary>
    public class HuntState : IEntityState
    {
        public EntityState StateType => EntityState.Hunt;

        private const float CATCH_DISTANCE = 2f; // Distanza per "catturare" la preda
        private const float GIVE_UP_DISTANCE = 30f; // Distanza oltre cui si abbandona la caccia
        private const float GIVE_UP_TIME = 10f; // Tempo massimo di caccia prima di arrendersi

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();
            
            // Imposta la preda più vicina come target
            Transform prey = context.SenseController.ClosestPrey;
            if (prey != null)
            {
                EntityStatus preyStatus = prey.GetComponent<EntityStatus>();
                context.SetTarget(prey, preyStatus != null ? preyStatus.EntityType : EntityType.None);
                
                // Notifica debug gizmos
                context.DebugGizmos?.SetDebugTarget(prey, true);
            }

            Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} inizia caccia");
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);

            // Aggiorna target se la preda originale è morta o troppo lontana
            if (context.CurrentTarget == null || !IsTargetValid(context))
            {
                // Cerca una nuova preda
                Transform newPrey = context.SenseController.ClosestPrey;
                if (newPrey != null)
                {
                    EntityStatus preyStatus = newPrey.GetComponent<EntityStatus>();
                    context.SetTarget(newPrey, preyStatus != null ? preyStatus.EntityType : EntityType.None);
                    context.DebugGizmos?.SetDebugTarget(newPrey, true);
                }
            }
        }

        public void OnFixedUpdate(StateContext context)
        {
            if (context.CurrentTarget == null) return;

            // Insegui la preda con accelerazione
            context.MovementController.MoveTowards(context.CurrentTarget.position, useAcceleration: true);

            // Verifica se la preda è stata catturata
            float distance = Vector3.Distance(context.Transform.position, context.CurrentTarget.position);
            if (distance <= CATCH_DISTANCE)
            {
                CatchPrey(context);
            }
        }

        public void OnExit(StateContext context)
        {
            context.DebugGizmos?.ClearDebugTarget();
            Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} termina caccia");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            // PRIORITÀ 1: Fuga da predatori (anche durante la caccia!)
            if (context.SenseController.HasThreats)
            {
                Transform predator = context.SenseController.ClosestPredator;
                if (predator != null && context.SenseController.IsPredatorTooClose(predator))
                {
                    return new FleeState();
                }
            }

            // PRIORITÀ 2: Stamina esaurita -> riposo
            if (context.EntityStatus.IsExhausted())
            {
                return new RestState();
            }

            // PRIORITÀ 3: Non più affamato -> idle
            if (!context.EntityStatus.IsHungry())
            {
                return new IdleState();
            }

            // PRIORITÀ 4: Nessuna preda disponibile o troppo tempo passato
            if (context.CurrentTarget == null || 
                !context.SenseController.HasPreyNearby ||
                context.StateTimer > GIVE_UP_TIME)
            {
                return new IdleState();
            }

            // PRIORITÀ 5: Preda troppo lontana
            if (context.CurrentTarget != null)
            {
                float distance = Vector3.Distance(context.Transform.position, context.CurrentTarget.position);
                if (distance > GIVE_UP_DISTANCE)
                {
                    return new IdleState();
                }
            }

            return null; // Continua a cacciare
        }

        /// <summary>
        /// Verifica se il target è ancora valido.
        /// </summary>
        private bool IsTargetValid(StateContext context)
        {
            if (context.CurrentTarget == null) return false;

            EntityStatus targetStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            if (targetStatus == null || !targetStatus.IsAlive) return false;

            float distance = Vector3.Distance(context.Transform.position, context.CurrentTarget.position);
            return distance <= GIVE_UP_DISTANCE;
        }

        /// <summary>
        /// Gestisce la cattura della preda.
        /// </summary>
        private void CatchPrey(StateContext context)
        {
            EntityStatus preyStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            if (preyStatus != null && preyStatus.IsAlive)
            {
                // Uccidi la preda
                preyStatus.ApplyDamage(preyStatus.Profile.maxHealth);

                // Consuma cibo
                float foodValue = preyStatus.Profile.maxHealth * 0.5f; // 50% della salute come cibo
                context.EntityStatus.ConsumeFood(foodValue);

                Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} ha catturato {preyStatus.Profile.creatureName}!");
            }

            context.ClearTarget();
        }
    }
}
