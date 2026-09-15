using UnityEngine;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Stato di attacco diretto (bite).
    /// L'entità attacca immediatamente quando il target entra nella Bite Zone (1m).
    /// Questo è l'attacco più aggressivo e diretto.
    /// </summary>
    public class BiteState : IEntityState
    {
        public EntityState StateType => EntityState.Bite;

        private const float ATTACK_DURATION = 0.5f; // Durata massima dell'attacco

        public void OnEnter(StateContext context)
        {
            context.ResetTimer();

            if (context.CurrentTarget != null)
            {
                context.DebugGizmos?.SetDebugTarget(context.CurrentTarget, true);
                Debug.Log($"[BiteState] {context.EntityStatus.Profile.creatureName} inizia attacco diretto a {context.CurrentTarget.gameObject.name}");

                // Esegui attacco immediato
                PerformAttack(context);
            }
        }

        public void OnUpdate(StateContext context)
        {
            context.IncrementTimer(Time.deltaTime);
        }

        public void OnFixedUpdate(StateContext context)
        {
            // Durante bite, non muoversi, resta fermo ad attaccare
            if (context.CurrentTarget != null)
            {
                // Ruota verso il target ma non muoverti
                Vector3 directionToTarget = context.CurrentTarget.position - context.Transform.position;
                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    context.Transform.rotation = Quaternion.Slerp(
                        context.Transform.rotation,
                        targetRotation,
                        Time.fixedDeltaTime * context.EntityStatus.Profile.rotationSpeed
                    );
                }
            }
        }

        public void OnExit(StateContext context)
        {
            context.DebugGizmos?.ClearDebugTarget();
            Debug.Log($"[BiteState] {context.EntityStatus.Profile.creatureName} termina attacco");
        }

        public IEntityState CheckTransitions(StateContext context)
        {
            // Durata massima dell'attacco
            if (context.StateTimer >= ATTACK_DURATION)
            {
                if (context.CurrentTarget != null)
                {
                    FovZone zone = context.SenseController.GetFovZone(context.CurrentTarget.position);
                    switch (zone)
                    {
                        case FovZone.Bite1:
                            // Target ancora nella bite zone - continua attaccando
                            PerformAttack(context);
                            return null;

                        case FovZone.Decision4:
                            // Target si è allontanato leggermente - vai a cacciare
                            return new HuntState();

                        default:
                            // Target troppo lontano - torna normale
                            context.ClearTarget();
                            return new IdleState();
                    }
                }
                else
                {
                    // Nessun target - torna normale
                    return new IdleState();
                }
            }

            // Continuiamo l'attacco se target è ancora vicino
            return null;
        }

        /// <summary>
        /// Esegue un attacco singolo contro il target.
        /// </summary>
        private void PerformAttack(StateContext context)
        {
            if (context.CurrentTarget == null) return;

            // Trova componente di stato del target
            EntityStatus entityStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            PlayerStatus playerStatus = context.CurrentTarget.GetComponent<PlayerStatus>();

            float damage = context.EntityStatus.Profile.attackDamage;

            if (entityStatus != null && entityStatus.IsAlive)
            {
                // Attacca entità normale
                entityStatus.ApplyDamage(damage);

                if (entityStatus.IsAlive)
                {
                    // Vittima sopravvissuta - ricomincia timer
                    context.ResetTimer();
                    Debug.Log($"[BiteState] {context.EntityStatus.Profile.creatureName} morde {entityStatus.Profile.creatureName} per {damage} danni!");
                }
                else
                {
                    // Vittima morta - consuma cibo
                    float foodValue = entityStatus.Profile.maxHealth * 0.5f;
                    context.EntityStatus.ConsumeFood(foodValue);
                    context.ClearTarget();
                    Debug.Log($"[BiteState] {context.EntityStatus.Profile.creatureName} uccide e mangia {entityStatus.Profile.creatureName}!");
                }
            }
            else if (playerStatus != null && playerStatus.IsAlive)
            {
                // Attacca il Player
                playerStatus.ApplyDamage(damage);
                context.ResetTimer(); // Ricomincia timer per possibili attacchi successivi
                Debug.Log($"[BiteState] {context.EntityStatus.Profile.creatureName} morde il Player per {damage} danni!");
            }

            // Aggiorna cooldown di attacco
            HuntState huntState = new HuntState();
            // Nota: Questa è una soluzione temporanea, dovremmo avere un campo lastAttackTime
            // accessibile o una gestione migliore del cooldown
        }
    }
}
