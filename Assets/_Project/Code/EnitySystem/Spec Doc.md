# **Specifiche Tecniche: Deeploration \- Sistema di Gestione delle Entità**

| Campo | Valore |
| :---- | :---- |
| **Titolo del Sistema** | Deeploration: Sistema Astratto di Gestione delle Entità |
| **Versione** | 1.1 (Aggiornato) |
| **Autore** | Gemini LLM |
| **Data** | 23 Ottobre 2025 |

## **1\. Overview (Visione d'Insieme)**

Il sistema di Gestione delle Entità di Deeploration è il framework fondamentale per la simulazione di una fauna marina dinamica e interconnessa. Il suo scopo principale è fornire una base astratta e basata sui dati per definire e gestire il comportamento, lo stato vitale (Fame, Stamina) e le interazioni ecologiche (Catena Alimentare, Paura) di ogni singola creatura all'interno del mondo di gioco. Utilizzando profili configurabili esternamente (non hard-coded), questo sistema assicura che il design di nuove creature sia flessibile, rapido e non richieda modifiche al codice di base per ogni nuova implementazione, garantendo la vivacità dell'ecosistema di sopravvivenza subacquea.

## **2\. Goals & Key Features (Obiettivi e Funzionalità Chiave)**

Il sistema deve soddisfare i seguenti requisiti funzionali:

* **Definizione Astratta delle Entità:** Creare un'unica struttura dati configurabile (Profilo Creatura) da cui tutte le istanze di entità (pesci, predatori, ecc.) derivano le loro variabili primarie e parametri comportamentali.  
* **Gestione dello Stato Vitale (API):** Fornire API pubbliche per la lettura e la modifica dei parametri vitali (Fame, Stamina, Salute) dell'entità, garantendo che i componenti esterni non accedano direttamente ai campi privati.  
* **Simulazione della Catena Alimentare:** Implementare una logica che permetta a un'entità di identificare in modo configurabile quali altre entità sono considerate **Prede** e quali **Predatori**.  
* **Comportamento Reattivo (Caccia/Fuga):** Sviluppare un EntityStateManager che determini lo stato comportamentale corrente (es. Passeggia, Caccia, Fuggi, Riposa) in base ai parametri vitali (inclusa la soglia di Fame) e alle minacce percepite.  
* **Controllo del Movimento Guidato dallo Stato:** Un MovementController dedicato deve ricevere istruzioni dallo StateManager e tradurle in movimenti fisici specifici (nuoto veloce per la fuga, nuoto lento per la ricerca di cibo, ecc.), **gestendo l'uso della Stamina come risorsa per la velocità incrementata**.  
* **Sistema di Pooling per le Entità:** Implementare un sistema per il riciclo (pooling) delle istanze di entità per ottimizzare le prestazioni riducendo l'overhead di creazione e distruzione.  
* **Modularità per il Debug:** Tutti i componenti critici devono essere **separati e modulari**, esponendo i dati di stato e le transizioni tramite Eventi Pubblici per facilitare il debug e il profiling.

## **3\. Non-Goals (Delimitare il Perimetro)**

Le seguenti funzionalità **non** saranno implementate in questa versione (1.1) del sistema:

* **Pathfinding Avanzato o NavMesh:** Il movimento si baserà inizialmente su logiche di inseguimento e fuga basilari. La logica di movimento si baserà sul **pure steering** (steering forces), con **Raycast** o equivalenti per l'evitamento degli ostacoli in tempo reale (collision avoidance), specialmente durante la caccia o la fuga. Non sarà utilizzato un sistema di NavMesh 3D complesso o pathfinding basato su IA.  
* **Animazioni di Attacco/Danno:** Il sistema gestirà solo la logica di danno/morte, ma non le animazioni specifiche di attacco (queste saranno gestite in un sistema separato di VSFX/Animation).  
* **Networking/Multiplayer:** Il sistema è progettato per un'esperienza single-player; non sono inclusi meccanismi di sincronizzazione di rete.

## **4\. High-Level Architecture (Architettura ad Alto Livello)**

L'architettura proposta si basa sulla separazione delle responsabilità (Component-Based Design), sulla **modularità per il debug** e sull'uso intensivo di **Scriptable Objects** per la configurazione dei dati e del **State Pattern** per la gestione del comportamento.

### **Design Pattern**

