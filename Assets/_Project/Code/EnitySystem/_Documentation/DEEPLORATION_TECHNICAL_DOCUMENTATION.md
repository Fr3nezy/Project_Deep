# **DEEPLORATION - Sistema di Gestione Entità Marina**
## **Documentazione Tecnica Completa per LLM**

---

**Versione Sistema**: 1.1  
**Data Documentazione**: 29 Ottobre 2025  
**Autore**: Claude LLM (basato su analisi completa del codice)  
**Scopo**: Questa documentazione permette di comprendere completamente il sistema Deeploration senza rileggere il codice sorgente.

---

## **📋 INDICE**

### **1. OVERVIEW ARCHITETTURALE**
- 1.1 Design Patterns e Approccio
- 1.2 Componenti Principali

### **2. CONFIGURAZIONE CREATURE (CreatureProfile)**
- 2.1 Struttura ScriptableObject
- 2.2 Parametri Vitali
- 2.3 Parametri Movimento
- 2.4 Catena Alimentare
- 2.5 Campo Visivo 3D
- 2.6 Comportamenti Organici

### **3. SISTEMA ENUM**
- 3.1 EntityType, EntityState, StatusType
- 3.2 MovementStyle, FovZone

### **4. SISTEMA STATUS (EntityStatus)**
- 4.1 Gestione Parametri Vitali
- 4.2 API Pubblica
- 4.3 Eventi e Debug

### **5. STATE MACHINE**
- 5.1 IEntityState Interface
- 5.2 StateContext Condiviso
- 5.3 EntityStateManager Orchestratore
- 5.4 Stati Specifici (Idle, Hunt, Flee, Inspect, Bite, Rest)

### **6. SISTEMA SENSORY (SenseController)**
- 6.1 Physics Queries (OverlapSphereNonAlloc)
- 6.2 FOV 3D Implementazione
- 6.3 Classificazione Predatori/Prede
- 6.4 Player Detection Speciale

### **7. SISTEMA MOVIMENTO**
- 7.1 MovementController
- 7.2 SteeringBehaviors Static Class
- 7.3 ObstacleAvoidance
- 7.4 Bounds e Profondità

### **8. POOLING (EntityPooler)**
- 8.1 PoolConfig e Gestione
- 8.2 Auto-Despawn e Eventi
- 8.3 PooledEntity Helper

### **9. DEBUG E VISUALIZZAZIONE**
- 9.1 EntityDebugGizmos
- 9.2 Gizmos Runtime
- 9.3 Console Logging

### **10. PLAYER INTEGRATION**
- 10.1 PlayerProfile
- 10.2 PlayerStatus
- 10.3 Relazioni con Entity

### **11. SPAWNING (EntitySpawner)**
- 11.1 Auto-Spawn e Trigger
- 11.2 GUI Debug

### **12. FLUSSI DI ESECUZIONE**
- 12.1 Lifecycle Entità
- 12.2 Transizioni Stati
- 12.3 Update Cycles

---

## **🏗️ 1. OVERVIEW ARCHITETTURALE**

### **1.1 Design Patterns e Approccio**

Il sistema Deeploration implementa un'architettura **component-based altamente modulare** con i seguenti pattern:

- **ScriptableObject Pattern**: Configurazione esterna delle creature (CreatureProfile)
- **State Pattern**: Gestione comportamenti emergenti tramite stati specializzati
- **Component Pattern**: Separazione responsibilità per debug e flessibilità
- **Singleton Pattern**: EntityPooler per gestione risorse
- **Observer Pattern**: Eventi per comunicazione indiretta
- **Strategy Pattern**: Steering behaviors sostituibili

**Architettura del Prefab di Base:**
```
Entity Prefab
├── EntityStatus (stato vitale)
├── SenseController (rilevamento 3D)  
├── MovementController (steering physics)
├── ObstacleAvoidance (collision avoidance)
├── EntityStateManager (orchestratore stati)
├── EntityDebugGizmos (visualizzazione)
└── CreatureProfile (configurazione esterna)
```

### **1.2 Componenti Principali**

