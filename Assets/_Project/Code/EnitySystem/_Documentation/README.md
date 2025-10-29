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

## 🧪 TUTORIAL COMPLETO - Test Scenario Plankton-SmallFish

Questa sezione fornisce una guida passo-passo per creare e testare uno scenario base con **Plankton** (preda) e **SmallFish** (predatore). Questo è il test fondamentale per verificare che il sistema funzioni correttamente.

### ⏱️ Tempo Stimato: 30-40 minuti

---

## 📋 Step 1: Setup Layer in Unity (5 minuti)

Il sistema richiede un layer dedicato per il rilevamento delle entità.

### Configurazione:

1. **Apri Project Settings**:
   - `Edit > Project Settings > Tags and Layers`

2. **Crea il Layer "Entity"**:
   - Trova un User Layer libero (es. Layer 6)
   - Rinominalo in `Entity`

3. **[OPZIONALE] Configura Physics Matrix**:
   - `Edit > Project Settings > Physics`
   - Nella matrice di collisione, assicurati che `Entity` possa collidere con se stesso
   - Questo è importante se usi collisioni fisiche tra creature

### ✅ Verifica:
- Layer "Entity" visibile nel dropdown dei GameObject

---

## 📝 Step 2: Crea CreatureProfile - Plankton (3 minuti)

Il Plankton è la base della catena alimentare: non caccia nessuno e viene cacciato da SmallFish.

### Procedura:

1. **Click destro nel Project** → `Create > Deeploration > Creature Profile`
2. **Rinomina** in `Profile_Plankton`
3. **Configura i parametri**:

```
=== IDENTITÀ ===
Entity Type: Plankton
Creature Name: "Plankton"

=== PARAMETRI VITALI ===
Max Health: 30
Max Stamina: 50
Max Hunger: 100

=== TASSO DI DECADIMENTO ===
Hunger Rate: 0.5        (muore lentamente di fame)
Stamina Recovery Rate: 10

=== SOGLIE COMPORTAMENTALI ===
Hunger Threshold: 30    (cerca cibo quando Fame < 30)
Stamina Threshold: 15
Sense Radius: 10        (rileva predatori entro 10m)
Fear Threshold: 5       (fugge se predatore < 5m)

=== PARAMETRI DI MOVIMENTO ===
Speed Base: 1.5         (lento)
Speed Flee: 4           (fuga moderata)
Stamina Cost Per Second: 8
Rotation Speed: 90

=== CATENA ALIMENTARE ===
Prey Types: [VUOTO]     ⚠️ Il Plankton non caccia nessuno!
Predator Types: [SmallFish, MediumFish]

=== PARAMETRI DI STEERING ===
Max Steer Force: 8
Obstacle Detection Distance: 3
```

### ✅ Verifica:
- `Predator Types` deve contenere almeno `SmallFish`
- `Prey Types` deve essere vuoto

---

## 📝 Step 3: Crea CreatureProfile - SmallFish (3 minuti)

SmallFish è il predatore del Plankton.

### Procedura:

1. **Click destro nel Project** → `Create > Deeploration > Creature Profile`
2. **Rinomina** in `Profile_SmallFish`
3. **Configura i parametri**:

```
=== IDENTITÀ ===
Entity Type: SmallFish
Creature Name: "Small Fish"

=== PARAMETRI VITALI ===
Max Health: 100
Max Stamina: 100
Max Hunger: 100

=== TASSO DI DECADIMENTO ===
Hunger Rate: 1          (ha fame più velocemente del Plankton)
Stamina Recovery Rate: 5

=== SOGLIE COMPORTAMENTALI ===
Hunger Threshold: 40    (caccia quando Fame < 40)
Stamina Threshold: 20
Sense Radius: 15        (rileva prede/predatori entro 15m)
Fear Threshold: 8

=== PARAMETRI DI MOVIMENTO ===
Speed Base: 3           (più veloce del Plankton)
Speed Flee: 8
Stamina Cost Per Second: 10
Rotation Speed: 120

=== CATENA ALIMENTARE ===
Prey Types: [Plankton]  ⚠️ Può cacciare SOLO Plankton!
Predator Types: [MediumFish, LargeFish, Shark, Player]

=== PARAMETRI DI STEERING ===
Max Steer Force: 10
Obstacle Detection Distance: 5
```

