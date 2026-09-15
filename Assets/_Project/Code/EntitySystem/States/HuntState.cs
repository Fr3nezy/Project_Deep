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

        // Stato per gestire cooldown attacchi
        private float lastAttackTime;

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

            // PRIORITÀ 5: Preda troppo lontana o uscita dal FOV
            if (context.CurrentTarget != null)
            {
                float distance = Vector3.Distance(context.Transform.position, context.CurrentTarget.position);
                if (distance > GIVE_UP_DISTANCE)
                {
                    return new IdleState();
                }

                // Verifica se la preda è ancora nel FOV
                FovZone targetZone = context.SenseController.GetFovZone(context.CurrentTarget.position);
                if (targetZone == FovZone.None)
                {
                    // Target uscito dal campo visivo
                    context.ClearTarget();
                    return new IdleState();
                }
            }

            return null; // Continua a cacciare
        }

        /// <summary>
        /// Verifica se il target è ancora valido (supporta sia EntityStatus che PlayerStatus).
        /// </summary>
        private bool IsTargetValid(StateContext context)
        {
            if (context.CurrentTarget == null) return false;

            // Controlla distanza prima
            float distance = Vector3.Distance(context.Transform.position, context.CurrentTarget.position);
            if (distance > GIVE_UP_DISTANCE) return false;

            // Controlla EntityStatus (altre creature)
            EntityStatus entityStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            if (entityStatus != null) return entityStatus.IsAlive;

            // Controlla PlayerStatus (Player)
            PlayerStatus playerStatus = context.CurrentTarget.GetComponent<PlayerStatus>();
            if (playerStatus != null) return playerStatus.IsAlive;

            // Nessun componente di stato trovato
            return false;
        }

        /// <summary>
        /// Gestisce la cattura della preda con danno progressivo.
        /// </summary>
        private void CatchPrey(StateContext context)
        {
            // Controlla cooldown tra attacchi basato sul profilo
            float attackCooldown = context.EntityStatus.Profile.attackCooldown;
            if (Time.time - lastAttackTime < attackCooldown)
            {
                return; // Troppo presto per attaccare di nuovo
            }

            // Cerca prima EntityStatus (altre creature)
            EntityStatus preyStatus = context.CurrentTarget.GetComponent<EntityStatus>();
            if (preyStatus != null && preyStatus.IsAlive)
            {
                // Applica danno basato sul profilo
                float damage = context.EntityStatus.Profile.attackDamage;
                preyStatus.ApplyDamage(damage);
                lastAttackTime = Time.time; // Aggiorna cooldown

                if (!preyStatus.IsAlive)
                {
                    // Preda morta - consuma cibo
                    float foodValue = preyStatus.Profile.maxHealth * 0.5f;
                    context.EntityStatus.ConsumeFood(foodValue);
                    context.ClearTarget();
                    Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} ha ucciso e mangiato {preyStatus.Profile.creatureName}!");
                }
                else
                {
                    // Preda sopravvissuta - ricomincia timer per prossimo attacco
                    context.ResetTimer();
                    Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} ha attaccato {preyStatus.Profile.creatureName} infliggendo {damage} danni. Preda sopravvive!");
                }
            }
            else
            {
                // Cerca PlayerStatus (se il target è il Player)
                PlayerStatus playerStatus = context.CurrentTarget.GetComponent<PlayerStatus>();
                if (playerStatus != null && playerStatus.IsAlive)
                {
                    // Applica danno al Player
                    float damage = context.EntityStatus.Profile.attackDamage;
                    playerStatus.ApplyDamage(damage);
                    lastAttackTime = Time.time; // Aggiorna cooldown

                    Debug.Log($"[HuntState] {context.EntityStatus.Profile.creatureName} ha attaccato il Player infliggendo {damage} danni!");
                    context.ResetTimer(); // Ricomincia timer per prossimo attacco
                }
                else if (preyStatus != null && !preyStatus.IsAlive)
                {
                    // Se la preda è già morta (dall'ultimo attacco), pulisci target
                    context.ClearTarget();
                }
            }
        }
    }
}