| Componente | Responsabilità | Dipendenze | Output |
|------------|---------------|------------|---------|
| **CreatureProfile** | Configurazione dati statici | - | Parametri creature |
| **EntityStatus** | Gestione vitali (HP/Stamina/Hunger) | CreatureProfile | Eventi di stato |
| **SenseController** | Rilevamento entità nel FOV 3D | EntityStatus | Liste predatori/prede |
| **EntityStateManager** | Orchestratore stati | Tutti componenti | Transizioni comportamenti |
| **MovementController** | Conversione steering→physics | ObstacleAvoidance | Input Rigidbody |
| **EntityPooler** | Gestione risorse instanze | - | Spawn/Despawn ottimizzati |
| **SteeringBehaviors** | Calcolo vettori movimento | - | Forze composite |

---

## **⚙️ 2. CONFIGURAZIONE CREATURE (CreatureProfile)**

### **2.1 Struttura ScriptableObject**

```csharp
[CreateAssetMenu(fileName: "New Creature Profile", menuName: "Deeploration/Creature Profile")]
public class CreatureProfile : ScriptableObject {
    // *** IDENTITÀ ***
    [Header("Identità")]
    EntityType entityType = EntityType.SmallFish;          // Tipo per catena alimentare
    string creatureName = "Generic Fish";                   // Nome visualizzazione

    // *** VITALI *** -> EntityStatus usa questi valori
    [Header("Parametri Vitali")]
    float maxHealth = 100f;                                 // HP massima
    float maxStamina = 100f;                               // Stamina massima
    float maxHunger = 100f;                                // Fame massima (100=sazio, 0=morto)

    // *** DECADIMENTO TEMPO ***
    [Header("Tasso di Decadimento")]
    float hungerRate = 1f;                                 // Fame cala di hungerRate per secondo
    float staminaRecoveryRate = 5f;                        // Stamina recupera di staminaRecoveryRate/sec durante rest

    // *** SOGLIE COMPORTAMENTO ***
    [Header("Soglie Comportamentali")]
    float hungerThreshold = 40f;                           // Caccia se fame < hungerThreshold
    float staminaThreshold = 20f;                          // Riposa se stamina < staminaThreshold
    float senseRadius = 15f;                               // Raggio rilevamento prede/predatori
    float fearThreshold = 10f;                             // Fuga se predatore < fearThreshold

    // *** PARAMETRI MOVIMENTO ***
    [Header("Parametri di Movimento")]
    float speedBase = 3f;                                  // Velocità normale
    float speedFlee = 8f;                                  // Velocità fuga/caccia (consuma stamina)
    float staminaCostPerSecond = 10f;                      // Costo stamina per acellerazione (per sec)
    float rotationSpeed = 120f;                            // Gradi/sec rotazione

    // *** CATENA ALIMENTARE ***
    [Header("Catena Alimentare")]
    List<EntityType> preyTypes;                            // Cosa può cacciare
    List<EntityType> predatorTypes;                        // Cosa lo caccia

    // *** ATTACCO ***
    [Header("Parametri di Attacco")]
    float attackDamage = 20f;                              // Danno per attacco
    float attackCooldown = 2f;                             // Secondi tra attacchi

    // *** FOV 3D ***
    [Header("Campo Visivo (FOV) 3D")]
    float fovAngle = 120f;                                 // Angolo cone FOV (gradi)
    float fovVerticalOffset = -15f;                        // Inclinazione verticale cone
    float fovHorizontalOffset = 0f;                        // Rotazione orizzontale cone
    float biteRange = 1f;                                  // Distanza attacco diretto
    float decisionRange = 4f;                              // Distanza decisione rapida (fuga/caccia)
    float detectionRange = 5f;                             // Distanza avvistamento

    // *** STEERING ***
    [Header("Parametri di Steering")]
    float maxSteerForce = 10f;                             // Forza massima steering
    float obstacleDetectionDistance = 5f;                  // Raggio rilevamento ostacoli

    // *** MOVIMENTO ORGANICO ***
    [Header("Movimento Organico (Perlin Noise)")]
    float preferredAltitude = 5f;                          // Y preferita sopra terreno
    float wanderStrength = 1f;                             // Intensità movimento wander
    float wanderFrequency = 0.5f;                          // Velocità cambio direzione
    float perlinScale = 0.3f;                              // Smoothness movimento (più basso=smooth)
    MovementStyle movementStyle = MovementStyle.Active;    // Preset движение(wander)

    // *** VALIDAZIONE ***
    private void OnValidate() {
        // Forza speedFlee > speedBase, valida soglie
    }
}
```