### ✅ Verifica:
- `Prey Types` deve contenere `Plankton`
- Speed Base (3) < Speed Flee (8)
- SmallFish è più veloce di Plankton in entrambe le velocità

---

## 🎮 Step 4: Setup Prefab Plankton (5 minuti)

Crea il prefab dell'entità Plankton.

### Procedura:

1. **Crea GameObject** nella scena: `Plankton_Prefab`
2. **Aggiungi componenti nell'ordine**:

#### a) Rigidbody
```
Use Gravity: false
Is Kinematic: false
Interpolate: Interpolate
Collision Detection: Continuous
Constraints: Freeze Rotation X, Y, Z
```

#### b) Sphere Collider
```
Is Trigger: false
Radius: 0.5
```

#### c) EntityStatus
```
Profile: Profile_Plankton
```

#### d) ObstacleAvoidance
```
Obstacle Layer: Default (o il layer dei tuoi ostacoli/terreno)
Show Debug Rays: true (per test)
```

#### e) MovementController
- Nessuna configurazione necessaria (legge dal Profile)

#### f) SenseController
```
Entity Layer: Entity ⚠️ CRITICO!
Sense Update Interval: 0.3
Max Detections: 20
Require Line Of Sight: false (opzionale, imposta true se hai ostacoli)
Obstacle Layer: Default (se Line of Sight = true)
Show Debug Spheres: true
```

#### g) EntityStateManager
```
Initial State: Idle
```

#### h) EntityDebugGizmos [OPZIONALE]
```
Show Status Bars: true
Show Sense Radius: true
Show State Info: true
```

3. **Imposta Layer del GameObject**:
   - Seleziona `Plankton_Prefab`
   - Inspector → Layer → `Entity` ⚠️ **FONDAMENTALE!**

4. **Crea il Prefab**:
   - Drag & Drop `Plankton_Prefab` dalla Hierarchy al Project
   - Elimina dalla scena (o lascialo disattivato)

### ✅ Verifica:
- Layer = Entity
- EntityStatus ha Profile_Plankton assegnato
- SenseController ha entityLayer = Entity

---

## 🎮 Step 5: Setup Prefab SmallFish (5 minuti)

Procedura identica al Plankton, con alcune differenze.

### Procedura:

1. **Crea GameObject** nella scena: `SmallFish_Prefab`
2. **Aggiungi gli stessi componenti del Plankton** (vedi Step 4)
3. **Differenze nella configurazione**:

```
EntityStatus:
  Profile: Profile_SmallFish ⚠️ Usa il profile corretto!

Sphere Collider:
  Radius: 0.7 (leggermente più grande)

SenseController:
  Entity Layer: Entity
  Sense Update Interval: 0.3
  Max Detections: 20
  (stesso setup)
```

4. **Imposta Layer**: `Entity` ⚠️ **FONDAMENTALE!**
5. **Crea il Prefab** nel Project
6. **Elimina o disattiva** dalla scena

### ✅ Verifica:
- Layer = Entity
- EntityStatus ha Profile_SmallFish
- SenseController entityLayer = Entity

---

## 🏭 Step 6: Setup EntityPooler (5 minuti)

Crea il sistema di pooling che gestirà spawn/despawn delle entità.

### Procedura:

1. **Crea GameObject vuoto** nella scena: `EntityPooler`
2. **Aggiungi componente** `EntityPooler`
3. **Configura Pool Configs**:

#### Pool Config [0] - Plankton
```
Pool Name: "Plankton"
Prefab: Plankton_Prefab
Initial Size: 10
Max Size: 50
Allow Expansion: true
```

#### Pool Config [1] - SmallFish
```
Pool Name: "SmallFish"
Prefab: SmallFish_Prefab
Initial Size: 3
Max Size: 20
Allow Expansion: true
```

4. **Pool Container**:
   ```
   Pool Container: [LASCIA VUOTO]
   ```

### 🔍 Spiegazione Pool Container (il tuo dubbio!)

**Cos'è?** 
- Un semplice `Transform` che funge da "cartella" nella Hierarchy

