# Deeploration - Sistema di Gestione Entità

## Overview

Sistema completo per la simulazione di una fauna marina dinamica con comportamenti emergenti basati su:
- **State Machine Pattern** per comportamenti complessi
- **Steering Behaviors** per movimento fluido 3D
- **Object Pooling** per performance ottimizzate
- **Data-driven design** con ScriptableObjects

## Struttura del Sistema

```
Code/EntitySystem/
├── Core/
│   ├── EntityStatus.cs           # Gestione vita/fame/stamina + API
│   ├── EntityStateManager.cs     # Orchestratore state machine
│   └── EntityPooler.cs           # Sistema di pooling
├── ScriptableObjects/
│   └── CreatureProfile.cs        # Configurazione creature (SO)
├── Movement/
│   ├── SteeringBehaviors.cs      # Logiche di steering (statico)
│   ├── ObstacleAvoidance.cs      # Collision avoidance con raycast
│   └── MovementController.cs     # Movimento fisico + integrazione
├── Sensors/
│   └── SenseController.cs        # Rilevamento prede/predatori
├── States/
│   ├── IEntityState.cs           # Interfaccia stati + Context
│   ├── IdleState.cs              # Vagabondaggio
│   ├── HuntState.cs              # Caccia attiva
│   ├── FleeState.cs              # Fuga da predatori
│   └── RestState.cs              # Recupero stamina
├── Debug/
│   └── EntityDebugGizmos.cs      # Visualizzazione debug
└── Enums/
    └── EntityEnums.cs            # EntityType, EntityState, StatusType
```

## Setup Rapido

### 1. Crea un Creature Profile

1. Click destro nel Project: `Create > Deeploration > Creature Profile`
2. Configura i parametri:
   - **Identità**: Nome, EntityType (SmallFish, MediumFish, etc.)
   - **Vitali**: maxHealth, maxStamina, maxHunger
   - **Soglie**: hungerThreshold (quando cerca cibo), staminaThreshold (quando riposa)
   - **Movimento**: speedBase, speedFlee, rotationSpeed
   - **Catena Alimentare**: preyTypes (cosa caccia), predatorTypes (cosa lo caccia)

### 2. Setup Prefab Entità

Crea un GameObject con i seguenti componenti **nell'ordine**:

1. **Rigidbody** (configurato automaticamente da MovementController)
2. **Collider** (per physics queries)
3. **EntityStatus** - Assegna il CreatureProfile creato
4. **ObstacleAvoidance** - Configura obstacleLayer
5. **MovementController**
6. **SenseController** - Configura entityLayer
7. **EntityStateManager**
8. **EntityDebugGizmos** (opzionale ma raccomandato)

### 3. Setup EntityPooler

1. Crea un GameObject vuoto nella scena: "EntityPooler"
2. Aggiungi componente `EntityPooler`
3. Configura i pool:
   - **poolName**: Nome identificativo (es. "SmallFish")
   - **prefab**: Riferimento al prefab entità
   - **initialSize**: Numero pre-istanziato (es. 10)
   - **maxSize**: Limite massimo (es. 50)
   - **allowExpansion**: Permetti espansione dinamica

### 4. Spawn Entità

```csharp
// Spawn tramite pooler
GameObject fish = EntityPooler.Instance.Spawn(
    "SmallFish", 
    spawnPosition, 
    Quaternion.identity
);

// Despawn manuale (opzionale, auto-despawn alla morte)
PooledEntity pooled = fish.GetComponent<PooledEntity>();
pooled.Despawn();
```

## Comportamenti degli Stati

### Idle State
- **Trigger**: Stato di default, nessuna minaccia o bisogno
- **Comportamento**: Movimento wander casuale
- **Transizioni**:
  1. Predatore vicino → **Flee**
  2. Stamina bassa → **Rest**
  3. Affamato + preda rilevata → **Hunt**

### Hunt State
- **Trigger**: Fame < hungerThreshold E preda rilevata
- **Comportamento**: Insegue preda con accelerazione (consuma stamina)
- **Cattura**: Distanza < 2m → uccide preda e recupera fame
- **Transizioni**:
  1. Predatore vicino → **Flee** (priorità massima)
  2. Stamina esaurita → **Rest**
  3. Non più affamato → **Idle**
  4. Preda persa/troppo lontana → **Idle**

### Flee State
- **Trigger**: Predatore entro fearThreshold
- **Comportamento**: Fuga con massima velocità (consuma stamina)
- **Transizioni**:
  1. Stamina = 0 → **Rest** (anche se ancora in pericolo)
  2. Al sicuro per 5s → **Rest** (se stanco) o **Idle**

### Rest State
- **Trigger**: Stamina < staminaThreshold
- **Comportamento**: Movimento lento (30% velocità) + recupero stamina
- **Transizioni**:
  1. Predatore troppo vicino → **Flee** (priorità)
  2. Stamina > 60% → **Hunt** (se affamato) o **Idle**

## Parametri Chiave da Bilanciare