1. **Component-Based Design (Focus sul Debug):** La logica è distribuita su componenti Unity separati (EntityStatus, MovementController, SenseController, FearBehaviour) che comunicano **indirettamente** tramite **API pubbliche** e/o **Eventi**. Questa separazione garantisce che ogni componente possa essere disattivato o analizzato isolatamente.  
2. **Scriptable Objects (Configurazione):** Verrà utilizzato uno ScriptableObject (CreatureProfile) per disaccoppiare i dati di configurazione (statistiche, parametri ecologici) dalla logica di runtime. *Motivazione: Massima flessibilità, riutilizzo dei dati e non hard-coding del design.*  
3. **State Pattern (Comportamento):** Il EntityStateManager utilizzerà lo State Pattern per gestire la transizione tra stati complessi. *Motivazione: Logicai pulita, testabile e scalabile per la complessità comportamentale.*  
4. **Comunicazione Indiretta (API/Eventi):** Le componenti non chiamano direttamente i metodi privati di altri componenti. Ad esempio, il MovementController **richiede** l'uso della Stamina al EntityStatus tramite un metodo pubblico, anziché modificarla direttamente.

## **5\. Core Components & Class Design (Componenti Principali)**

Di seguito sono dettagliati i componenti fondamentali del sistema:

### **5.1. ScriptableObject: CreatureProfile**