**Cosa succede se lo lasci vuoto?**
- Il sistema crea automaticamente un GameObject: `EntityPool_Container`
- Questo GameObject diventa figlio di `EntityPooler`
- Tutte le entità poolate saranno figlie di questo container

**Cosa succede se lo assegni manualmente?**
- Puoi creare un GameObject vuoto: `PooledEntities`
- Assegnarlo al campo `Pool Container`
- Le entità verranno organizzate sotto quel GameObject

**Raccomandazione**: Lascialo vuoto! Il sistema lo gestisce perfettamente.

**Esempio di Hierarchy a runtime**:
```
EntityPooler
└── EntityPool_Container (creato automaticamente)
    ├── Plankton_0 (inactive)
    ├── Plankton_1 (inactive)
    ├── ...
    ├── SmallFish_0 (inactive)
    └── SmallFish_1 (inactive)
```

Quando spawni un'entità, viene **attivata** e può essere riposizionata altrove.  
Quando viene despawnata, torna sotto `EntityPool_Container` e viene **disattivata**.

5. **Debug Settings**:
   ```
   Show Pool Stats: true (per vedere statistiche GUI)
   ```

### ✅ Verifica:
- 2 Pool Config creati
- Prefab assegnati correttamente
- Pool Container lasciato vuoto

---

## 💻 Step 7: Script di Test - EntitySpawner.cs (10 minuti)

Crea uno script per testare facilmente il sistema.

### Procedura:

1. **Crea script**: `EntitySpawner.cs` nella cartella `EnitySystem/`
2. **Copia il codice seguente**:

```csharp
using UnityEngine;
using Deeploration.EntitySystem;

/// <summary>
/// Script di test per spawnare entità facilmente.
/// Premi Space per spawnare Plankton, P per SmallFish.
/// </summary>
public class EntitySpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Vector3 spawnAreaCenter = Vector3.zero;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(20f, 10f, 20f);
    
    [Header("Auto Spawn")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private int planktonCount = 10;
    [SerializeField] private int smallFishCount = 3;
    
    [Header("Debug")]
    [SerializeField] private bool showSpawnArea = true;
    
    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnInitialEntities();
        }
    }
    
    private void Update()
    {
        // Spawn manuale con tasti
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnRandomPlankton();
            Debug.Log("Spawned Plankton - Press Space to spawn more");
        }
        
        if (Input.GetKeyDown(KeyCode.P))
        {
            SpawnRandomSmallFish();
            Debug.Log("Spawned SmallFish - Press P to spawn more");
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            DespawnAll();
            Debug.Log("Cleared all entities");
        }
    }
    
    private void SpawnInitialEntities()
    {
        Debug.Log($"=== Auto Spawn: {planktonCount} Plankton + {smallFishCount} SmallFish ===");
        
        for (int i = 0; i < planktonCount; i++)
        {
            SpawnRandomPlankton();
        }
        
        for (int i = 0; i < smallFishCount; i++)
        {
            SpawnRandomSmallFish();
        }
        
        Debug.Log("✓ Spawn completato! Osserva i pesci cacciare.");
    }
    
    private void SpawnRandomPlankton()
    {
        Vector3 position = GetRandomPositionInArea();
        GameObject plankton = EntityPooler.Instance.Spawn("Plankton", position, Quaternion.identity);
        
        if (plankton == null)
        {
            Debug.LogError("Failed to spawn Plankton - Pool exhausted?");
        }
    }
    
    private void SpawnRandomSmallFish()
    {
        Vector3 position = GetRandomPositionInArea();
        GameObject fish = EntityPooler.Instance.Spawn("SmallFish", position, Quaternion.identity);
        
        if (fish == null)
        {
            Debug.LogError("Failed to spawn SmallFish - Pool exhausted?");
        }
    }
    
    private Vector3 GetRandomPositionInArea()
    {
        float x = Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2);
        float y = Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2);
        float z = Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2);
        
        return spawnAreaCenter + new Vector3(x, y, z);
    }
    
    private void DespawnAll()
    {
        if (EntityPooler.Instance != null)
        {
            EntityPooler.Instance.DespawnAllEntities();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showSpawnArea) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
    
    private void OnGUI()
    {
        // Help text
        GUI.Box(new Rect(10, Screen.height - 100, 300, 90), "=== Entity Spawner ===");
        GUI.Label(new Rect(20, Screen.height - 75, 280, 20), "Space: Spawn Plankton");
        GUI.Label(new Rect(20, Screen.height - 55, 280, 20), "P: Spawn SmallFish");
        GUI.Label(new Rect(20, Screen.height - 35, 280, 20), "C: Clear All Entities");
    }
}
```