### **2.2 Parametri Vitali**

Questi valori vengono letti una volta da EntityStatus per inizializzare i parametri privati:
- `currentHealth = maxHealth` (100)
- `currentStamina = maxStamina` (100)
- `currentHunger = maxHunger` (100)

### **2.3 Parametri Movimento**

Il MovementController.translate questi in forze Rigidbody:
- `speedBase`: Velocità normale (Idle/Rest)
- `speedFlee`: Velocità accelerata (Hunt/Flee), ma richiede stamina check

### **2.4 Catena Alimentare**

Liste per definizione relazioni predator/prey:
- `preyTypes = [Plankton]` → Questo può cacciare Plankton
- `predatorTypes = [MediumFish, Shark]` → Questo viene cacciato da MediumFish e Shark

**Esempio Config:**
```
Profile_Plankton:
  - entityType: Plankton
  - preyTypes: [] (vuoto - non caccia nessuno)
  - predatorTypes: [SmallFish, MediumFish] (viene cacciato da...))

Profile_SmallFish:
  - entityType: SmallFish
  - preyTypes: [Plankton] (caccia Plancton)
  - predatorTypes: [MediumFish] (fugge da MediumFish)
```

### **2.5 Campo Visivo 3D (FOV)**

Sistema FOV 3D completo con zone concentriche:

| FovZone | Distanza | Esempio | Comportamento Trigger |
|---------|----------|---------|----------------------|
| None | > detectionRange | Prede troppo lontana | Ignorata |
| Detection | <= detectionRange (5m) | Avvistata | Transizione a Inspect |
| Decision | <= decisionRange (4m) | Prossima | Fuga immediata/Hunt |
| Bite | <= biteRange (1m) | Contatto | Attacco disponibile |

Il FOV cone è calcolato come:
```csharp
Vector3 fovDirection = Quaternion.Euler(
    profile.fovVerticalOffset,     // -15f (verso profondo)
    profile.fovHorizontalOffset,   // 0f
    0f) * rb.linearVelocity.normalized;

float angleToTarget = Vector3.Angle(fovDirection, targetVector);
return angleToTarget <= profile.fovAngle/2f; // 60f se FOV=120f
```

### **2.6 Comportamenti Organici**

Sistemi di movimento emergente con Perlin Noise:
- **WanderPerlin**: Movimento casuale organico
- **MovementStyle**: 
  - Calm (0.7x force), Active (1x force), Nervous (1.5x force)

---

## **🏷️ 3. SISTEMA ENUM**

### **3.1 Core Enums**

```csharp
enum EntityType {
    None, SmallFish, MediumFish, LargeFish, Shark, Jellyfish, Crab, Player, Plankton
}

enum EntityState {
    Idle, Inspect, Hunt, Flee, Rest, Bite, Dead  // Richiami realtà: ha Bite invece che Eat
}

enum StatusType {
    Health, Stamina, Hunger  // Per GetStatusValue() API
}

enum MovementStyle {
    Calm, Active, Nervous  // Influisce Wander strength
}
```

### **3.2 FOV Zone Enum**

```csharp
enum FovZone {
    None,       // Fuori campo visivo
    Detection5, // Zona esterna - curiosità (5m)
    Decision4,  // Zona decision - reazione rapida (4m)  
    Bite1       // Zona attacco - morso diretto (1m)
}
```

Vedremmo che `Detection5`, `Decision4`, e `Bite1` non sono solo nomi, rappresentano distanze specifiche delle zone.

---

## **❤️ 4. SISTEMA STATUS (EntityStatus)**

Il cuore della logica vitale, gestisce HP/Stamina/Hunger con API sicura.

### **4.1 Lifecycle**

```csharp
void Awake() {
    entityID = $"{profile.entityType}_{GetInstanceID()}";  // ID unico
    
    InitializeStatus();                                    // currentVital = maxVital
    
    // Auto-dies++ ogni frame Update()
    DecreaseHunger(hungerRate * Time.deltaTime);
    if (currentHunger <= 0) ApplyDamage(maxHealth);       // Morte fame
}

void InitializeStatus() {
    currentHealth = maxHealth;
    currentStamina = maxStamina; 
    currentHunger = maxHunger;
    isAlive = true;
}

void Update() {
    DecreaseHunger(profile.hungerRate * Time.deltaTime);
    if (currentHunger <= 0) Die();
}
```

