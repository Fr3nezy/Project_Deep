# Darkness Stress — Documentation

**Version:** 1.0  
**Date:** 2026-08-27  
**Status:** Implemented

---

## Overview

Darkness Stress rende il buio la condizione ambientale predefinita di Deeplonauts: fuori dalle isole luminose il Diver accumula stress, mentre le zone illuminate ne riducono gradualmente il contributo. Il sistema usa trigger espliciti invece di campionare l'illuminazione fisica, mantenendo coerenti resa visiva e gameplay con valori controllabili dal level designer.

## Architecture

**Patterns used:** component composition, interface-based source aggregation, trigger zones, strongest-wins overlap resolution.

**Data flow:** `StressLightZone` rileva l'ingresso del collider del player e si registra nel relativo `DarknessStressSource`. La source seleziona il sollievo maggiore tra le zone attive, lo interpola in modo indipendente dal frame rate e converte il risultato in stress al secondo. `StressSystem`, tramite l'interfaccia `IStressSource`, aggrega questo valore insieme alle altre fonti e applica il normale flusso stress → consumo O₂ → feedback della tuta.

**Key design decisions:**

- Il buio è la baseline: non serve coprire il livello con volumi di oscurità.
- Luce visiva e sollievo gameplay vivono sulla stessa zona, ma l'intensità della `Light` non viene campionata a runtime.
- Le zone sovrapposte non sommano il sollievo: prevale il valore più alto.
- Le transizioni sono smussate con risposta esponenziale frame-rate independent.
- Zone disabilitate o distrutte vengono eliminate automaticamente dalla lista attiva.
- Nessun nuovo ScriptableObject: i pochi valori di tuning restano serializzati sui componenti.

## Class Reference

### DarknessStressSource

**File:** `Assets/_Project/Code/Player/DarknessStressSource.cs`  
**Type:** MonoBehaviour, `IStressSource`  
**Responsibility:** calcola il contributo di stress ambientale in base alla migliore zona luminosa attiva.

**Serialized Fields:**

| Field | Type | Description |
|---|---|---|
| `darkStressPerSecond` | `float` | Stress al secondo nel buio completo. Default: `8`. |
| `minimumStressPerSecond` | `float` | Stress residuo con sollievo luminoso pieno. Default: `0`. |
| `lightTransitionSpeed` | `float` | Velocità di convergenza verso il sollievo target. Default: `2`. |
| `currentLightRelief` | `float` | Sollievo corrente smussato, normalizzato `0–1`; visibile per debug. |

**Public API:**

| Method / Event | Signature | Description |
|---|---|---|
| `RegisterLightZone` | `(StressLightZone zone) : void` | Registra una zona luminosa attualmente occupata. |
| `UnregisterLightZone` | `(StressLightZone zone) : void` | Rimuove una zona luminosa. |
| `GetStressPerSecond` | `() : float` | Restituisce lo stress ambientale corrente; restituisce `0` se il componente è disabilitato. |

**Dependencies:** `IStressSource`, `StressLightZone`, `StressSystem`

### StressLightZone

**File:** `Assets/_Project/Code/Environment/StressLightZone.cs`  
**Type:** MonoBehaviour  
**Responsibility:** rappresenta un'isola luminosa e registra il relativo sollievo sulla source del player quando questo attraversa il trigger.

**Serialized Fields:**

| Field | Type | Description |
|---|---|---|
| `stressRelief` | `float` | Sollievo normalizzato `0–1`; `1` annulla il contributo ambientale fino al minimo configurato. |
| `visualLight` | `Light` | Luce Unity associata alla zona; obbligatoria per mantenere coerenza visiva e gameplay. |

**Public API:**

| Method / Event | Signature | Description |
|---|---|---|
| `StressRelief` | `float { get; }` | Espone il sollievo configurato alla source del player. |

**Dependencies:** `Collider`, `Light`, `DarknessStressSource`

---

## Usage Guide

Il sistema viene normalmente usato tramite composizione in scena. `StressSystem` trova automaticamente le implementazioni di `IStressSource` presenti nei figli durante `Awake`.

