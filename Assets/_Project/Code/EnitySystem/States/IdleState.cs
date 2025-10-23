using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di vagabondaggio tranquillo.
    /// L'entità si muove casualmente finché non rileva prede (se affamata) o predatori.
    /// </summary>
    public class IdleState : IEntityState
    {
        public EntityState StateType => EntityState.Idle;

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();
            context.ClearTarget();
            
            Debug.Log($"[IdleState] {context.EntityStatus.Profile.creatureName} entra in Idle");
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);
        }

        public void OnFixedUpdate(StateContext context)
        {
            // Movimento di wander
            context.MovementController.Wander();
        }

        public void OnExit(StateContext context)
        {
            Debug.Log($"[IdleState] {context.EntityStatus.Profile.creatureName} esce da Idle");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            // PRIORITÀ 1: Fuga da predatori
            if (context.SenseController.HasThreats)
            {
                Transform predator = context.SenseController.ClosestPredator;
                if (predator != null && context.SenseController.IsPredatorTooClose(predator))
                {
                    return new FleeState();
                }
            }

            // PRIORITÀ 2: Riposo se stamina bassa
            if (context.EntityStatus.IsExhausted())
            {
                return new RestState();
            }

            // PRIORITÀ 3: Caccia se affamato e c'è preda
            if (context.EntityStatus.IsHungry() && context.SenseController.HasPreyNearby)
            {
                return new HuntState();
            }

            return null; // Rimane in Idle
        }
    }
}