3. **Aggiungi script alla scena**:
   - Seleziona `EntityPooler` GameObject
   - Aggiungi componente `EntitySpawner`

4. **Configura**:
   ```
   Spawn Area Center: (0, 0, 0)
   Spawn Area Size: (20, 10, 20)
   Auto Spawn On Start: true
   Plankton Count: 10
   Small Fish Count: 3
   Show Spawn Area: true
   ```

### ✅ Verifica:
- Script compilato senza errori
- EntitySpawner aggiunto a EntityPooler GameObject

---

## ▶️ Step 8: Esecuzione Test (5 minuti)

Finalmente è il momento di vedere il sistema in azione!

### Procedura:

1. **Premi Play** in Unity
2. **Osserva l'auto-spawn**:
   - Dovresti vedere 10 Plankton e 3 SmallFish apparire nell'area di spawn
   - Verifica nella Console: `"✓ Spawn completato! Osserva i pesci cacciare."`

3. **Cosa aspettarsi**:

#### Comportamento Plankton:
- Si muove lentamente in modo casuale (Idle State)
- Se vede SmallFish → cambia colore stato a ROSSO (Flee State) e scappa
- Barre di debug mostrano Fame che scende lentamente

#### Comportamento SmallFish:
- Inizia in Idle State (movimento casuale)
- Dopo ~40 secondi, Fame < 40 → entra in Hunt State (giallo)
- Insegue il Plankton più vicino
- Quando distanza < 2m → **cattura e mangia il Plankton**
- Console log: `"[HuntState] SmallFish_0 ha catturato Plankton_5!"`
- Plankton scompare (despawnato automaticamente)
- SmallFish recupera Fame e torna in Idle

4. **Verifica Visiva (Gizmos nella Scene View)**:
   - **Linee Verdi**: SmallFish → Plankton (caccia attiva)
   - **Linee Rosse**: Plankton ← SmallFish (fuga)
   - **Sfere Wireframe**: Raggio di sensing di ogni creatura
   - **Barre colorate**: Vita (rosso), Stamina (blu), Fame (arancione)

5. **Test Manuali**:
   - Premi **Space**: Spawna 1 Plankton aggiuntivo
   - Premi **P**: Spawna 1 SmallFish aggiuntivo
   - Premi **C**: Despawna tutte le entità

6. **GUI Statistics**:
   - In alto a sinistra dovresti vedere:
     ```
     === Entity Pool Stats ===
     Plankton: Active=8, Available=2, Total=10
     SmallFish: Active=3, Available=0, Total=3
     ```

### ✅ Test Superato Se:
- ✓ SmallFish rileva Plankton (linee verdi nei Gizmos)
- ✓ SmallFish entra in Hunt State quando affamato
- ✓ SmallFish mangia Plankton (Plankton despawna)
- ✓ Fame di SmallFish aumenta dopo aver mangiato
- ✓ Pool statistics aggiornate correttamente
- ✓ Nessun errore nella Console

---

## 🐛 Step 9: Troubleshooting Scenario-Specifico (5 minuti)

Problemi comuni e come risolverli:

### ❌ "SmallFish non rileva Plankton"

**Causa**: Layer non configurato correttamente

**Soluzioni**:
1. Verifica che **ENTRAMBI** i prefab abbiano Layer = `Entity`
2. Verifica in SenseController: `Entity Layer` deve includere il layer "Entity"
3. Test rapido: Seleziona SmallFish nella Scene → Inspector → SenseController → Verifica che `Prey Count > 0` quando vicino a Plankton

