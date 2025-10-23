using UnityEngine;
using System;

namespace Deeploration.EntitySystem
{
    /// <summary>
    /// Gestisce la state machine dell'entità.
    /// Controlla le transizioni tra stati e invoca i metodi appropriati.
    /// </summary>
    [RequireComponent(typeof(EntityStatus))]
    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(SenseController))]
    public class EntityStateManager : MonoBehaviour
    {
        [Header("Configurazione")]
        [SerializeField, Tooltip("Stato iniziale dell'entità")]
        private EntityState initialState = EntityState.Idle;

        [Header("Debug Info (Read-Only)")]
        [SerializeField, Tooltip("Stato corrente")]
        private EntityState currentStateType;

        [SerializeField, Tooltip("Tempo nello stato corrente")]
        private float timeInCurrentState;

        // Eventi
        public event Action<EntityState, EntityState> OnStateChanged;

        // Componenti
        private EntityStatus entityStatus;
        private MovementController movementController;
        private SenseController senseController;
        private EntityDebugGizmos debugGizmos;

        // State machine
        private IEntityState currentState;
        private StateContext context;

        // Proprietà pubbliche
        public EntityState CurrentStateType => currentStateType;
        public float TimeInCurrentState => timeInCurrentState;

        private void Awake()
        {
            // Ottieni componenti
            entityStatus = GetComponent<EntityStatus>();
            movementController = GetComponent<MovementController>();
            senseController = GetComponent<SenseController>();
            debugGizmos = GetComponent<EntityDebugGizmos>();

            // Crea contesto condiviso
            context = new StateContext(
                entityStatus,
                movementController,
                senseController,
                debugGizmos,
                transform
            );

            // Inizializza con lo stato iniziale
            SetState(CreateState(initialState));
        }

        private void Update()
        {
            if (!entityStatus.IsAlive)
            {
                // Se morto, ferma la state machine
                if (currentStateType != EntityState.Dead)
                {
                    SetState(null);
                    currentStateType = EntityState.Dead;
                }
                return;
            }

            if (currentState == null) return;

            // Update dello stato corrente
            currentState.OnUpdate(context);
            timeInCurrentState = context.StateTimer;

            // Controlla transizioni
            IEntityState newState = currentState.CheckTransitions(context);
            if (newState != null)
            {
                SetState(newState);
            }
        }

        private void FixedUpdate()
        {
            if (!entityStatus.IsAlive || currentState == null) return;

            // FixedUpdate dello stato corrente
            currentState.OnFixedUpdate(context);
        }

        /// <summary>
        /// Cambia lo stato corrente.
        /// </summary>
        private void SetState(IEntityState newState)
        {
            EntityState previousStateType = currentStateType;

            // Exit dallo stato precedente
            currentState?.OnExit(context);

            // Imposta nuovo stato
            currentState = newState;
            currentStateType = newState?.StateType ?? EntityState.Dead;

            // Enter nel nuovo stato
            if (currentState != null)
            {
                currentState.OnEnter(context);
            }

            // Emetti evento
            OnStateChanged?.Invoke(previousStateType, currentStateType);

            Debug.Log($"[StateManager] {entityStatus.Profile.creatureName}: {previousStateType} -> {currentStateType}");
        }

        /// <summary>
        /// Crea un'istanza dello stato specificato.
        /// </summary>
        private IEntityState CreateState(EntityState stateType)
        {
            return stateType switch
            {
                EntityState.Idle => new IdleState(),
                EntityState.Hunt => new HuntState(),
                EntityState.Flee => new FleeState(),
                EntityState.Rest => new RestState(),
                _ => new IdleState()
            };
        }

        /// <summary>
        /// Forza un cambio di stato (per debug o eventi esterni).
        /// </summary>
        public void ForceState(EntityState stateType)
        {
            IEntityState newState = CreateState(stateType);
            SetState(newState);
        }

        /// <summary>
        /// Verifica se l'entità è in uno stato specifico.
        /// </summary>
        public bool IsInState(EntityState stateType)
        {
            return currentStateType == stateType;
        }

        /// <summary>
        /// Ottiene informazioni di debug sullo stato corrente.
        /// </summary>
        public string GetStateDebugInfo()
        {
            if (currentState == null) return "No State";

            return $"State: {currentStateType}\n" +
                   $"Time: {timeInCurrentState:F1}s\n" +
                   $"Target: {(context.CurrentTarget != null ? context.CurrentTarget.name : "None")}";
        }

        #region Event Subscriptions

        private void OnEnable()
        {
            // Sottoscrivi eventi dal SenseController
            if (senseController != null)
            {
                senseController.OnPreyDetected += HandlePreyDetected;
                senseController.OnPredatorDetected += HandlePredatorDetected;
                senseController.OnThreatsCleared += HandleThreatsCleared;
            }

            // Sottoscrivi eventi dall'EntityStatus
            if (entityStatus != null)
            {
                entityStatus.OnEntityDied += HandleEntityDied;
                entityStatus.OnStaminaDepleted += HandleStaminaDepleted;
            }
        }

        private void OnDisable()
        {
            // Rimuovi sottoscrizioni
            if (senseController != null)
            {
                senseController.OnPreyDetected -= HandlePreyDetected;
                senseController.OnPredatorDetected -= HandlePredatorDetected;
                senseController.OnThreatsCleared -= HandleThreatsCleared;
            }

            if (entityStatus != null)
            {
                entityStatus.OnEntityDied -= HandleEntityDied;
                entityStatus.OnStaminaDepleted -= HandleStaminaDepleted;
            }
        }

        #endregion

        #region Event Handlers

        private void HandlePreyDetected(Transform prey, EntityType preyType)
        {
            // La logica di caccia è gestita nelle transizioni degli stati
            // Questo handler può essere usato per effetti sonori/visivi
        }

        private void HandlePredatorDetected(Transform predator, EntityType predatorType)
        {
            // La fuga è gestita automaticamente dalle transizioni degli stati
            // Questo handler può essere usato per effetti sonori/visivi (es. suono di allarme)
        }

        private void HandleThreatsCleared()
        {
            // Le minacce sono scomparse
            // La transizione da Flee a Idle/Rest è gestita automaticamente
        }

        private void HandleEntityDied(string entityID, EntityType entityType)
        {
            // L'entità è morta
            SetState(null);
            currentStateType = EntityState.Dead;
            
            // Ferma movimento
            movementController.Stop();
        }

        private void HandleStaminaDepleted(string entityID)
        {
            // Stamina esaurita - questo viene gestito dalle transizioni degli stati
            // ma possiamo aggiungere effetti visivi qui
            Debug.Log($"[StateManager] {entityStatus.Profile.creatureName} ha esaurito la stamina!");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (currentState == null || !Application.isPlaying) return;

            // Visualizza informazioni sullo stato corrente
            #if UNITY_EDITOR
            Vector3 textPosition = transform.position + Vector3.up * 3f;
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = GetStateColor(currentStateType);
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            UnityEditor.Handles.Label(textPosition, GetStateDebugInfo(), style);
            #endif
        }

        private Color GetStateColor(EntityState state)
        {
            return state switch
            {
                EntityState.Idle => Color.cyan,
                EntityState.Hunt => Color.yellow,
                EntityState.Flee => Color.red,
                EntityState.Rest => Color.blue,
                EntityState.Dead => Color.gray,
                _ => Color.white
            };
        }

        #endregion
    }
}
