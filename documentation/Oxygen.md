# Oxygen System
Ultima verifica: 2026-11-10

## Scopo e confini
Gestisce la riserva vitale di ossigeno (O₂) nello scafandro del diver. L'ossigeno viene costantemente consumato dal metabolismo basale della tuta e subisce incrementi di drenaggio derivanti dallo sforzo motorio (`DiverController`) e dal livello di agitazione psicologica (`StressSystem`).
Il sistema è completamente disaccoppiato dalle singole sorgenti tramite l'interfaccia `IOxygenDrainSource`. Quando l'ossigeno si esaurisce, notifica l'evento critico per la gestione del fallimento o del soffocamento.

## File e componenti
- `Assets/_Project/Code/Player/Oxygen/OxygenSystem.cs`: Gestore principale della quantità di O₂, del consumo, del refill e degli stati di allerta.
- `Assets/_Project/Code/Player/Oxygen/OxygenProfile.cs`: ScriptableObject con la configurazione di capacità, consumi base e soglie critiche.
- `Assets/_Project/Code/Player/Oxygen/IOxygenDrainSource.cs`: Interfaccia implementata da qualsiasi sistema che consuma ossigeno in unità/secondo.
- `OxygenLevel` (enum definito in `OxygenSystem.cs`): Livello di severità della riserva (`Normal`, `Warning`, `Critical`, `Depleted`).

## Dipendenze e flusso dati
- **Sorgenti di consumo**: Registra automaticamente o via codice ogni componente che implementa `IOxygenDrainSource` (nello specifico: `DiverController` per il moto e `StressSystem` per il panico).
- **Ricarica**: Interagisce con le stazioni ambientali come `OxygenRefillStation` tramite `Refill(float deltaTime)`.
- **UI & Feedback**: Notifica la variazione normalizzata a `SuitDebugOverlay` via `OnOxygenChanged`.

```mermaid
flowchart TD
    Diver[DiverController] -->|IOxygenDrainSource| Oxy[OxygenSystem]
    Stress[StressSystem] -->|IOxygenDrainSource| Oxy
    BaseDrain[OxygenProfile.baseDrainPerSecond] --> Oxy
    Station[OxygenRefillStation] -->|Refill deltaTime| Oxy
    Oxy -->|OnOxygenChanged| UI[SuitDebugOverlay]
    Oxy -->|OnOxygenLevelChanged / OnOxygenDepleted| GameRules[Game Loop / Respawn]
```

## Componenti

### OxygenSystem
- **Responsabilità e ciclo di vita**:
  - `Awake()`: Tenta di caricare il profilo `profile`. Cerca tutte le istanze di `IOxygenDrainSource` nei componenti figli (`GetComponentsInChildren<IOxygenDrainSource>(true)`). Inizializza `currentOxygen` a `profile.maxOxygen × startNormalized` e valuta subito il livello (dal 2026-11-10; prima sempre pieno).
  - `Update()`: Calcola la somma di tutti i drain attivi + `profile.baseDrainPerSecond`. Se il diver non è in ricarica, decrementa `currentOxygen` per `Time.deltaTime` e verifica le transizioni di `OxygenLevel`.
- **Campi Inspector**:
  - `OxygenProfile profile`: Configurazione dei valori di O₂.
  - `float startNormalized` (range 0-1, default `1`): frazione del serbatoio all'avvio. In `SCN_Gameplay` l'istanza del Player ha `0.52` (decisione del 2026-11-09).
  - `float currentOxygen` (debug sola lettura): Quantità attuale di litri/unità O₂.
  - `float currentDrainPerSecond` (debug sola lettura): Consumo istantaneo totale al secondo.
  - `OxygenLevel currentLevel` (debug sola lettura): Livello di rischio corrente.
- **API pubbliche**:
  - `event Action<float> OnOxygenChanged`: Evento con percentuale normalizzata (0.0 a 1.0).
  - `event Action<OxygenLevel> OnLevelChanged`: Invocato al cambio di livello (`Safe`, `Warning`, `Critical`, `Emergency`, `Depleted`).
  - `event Action OnOxygenDepleted`: Invocato quando l'ossigeno tocca 0.
  - `float CurrentOxygen { get; }`
  - `float MaxOxygen { get; }`
  - `float NormalizedOxygen { get; }`
  - `float CurrentDrainPerSecond { get; }`
  - `OxygenLevel Level { get; }`
  - `void RegisterDrainSource(IOxygenDrainSource source)`: Aggiunge dinamicamente una fonte di consumo.
  - `void UnregisterDrainSource(IOxygenDrainSource source)`: Rimuove una fonte di consumo.
  - `bool Refill(float deltaTime)`: Ricarica la riserva di `profile.refillRate * deltaTime`; ritorna false a serbatoio pieno.
  - `void SetNormalized(float normalized)` (dal 2026-11-10): porta il serbatoio a una frazione della capacità e rivaluta il livello, uscendo anche da `Depleted`. Usato da `DiverRespawn`.
  - `void Consume(float amount)` (dal 2026-10-15): perdita istantanea di ossigeno oltre al consumo continuo; valori negativi ignorati, a serbatoio vuoto non fa niente. Usato dal morso del viperfish (`DiverBiteReceiver`, vedi [Creatures.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Creatures.md)).
  - Correzione del 2026-10-15: questa lista elencava `OnOxygenLevelChanged`, `CurrentLevel`, `IsDepleted` e `SetOxygenDirect`, che nel codice non esistono (i nomi veri sono `OnLevelChanged` e `Level`).