**Debug Command**:
```csharp
// Aggiungi temporaneamente in EntitySpawner.Update():
if (Input.GetKeyDown(KeyCode.D))
{
    var fish = GameObject.Find("SmallFish_0");
    if (fish != null)
    {
        var sense = fish.GetComponent<SenseController>();
        Debug.Log($"Prey detected: {sense.PreyCount}, Has prey: {sense.HasPreyNearby}");
    }
}
```

---

### ❌ "SmallFish vede Plankton ma non lo caccia"

**Causa**: Non è ancora affamato

**Soluzioni**:
1. **Attendi 40-60 secondi** (hungerThreshold = 40, hungerRate = 1/s)
2. **Test accelerato**: Nel CreatureProfile SmallFish, aumenta `hungerRate` a 5
3. **Force Hunt State**: Aggiungi al tuo EntitySpawner:

```csharp
if (Input.GetKeyDown(KeyCode.H))
{
    var fish = GameObject.Find("SmallFish_0");
    if (fish != null)
    {
        // Forza fame bassa
        var status = fish.GetComponent<EntityStatus>();
        // Nota: non c'è un setter pubblico, quindi devi modificare EntityStatus
        // per test, o semplicemente aumentare hungerRate nel Profile
        
        var manager = fish.GetComponent<EntityStateManager>();
        manager.ForceState(EntityState.Hunt);
        Debug.Log("Forced Hunt State - Fish should now chase prey");
    }
}
```

---

### ❌ "SmallFish caccia ma non cattura mai Plankton"

**Causa**: SmallFish troppo lento o distanza di cattura troppo piccola

**Verifica**:
- SmallFish speedFlee (8) deve essere > Plankton speedFlee (4) ✓
- Distanza di cattura è hardcoded a 2m in HuntState

**Soluzioni**:
1. Aumenta SmallFish `speedFlee` a 10
2. Riduci Plankton `speedFlee` a 3
3. Se il problema persiste, verifica in SenseController che `senseRadius` sia sufficiente (default 15m)

---

### ❌ "Plankton non scappa da SmallFish"

**Causa**: fearThreshold troppo piccolo o Flee State non attivato

**Verifica**:
1. Plankton Profile: `fearThreshold` = 5m
2. SmallFish deve essere entro 5m dal Plankton
3. Verifica transizioni in FleeState.cs

**Test**:
- Spawna 1 Plankton e 1 SmallFish vicini (~3m di distanza)
- Plankton dovrebbe immediatamente entrare in Flee (rosso)
- Se non accade, verifica che `predatorTypes` in Plankton includa `SmallFish`

---

### ❌ "Pool esaurito - Cannot spawn more entities"

**Causa**: Hai spawnato più entità di `maxSize`

**Soluzioni**:
1. Aumenta `maxSize` nel Pool Config (es. 100 per Plankton)
2. Usa `allowExpansion = true` (già impostato)
3. Despawna entità morte più velocemente (ridurre delay in PooledEntity.HandleEntityDied)

---

### ❌ "Entità spawna ma è invisibile"

**Causa**: Nessun mesh/sprite assegnato (questo tutorial usa solo collider)

**Soluzioni**:
Per vedere le entità visivamente:
1. Aggiungi un cubo o sfera 3D come child del prefab
2. Scala appropriata (es. 0.5 per Plankton, 1 per SmallFish)
3. Applica materiali colorati (es. verde per Plankton, blu per SmallFish)

**OPPURE** semplicemente usa i **Gizmos** per il debug (già abilitati).

---

### ❌ "Performance basse con molte entità"

**Causa**: Troppi physics queries

**Ottimizzazioni**:
1. Aumenta `senseUpdateInterval` a 0.5s (da 0.3s)
2. Riduci `maxDetections` a 10 (da 20)
3. Disabilita `showDebugSpheres` in SenseController per la build finale
4. Disabilita `showDebugRays` in ObstacleAvoidance

---

## 📊 Step 10: Test Avanzati (Opzionale)

### Test A: Stress Test del Pool

Spawna molte entità per verificare le performance:

