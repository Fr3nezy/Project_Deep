# Comms
Ultima verifica: 2026-11-25

## Scopo e confini
Schermo comunicazioni del casco: le battute di H.E.L.M. e BATHY appaiono su un pannello montato su una cerniera dentro il casco, che si ribalta verso il giocatore quando c'è un messaggio, scrive a macchina il nome di chi parla e la battuta, suona la voce se c'è e si richiude. È il sottotitolo diegetico del gioco (GDD: voci solo in inglese, testo in italiano e inglese) e sostituisce, per il gameplay, i sottotitoli su Timeline del prologo (`SubtitlePanel`/`SubtitleTrack`, deprecati, vedi [Prologue.md](Prologue.md)).

Non scrive le battute (sono asset `SubtitleLine` con testi nella tabella `Comms`) e non decide quando parlano: lo fanno i `CommsTrigger` in scena o chiunque chiami `HelmetComms.Say`.

## File e componenti
- `Assets/_Project/Code/Comms/HelmetComms.cs`: enum `CommsPriority` e componente `HelmetComms` (pannello, testi, coda, audio).
- `Assets/_Project/Code/Comms/CommsTrigger.cs`: manda una o più battute allo schermo all'ingresso del diver in un trigger, quando guarda un oggetto, o da un `UnityEvent` di scena.
- `Assets/_Project/Code/Comms/CommsHint.cs`: suggerimento a tempo, dopo N secondi di silenzio da quando si arma, annullabile se il giocatore risolve da solo.
- `Assets/_Project/Code/Comms/OxygenComms.cs` (2026-11-25): avvisi vocali di H.E.L.M. sull'ossigeno in consumo (50%, 25%, 30 e 10 secondi rimasti), come il PDA di una tuta.
- `Assets/_Project/Code/Comms/FaunaCommsCue.cs`: battuta critica alla prima allerta di una creatura stealth mentre il giocatore tiene in mano una cella accesa.
- `Assets/_Project/Code/Prologue/SubtitleLine.cs`: asset della battuta, condiviso col prologo (vedi [Prologue.md](Prologue.md)); i campi `voice` e `intro` (suono prima della battuta, es. jingle di avvio di H.E.L.M.) servono solo qui.
- Asset: modello `Assets/_Project/Assets/Models/Helmet/SM_CommsPanel.fbx` (radice `MSH_CommsHinge` = cerniera e perno della rotazione, figli `MSH_ScreenChassist` = telaio, `MSH_Screen` = schermo, `MSH_Glass` = vetro; importer con `preserveHierarchy` attivo, altrimenti Unity fonde la radice unica del FBX con l'oggetto del prefab), tabella stringhe `Assets/_Project/Localization/Comms.asset` (+ `Comms_en`, `Comms_it`), battute in `Assets/_Project/Comms/Lines/`, font `Assets/_Project/Assets/Fonts/VT323 SDF.asset` (VT323, licenza OFL in `VT323_OFL.txt`).

## Dipendenze e flusso dati
- **Unity Localization**: `SubtitleLine.speaker` e `.text` sono `LocalizedString`, risolte con `GetLocalizedString()` all'inizio di ogni messaggio (lingua di sistema, `en` di riserva).
- **TextMeshPro 3D**: due `TMP_Text` sulla faccia del pannello; la scrittura a macchina usa `maxVisibleCharacters`.
- **`DiverFlashlight`** (`Deeploration.Player`): con la torcia in modalità `Red` l'inchiostro passa da ambra a rosso emergenza, come gli altri strumenti del casco.

```mermaid
flowchart LR
    Trig[CommsTrigger / evento di scena] -->|Say line, priority| Comms[HelmetComms]
    Comms -->|coda per priorità| Q[(queue)]
    Q -->|Begin| Panel[MSH_CommsHinge su cerniera]
    Panel --> Txt[Speaker + Body TMP]
    Comms -->|voice o tick| Audio[AudioSource 2D]
    Flash[DiverFlashlight.CurrentMode] -->|ambra / rosso| Txt
```

## Componenti

### CommsPriority
Enum, in ordine crescente (GDD, «Concorrenza degli avvisi»):
- `Hint = 0`: consiglio da tutorial. In coda scade dopo `hintExpirySeconds`; se interrotto si scarta.
- `Dialogue = 1`: narrazione. Se interrotta torna in testa alla coda e riparte dall'inizio della frase.
- `Critical = 2`: interrompe tutto, resta sullo schermo `criticalSeconds`.

### HelmetComms
- **Responsabilità e ciclo di vita**:
  - `Awake()`: trova `DiverFlashlight` se vuoto, aggiunge un `AudioSource` 2D se `voiceSource` è vuoto, sintetizza il tick se `typeTick` è vuoto e il bip se `incomingBeep` è vuoto, memorizza la rotazione locale del pannello come posa aperta, chiude il pannello e svuota i testi.
  - `Update()` (tempo non scalato): scarta i consigli scaduti, avvia il prossimo messaggio passato `gapSeconds` dalla fine del precedente, suona `incomingBeep` quando il pannello comincia ad aprirsi (passaggio da chiuso ad aperto, non quando un messaggio arriva a pannello già aperto), muove l'apertura verso 1 o 0 in `flipSeconds` con SmoothStep, applica il colore dell'inchiostro. A pannello aperto scrive una parola intera alla volta: con la voce al ritmo `caratteri / (durata del clip − voiceTailSeconds)`, così le parole seguono il parlato; senza voce a `charsPerSecond` con un tick per parola. Il testo che non entra va su più pagine (`bodyText` in overflow `Page`): con la voce la pagina volta alla prima parola che non ci sta, senza voce c'è prima una pausa di `max(minHoldSeconds, caratteri della pagina × readSecondsPerChar)`. Finita la scrittura tiene la battuta `criticalSeconds` (critici) o `max(minHoldSeconds, caratteri dell'ultima pagina × readSecondsPerChar)` e comunque finché la voce suona. Poi il pannello resta aperto `lingerSeconds`: un messaggio che arriva in quel tempo parte senza richiudere e riaprire.
  - I testi sono accesi solo a pannello tutto aperto: lo shader TMP (`TMP_SDF-Mobile`) usa `ZTest [unity_GUIZTestMode]`, globale, che la UI può lasciare ad Always; un testo acceso mentre il pannello scende dietro il bordo del casco si vedrebbe sopra il casco senza il pannello (segnalato da Manu il 2026-11-24). `Begin` accende `bodyText` solo per misurare la battuta (TMP non conta i caratteri di un testo spento).
  - Testo vuoto (chiave mancante nella tabella): niente scrittura, si chiude dopo `minHoldSeconds`.
  - La voce non parte in `Begin`: parte nel primo `Update` a pannello tutto aperto (`voicePending`), insieme alla prima parola e dopo il bip, che dura 0.21 s contro 0.45 s di ribaltamento.
  - Se la battuta ha `intro` (2026-11-24), a pannello aperto suona prima quella (`introPending`, `PlayOneShot`) e voce e scrittura aspettano la sua durata (`introUntil`). Dal 2026-11-25, con `bootRoot` assegnato, l'attesa dura almeno `bootSeconds` e il pannello mostra la schermata di avvio al posto di nome e testo (dall'apertura del pannello fino a fine caricamento): logo THALADYNE e la barra finta `bootProgress` `[####....] 40%` a scatti del 5%, più lenta all'inizio (`t^1.6`); al 100% parte la voce. Un messaggio critico che interrompe ferma anche l'intro (`voiceSource.Stop`).
- **Campi Inspector** (avvio, dal 2026-11-25): `GameObject bootRoot` (`null` = niente schermata), `TMP_Text bootProgress` (colore dell'inchiostro, ambra o rosso), `float bootSeconds` (`3`, mai meno del jingle), `int bootBarCells` (`20`). In `SCN_Gameplay`: `.../MSH_Screen/CommsTexts/Boot` (inattivo) con `Logo` (quad senza collider, (0, 0.009, −0.0002), scala (0.064, 0.0154), `M_Thaladyne_Logo.mat` = copia di `M_HelmetMarker` con `T_Thaladyne_Logo.png` (logo di Manu ritagliato, bianco su alfa, 928×224) e `_BaseColor` ambra (1, 0.69, 0.25)) e `Progress` (copia di `Body`, centrato, (0, −0.008), fontSize 0.07). Asset in `Assets/_Project/Assets/Models/Helmet/`. Verificato in Play con cattura (logo e barra al 60%).
- **Campi Inspector**:
  - `Transform panel` (`null`): pannello che ruota. Importato in posa aperta: quella posa è angolo 0.
  - `float closedAngle` (`-162.5`): angolo del pannello chiuso attorno all'asse X del genitore, in gradi. Riproduce la posa chiusa di `Helmet.blend`.
  - `float flipSeconds` (`0.45`, min 0.05): durata del ribaltamento.
  - `TMP_Text speakerText`, `TMP_Text bodyText` (`null`): nome di chi parla e battuta.
  - `float charsPerSecond` (`40`): velocità della scrittura senza voce.
  - `float voiceTailSeconds` (`0.5`): coda di riverbero a fine clip esclusa dalla sincronia (ambiente `scafo` di `robot.py`).
  - `float readSecondsPerChar` (`0.045`), `float minHoldSeconds` (`1.5`): tempo di lettura dopo la scrittura e fra le pagine.
  - `float lingerSeconds` (`3`): quanto il pannello resta aperto dopo un messaggio.
  - `AudioSource voiceSource` (`null` = aggiunta in `Awake`): sorgente 2D di voce e tick.
  - `AudioClip typeTick` (`null` = 12 ms di rumore sintetizzato), `float tickVolume` (`0.25`).
  - `AudioClip incomingBeep` (`null` = bip sintetizzato: due toni quadri smussati da 90 ms, 1320 Hz poi 880 Hz, 30 ms di pausa), `float beepVolume` (`0.5`): avviso di comunicazione in arrivo, sulla stessa `voiceSource`.
  - `float criticalSeconds` (`4.5`), `float hintExpirySeconds` (`8`), `float gapSeconds` (`0.4`), `float repeatBlockSeconds` (`10`): regole della coda.
  - `DiverFlashlight flashlight` (`null` = `FindFirstObjectByType`).
- **API pubbliche**:
  - `public static event System.Action<SubtitleLine> LineStarted`: lanciato in `Begin`, quando una battuta comincia sullo schermo (non quando entra in coda; un dialogo interrotto lo rilancia alla ripresa). Lo ascolta `HelmetMarkerTarget` per far comparire i marker solo quando H.E.L.M. nomina il POI (vedi [SuitFeedback.md](SuitFeedback.md)).
  - `public static event System.Action<SubtitleLine> LineFinished` (2026-11-25): lanciato quando una battuta ha finito voce e tempo di lettura e lascia lo schermo; non quando viene interrotta da una battuta più urgente (riparte e lo lancia alla fine). Lo ascolta `LeverSwitch` (vedi [Interaction.md](Interaction.md)).
  - `public bool IsBusy { get; }`: vero con un messaggio sullo schermo o in coda.
  - `public void Say(SubtitleLine line, CommsPriority priority = CommsPriority.Dialogue)`: mette in coda. Ignora la battuta se è identica a quella sullo schermo, a una già in coda o a una mostrata da meno di `repeatBlockSeconds`. Con priorità maggiore del messaggio corrente lo interrompe subito (dialogo interrotto rimesso in testa, consiglio interrotto perso, voce fermata). A pari priorità l'ordine è di arrivo.
- **Riferimenti obbligatori**: `panel`, `speakerText`, `bodyText`. Senza `panel` il pannello non si muove; senza i testi `Begin` lancia `NullReferenceException`. Battuta nulla o senza testo: `Debug.LogError` e scartata.

### CommsTrigger
- **Responsabilità e ciclo di vita**: `Start()` cerca `HelmetComms` in scena (`Debug.LogError` se manca). `OnTriggerEnter`/`OnTriggerExit` tengono traccia di dentro/fuori del diver (collider con `CharacterController` nei genitori; niente `OnTriggerStay`). Con `playOnEnter` (e `playOnGaze` spento) l'ingresso chiama `Play()`. Con `playOnGaze`, `Update()` accumula il tempo in cui il diver, dentro il volume, guarda `gazeTarget`: camera principale, angolo fra `forward` e direzione al bersaglio < `gazeAngle`, linea libera (raycast dalla camera al bersaglio; il primo collider, ignorando quelli del diver e i trigger che non sono del bersaglio, deve essere del bersaglio o di un suo figlio; dal 2026-11-25 conta anche un trigger del bersaglio, perché il `RedLightModule` ha solo un `BoxCollider` trigger: prima il raycast ignorava tutti i trigger e lo sguardo sul modulo non scattava mai). Il tempo si azzera appena lo sguardo si interrompe e all'uscita; a `gazeSeconds` chiama `Play()`. Serve un collider con `isTrigger` sullo stesso oggetto.
  - **Scelta di progetto:** con `playOnGaze` attivo l'ingresso da solo non fa partire la battuta, anche con `playOnEnter` acceso: lo sguardo prevale (altrimenti il cartello parlerebbe prima di essere visto).
- **Campi Inspector**:
  - `SubtitleLine[] lines` (vuoto): battute mandate in ordine.
  - `CommsPriority priority` (`Dialogue`).
  - `bool once` (`true`): parte una volta sola per partita.
  - `bool playOnEnter` (`true`): parte all'ingresso nel trigger; spento, solo da `Play()`. Ignorato se `playOnGaze` è attivo.
  - `bool playOnGaze` (`false`): parte quando il diver, dentro il trigger, guarda `gazeTarget` per `gazeSeconds` di fila.
  - `Transform gazeTarget` (`null`): oggetto guardato (es. il cartello). Senza, lo sguardo non scatta mai.
  - `float gazeSeconds` (`1`, min 0): secondi continui di sguardo.
  - `float gazeAngle` (`10`, 1-90): semi-angolo in gradi fra `forward` della camera e direzione verso il bersaglio.
  - `Grabbable playWhenHeld` (`null`): se impostato, `Update` chiama `Play()` la prima volta che l'oggetto è in mano (`IsHeld`); indipendente dal collider (2026-11-24, per la cella di Chiusa 1).
- **API pubbliche**: `public void Play()`: mette in coda tutte le battute (una volta sola con `once`). Collegabile da `UnityEvent` (es. `GrabSocket.onRejected`, raccolta del modulo luce rossa).

### CommsHint *(2026-11-25)*
- **Responsabilità e ciclo di vita**: suggerimento a tempo di H.E.L.M. per chi si blocca (richiesta di Manu). `Start()` cerca `HelmetComms` (`Debug.LogError` se manca). Si arma una volta sola: alla fine di `armAfterLine` (`HelmetComms.LineFinished`), all'ingresso del diver nel proprio trigger se `armOnEnter` (collider con `CharacterController` nei genitori), o da `Arm()`. Armato, `Update()` conta i secondi di silenzio: il conto riparte a ogni `LineFinished` e finché `HelmetComms.IsBusy`, così il suggerimento non si sovrappone ad altre battute. A `seconds` manda `line` con `priority` e invoca `onPlayed`, una volta sola. `Cancel()` o l'inizio di `cancelOnLine` (`LineStarted`) lo annullano per sempre, anche prima dell'armamento.
- **Campi Inspector**: `SubtitleLine line`, `CommsPriority priority` (`Dialogue`), `float seconds` (`40`, min 1), `SubtitleLine armAfterLine` (vuoto = solo trigger o `Arm()`), `bool armOnEnter` (`false`), `SubtitleLine cancelOnLine` (vuoto = solo `Cancel()`), `UnityEvent onPlayed`.
- **API pubbliche**: `bool Armed { get; }`, `bool Done { get; }` (vero dopo il suggerimento o l'annullamento), `void Arm()`, `void Cancel()`.
- **Riferimenti obbligatori**: `HelmetComms` in scena; senza, non parla mai. Con `armOnEnter` serve un collider `isTrigger` sullo stesso oggetto.

### OxygenComms *(2026-11-25)*
- **Responsabilità e ciclo di vita**: avvisi di H.E.L.M. sull'ossigeno in consumo, richiesta di Manu («come in Subnautica, quando il PDA ti dice oxygen, 30 seconds»). `Start()` risolve `OxygenSystem` (campo vuoto = `FindFirstObjectByType`) e `HelmetComms` (`Debug.LogError` se manca uno dei due) e segna come già dette le soglie sotto cui si parte (cheat, debug). `Update()` (fermo con `OxygenSystem.InfiniteOxygen`) confronta ogni soglia: frazione del serbatoio (`NormalizedOxygen`) o secondi rimasti al consumo attuale (`CurrentOxygen / CurrentDrainPerSecond`; consumo nullo = infiniti). Scesa una soglia la segna come detta e ricorda il livello del serbatoio; se più soglie scattano nello stesso frame (morso, mina) parla solo la più grave, cioè l'ultima della lista, e quelle meno gravi già superate restano consumate. Chiama `HelmetComms.Say(line, priority)`.
- **Riarmo**: una soglia detta torna attiva solo quando il serbatoio risale di `rearmMargin` sopra il livello a cui ha parlato (ricarica, respawn). Non si riarma sui secondi rimasti: lo scatto alza il consumo e farebbe ripetere «30 secondi» a ogni cambio di passo.
- **Campi Inspector**: `OxygenSystem oxygen` (vuoto = cercato), `Callout[] callouts` (in ordine di gravità crescente), `float rearmMargin` (`0.1`, 0.01..1, frazione del serbatoio). `Callout` (struct serializzabile): `SubtitleLine line`, `bool seconds` (vero = `threshold` in secondi rimasti, falso = frazione 0..1), `float threshold`, `CommsPriority priority`.
- **API pubbliche**: nessuna.
- **Riferimenti obbligatori**: `OxygenSystem` e `HelmetComms` in scena; senza, il componente resta muto. Una `line` vuota consuma la soglia senza parlare.

### FaunaCommsCue
- **Responsabilità e ciclo di vita**: componente di scena. `OnEnable`/`OnDisable` si iscrivono a `CreatureStealth.AnyAlertChanged` (evento statico: le creature sono in pool). Alla prima transizione verso `Investigate` o `Attack` (qualunque livello diverso da `Neutral`), se il giocatore tiene in mano una cella accesa, manda `line` a `HelmetComms.Say(line, CommsPriority.Critical)` e non lo rifà (con `once`).
- **Cella accesa**: `PlayerGrab.Held` con `Grabbable.IsHeld` vero e che emette davvero uno stimolo: il componente chiama `Grabbable.EmitStimuli` su una lista di prova e cerca uno stimolo `WeakLight` con `FromPlayer`. Si usa `IsHeld`, non `AlertsCreatures` (vero anche per una cella lanciata e ancora in moto). Un `Grabbable` con `stimulusReach` 0 non è una cella e non fa scattare.
- **Campi Inspector**: `SubtitleLine line` (`null`; senza, non fa nulla), `bool once` (`true`): una volta sola per partita.
- **API pubbliche**: nessuna.
- **Riferimenti obbligatori**: `HelmetComms` e `PlayerGrab` cercati in scena al primo bisogno; senza `HelmetComms` `Debug.LogError`.

## Setup in Unity
1. Istanziare `SM_CommsPanel.fbx` come figlio di `Helmet` con transform locale nullo (il FBX è esportato nello spazio del casco, in posa aperta).
2. Un materiale per mesh: `PH_Metal` su `MSH_CommsHinge` e `MSH_ScreenChassist`, `PH_Screen` su `MSH_Screen`, `PH_Glass` su `MSH_Glass` (spento, come gli altri vetri del casco). Schermo e telaio sono mesh separate in `Helmet.blend`: con un solo mesh a due materiali il bevel portava il colore dello schermo sugli spigoli dell'incasso. Mai `PH_Screen` sul telaio: diventa un rettangolo olivastro che sembra UI senza pannello.
3. Sotto `MSH_Screen` un figlio `CommsTexts`, 1 mm davanti al centro dello schermo (95×34 mm), con due `TextMeshPro` larghi 89 mm: `Speaker` (Jersey15 SDF, 0.08, in alto) e `Body` (VT323 SDF, 0.08, 3 righe, interlinea −15, a capo, overflow `Page`). Ombre spente.
4. `HelmetComms` sulla radice `SM_CommsPanel`, con `panel = MSH_CommsHinge` e i due testi.
5. Battuta nuova: voci `SPK_*` e testo nella tabella `Comms` (en e it), asset `SubtitleLine` in `Assets/_Project/Comms/Lines/`, voce in inglese opzionale (H.E.L.M. con `robot.py`, preset `helm`).

## Configurazione verificata in prefab e scene
- `Player.prefab`: `MainCamera/HelmetVisualPivot/Helmet/SM_CommsPanel` con `HelmetComms` collegato a `MSH_CommsHinge`, `Speaker`, `Body`; valori di default.
- `SM_CommsPanel.fbx`: pannello largo 109 mm (accorciato all'85% in `Helmet.blend` il 2026-11-24 perché si sovrapponeva al CRT di profondità); telaio, schermo e vetro separati da Manu lo stesso giorno (prima un solo mesh `Plane` con `PH_Screen` su tutto); box di testo larghi 89 mm sullo schermo. Verificato in Play: ribaltamento sulla cerniera e testo sullo schermo.
- Tabella `Comms`: `SPK_HELM`, `SPK_BATHY`, `TEST_001` e le battute della Z0 `HELM_BOOT_01`, `HELM_WAKE_01-02`, `HELM_BRIEF_01-04`, `HELM_LEVER_01-02` (2026-11-25), `HELM_MODULE_01` (2026-11-25), `HELM_FISH_01-02`, `HELM_O2_01-02`, `HELM_CELL_01-02`, `HELM_POWER_01`, `HELM_RED_01-03`, `HELM_GATE_01` (en e it, bozze da rifinire con Manu). Un asset `SubtitleLine` per chiave in `Assets/_Project/Comms/Lines/`.
- Voci di H.E.L.M. in `Assets/_Project/Assets/Audio/VO/HELM/` (`<id>.wav`, `robot.py` preset `helm`), sorgente `_lines_z0.csv` e `_manifest.csv` di `robot.py` accanto. Per rigenerarle: `python Z:\_PROJECTS\GitHub\HarnessSystem\skills\voce\robot.py batch _lines_z0.csv -d <questa cartella>`.
- Battute FAUNA PROTOCOL (Z2, **bozze da rifinire con Manu**, 2026-11-24), en e it nella tabella `Comms`, un `SubtitleLine` per chiave con voce collegata: `HELM_FAUNA_01` «Fauna protocol.» / «Protocollo fauna.»; `HELM_FAUNA_02` «Thaladyne reminds you: the fauna is not company property. You are.» / «La Thaladyne ricorda: la fauna non è proprietà aziendale. Tu sì.»; `HELM_DROP_01` «Drop the cell. Hold still.» / «Molla la cella. Resta fermo.». Voci generate con `_lines_z2.csv` (stessa cartella HELM); `_manifest.csv` di `robot.py` viene sovrascritto a ogni batch: le righe Z0 e Z2 sono state riunite a mano, rilanciando un batch bisogna rifarlo. Montate in scena il 2026-11-24 (sotto).
- `SCN_Gameplay`, `Zones/Z3_Viperfish/Comms_Z2` (zona Z2, la gerarchia usa ancora il vecchio nome):
  - `Comms_Fauna`: `BoxCollider` trigger 8×4×8 m a (45.2, −19.5, −129.3), davanti al cartello FAUNA PROTOCOL (`SignPost_Fauna`, 49.2, −129.0; vedi [Environment.md](Environment.md)); `CommsTrigger` con `lines` = `HELM_FAUNA_01`, `HELM_FAUNA_02`, `priority` Dialogue, `once` vero, `playOnGaze` vero, `gazeTarget` = `Sign_FAUNA_PROTOCOL`, `gazeSeconds` 1, `gazeAngle` 10. Il `Housing` del cartello ha un `BoxCollider` non trigger (verificato), quindi la linea libera conta.
  - `FaunaCommsCue`: `line` = `HELM_DROP_01`, `once` vero.
- `SCN_Gameplay`, `Z0_Tutorial/Comms_Z0`. Dal 2026-11-24 sera il briefing è **scandito lungo il percorso** (Manu: prima ~40 s di parlato di fila al risveglio, si arrivava alle zone con H.E.L.M. che parlava ancora). Timeline (durate voce da `_manifest.csv`, più ~1 s fra battute per bip, ribaltamento e `gapSeconds`):

  | Momento | Trigger | Battute | Parlato |
  |---|---|---|---|
  | Risveglio in capsula, 3 s dopo la testa rialzata (`PlayerWakeUpSequence.bootDelay`, dal 2026-11-25; prima la battuta partiva a 1.2 s, durante il sollevamento) | `PlayerWakeUpSequence.wakeUpSubtitle` (`HELM_BOOT_01`), subito dopo `onCompleted` → `Comms_Brief` (senza collider) | jingle `HELM_BOOT_JINGLE` (1.3 s) con schermata di avvio THALADYNE (3 s) + `HELM_BOOT_01`; `HELM_WAKE_01`, `HELM_WAKE_02`, `HELM_BRIEF_01`, `HELM_LEVER_01` (dal 2026-11-25) | ~31 s |
  | Leva non tirata per 12 s dalla fine dell'ultima battuta (dal 2026-11-25, prima 20 s dallo sblocco) | `Comms_LeverHint` (senza collider, `once` falso), da `MSH_LeverHandle` `LeverSwitch.onReminder`; si ripete 12 s dopo la fine di ogni `LEVER_02` | `HELM_LEVER_02` | ~5 s |
  | Fuori dal portello | `Comms_Fish`, 30×30×3 m a (−5, 1, −10), prima dei lanternfish a z −14 | `HELM_FISH_01` (`HELM_FISH_02` tolto dal trigger il 2026-11-25, asset e voce restano; dal 2026-11-25 sera parla delle luci; l'avviso «LIGHTS ONLINE [F] CYCLE» del CRT parte all'inizio di questa battuta, `HelmetInstruments.lightsNoticeLine`) | ~6 s |
  | Canyon, direzione | `Comms_Gate_Dir`, 30×30×4 m a (−8, −1, −25) | `HELM_BRIEF_04` (rivela il marker del gate) | ~7 s |
  | Davanti alla stazione O₂ 1 | `Comms_O2`, 30×30×4 m a (−13, −6, −64); stazione a (−12.6, −78.7) | `HELM_O2_01-02` (rivelano i marker O₂ e cella) | ~9 s |
  | Prima presa della cella | `Comms_Cell` (senza collider, `playWhenHeld` = `Chiusa1_Puzzle/PowerCell`) | `HELM_CELL_01-02` | ~9 s |
  | Cella inserita | `Comms_Power` (senza collider), da `PowerStation/PowerSocket.onSocketed` | `HELM_POWER_01` | ~5 s |
  | Sguardo sul modulo luce rossa (dal 2026-11-25) | `Comms_Module`, `BoxCollider` trigger 8×6×8 m centrato sul modulo (−16.0, −10.8, −85.8; ridotto da 14×8×14 perché la faccia arrivava a 0.1 m dalla stazione O₂ e il raggio di `PlayerInteraction`, che colpisce anche i trigger, si sarebbe fermato lì); `playOnGaze`, `gazeTarget` = `Chiusa1_Puzzle/RedLightModule`, `gazeSeconds` 0.8, `gazeAngle` 10, `once` vero | `HELM_MODULE_01` «Red light module. Compatible with your suit lamp. Install it.» / «Modulo luce rossa. Compatibile con la lampada della tuta. Montalo.» (bozza, voce 4.94 s); rivela il marker del modulo e l'avviso «RED MODULE [E] INSTALL» del CRT (`HelmetInstruments.useNoticeLine`) | ~5 s |
  | Modulo non trovato: 40 s di silenzio dopo l'ingresso nell'area della chiusa (dal 2026-11-25) | `Comms_ModuleHint`, `BoxCollider` trigger 16×8×16 m centrato sul `Gate` (−18.5, −11.4, −91.8), `CommsHint` con `armOnEnter`, `seconds` 40, `cancelOnLine` `HELM_MODULE_01`; annullato anche da `RedLightModule` `SimpleInteractable.onInteracted` → `Cancel`; `onPlayed` → `HelmetMarkerTarget.Reveal` del modulo | `HELM_MODULE_02` «Light source nearby. It looks like a Thaladyne device.» / «Fonte luminosa nelle vicinanze. Sembra un dispositivo Thaladyne.» (bozza, 4.14 s) | ~4 s |
  | Modulo luce rossa raccolto | `Comms_Red` (senza collider), da `RedLightModule` (`SimpleInteractable.onInteracted`, override sull'istanza) | `HELM_RED_01-03` (03: hint sul sensore ottico del gate) | ~11 s |
  | Prima ricarica alla stazione O₂ 1 | `Comms_Refill` (senza collider, era `Comms_Canyon`), da `Chiusa1_Puzzle/O2Station/SafeOxygenStressZone` `OxygenRefillStation.onDocked` | `HELM_BRIEF_03` (dal 2026-11-25: «Oxygen replenished. Survival probability: 0.002%. It doubled.» / «Ossigeno ricaricato. Probabilità di sopravvivenza: 0,002%. È raddoppiata.»; `HELM_BRIEF_02` non è più in nessun trigger) | ~7 s |
  | Fotocellula non risolta: 40 s di silenzio dopo la fine di `HELM_RED_03` (dal 2026-11-25) | `Comms_PhotoHint` (senza collider), `CommsHint` con `armAfterLine` `HELM_RED_03`, `seconds` 40; annullato da `Chiusa1_Gate/PhotocellUnit/Photocell.onTriggered` → `Cancel` | `HELM_RED_04` «Optical sensor, red spectrum. Switch to red light, and aim it at the sensor.» / «Sensore ottico, spettro rosso. Passa alla luce rossa e puntala sul sensore.» (bozza, 5.99 s) | ~6 s |
  | Fotocellula scattata | `Comms_GateOpen` (senza collider), da `Chiusa1_Gate/PhotocellUnit/Photocell.onTriggered` | `HELM_GATE_01` | ~4 s |

  Testi del 2026-11-25 (en / it, bozze): `HELM_LEVER_01` «The hatch is jammed. Use the emergency release, on your right.» / «Il portello è bloccato. Usa lo sblocco di emergenza, alla tua destra.»; `HELM_LEVER_02` «Emergency release, on your right. Grab it and pull it up.» / «Sblocco di emergenza, alla tua destra. Afferralo e tiralo su.»; `HELM_FISH_01` riscritta sulle luci (era «…works like a dinner bell.»): «Suit lights online. Fauna ahead. At this depth, light gets noticed.» / «Luci della tuta attive. Fauna in vista. A questa profondità, la luce si fa notare.». Voci rigenerate con `robot.py` (4.38, 4.56, 5.59 s), righe in `_lines_z0.csv` e `_manifest.csv`.

  Testi (en / it) delle battute aggiunte o cambiate il 2026-11-24 sera, **bozze da rifinire con Manu**: `HELM_BOOT_01` «H.E.L.M. online. Hazard and Environment Linked Monitor. Your suit assistant.» (acronimo dal GDD); `HELM_WAKE_01` «Suit integrity: 61%. Primary systems offline.» (tolto «H.E.L.M. online», ora in BOOT); `HELM_CELL_01` «Nuclear energy cell. It flares when it moves. Carry it gently.»; `HELM_CELL_02` «It is also radioactive. Do not sleep next to it.»; `HELM_POWER_01` «Power restored. Stand close to the station to refill your oxygen.»; `HELM_RED_03` «The gate has an optical sensor. It is waiting for a specific light.»; `HELM_GATE_01` «Optical sensor: accepted. Lock gate opening.». Voci con `robot.py` preset `helm`; jingle `HELM_BOOT_JINGLE.wav` sintetizzato (tre note quadre smussate 660/880/1320 Hz da 110 ms + coda sinusoidale 990/1485 Hz a decadimento esponenziale, 1.3 s), collegato come `intro` di `HELM_BOOT_01`.
- Verificato in Play: il risveglio manda `HELM_WAKE_01` con la voce e mette in coda le 5 battute del briefing.
- Verificato in Play da codice il 2026-11-25 (`seconds` portato a 5 solo nel test, leva disattivata per fermare `HELM_LEVER_02` dopo il warp): warp su `Warp_Chiusa_1_davanti` girati dall'altra parte → `HELM_MODULE_02` dopo 5 s di silenzio e marker del modulo visibile; modulo montato → `HELM_RED_01-03`, poi `HELM_RED_04` 5 s dopo la fine di `HELM_RED_03`. Annullamenti (`cancelOnLine`, `Photocell.onTriggered`) collegati ma non provati.

- `Player/PlayerCapsule/OxygenComms` (2026-11-25): `OxygenComms` con `oxygen` = `OxygenSystem` del `PlayerCapsule`, `rearmMargin` 0.1, quattro soglie: `HELM_O2_50` (frazione 0.5, `Dialogue`; «Oxygen at fifty percent.» / «Ossigeno al cinquanta per cento.»), `HELM_O2_25` (frazione 0.25, `Dialogue`; «Oxygen at twenty-five percent. Locate a refill station.» / «Ossigeno al venticinque per cento. Cerca una stazione di ricarica.»), `HELM_O2_30S` (30 secondi, `Critical`; «Thirty seconds of oxygen remaining.» / «Trenta secondi di ossigeno rimasti.»), `HELM_O2_10S` (10 secondi, `Critical`; «Warning. Oxygen critical. Ten seconds.» / «Attenzione. Ossigeno critico. Dieci secondi.»). Voci in `Assets/_Project/Assets/Audio/VO/HELM/HELM_O2_*.wav` (2.58, 4.65, 2.94, 4.13 s; testi in `_lines_o2.csv`, righe in `_manifest.csv`). Con il profilo attuale (400, consumo base 0.8/s) il 50% arriva a ~250 s dalla fine, il 25% a ~125 s.

## Estensione del sistema
- Nuovo parlante: chiave `SPK_<NOME>` nella tabella `Comms`.
- Battuta da codice: tenere un riferimento a `HelmetComms` e chiamare `Say(line, priority)`.

## Limiti e problemi noti
- Implementato e verificato in Play il 2026-11-24: ribaltamento, scrittura a macchina, leggibilità a 1280×720, inchiostro rosso con la torcia rossa, lingua di sistema.
- Verificato in Play il 2026-11-24 (pomeriggio): parole in sincronia con la voce, pannello che resta su fra battute in coda.
- Bip di comunicazione in arrivo (2026-11-24 sera): verificato da codice in Play, frame per frame con `EditorApplication.Step`: il bip parte con il pannello ad apertura 0.04. L'avvio della voce ad apertura 1 non è stato colto frame per frame (con l'editor a passi l'audio non avanza): letto dopo, a pannello aperto, `voicePending` falso e voce in riproduzione. Non ascoltato: volume e timbro da sentire in cuffia. Un messaggio critico che interrompe a pannello già aperto non suona il bip.
- Pianificato: LED ambra sulla cerniera che lampeggia insieme al bip (mesh da modellare in `Helmet.blend`).
- Non verificati (2026-11-24): `playOnGaze` e `FaunaCommsCue` compilano e sono montati in scena (`Comms_Z2`) ma non sono stati provati in Play; voci FAUNA non ascoltate.
- Non verificati: interruzioni, cambio pagina, sovrapposizione con la cella portata (16.5° sotto la linea di vista).
- **Temporaneo (2026-11-24):** in `LocalizationSettings` il `SpecificLocaleSelector(en)` è in testa ai selettori d'avvio, così il gioco parte sempre in inglese per i playtest. Per tornare alla lingua di sistema rimetterlo dopo `SystemLocaleSelector`.
- Pianificato: prefisso «— Interrupting.» sul messaggio che interrompe (GDD); migrazione del prologo da `SubtitleTrack` a `HelmetComms`; battute vere e voci di H.E.L.M. e BATHY.
- `HELM_MODULE_01` in coda dietro altre battute (cella, O₂) può partire dopo che il giocatore ha già montato il modulo: la voce dice comunque «Install it.». L'avviso «[E] INSTALL» del CRT in quel caso non compare (guardia su `DiverFlashlight.RedUnlocked` in `HelmetInstruments`).
- I volumi trigger dei `CommsTrigger` sono su `Default` e `PlayerInteraction` colpisce i trigger: da fuori, la faccia di un volume fra il giocatore e un interattivo nasconde l'interattivo. Tenere i volumi lontani almeno 2.6 m dagli interattivi o metterli su `Ignore Raycast`.
- `OxygenComms`: una battuta `Dialogue` interrotta da una `Critical` viene rimessa in coda da `HelmetComms` e può ripartire dopo, anche a serbatoio già ricaricato (visto nel test compresso del 2026-11-25: «25%» ripetuto dopo «10 secondi»). In gioco con il consumo base 25% e 30 secondi distano ~95 s; succede solo con perdite improvvise grandi.
- La lingua si legge a inizio messaggio: un cambio di lingua non aggiorna il messaggio già sullo schermo.

## Verifica
- `OxygenComms`, Play da codice (2026-11-25), `SetNormalized` a scalini: 0.505 → `HELM_O2_50` dopo 2.4 s; 0.253 → `HELM_O2_25`; 30 s → `HELM_O2_30S` subito; 10.5 s → `HELM_O2_10S`; ricarica a 1 e poi 0.503 → `HELM_O2_50` di nuovo (riarmo). Nessun errore. Voci non ascoltate.
- In Play: `FindFirstObjectByType<HelmetComms>().Say(asset)` da `unity_execute_code`; lo stato interno (`openness`, `typedChars`, `totalChars`) si legge per reflection. Con l'editor non a fuoco Unity non avanza i frame: impostare `Application.runInBackground = true`.

## Sistemi collegati
- [Prologue.md](Prologue.md): `SubtitleLine`, sottotitoli su Timeline deprecati.
- [Creatures.md](Creatures.md): `CreatureStealth.AnyAlertChanged` (evento statico ascoltato da `FaunaCommsCue`), `Grabbable`.
- `DiverFlashlight` e `HelmetInstruments` (strumenti del casco, stessi colori ambra e rosso).