```csharp
public sealed class TemporarySafeArea : MonoBehaviour
{
    [SerializeField] private DarknessStressSource darkness;
    [SerializeField] private StressLightZone zone;

    public void EnableRelief() => darkness.RegisterLightZone(zone);
    public void DisableRelief() => darkness.UnregisterLightZone(zone);
}
```

**Common patterns:**

- Aggiungere `DarknessStressSource` sul `PlayerCapsule` prima dell'esecuzione di `StressSystem.Awake`.
- Usare `stressRelief = 1` per un'isola sicura e `0.5` per una zona di penombra.
- Lasciare che i trigger registrino e rimuovano automaticamente le zone; chiamare l'API pubblica solo per sorgenti non fisiche.
- Disabilitare una zona è sicuro: `OnDisable` rimuove tutte le registrazioni correnti.

---

## Manual Setup Wiki

> Seguire questi passaggi per ricreare manualmente il setup della scena `Prototype`.

### Step 1: Scripts

I file si trovano in:

- `Assets/_Project/Code/Player/DarknessStressSource.cs`
- `Assets/_Project/Code/Environment/StressLightZone.cs`

Unity li compila automaticamente.

### Step 2: ScriptableObject Assets

Nessun nuovo ScriptableObject è richiesto. Il sistema usa lo `StressProfile` già assegnato a `StressSystem`.

### Step 3: Scene GameObjects

| GameObject | Component | Parent | Notes |
|---|---|---|---|
| `PlayerCapsule` | `DarknessStressSource` | Player root | Source rilevata automaticamente da `StressSystem`. |
| `SafeLightZone` | `Light`, `BoxCollider`, `StressLightZone` | Scene environment | Posizione `(-1, 2, -18)`; trigger `14 × 6 × 14`. |
| `DimLightZone` | `Light`, `BoxCollider`, `StressLightZone` | Scene environment | Posizione `(7, 2, -4)`; trigger `10 × 5 × 10`. |

### Step 4: Inspector Wiring

**PlayerCapsule — DarknessStressSource:**

- `Dark Stress Per Second` → `8`
- `Minimum Stress Per Second` → `0`
- `Light Transition Speed` → `2`

**SafeLightZone:**

- `BoxCollider.isTrigger` → attivo
- `BoxCollider.size` → `(14, 6, 14)`
- `Light.intensity` → `6`
- `Light.range` → `8`
- `Stress Relief` → `1`
- `Visual Light` → trascinare la `Light` dello stesso GameObject

**DimLightZone:**

- `BoxCollider.isTrigger` → attivo
- `BoxCollider.size` → `(10, 5, 10)`
- `Light.intensity` → `2`
- `Light.range` → `6`
- `Stress Relief` → `0.5`
- `Visual Light` → trascinare la `Light` dello stesso GameObject

**Lighting Settings:**

- Ambient Source → `Color`
- Ambient Color → `(0.005, 0.01, 0.02)`
- Ambient Intensity → `0.1`

### Step 5: VContainer Registration

Nessuna registrazione VContainer è necessaria. Il sistema usa componenti Unity e la scansione locale già eseguita da `StressSystem`.

### Step 6: Verify

1. Entrare in Play Mode fuori dalle zone luminose.
2. Verificare nel counter di debug uno stress ambientale di circa `8 stress/s`.
3. Entrare in `SafeLightZone`: il contributo deve scendere gradualmente fino a `0`.
4. Uscire dalla zona: il contributo deve tornare gradualmente verso `8 stress/s`.
5. Entrare in `DimLightZone`: il contributo deve convergere circa a metà del valore di buio.
6. Sovrapporre due zone: deve prevalere quella con `Stress Relief` maggiore.
7. Disabilitare una zona mentre il player è al suo interno: il sollievo deve essere rimosso senza residui.
8. Console: nessun errore. Se `Visual Light` non è assegnata, la zona deve segnalare l'errore e disabilitarsi.
