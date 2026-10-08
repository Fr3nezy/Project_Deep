# Editor Tools & Level Builders
Ultima verifica: 2026-10-07

## Scopo e confini
Raggruppa gli strumenti di automazione, generazione procedurale e utility dell'Editor di Unity per accelerare il blockout delle scene, la configurazione dei prefab e la creazione di asset di dati:
1. `DeeplonautsLevelBuilder`: Generatore automatico del livello di graybox per la slice prototipale (fossa oceanica, crash site, base DR-04, alloggiamenti energetici, trigger di missione, illuminazione e player setup).
2. `CreatureProfileCreator`: Utility per istanziare rapidamente i file ScriptableObject `CreatureProfile` dalla Project Window.
3. `PrologueSmokeCheck`: Controllo parziale eseguibile in Play Mode per input e reset del moto dopo una sequenza del prologo.
4. `PrologueWave1Builder`: Strumento di setup automatico per la Wave 1 (LocalizationSettings, tabelle stringhe, Timeline intro/exit, allestimento UI Canvas e scena `SCN_Gameplay`).
5. `PrologueElevatorSceneBuilder`: Costruttore e setup automatico della scena isolata di prologo `SCN_Intro.unity` (cabina ascensore con oblò, slot mesh Blender, player in prima persona e treadmill sottomarino).

## File e componenti
- `Assets/_Project/Code/Editor/DeeplonautsLevelBuilder.cs`: Script Editor (disponibile via menu `Tools/Deeplonauts/Build Prototype Graybox Level`) che costruisce l'intera scena prototipo da zero.
- `Assets/_Project/Code/Editor/PrologueElevatorSceneBuilder.cs`: Script Editor (menu `Deeplonauts/Prologue/Build Elevator Prologue Scene`) che genera la scena isolata di discesa `SCN_Intro.unity`.
- `Assets/_Project/Code/Editor/PrologueSmokeCheck.cs`: Script Editor con menu `Deeploration/Tests/Check Prologue Control Restored`.
- `Assets/_Project/Code/Editor/PrologueWave1Builder.cs`: Script Editor con menu `Deeplonauts/Prologue/Build Wave 1 Prologue Setup`.
- `Assets/_Project/Code/Editor/RockBlendDebugMenu.cs`: Script Editor con menu `Deeplonauts/Debug/Rock Blend Masks` che mostra le maschere di fusione delle rocce come colori. Documentato in [EnvironmentShading.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/EnvironmentShading.md).
- `Assets/_Project/Code/EntitySystem/Editor/CreatureProfileCreator.cs`: Script Editor (menu `Assets/Create/Deeploration/Creature Profile`) per la creazione guidata di asset creatura.
flowchart TD
    Menu[Menu: Tools/Deeplonauts/Build Prototype Graybox Level] --> Builder[DeeplonautsLevelBuilder]
    Builder --> Terrain[Scogliera / Gola / Pavimentazione]
    Builder --> Base[Base DR-04 & AirlockDoor]
    Builder --> Items[Power Cells & ItemSockets]
    Builder --> Station[OxygenRefillStation]
    Builder --> Quests[QuestManager & QuestProfile]
    Builder --> Player[Player Setup & Positioning]
    MenuAsset[Menu: Assets/Create/.../Creature Profile] --> Creator[CreatureProfileCreator]
    Creator --> Asset[CreatureProfile.asset]