```csharp
// In EntitySpawner, aggiungi:
if (Input.GetKeyDown(KeyCode.S))
{
    Debug.Log("=== STRESS TEST: Spawning 50 Plankton + 10 SmallFish ===");
    for (int i = 0; i < 50; i++) SpawnRandomPlankton();
    for (int i = 0; i < 10; i++) SpawnRandomSmallFish();
}
```

**Risultato atteso**:
- FPS deve rimanere > 30 anche con 60+ entità
- Pool deve espandersi automaticamente (se allowExpansion=true)
- Nessun errore di "pool exhausted"

---

### Test B: Catena Alimentare Completa

Aggiungi MediumFish che caccia SmallFish:

1. Crea `Profile_MediumFish`:
   - preyTypes = [SmallFish]
   - predatorTypes = [LargeFish, Shark]
   - speedFlee = 10 (più veloce di SmallFish)

2. Aggiungi al pooler e spawna 2 MediumFish

**Risultato atteso**:
- SmallFish deve scappare da MediumFish
- MediumFish deve cacciare SmallFish quando affamato
- Plankton → SmallFish → MediumFish (catena funzionante)

---

### Test C: Morte per Fame

Riduci maxHunger e aumenta hungerRate per vedere la morte naturale:

```
Plankton Profile:
  maxHunger: 50 (invece di 100)
  hungerRate: 5 (invece di 0.5)
```

**Risultato atteso**:
- Plankton muore dopo ~10 secondi
- Console log: "[EntityStatus] Plankton è morto!"
- Plankton despawna automaticamente dopo 2 secondi
- Pool statistics aggiornate

---

## 🎓 Conclusione Tutorial

**Congratulazioni!** Hai completato il setup e test del sistema di entità.

### 📋 Checklist Finale:
- ✓ Layer "Entity" creato e configurato
- ✓ 2 CreatureProfile creati (Plankton e SmallFish)
- ✓ 2 Prefab configurati con tutti i componenti
- ✓ EntityPooler funzionante con 2 pool
- ✓ EntitySpawner per test rapidi
- ✓ SmallFish caccia e mangia Plankton
- ✓ Pool statistics visibili
- ✓ Nessun errore in Console

### 🚀 Prossimi Passi:

1. **Aggiungi Visual Mesh**:
   - Crea modelli 3D per Plankton e SmallFish
   - Oppure usa primitive con materiali colorati

2. **Espandi Catena Alimentare**:
   - Aggiungi MediumFish, LargeFish, Shark
   - Crea ecosistema complesso

3. **Aggiungi Comportamenti**:
   - Schooling (banchi di pesci)
   - Territorialità
   - Riproduzione

4. **Ottimizza Performance**:
   - Spatial partitioning per entità
   - LOD per rendering
   - Object culling

5. **Aggiungi Gameplay**:
   - Player controller per interagire
   - Quest system basato su ecosistema
   - Resource gathering

---

## 📖 Riferimenti Rapidi

### Comando Spawn da Codice:
```csharp
GameObject entity = EntityPooler.Instance.Spawn("PoolName", position, rotation);
```

### Forza Cambio Stato (Debug):
```csharp
entity.GetComponent<EntityStateManager>().ForceState(EntityState.Hunt);
```

### Modifica Parametri Runtime (Test):
```csharp
EntityStatus status = entity.GetComponent<EntityStatus>();
// Nota: non ci sono setter pubblici per protezione
// Usa CreatureProfile per configurazioni permanenti
```

### Eventi Utili:
```csharp
status.OnEntityDied += (id, type) => Debug.Log($"{id} è morto!");
sense.OnPreyDetected += (prey, type) => Debug.Log("Preda rilevata!");
manager.OnStateChanged += (old, new) => Debug.Log($"Stato: {old} → {new}");
```

---

## Estensioni Future

- **Schooling**: Aggiungere Cohesion/Alignment a SteeringBehaviors
- **Territorialità**: Nuovo stato per difesa territorio
- **Riproduzione**: Sistema di spawn basato su condizioni
- **Livelli di aggressività**: Parametro nel Profile
- **Day/Night cycle**: Modifica comportamenti in base al tempo

---

**Versione**: 1.2  
**Autore**: Gemini LLM + Cline  
**Data**: 26 Ottobre 2025  
**Tutorial Aggiunto**: Sistema di Testing Completo