### Fame
- **hungerRate**: Velocità degrado (default: 1/s)
- **hungerThreshold**: Soglia caccia (default: 40/100)
- **Morte per fame**: A 0 l'entità muore

### Stamina
- **staminaCostPerSecond**: Costo accelerazione (default: 10/s)
- **staminaRecoveryRate**: Recupero durante riposo (default: 5/s)
- **staminaThreshold**: Soglia riposo forzato (default: 20/100)

### Velocità
- **speedBase**: Velocità normale (default: 3 m/s)
- **speedFlee**: Velocità fuga/caccia (default: 8 m/s)
- Ratio raccomandato: speedFlee = speedBase × 2.5

## Debug e Visualizzazione

### EntityDebugGizmos
Visualizza in tempo reale:
- **Barre colorate**: Vita (rosso), Stamina (blu), Fame (arancione)
- **Sfera wireframe**: Raggio di sensing
- **Linee**: Verde = verso preda, Rosso = da predatore
- **Testo 3D**: Stato corrente e parametri

### Console Logs
Ogni transizione di stato viene loggata:
```
[StateManager] SmallFish_0: Idle -> Hunt
[HuntState] SmallFish_0 inizia caccia
[HuntState] SmallFish_0 ha catturato Plankton_5!
```

### Pool Statistics
EntityPooler mostra GUI overlay con:
- Entità attive per pool
- Entità disponibili
- Totale create

## API Pubblica Essenziale

### EntityStatus
```csharp
// Lettura stato
float health = status.GetStatusValue(StatusType.Health);
bool isHungry = status.IsHungry();
bool isExhausted = status.IsExhausted();

// Modifica stato
status.ApplyDamage(10f);
status.ConsumeFood(25f);
bool hasStamina = status.TryUseStamina(10f, Time.deltaTime);

// Eventi
status.OnEntityDied += (id, type) => { };
status.OnStaminaDepleted += (id) => { };
```

### SenseController
```csharp
// Query
Transform prey = sense.ClosestPrey;
bool hasThreats = sense.HasThreats;
int preyCount = sense.PreyCount;

// Utilità
bool tooClose = sense.IsPredatorTooClose(predator);
Vector3 fleeDir = sense.GetAverageFleeDirection();

// Eventi
sense.OnPreyDetected += (prey, type) => { };
sense.OnPredatorDetected += (pred, type) => { };
```

### MovementController
```csharp
// Movimento
movement.MoveTowards(targetPos, useAcceleration: true);
movement.FleeFrom(threatPos, useAcceleration: true);
movement.Wander();
movement.Rest();

// Query
float speed = movement.CurrentSpeed;
bool isAccelerating = movement.IsAccelerating;
```

### EntityStateManager
```csharp
// Query stato
EntityState current = manager.CurrentStateType;
bool isHunting = manager.IsInState(EntityState.Hunt);

// Debug
manager.ForceState(EntityState.Idle);
string info = manager.GetStateDebugInfo();
```

## Ottimizzazioni Performance

1. **Pooling**: Riutilizzo istanze (no GC overhead)
2. **Sense Update Interval**: 0.3s default (riduce physics queries)
3. **NonAlloc Physics**: `OverlapSphereNonAlloc`, pre-allocated arrays
4. **Cached Components**: Tutti i GetComponent() in Awake()
5. **Steering Behaviors**: Classe statica (no overhead)

## Catena Alimentare di Esempio

```
Player (top)
  ↓ caccia
Shark (predatore apicale)
  ↓ caccia
LargeFish
  ↓ caccia
MediumFish
  ↓ caccia
SmallFish
  ↓ caccia
Plankton (base)
```

Configurazione CreatureProfile:
- **SmallFish**: preyTypes=[Plankton], predatorTypes=[MediumFish, LargeFish, Shark, Player]
- **MediumFish**: preyTypes=[SmallFish], predatorTypes=[LargeFish, Shark, Player]
- **LargeFish**: preyTypes=[MediumFish, SmallFish], predatorTypes=[Shark, Player]

## Troubleshooting

### Entità non si muove
- Verifica Rigidbody (useGravity=false, constraints corretti)
- Controlla che CreatureProfile sia assegnato
- Verifica che EntityStateManager sia attivo

### Collisioni non rilevate
- Configura correttamente i Layer
- Assicurati che entityLayer in SenseController includa le entità
- Verifica che i Collider siano attivi

### Performance basse
- Aumenta senseUpdateInterval (es. 0.5s)
- Riduci maxDetections in SenseController
- Usa pooling per tutte le entità dinamiche
- Disabilita showDebugRays in ObstacleAvoidance per la build

## Estensioni Future

- **Schooling**: Aggiungere Cohesion/Alignment a SteeringBehaviors
- **Territorialità**: Nuovo stato per difesa territorio
- **Riproduzione**: Sistema di spawn basato su condizioni
- **Livelli di aggressività**: Parametro nel Profile
- **Day/Night cycle**: Modifica comportamenti in base al tempo

---

**Versione**: 1.1  
**Autore**: Gemini LLM + Cline  
**Data**: 23 Ottobre 2025