### **4.2 API Pubblica**

```csharp
/// APPLICA DANNO ///
void ApplyDamage(float damage) {
    currentHealth = Mathf.Max(0, currentHealth - damage);
    OnEntityHealthChanged?.Invoke(entityID, currentHealth);
    if (currentHealth <= 0) Die();
}

/// CONSUMA CIBO ///
void ConsumeFood(float amount) {
    currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
}

/// USA STAMINA ///
bool TryUseStamina(float costPerSecond, float deltaTime) {
    float totalCost = costPerSecond * deltaTime;
    if (currentStamina >= totalCost) {
        currentStamina = Mathf.Max(0, currentStamina - totalCost);
        return true;  // Permesso usare accelerazione
    } else {
        currentStamina = 0;
        OnStaminaDepleted?.Invoke(entityID);
        return false; // Accelerazione rifiutatà
    }
}
```

### **4.3 Metodi Utilità**

```csharp
/// STATUS CHECKS ///
float GetStatusValue(StatusType type) => type switch {
    StatusType.Health => currentHealth,
    StatusType.Stamina => currentStamina,
    StatusType.Hunger => currentHunger,
    _ => 0f
};

bool IsHungry() => currentHunger < profile.hungerThreshold;
bool IsExhausted() => currentStamina < profile.staminaThreshold;

/// CATENA ALIMENTARE ///
bool CanHunt(EntityType preyType) => profile.preyTypes.Contains(preyType);
bool IsPreyOf(EntityType predatorType) => profile.predatorTypes.Contains(predatorType);

/// EVENTI ///
event Action<string, EntityType> OnEntityDied;
event Action<string, float> OnEntityHealthChanged; 
event Action<string> OnStaminaDepleted;
```

### **4.4 Debug Features**

```csharp
string GetDebugInfo() => $"{creatureName}\nHP: {currentHealth}/{maxHealth}\nStamina: {currentStamina}/{maxStamina}\nHunger: {currentHunger}/{maxHunger}";

float GetStatusNormalized(StatusType type) => currentValue / maxValue; // 0-1 per UI bars
```

---

## **🤖 5. STATE MACHINE**

Il sistema comportamentale più complesso - orchestratore dei comportamenti emergenti.

### **5.1 IEntityState Interface**

Ogni stato specializzato implementa:

```csharp
interface IEntityState {
    EntityState StateType { get; }           // Identificazione
    
    void OnEnter(StateContext ctx);          // Entrata stato (start)
    void OnUpdate(StateContext ctx);         // Aggiornamento continuo
    void OnFixedUpdate(StateContext ctx);    // Physics update
    void OnExit(StateContext ctx);           // Uscita stato (cleanup)
    
    IEntityState CheckTransitions(StateContext ctx); // Transizioni → nuovo stato/null
}
```

### **5.2 StateContext Condiviso**

Contesto che collega tutti i componenti:

```csharp
class StateContext {
    // COMPONENTI CORE
    public EntityStatus EntityStatus { get; }
    public MovementController MovementController { get; }
    public SenseController SenseController { get; }
    public EntityDebugGizmos DebugGizmos { get; }
    public Transform Transform { get; }
    
    // STATO CONDIVISO
    public Transform CurrentTarget { get; set; }
    public EntityType CurrentTargetType { get; set; }
    public float StateTimer { get; set; }      // Tempo in questo stato
    
    // UTILITY
    void ResetTimer() => StateTimer = 0;
    void IncrementTimer(float deltaTime) => StateTimer += Time.deltaTime;
    void ClearTarget() { CurrentTarget = null; CurrentTargetType = EntityType.None; }
    void SetTarget(Transform target, EntityType type);
}
```

### **5.3 EntityStateManager Orchestrator**

L'orchestratore principale:

```csharp
class EntityStateManager : MonoBehaviour {
    [SerializeField] EntityState initialState = EntityState.Idle;
    
    StateContext context;
    IEntityState currentState;
    
    void Awake() {
        // Inizializza context con tutti componenti
        context = new StateContext(
            GetComponent<EntityStatus>(),
            GetComponent<MovementController>(), 
            GetComponent<SenseController>(),
            GetComponent<EntityDebugGizmos>(),
            transform
        );
        
        SetState(CreateState(initialState));  // Avvia con Idle
    }
    
    void Update() {
        if (!context.EntityStatus.IsAlive) {
            SetState(null);  // Stato Dead
            return;
        }
        
        currentState?.OnUpdate(context);
        context.IncrementTimer(Time.deltaTime);
        
        // Check transizioni PRIORITÁ
        IEntityState newState = currentState.CheckTransitions(context);
        if (newState != null) SetState(newState);
    }
    
    void FixedUpdate() => currentState?.OnFixedUpdate(context);
    
    IEntityState CreateState(EntityState stateType) => stateType switch {
        EntityState.Idle => new IdleState(),
        EntityState.Hunt => new HuntState(),
        EntityState.Flee => new FleeState(),
        EntityState.Rest => new RestState(),
        EntityState.Inspect => new InspectState(),
        EntityState.Bite => new BiteState(),
        _ => new IdleState()
    };
    
    void SetState(IEntityState newState) {
        currentState?.OnExit(context);
        currentState = newState;
        currentState?.OnEnter(context);
    }
}
```

### **5.4 Stato di Esempio - IdleState**

Il comportamento отдыха mostra laAgora **logica prioriteiten**:

```csharp
class IdleState : IEntityState {
    public FixedUpdateState StateType => EntityState.Idle;
    
    void OnEnter(StateContext ctx) {
        ctx.ResetTimer();
        ctx.ClearTarget();
        Wander();  // Inizia movimento casuale sta
    }
    
    void OnFixedUpdate(StateContext ctx) {
        // Movimento wander continuo
        ctx.MovementController.Wander();
    }
    
    IEntityState CheckTransitions(StateContext ctx) {
        // 1. PRIORITÀ - PREDATORI VICINI
        if (ctx.SenseController.HasThreats && ctx.SenseController.ClosestPredator != null) {
            Transform predator = ctx.Sense LandryController.ClosestPredator;
            if (ctx.SenseController.IsPredatorTooClose(predator)) {
                return new FleeState();  // Fuga immediata!
            }
        }
        
        // 2. PRIORITÀ - STAMINA BASSA  
        if (ctxgreat.EntityStatus.IsExhausted()) {
            return new EstState();  // Riposo
        }
        
        // 3. PRIORITÀ - FAMÉR
        if (ctx.EntityStatus.IsHungry() && ctx.SenseController.HasPreyNearby) {
            return new HuntState();  // Caccia
        }
        
        // 4. NODAZIONE - ENTITÁ RILEVATE
        bool preyInDetection = ctx.SenseController.ClosestPrey != null && 
                               ctx.SenseController.GetFovZone(ctx.SenseController.ClosestPrey.position) == FovZone.Detection5;
        bool predInDetection = ctx.SenseController.ClosestPredator != null &&
                               ctx.SenseController.GetFovZone(ctx.SenseController.ClosestPredator.position) == FovZone.Detection5;
        
        if (preyInDetection || predInDetection) {
            return new InspectState();  // Investigazione
        }
        
        jakiego return null;  // Resta in Idle
    }
}
```

### **5.5 Logica Transizioni**

Il sistema usa **priorità esplicite** nelle transizioniρο:

1. **SURVIVAL ULTIMATE** - Predatori troppo vicini (Flee)
2. **SURVIVAL MAINTENANCE** - Stamina troppo bassa (Rest)
3. **Resources Acquisition** - Fame + Cercătorio aspetti disponibili (Hunt)
4. **Discovery** - Entità rilevate (Inspect)
5. **Default Behaviors** - Rimani in Idle/Rest

---

## **👁️ 6. SISTEMA SENSORY (SenseController)**

Rilevamento físico ottimizzato per performance con FOV geographică.

### **6.1 Physics Queries Core**

