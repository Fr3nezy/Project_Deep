using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di riposo per recuperare stamina.
    /// L'entità si muove lentamente e recupera stamina fino a raggiungere una soglia sicura.
    /// </summary>
    public class RestState : IEntityState
    {
        public EntityState StateType => EntityState.Rest;

        private const float RECOVERY_THRESHOLD = 60f; // Percentuale di stamina per uscire dal riposo

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();
            context.ClearTarget();
            
            Debug.Log($"[RestState] {context.EntityStatus.Profile.creatureName} inizia riposo per recuperare stamina");
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);
        }

        public void OnFixedUpdate(StateContext context)
        {
            // Movimento lento con recupero stamina
            context.MovementController.Rest();
        }

        public void OnExit(StateContext context)
        {
            Debug.Log($"[RestState] {context.EntityStatus.Profile.creatureName} ha finito di riposare");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            // PRIORITÀ 1: Predatori troppo vicini -> fuga (anche se esausto)
            if (context.SenseController.HasThreats)
            {
                Transform predator = context.SenseController.ClosestPredator;
                if (predator != null && context.SenseController.IsPredatorTooClose(predator))
                {
                    // Situazione critica: fuga anche con poca stamina
                    return new FleeState();
                }
            }

            // PRIORITÀ 2: Stamina recuperata sufficientemente
            float currentStamina = context.EntityStatus.GetStatusValue(StatusType.Stamina);
            float maxStamina = context.EntityStatus.Profile.maxStamina;
            float staminaPercentage = (currentStamina / maxStamina) * 100f;

            if (staminaPercentage >= RECOVERY_THRESHOLD)
            {
                // Stamina recuperata, valuta prossimo stato
                
                // Se molto affamato e c'è preda -> caccia
                if (context.EntityStatus.IsHungry() && context.SenseController.HasPreyNearby)
                {
                    return new HuntState();
                }
                
                // Altrimenti torna a idle
                return new IdleState();
            }

            return null; // Continua a riposare
        }
    }
}
