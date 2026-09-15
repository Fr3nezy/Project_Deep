# Action Plan — Darkness Stress [STORICO / COMPLETATO]

> [!NOTE]
> **DOCUMENTO STORICO / SPECIFICA INIZIALE ARCHIVIATA**
> Questo documento rappresenta il piano operativo originario del 2026-08-27, interamente implementato e verificato.
> Per la documentazione tecnica canonica e i dettagli del codice attuale, consultare [DarknessStress-docs.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/DarknessStress-docs.md).

**Goal:** Rendere il buio la baseline ambientale di Deeplonauts e usare poche isole luminose, coerenti visivamente, per ridurre in modo configurabile e graduale lo stress del Diver.

**Estimated tasks:** 5 across 3 phases (Completate)

---

### Phase 1: Foundation
*Contratto minimo e sorgente di stress da oscurità*

- [x] **Task 1: Definire l'interfaccia IStressSource e aggiornare StressSystem**
  - Path: `Assets/_Project/Code/Player/Stress/IStressSource.cs` e `Assets/_Project/Code/Player/Stress/StressSystem.cs`
  - Contratto: `float GetStressPerSecond()`
  - Auto-discovery via `GetComponentsInChildren<IStressSource>(true)`

- [x] **Task 2: Creare DarknessStressSource**
  - Path: `Assets/_Project/Code/Player/Stress/DarknessStressSource.cs`
  - Calcola lo stress in base a oscurità e torcia

---

### Phase 2: World Integration
*Trigger ambientali e integrazione con la torcia*

- [x] **Task 3: Creare il componente StressLightZone**
  - Path: `Assets/_Project/Code/Environment/StressLightZone.cs`
  - Registrazione su trigger enter/exit, validazione `visualLight`

- [x] **Task 4: Setup scene Prototype con isole luminose**
  - Configurate `SafeLightZone` e `DimLightZone`

---

### Phase 3: Feedback & Polish
*Validazione e visualizzazione*

- [x] **Task 5: Collaudo e documentazione**
  - Validazione con `SuitDebugOverlay` e profilo di vignetta URP