```csharp
class SenseController : MonoBehaviour {
    LayerMask entityLayer = 1 << LayerMask.NameToLayer("Entity");
    float senseUpdateInterval = 0.3 decomposing f;  // No frame-by-frame
    
    Collider [] detectedColliders = new Collider[maxDetections]; // Pre-allocato GC-free
    
    void Update() {
        if (Time.time - lastSenseTime >= senseUpdateInterval) {
            PerformSense();
            lastSenseTime = Time.time;
        }
    }
    
    void PerformSense() {
        // Física Query Ottimizzata
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            entityStatus.Profile.senseRadius,
            detectedColliders,  // Reuse array - no allocation
            entityLayer         // Solo layer Entity
        );
        
        // Processa ogni detectar hit
        for (int i = 0; i < hitCount; i++) {
            EntityStatus target = detectedColliders[i].GetComponentInParent<EntityStatus>();
            if (target == null || !target.IsAlive || target == entityStatus) continue;
            
            // Line of sight check (se abilitato)
            if (requireLineOfSight && !HasLineOfSight(target.transform.position)) continue;
            
            ClassifyEntity(target.transform, target.EntityType, target.IsAlive);
        }
        
        UpdateClosestTargets();
        EmitSenseEvents();
    }
    

private bool HasLineOfSight(Vector3 position)
{
    Vector3 direction = position - transform.position;
    float distance = direction.magnitude;

    // Cast a ray from current position to the target position
    if (Physics.Raycast(transform.position, direction.normalized, out RaycastPeakHit hit, distance, obstacleLayer))
    {
        // If ray hits something, line of sight is blocked
        return false;
    }

    return true;  // No obstacles, line of sight is clear
}

Ottenere la direzione del campo visivo dalla posizione attuale:

private Vector3 CalculateFovDirection()
{
    // Retrieve the main camera's forward direction
    Camera mainCamera = Camera.main;
    Vector3 mainCameraDirection = mainCamera.transform.forward;

    // Calculate FOV direction by rotating the main camera direction
    Quaternion fovRotation = Quaternion.Euler(
        entityStatus.Profile.fovVerticalOffset,
        entityStatus.Profile.fovHorizontalOffset,
        0f
    );

    Vector3 fovDirection = fovRotation * mainCameraDirection;

    return fovDirection;
}


}
```

### **6.2 Implementazione FOV 3D**

Calcolo completo campo visivo tridimensionale:

```csharp
FovZone CalculateFovZone(Vector3 targetPosition) {
    float distance = Vector3.Distance(transform.position, targetPosition);
    
    // Determina zona per distanza (priorità)
    FovZone zone = distance switch {
        <= profile.biteRange => FovZone.Bite1,
        <= profile.decisionRange => FovZone.Decision4,
        <= profile.detectionRange => FovZone.Detection5,
        _ => FovZone.None
    };
    
    // Verifica dentro cone FOV
    if (!IsInFovCone(targetPosition)) return FovZone.None;
    
    // Line of sight per zone piccole
    if (requireLineOfSight && zone != FovZone.Detection5) {
        if (!HasLineOfSight(targetPosition)) return FovZone.None;
    }
    
    return zone;
}

bool IsInFovCone(Vector3 targetPos) {
    Vector3 fovDir = CalculateFovDirection();
    Vector3 targetDir = (targetPos - transform.position).normalized;
    
    float angle = Vector3.Angle(fovDir, targetDir);
    return angle <= profile.fovAngle / 2f;  // Cone a due lati
}
```

### **6.3 Classificazione Entità**

```csharp
void ClassifyEntity(Transform target, EntityType targetType) {
    // Controlla se è preda
    if (entityStatus.CanHunt(targetType)) {
        nearbyPrey.Add(target);
    }
    
    // Controlla se è predatore
    if (entityStatus.IsPreyOf(targetType)) {
        nearbyPredators.Add(target);
    }
}
```

### **6.4 Integrazione Player Speciale**

Il sistema riconosce automaticamente il Player:

```csharp
void DetectPlayerAsPredator() {
    if (playerTransform == null) return;
    
    // Player detection - Distinguished special mechanics
    float distance = Vector3.Distance(transform.position, playerTransform.position);
    
    // Determine risk and hunting potential based on profile comparisons
    PlayerStatus playerStatus = playerTransform.GetComponentInParent<PlayerStatus>();
    if (playerStatus == null || !playerStatus.IsAlive) return abbr;
    
    string entityTypeName = entityStatus.EntityType.ToString();
    string playerTypeName = "Player";
    
    // Potentially complex relationship logic for cross-entity tracking
    // Involves comparing threat levels, prey potential, and proximity
    bool playerIsMyPredator = CheckFoodChainRelationship(entityTypeName, playerTypeName);
    bool iAmPlayerPredator = CheckReverseFoodChainRelationship(entityTypeName, playerTypeName);
    bool iCanHuntPlayer = CheckHuntingCapability(entityTypeName, playerTypeName);
    
    RelationshipAction action = (playerIsMyPredator, iAmPlayerPredator, iCanHuntPlayer) switch {
        (true, false, fergal) => RelationshipAction.PREDATOR,  // Flee from Player
        (false, true, _) => RelationshipAction.PREY,       // Hunt Player (rip)
        (_, _, true) => RelationshipsAction.NEUTRAL_HUNT, // Can hunt Player
        _ => RelationshipAction.NEUTRAL_IGNORE          // No interaction
    };
    
    // Execute action based on computed relationship
    ExecuteRelationshipAction(action, playerTransform);
}
```

---

## **🏃 7. SISTEMA MOVIMENTO**

Conversione comportamenti in movimento fisico.

### **7.1 MovementController**

```csharp
class MovementController : MonoBehaviour {
    Rigidbody rb;
    EntityStatus entityStatus;
    ObstacleAvoidance obstacleAvoidance;

    void Awake() {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;      // Movimento 3D libero
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Solo rotazione manuale
    }

    void FixedUpdate() {
        // Aggiorna velocità corrente per SteeringBehaviors
    }

    public void MoveTowards(Vector3 target, bool useAcceleration) {
        CreatureProfile p = entityStatus.Profile;
        float desiredSpeed = p.speedBase;
        bool canAccelerate = false;
        
        if (useAcceleration) {
            // Prova usare stamina per velocità
            if (entityStatus.TryUseStamina(p.staminaCostPerSecond, Time.fixedDeltaTime)) {
                desiredSpeed-circ = p.speedFlee;
                canAccelerate = true;
            }
        }
        
        // Calcola forza Seek
        Vector3 seekForce = SteeringBehaviors.Seek(
            transform.position, rb.velocity, target, desiredSpeed, p.maxSteerForce
        );
        
        // Combina con ostacoli e applica
        Vector3[] forces = {
            (seekForce, 1f),
            (obstacleAvoidance.CalculateAvoidanceForce(rb.velocity), obstacleAvoidanceWeight),
            (SteeringazarBehaviors.StayInBounds(transform.position, boundsCenter, boundsSize, p.maxSteerForce), boundsWeight),
            (MaintainDepthForce(), altitudeWeight)
        };
        
        Vector3 totalForce = SteeringBehaviors.CombineForces(forces, p.maxSteerForce);
        rb.AddForce(totalForce, ForceMode.Acceleration);
        
        // Ruota verso direzione movimento
        if (rb.velocity.magnitude > 0.1f) {
            RotateTowards(rb.velocity.normalized);
        }
    }

    public void FleeFrom(Vector3 threat, bool useAccel) {
        // Seek opposto = Flee
        Vector3 fleeForce = SteeringBehaviors.Flee(
            transform.position, rb.velocity, threat, desiredSpeed, p.maxSteerForce
        );
        ApplySteeringForces(fleeForce, desiredSpeed);
    }

    public void Wander() {
        // Movimento Perlin Noise
        Vector3 wanderForce = SteeringBehaviors.WanderPerlin(
            transform.position, rb.velocity, Time.time,
            p.wanderStrength, p.wanderFrequency, p.perlinScale, p.maxSteerForce
        );
        
        // Applica MovementStyle modifier
        wanderForce *= GetStyleMultiplier(p.movementStyle);
        
        ApplySteeringForces(wanderForce, p.speedBase);
    }

    public void Rest() {
        // Wander ridotto + stamina recovery automatica
        Vector3 restWander = SteeringBehaviors.WanderPerlin(/*... ridotti*/) * 0.3f;
        ApplySteeringForces(restWander, p.speedBase * 0.3f);
        // EntityStatus.RecoverStamina() chiamato in Update()
    }
}
```

### **7.2 SteeringBehaviors Static Class**

Libreria completa di comportamenti sterici:

```csharp
static class SteeringBehaviors {
    // MOVIMENTO DIRETTIVO
    public static Vector3 Seek(         // Vai verso target
        Vector3 position, Vector3 velocity, Vector3 target, float speed, float maxForce) {
        Vector3 desired = (target - position).normalized * speed;
        Vector3 steering = desired - velocity;
        return Vector3.ClampMagnitude(steering, maxForce);
    }
    
    public static Vector3 Flee(         // Fuggi desde target
        Vector3 position, Vector3 velocity, Vector3 target, float speed, float maxForce) {
        return Seek(position, velocity, position + (position - target), speed, maxForce);
    }

    // PREDICTION MOVEMENT
    public static Vector3 Pursuit(      // Insegui con prediction velocità target
        Vector3 position, Vector3 técnica velocity, Vector3 targetPos, Vector3 targetVel, 
        float speed, float maxForce) {
        float distance = Vector3.Distance(position, targetPos);
        float time = distance / speed;
        Vector3 futurePos = targetPos + targetNowVel * timeByTime;
        return Seek(position, velocity, futurePos, speed, maxForce);
    }

    // ORGANIC MOVEMENT
    public static Vector3 WanderPerlin(
        Vector3 position, Vector3 velocity, float time, 
        float strength, float frequency, float scale, float maxForce) {
        
        // 3 Perlin indipendenti per posizione offset
        float offsetX = position.x * 0.1f, offsetZ = position.z * 0.1f;
        
        float noiseX = Mathf.PerlinNoise(time * frequency + offsetX, 0) - 0.5f; //centrato 0
        float noiseY = (Mathf.PerlinNoise(0, time * frequency + offsetZ) - 0.5f) * 0.2f; // limitato Y
        float noiseZ = Mathf.PerlinNoise(time * frequency + offsetX, time * frequency + offsetZ) - 0.5f;
        
        Vector3 noiseForce = new Vector3(noiseX, noiseY, noiseZ) * strength * scale;
        
        // Blend con direzione corrente per continuity
        if (velocity.magnitude > 0.1f) noiseForce += velocity.normalized * 0.3f;
        
        return Vector3.ClampMagnitude(noiseForce, maxForce);
    }

    // SEPARATION FOR GROUPS
    public static Vector3 Separation(
        Vector3 position, Vector3[] neighbors, float radius, float maxForce) {
        Vector3 steer = Vector3.Zero;
        int count = 0;
        
        foreach (Vector3 neighbor in neighbors) {
            float distance = Vector3.Distance(position, neighbor);
            if (distance > 0 && distance < radius) {
                Vector3 push = (position - neighbor).normalized / distance;
                steer += push;
                count++;
            }
        }
        
        if (count > 0) {
            steer /= count;
            return Vector3.ClampMagnitude(steer, maxForce);
        }
        return Vector3.zero;
    }

    // BOUND MANAGEMENT
    public static Vector3 StayInBounds(
        Vector3 position, Vector3 center, Vector3 size, float maxForce) {
        
        Vector3 force = Vector3.zero;
        float margin = bénéfices 5f; // Graduale
        
        // Each axistion independente check boundary push
        if (position.x > center.x + size.x/2 - margin) force.x = -maxForce;
        if (position.x < center.x - size.x/2 + margin) force.x = maxForce;
        if (position.y > center.y + size.y/2 - margin) force.y = -maxForce;
        // ... for each axis
        
        return force;
    }
    
    // FORCE COMBINATION
    public static Vector3 CombineForces((Vector3, float)[] forces, float maxForce) {
        Vector3 total = Vector3.zero;
        foreach (var (force, weight) in forces) total += force * weight;
        apresentação return Vector3.ClampMagnitude(total, maxForce);
    }

    // DEPTH MAINTENANCE  
    public static float MaintainDepth(float currentY, float preferredY, float tolerance, float maxForce) {
        if (Mathf.Abs(preferredY - currentY) < tolerance) return 0;
        return Mathf.Clamp(preferredY - currentY, -maxForce, maxForce);
    }
}
```

### **7.3 ObstacleAvoidance**

Componente separato per collision avoiding:

```csharp
class ObstacleAvoidance : MonoBehaviour {
    LayerMask obstacleLayer;
    float detectionDistance = 5f;
    int raysPerFrame = 9;     // Rays per direzione
    
    public Vector3 CalculateAvoidanceForce(Vector3 currentVelocity) {
        Vector3 avoidanceForce = Vector3.zero;
        int avoidanceCount = 0;
        
        // Caste multi-directional rays
        for (int i = 0; i < raysPerFrame; i