```

## Componenti

### DeeplonautsLevelBuilder
- **Responsabilità e ciclo di vita**:
  - Classe statica con metodo menu `[MenuItem("Tools/Deeplonauts/Build Prototype Graybox Level")]`.
  - Distrugge la vecchia gerarchia di graybox (`_GRAYBOX_ROOT`) se già presente, prevenendo duplicati.
  - Ricostruisce ordinatamente:
    1. Geometria del canyon e della piana abissale.
    2. Modulo base scientifica DR-04 con paratia mobile `AirlockDoor`.
    3. Stazione di ricarica ossigeno `OxygenRefillStation`.
    4. Console di comunicazione e pod di rifornimento.
    5. Tre celle energetiche (`ItemPickup`) e il generatore principale (`ItemSocket`).
    6. Materiali URP con colori e proprietà fisiche/emissive appropriate.
    7. Generatore di particelle marine con `MarineSnowFollower`.
    8. Setup e riposizionamento della capsula giocatore (`PlayerCapsule`) con `PlayerInteraction` e `PlayerHands`.
    9. Generazione dell'asset di quest `Quest_Objective_1.asset` con reflection dei campi privati.
- **API pubbliche**:
  - `static void BuildLevel()`: Punto di ingresso invocato dall'editor.
- **Riferimenti obbligatori e comportamento se mancanti**:
  - Cerca `PlayerCapsule` o GameObject con tag `"Player"`. Se non trovato, genera l'ambiente ma avvisa sulla console per il posizionamento del player.

### CreatureProfileCreator
- **Responsabilità e ciclo di vita**:
  - Classe statica con menu `[MenuItem("Assets/Create/Deeploration/Creature Profile", false, 1)]`.
  - Individua la cartella selezionata in Project Window via `AssetDatabase.GetAssetPath(Selection.activeObject)`.
  - Genera un nome file univoco (`New Creature Profile.asset`), istanzia l'asset e lo seleziona automaticamente per la modifica nell'Inspector.

### PrologueSmokeCheck
- **Responsabilità**: in Play Mode trova il primo `DiverController`, verifica che sia abilitato e che abbia `StarterAssetsInputs`, forza e poi azzera `jump`/`jumpDown`, chiama `ResetMotion()` e verifica `CurrentSpeed == 0`.
- **API pubbliche**: `static void Check()`, richiamabile dal menu `Deeploration/Tests/Check Prologue Control Restored`.
- **Limite**: è un controllo manuale e parziale; non verifica Timeline, ripristino degli altri componenti, sottotitoli, scena o input reale.

### PrologueWave1Builder
- **Responsabilità**: Allestisce l'intero stack narrativo e cinematico della Wave 1: inizializza `LocalizationSettings` con locale `it`/`en`, genera la String Table Collection `Prologue` con chiavi per portello e 7 battute dell'intro, istanzia i 7 ScriptableObject `SubtitleLine`, genera le Timeline `Intro_Blockout.playable` ed `ExitTitle_Blockout.playable`, e configura la scena `SCN_Gameplay.unity` con `PrologueCanvas`, `SubtitlePanel`, `FadeOverlay`, `Prologue_Hatch` con `SimpleInteractable` e cancelli `CinematicControlGate`. Include dialog di conferma prima di rieseguire su scene già esistenti (nessuna auto-esecuzione all'avvio).
- **API pubbliche**: `static void BuildWave1()`, richiamabile dal menu `Deeplonauts/Prologue/Build Wave 1 Prologue Setup`.

### PrologueElevatorSceneBuilder
- **Responsabilità**: Genera e allestisce la scena isolata di discesa `SCN_Intro.unity`. Ricostruisce la cabina di blocking dell'ascensore `01_Elevator_Rig` con pavimento a collider spesso (5 metri verso il basso per protezione tunneling), pareti con apertura per oblò e vetro (`Porthole_Glass`), punto di ancoraggio vuoto `DROP_BLENDER_MESH_HERE` per la mesh importata da Blender, istanza del prefab `Player` allineata a terra rivolta verso l'oblò, illuminazione interna soffusa/d'emergenza e backdrop esterno `Treadmill_Environment` con pareti abissali, fascio di luce oceanica e `ParticleSystem` a velocità ascensionale per simulare la discesa rapida senza spostare l'ascensore nello spazio world. Configura inoltre la sequenza runtime (`ElevatorPrologueSequence`), il blocco del moto con freelook, i cubi treadmill, i dialoghi UI localizzati, il camera shake e la dissolvenza verso il gameplay. È rigorosamente manuale via MenuItem e protetto da dialogo modale per impedire la sovrascrittura di modifiche manuali o mesh accordate.
- **API pubbliche**:
  - `static void BuildElevatorScene()`: Genera la scena da zero (menu `Deeplonauts/Prologue/Build Elevator Prologue Scene`, protetta da dialog se la scena esiste).
  - `static void SetupElevatorSceneComponents()`: Configura o aggiorna la sequenza, i dialoghi, il camera shake e i cubi nella scena attiva (menu `Deeplonauts/Prologue/Setup Elevator Sequence & Dialogues`).

### RockBlendDebugMenu
- **Responsabilità**: strumento di debug per `SG_Rock_Blend`; imposta la globale `_EnvRockBlendDebug` (rosso = contatto col suolo, verde = giuntura con le rocce vicine, blu = sedimento e cavità). Lo stato non è salvato e torna a 0 al riavvio dell'Editor.
- **API pubbliche**: nessuna (metodi privati). Dettagli completi in [EnvironmentShading.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/EnvironmentShading.md).

## Setup in Unity
- Per rigenerare il livello prototipo da zero (usare con cautela per non sovrascrivere modifiche manuali alla scena):
  1. Aprire la scena desiderata.
  2. Cliccare sulla barra dei menu di Unity: **Tools -> Deeplonauts -> Build Prototype Graybox Level**.
- Per creare un nuovo profilo creatura:
  1. Nella Project Window, fare clic destro su una cartella (es. `Assets/_Project/Entities/Profiles/`).
  2. Selezionare **Create -> Deeploration -> Creature Profile**.

## Configurazione verificata in prefab e scene
- La scena `Prototype.unity` (rimossa il 2026-10-07 nel commit `f0fb87b`, recuperabile dalla storia di Git) è stata inizialmente impostata tramite `DeeplonautsLevelBuilder` e poi rifinita a mano con volumi post-processing URP e illuminazione ambientale.

## Limiti e problemi noti
- I builder `PrologueElevatorSceneBuilder` e `PrologueWave1Builder` e i materiali generati da `DeeplonautsLevelBuilder` puntano ancora a `Assets/_Project/Prototype/` (`Prologue_Elevator.unity`, `GameplayLoop_Blockout.unity`, `M_*.mat`). Il 2026-10-07 le scene sono state spostate in `Assets/_Project/Scenes/` e rinominate `SCN_Intro` (ex `Prologue_Elevator`) e `SCN_Gameplay` (ex `GameplayLoop_Blockout`); `Prototype.unity` è stata rimossa. Rigenerare con i builder crea scene e materiali al vecchio percorso senza toccare quelle attuali: aggiornare le costanti `SCENE_PATH` e `SCENE_DIR` prima di usarli.
- L'esecuzione di `BuildLevel()` elimina qualsiasi oggetto sotto `_GRAYBOX_ROOT`. Non invocare senza aver preventivamente committato o salvato la scena.
- Non usare `BuildLevel()` per preparare `SCN_Gameplay.unity`: la Wave 1 deve conservare la mappa importata e le modifiche manuali della scena.

## Verifica
- `PrologueSmokeCheck` è stato eseguito in Play Mode con esito positivo (`PASS: controllo Diver e reset input`); verifica che il `DiverController` sia abilitato, gli input di salto funzionino e `ResetMotion()` azzeri `CurrentSpeed`.
- `PrologueWave1Builder` è stato eseguito con successo, generando asset di localizzazione, Timeline, canali segnali e allestimento della scena `SCN_Gameplay.unity`.

## Sistemi collegati
- [Environment.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Environment.md)
- [Quests.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Quests.md)
- [Interaction.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Interaction.md)
- [EntitySystem.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/EntitySystem.md)