- **Riferimenti obbligatori e comportamento se mancanti**:
  - Se `profile` è nullo, stampa `Debug.LogError` e disabilita il componente.

### OxygenProfile
- **Responsabilità e ciclo di vita**:
  - `ScriptableObject` che definisce i limiti fisici della bombola della tuta.
- **Campi Inspector**:
  - `float maxOxygen` (default: `400f`).
  - `float baselineDrain` (default: `0.8f` unità/s, consumo da fermo).
  - `float refillRate` (default: `25f` unità/s alla stazione).
  - `float warningThreshold` (`0.5f`), `float criticalThreshold` (`0.25f`), `float emergencyThreshold` (`0.1f`).
  - Correzione del 2026-11-10: la lista precedente riportava nomi e default vecchi (`baseDrainPerSecond` 0.25, `refillRatePerSecond` 15, soglie 35%/15%).

### DiverRespawn *(2026-11-10)*
- **Responsabilità e ciclo di vita**: checkpoint del diver, sul `PlayerCapsule` accanto a `OxygenSystem` (`RequireComponent`). Salva la posa iniziale in `Awake`; si iscrive a `OnOxygenDepleted` in `OnEnable`. A ossigeno finito aspetta `respawnDelay`, poi `Respawn()`: spegne il `CharacterController`, porta il diver al checkpoint (solo yaw) o alla posa iniziale se non ce n'è uno, lo riaccende, chiama `DiverController.ResetMotion()` e `OxygenSystem.SetNormalized(respawnOxygen)`.
- **Campi Inspector**: `float respawnDelay` (`2` s), `float respawnOxygen` (range 0-1, `1`).
- **API pubbliche**: `Transform Checkpoint { get; }`, `void SetCheckpoint(Transform point)` (lo chiama `OxygenRefillStation` quando il diver entra a stazione accesa), `void Respawn()`.
- **Limiti**: nessuna dissolvenza né suono; durante l'attesa il diver si muove ancora; non ripristina oggetti, porte o creature. Verificato in Play il 2026-11-10 da codice: ossigeno a 0 a z −100, dopo 2 s diver sul `RespawnPoint` della stazione O₂ 1, a terra, ossigeno 1.0.

### IOxygenDrainSource
- **Interfaccia**:
  ```csharp
  public interface IOxygenDrainSource
  {
      float GetOxygenDrainPerSecond();
  }
  ```
  Permette a qualsiasi modulo futuro (es. fughe dalla tuta, ferite, freddo estremo, attrezzi pneumatici) di sottrarre ossigeno senza accoppiarsi strettamente all'`OxygenSystem`.

## Setup in Unity
1. Aggiungere `OxygenSystem` al GameObject radice del player (`PlayerCapsule`).
2. Creare o assegnare un asset `OxygenProfile` (es. `OxygenProfile_Standard.asset`).
3. Assicurarsi che i componenti che implementano `IOxygenDrainSource` (`DiverController`, `StressSystem`) si trovino sul GameObject o nei suoi figli.

## Configurazione verificata in prefab e scene
- In [Player.prefab](file:///Z:/_PROJECTS/Unity/Project_Deep/Assets/_Project/Prefabs/Player.prefab):
  - `OxygenSystem` è posizionato sulla radice.
  - Riceve drain da `DiverController` (camminata/corsa) e da `StressSystem` (panico da buio/movimenti bruschi).

## Estensione del sistema
- Riconoscimento bombole secondarie / upgrade di capacità: sostituzione o modifica dinamica del profilo di capienza.
- Effetto soffocamento: sottoscrivere `OnOxygenDepleted` per avviare il blackout e il reload dal checkpoint.

## Limiti e problemi noti
- Se il diver entra in una stazione di ricarica (`OxygenRefillStation`), questa chiama `Refill(Time.deltaTime)` in `OnTriggerStay()`. La stazione ha un proprio parametro serializzato `refillRatePerSecond` che attualmente non viene usato, in quanto la velocità effettiva è governata da `OxygenProfile.refillRatePerSecond`.

## Configurazione in `SCN_Gameplay` (2026-11-10)
- Istanza del Player: `startNormalized` 0.52, `DiverRespawn` con i default. Dalla capsula alla stazione O₂ 1 (circa 90 m) si arriva al 24% circa al buio e al 37% circa con la torcia (stima dai consumi, non misurata camminando).

## Sistemi collegati
- [PlayerMovement.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/PlayerMovement.md)
- [Stress.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Stress.md)
- [Environment.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Environment.md)
- [SuitFeedback.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/SuitFeedback.md)