| Responsabilità | Campi Serializzati (Configurabili nell'Editor) | Riferimenti Principali |
| :---- | :---- | :---- |
| Contiene tutti i dati primari di un'entità. Utilizzato per generare istanze di entità con profili unici senza scrivere nuovo codice. | float maxHealth, float maxStamina, float maxHunger (range 0-100), float speedBase, float speedFlee (Velocità incrementata dalla Stamina), float hungerRate (per secondo), **float hungerThreshold (Soglia sotto cui si cerca cibo, es. 40\)**, float fearThreshold (distanza), List\<EntityType\> preyTypes, List\<EntityType\> predatorTypes | N/D (Dati grezzi, referenziato da componenti MonoBehaviour). |

### **5.2. MonoBehaviour: EntityStatus**

| Responsabilità | Campi Serializzati (Configurabili nell'Editor) | Riferimenti Principali |
| :---- | :---- | :---- |
| Gestisce i parametri vitali (Salute, Fame, Stamina) e la logica di decremento/incremento nel tempo. Fornisce API per la modifica dei valori. | CreatureProfile profile (Riferimento al SO), float currentHealth, float currentStamina, float currentHunger | EntityStateManager (Notifica lo stato vitale), GameEventSystem (Emette eventi di Morte/Danno), **MovementController (Riceve richieste di decremento Stamina).** |

### **5.3. MonoBehaviour: EntityStateManager**

| Responsabilità | Campi Serializzati (Configurabili nell'Editor) | Riferimenti Principali |
| :---- | :---- | :---- |
| Implementa lo **State Pattern**. Decide lo stato comportamentale (IDLE, HUNT, FLEE, REST) in base agli input (Fame da EntityStatus, Minaccia da SenseController). Gestisce le transizioni complesse. | N/D (Gestito internamente), BaseState currentState | MovementController (Fornisce lo stato di movimento richiesto), EntityStatus (Legge i parametri vitali), **SenseController (Riceve target di caccia/fuga).** |

### **5.4. MonoBehaviour: SenseController (Nuovo)**

| Responsabilità | Campi Serializzati (Configurabili nell'Editor) | Riferimenti Principali |
| :---- | :---- | :---- |
| Rileva entità vicine (Player, Prede, Predatori). Utilizza Raycast per evitare ostacoli e calcola la direzione di steer. | float sightRadius, LayerMask obstacleMask, float maxSteerForce | EntityStateManager (Notifica la presenza di minacce o prede), MovementController (Suggerisce la direzione per l'evitamento degli ostacoli). |

### **5.5. MonoBehaviour: MovementController**

| Responsabilità | Campi Serializzati (Configurabili nell'Editor) | Riferimenti Principali |
| :---- | :---- | :---- |
| Esegue il movimento fisico 3D basato sullo steering. Applica la velocità appropriata. **Gestisce la richiesta di velocità accelerata (Fuga/Caccia) e la traduce in una richiesta di Stamina a EntityStatus**. | float rotationSpeed, Rigidbody rb (o Componente Movimento) | EntityStateManager (Riceve l'istruzione di stato e il target), **EntityStatus (Richiede/verifica l'uso di Stamina per l'accelerazione).** |

## **6\. Public API & Interactions (Interfacce Pubbliche)**

Il sistema deve esporre i seguenti metodi e interagire tramite eventi.

### **6.1. Metodi Pubblici (Interfaccia IEntityAPI su EntityStatus)**

| Metodo | Descrizione | Chiamato da |
| :---- | :---- | :---- |
| void ApplyDamage(float amount) | Riduce la salute e verifica la condizione di morte. | Player (attacco), Altre Entità (predazione). |
| void ConsumeFood(float amount) | Diminuisce la fame (currentHunger). | EntityStateManager (quando lo stato è HUNT e trova cibo). |
| float GetStatusValue(StatusType type) | Ottiene il valore attuale di un parametro vitale (Health, Stamina, Hunger). | UI, Sistemi di Analisi/Scansione. |
| **bool TryUseStamina(float amount)** | Tenta di ridurre la stamina. Ritorna true se la stamina è sufficiente, altrimenti false. | **MovementController (Durante l'accelerazione Caccia/Fuga).** |
| bool IsPreyOf(EntityType type) | Verifica se l'entità corrente è una preda di un dato tipo. | Altre Entità (SenseController). |

### **6.2. Eventi Pubblici**

* OnEntityDied(EntityID id, EntityType type): Emesso quando la salute scende a zero.  
* OnEntityHealthChanged(EntityID id, float newHealth): Emesso ad ogni cambio significativo di salute.  
* OnEntityStateChanged(EntityID id, EntityState newState): Emesso quando lo StateManager passa a un nuovo stato (es. da IDLE a FLEE).  
* **OnStaminaDepleted(EntityID id):** Emesso quando TryUseStamina fallisce (Stamina a zero). Il MovementController dovrebbe ascoltare questo per forzare la velocità base.

## **7\. Test Cases & Casi Limite (Scenari di Test)**

| Scenario di Test | Azione/Stato di Partenza | Risultato Atteso | Caso Limite/Fonte di Errore |
| :---- | :---- | :---- | :---- |
| **Logica Caccia (Successo)** | Entità A: currentHunger \< hungerThreshold (30), Stamina 100\. Entità B (Prey) rilevata da SenseController. | EntityStateManager passa a HUNT. MovementController tenta di usare speedFlee. **MovementController chiama TryUseStamina(rate) su EntityStatus, la quale ritorna true e decrementa la Stamina.** | Stamina a 0: TryUseStamina ritorna false. MovementController deve usare speedBase e fermare la richiesta di Stamina. |
| **Logica Fuga (Successo)** | Entità A (Prey): Predatore rilevato. Stamina 100\. | EntityStateManager passa a FLEE. **MovementController accelera richiedendo Stamina. La Stamina scende finché la minaccia scompare o la risorsa si esaurisce.** | Ostacolo Immediato: SenseController deve rilevare l'ostacolo con Raycast e fornire una direzione di steering che eviti la collisione, mantenendo lo stato FLEE. |
| **Stamina Esaurita** | Entità C (Stamina 5). MovementController tenta TryUseStamina(10). | EntityStatus ritorna false. Evento OnStaminaDepleted emesso. | MovementController ignora OnStaminaDepleted: L'entità continua a tentare l'accelerazione in modo inefficiente. |
| **Configurazione Assente** | EntityStatus istanziato con CreatureProfile nullo. | Generazione di un errore di runtime chiaro e loggato in console (es. NullReferenceException con messaggio informativo). | Profilo nullo: Aggiungere un controllo iniziale if (profile \== null) in Awake(). |

## **8\. Piano di Sviluppo (Action Plan)**

La sequenza di implementazione suggerita per lo sviluppo del sistema è la seguente:

1. **Framework dei Dati (Scriptable Object):**  
   * Creare la definizione della classe CreatureProfile (ScriptableObject) con tutti i campi serializzati. Includere la variabile **hungerThreshold**.  
   * Definire l'enumeratore EntityType e creare un primo profilo di test.  
2. **Base Status & Stamina Logic:**  
   * Implementare EntityStatus e la sua logica di inizializzazione e decremento automatico.  
   * Implementare il metodo pubblico **TryUseStamina(float amount)** e la logica per emettere OnStaminaDepleted.  
3. **Sistema di Pooling:**  
   * Implementare una classe base di EntityPooler per gestire l'attivazione e la disattivazione efficiente delle entità.  
4. **Movimento Base & Steering:**  
   * Implementare MovementController con logica di base per il movimento 3D e l'applicazione di velocità.  
   * Implementare il codice per la transizione tra speedBase e speedFlee in base al successo di TryUseStamina.  
5. **Sense Controller:**  
   * Implementare SenseController per il rilevamento di Prede/Predatori e l'uso dei Raycast per l'evitamento degli ostacoli (Steering Logic).  
6. **State Machine (Core Logic):**  
   * Implementare EntityStateManager che utilizza **hungerThreshold** e gli input di **SenseController** per guidare le transizioni di stato (IDLE, HUNT, FLEE).  
7. **Integrazione Finale API/Eventi:**  
   * Connettere tutti gli eventi e assicurare che la comunicazione indiretta tra MovementController e EntityStatus funzioni correttamente durante gli stati di Caccia/Fuga.