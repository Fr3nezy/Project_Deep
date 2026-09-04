# Action Plan — Darkness Stress

**Goal:** Rendere il buio la baseline ambientale di Deeplonauts e usare poche isole luminose, coerenti visivamente, per ridurre in modo configurabile e graduale lo stress del Diver.

**Estimated tasks:** 5 across 3 phases

---

### Phase 1: Foundation
*Contratto minimo tra level design e sistema di stress esistente.*

1. **Creare `DarknessStressSource.cs`** in `Assets/_Project/Code/Player/`
   - Class type: `MonoBehaviour`, implementa `IStressSource`.
   - Il buio è lo stato predefinito: fuori da una zona luminosa restituisce uno stress ambientale configurabile al secondo.
   - Espone `GetStressPerSecond()` senza modificare `IStressSource`, `StressSystem` o `StressProfile`.
   - Mantiene un livello di protezione luminosa corrente, smussato nel tempo, e mappa il contributo ambientale da `darkStressPerSecond` fino a zero o a un minimo configurabile.
   - Campi iniziali: `darkStressPerSecond`, `minimumStressPerSecond`, `lightTransitionSpeed`.

2. **Creare `StressLightZone.cs`** in `Assets/_Project/Code/Environment/`
   - Class type: `MonoBehaviour` con `Collider` trigger.
   - Rappresenta un'isola luminosa e fornisce un valore `stressRelief` normalizzato `0–1`.
   - Richiede un riferimento alla luce visiva della zona, così configurazione gameplay e resa restano sullo stesso GameObject.
   - Registra e rimuove la propria influenza su `DarknessStressSource` quando il player entra/esce.
   - Campi iniziali: `stressRelief`, riferimento `Light`; nessun sampling runtime dell'illuminazione.

---

### Phase 2: Core Logic
*Composizione delle zone e transizioni senza scatti.*

3. **Implementare la risoluzione delle zone attive in `DarknessStressSource`**
   - Accettare registrazione e rimozione di più `StressLightZone` sovrapposte.
   - Usare il valore di sollievo più alto tra le zone attive, evitando che trigger sovrapposti sommino oltre il massimo.
   - Smussare il valore effettivo con una risposta frame-rate independent.
   - Gestire zone disabilitate o distrutte mentre il player è al loro interno senza lasciare protezione residua.

4. **Integrare la nuova fonte nel player**
   - Aggiungere `DarknessStressSource` come figlio o componente nell'albero già scansionato da `StressSystem`.
   - Lasciare invariati `LookStressSource`, recupero, vignetta e consumo O₂: lo stress ambientale entra una sola volta tramite l'aggregazione esistente.

---

### Phase 3: Scene Integration & Verification
*Allestimento del test environment nella scena Prototype.*

5. **Configurare `Prototype.unity` e verificare il loop**
   - Abbassare l'illuminazione ambientale per rendere il buio la condizione dominante.
   - Creare poche isole luminose con `Light`, trigger e `StressLightZone` sullo stesso GameObject.
   - Valori iniziali consigliati: buio `8 stress/s`, minimo in luce piena `0 stress/s`, transizione `2 s⁻¹`, sollievo zona sicura `1`, penombra `0.5`.
   - In Play Mode verificare: stress crescente nel buio, riduzione graduale entrando nella luce, ripresa graduale uscendo, nessun salto ai bordi, nessun doppio consumo O₂ e nessun errore Console.

---

## Fuori scope

- Creatura di test e refactor dell'`EntitySystem` legacy.
- Analisi fisica di luci, ombre, esposizione o pixel della camera.
- Nuovi profili ScriptableObject o campi ambientali in `StressProfile`.
- Multiplayer: i valori locali restano serializzati e potranno essere sincronizzati quando verrà scelto il modello di rete.
