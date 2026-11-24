# HelmetMarkers — Feedback prima iterazione e piano per iterazione 2
Data: 2026-11-24
Stato: **iterazione 2 implementata e verificata in Play il 2026-11-24 (Claude)**: i 4 punti sotto, più le richieste aggiunte da Manu nello stesso giro (vedi «Esito iterazione 2»). Reference canonica: `documentation/SuitFeedback.md`. Non ancora committata.

## Esito iterazione 2 (2026-11-24)
- Punto 1: `HelmetComms.LineStarted` + `HelmetMarkerTarget.revealOnLine`; comparsa una volta sola con dissolvenza `revealDuration` 0.8 s. Verificato: O2 compare con `HELM_O2_01`, non prima.
- Punto 2: `T_Marker_O2` = esagono + «O2» in Jersey15 (richiesta di Manu).
- Punto 3: target del Gate tolto e poi rimesso **solo** con `revealOnLine` `HELM_BRIEF_04`: Manu lo vuole perché il briefing nomina il gate («l'unica eccezione»). Si spegne a 8 m.
- Punto 4: in Play della bussola si vede solo la fascia delle lettere (sopra fuori campo, sotto il telaio). Scelta di Manu: icona nella fila delle lettere, stesso ambra più spento (`compassDim` 0.55), mai sopra una lettera (`compassLetterClearance` 8°) né sopra un'altra icona (`compassIconSpacing` 8.5°).
- Aggiunto su richiesta di Manu: marker sulla cella (`HELM_O2_02`, sparisce quando la prendi) e marker sulla presa O2 (`T_Marker_Plug`, compare alla prima presa della cella, sparisce a cella inserita).
- Aperto: `Centrale_Socket` (Z2) non ha una battuta che lo nomini, quindi non compare mai; serve una linea H.E.L.M. Le tacche fra le lettere possono restare sotto un'icona.
- Note di Manu per la prossima sessione (non toccate): i dialoghi Z0 sembrano troppo lunghi (si arriva alla zona mentre H.E.L.M. parla ancora), da ritarare insieme; mancano i dialoghi della Z1.

## Stato attuale su disco (non committato)
- `Assets/_Project/Code/Player/Feedback/HelmetMarkerTarget.cs`: componente sui POI con registro statico `Active`, `SetMarkerVisible(bool)` chiamabile da `UnityEvent`, `icon`, `localOffset`, `startVisible`.
- `Assets/_Project/Code/Player/Feedback/HelmetMarkers.cs`: su `Player.prefab` (`MainCamera/HelmetMarkers`), con due toggle indipendenti:
  - `showOnVisor` (`true`): quad icona + testo distanza `XXM` a `0.30 m` dall'occhio nella zona libera del frustum, fade-out ravvicinato (`6 m` → `2.5 m`).
  - `showOnCompass` (`true`): stessa icona sull'arco conico di `HelmetInstruments.CompassStrip` (`compassZOffset = -0.000055`, `compassIconSize = 0.0052`).
- `Assets/_Project/Code/Player/Feedback/HelmetInstruments.cs`: espone `CompassConeSlope`, `CompassStrip`, `CompassCenter`, `CompassRadius`, `CompassArcHalf`, `CompassWindowHalf`, `ReferenceText`.
- `Assets/_Project/Code/Editor/Screens/gen_marker_icons.py` → genera in `Assets/_Project/Assets/Models/Helmet/Markers/`: `T_Marker_Objective.png`, `T_Marker_O2.png`, `T_Marker_Power.png`, più `M_HelmetMarker.mat`.
- `Assets/_Project/Scenes/SCN_Gameplay.unity`: 3 target di prova (`O2Station` Chiusa 1, `Gate` Chiusa 1, `Centrale_Socket` Z2).
- Documentazione già allineata (`documentation/SuitFeedback.md`, `documentation/README.md`; `check_catalog.py` exit 0, 99/99 script).

## Feedback di Manu dal playtest (4 punti obbligatori per l'iterazione 2)

1. **Mai visibili all'avvio — solo triggerati da H.E.L.M. con fade-in**
   - Problema attuale: i marker compaiono subito a schermo ancora prima che H.E.L.M. menzioni qualsiasi cosa (`startVisible = true`).
   - Requisito: **nessun marker deve mai comparire prima che H.E.L.M. lo menzioni** (stile Subnautica: *"Oxygen station detected nearby..."* → il marker viene aggiunto all'interfaccia del casco).
   - Implementazione richiesta:
     - Default `startVisible = false` su `HelmetMarkerTarget` (e su tutti i target in scena).
     - Aggiungere un **fade-in morbido** (es. `revealDuration` ~0.6–1.0 s, interpolazione alpha 0 → 1 quando `IsVisible` passa a `true`) in `HelmetMarkers`, così il marker entra gradualmente invece di spuntare di colpo.
     - Collegare i trigger delle battute di H.E.L.M. (`CommsTrigger` / `HelmetComms` o evento di avvio/fine battuta specifica) a `HelmetMarkerTarget.SetMarkerVisible(true)`, e spegnere il marker (o tenerlo solo se serve come riferimento O₂) quando l'obiettivo/interazione associata si chiude.

2. **Icona O₂ da rifare (`T_Marker_O2.png`)**
   - Problema attuale: la goccia dentro l'esagono sembra acqua/olio, non ha nulla che rimandi all'ossigeno.
   - Requisito: sostituire il simbolo interno con qualcosa di immediatamente leggibile come **ossigeno** (es. scritta `O2` netta in stile geometrico/pixel dentro l'esagono, oppure bombola/simbolo O₂ inequivocabile), mantenendo lo stesso esagono e spessore tratto di `T_Crosshair_Ring` / `T_TorchMode_*` e leggibilità sotto `AnalogLook` (pixel 3).

3. **Ruolo dei marker: solo indizi indicativi stile Subnautica, MAI puntatore sul Gate**
   - Problema attuale: in `SCN_Gameplay` c'è un `HelmetMarkerTarget` su `Zones/Z0_Tutorial/Chiusa1_Gate/Gate` che fa da "quest arrow" fissa sulla porta.
   - Requisito: **rimuovere il `HelmetMarkerTarget` da `Chiusa1_Gate/Gate`**. I marker servono a dare riferimenti indicativi (stile Subnautica: "qui c'è la stazione O₂ / struttura X rilevata"), mai a puntare direttamente il gate o la soluzione del puzzle prima che il giocatore la scopra o che H.E.L.M. ne parli.

4. **Icone sulla bussola illeggibili (troppo basse e schiacciate)**
   - Problema attuale: con `compassZOffset = -0.000055` e `compassIconSize = 0.0052`, viste dallo scorcio della camera (`SM_Compass` ruotata di `17.2°` e schermo conico), le icone sulla bussola risultano **troppo basse e schiacciate/stretchate verticalmente**, risultando incomprensibili.
   - Direzioni possibili da provare col prossimo agente:
     - **A.** Compensare l'inclinazione/scorcio o alzare leggermente il marker, e **usare un segnalino geometrico semplice** (es. triangolino/diamante/tacca dedicata) invece dell'icona esagonale dettagliata che a `5 mm` sotto `AnalogLook` si impasta; oppure
     - **B.** Tenere i marker solo sulla visiera olografica (`showOnCompass = false`) se la bussola non ha abbastanza spazio verticale pulito per un secondo livello di simboli. Decidere con Manu dopo il fix rapido.

## Contesto per il prossimo agente
- I marker sulla visiera (`11M` / `25M` con esagono ambra) sono piaciuti molto a Manu nella prima prova: mantenere quello stile e proporzione.
- Dopo aver sistemato i 4 punti sopra e committato `HelmetMarkers`, la task parallela/successiva indicata da Manu è **aggiungere le linee vocali H.E.L.M. mancanti** (le bozze attuali si tengono per domani, ma mancano battute in vari punti; la lingua resta forzata in inglese per l'incontro del 25/11; `M_VisorGlass` è approvato così com'è).
