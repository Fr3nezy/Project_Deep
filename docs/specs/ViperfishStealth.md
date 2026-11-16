# Viperfish stealth (chiusa 2)

| Versione | Autore | Data | Stato |
|---|---|---|---|
| 1.0 | Claude Code | 2026-11-14 | approvata |

## Overview
Il viperfish della Z2 (Outpost minerario) diventa la sentinella di un puzzle stealth: il giocatore porta tre celle nucleari ai loro punti attraverso un prato di penne a chiazze, e due viperfish pattugliano la zona. Il predatore non ti sente più al buio: ti **vede**, solo nel suo campo visivo, quando c'è luce del sub (fascio, flash, cella presa o appena mossa, scia del prato) o quando ti muovi mentre ti sta già cercando. Il suo stato si legge sui fotofori, in modo diegetico: **neutrale** blu, **indaga** ambra, **attacco** rosso, ognuno con la sua pulsazione. Se resti immobile al buio o in rosso perde interesse e se ne va; se ti vede muovere ti si porta davanti e carica: il flash pieno nella finestra giusta lo respinge, altrimenti morde. Usato da: giocatore (lettura dei fotofori, luce rossa, flash), designer dall'Inspector (raggi, tempi, colori, zona di pattuglia). Design nel vault: `Flusso-Slice.md`, sezione «Chiusa 2 · tre celle sul prato del viperfish».

## Goals
- `G1` **Tre stati leggibili.** Il viperfish è sempre in uno di tre livelli d'allerta (Neutrale, Indaga, Attacco) e i fotofori mostrano colore e ritmo di quel livello entro 0.5 s dal cambio: blu e pulsazione lenta, ambra e pulsazione media, rosso e lampeggio rapido. Mascella, esca e occhi seguono il livello (bocca socchiusa e occhi sul punto in Indaga, bocca aperta e occhi sul casco in Attacco).
- `G2` **Vede solo nel campo visivo.** Uno stimolo luminoso del sub lo allerta solo se la **sorgente** è dentro il suo cono visivo (semi-angolo `viewHalfAngle`, default 60°, attorno al muso), entro la portata dello stimolo (quella che già calcola la sorgente, tagliata a `maxSightRange` 25 m) e in linea di vista libera. Per il fascio della torcia basta vedere la lampada: non serve che il fascio lo colpisca. Il rumore non lo allerta in nessuno stato.
- `G3` **Cosa lo allerta.** Il fascio (Full, Low), qualunque flash, la cella nucleare **presa** oppure **in movimento entro 10 s dall'ultimo rilascio**, la scia del prato sotto il sub (portata della sorgente, 6 m). Una cella ferma, una cella che si assesta da sola, le luci dell'ambiente e la luce rossa non lo allertano. La cella lo mostra a colpo d'occhio: celeste spento quando non allerta, azzurro saturo e acceso quando allerta (2026-11-16, `Grabbable.AlertsCreatures`); il suono verrà dopo, sullo stesso segnale.
- `G4` **Indaga.** Da Neutrale uno stimolo valido lo porta in Indaga: nuota a `investigateSpeed` verso l'ultimo punto visto, entro `inspectDistance` ci gira attorno guardandolo. Ogni nuovo stimolo valido aggiorna il punto; con due stimoli insieme sceglie il più vicino.
- `G5` **Perde interesse.** In Indaga, se per `giveUpTime` s (default 5) non vede stimoli validi né movimento del sub, torna Neutrale e si allontana: nuota verso un punto ad almeno `leaveDistance` m (default 10) dal punto indagato, scelto verso il centro della sua zona; raggiunto il punto (o dopo 15 s) riprende a pattugliare.
- `G6` **Movimento = attacco.** In Indaga, se vede il corpo del sub (cono, linea di vista, entro `motionSightRange` 12 m) muoversi oltre `motionThreshold` m/s (default 0.3, misurata sul tempo reale fra due controlli), passa ad Attacco. In Neutrale il movimento al buio non lo allerta. Girare la camera non è movimento.
- `G7` **Attacca solo di fronte.** In Attacco nuota a `approachSpeed` (più veloce della camminata del sub) verso un punto davanti alla camera del sub, girandogli attorno senza passargli attraverso. **Entra** in Carica solo se è entro ±`frontAngle` (default 45°) dallo sguardo orizzontale del sub e con linea di vista libera fra testa e casco. Se il punto davanti è dentro la roccia prova angoli vicini e distanze più corte.
- `G8` **Carica e flash.** In Carica resta fermo davanti al sub per `windupTime` s (default 0.8) poi scatta al casco; girare la camera durante la Carica non la annulla. Un **flash pieno** (sweet spot) durante Carica o Scatto lo respinge: fugge, torna Neutrale, niente attacchi per `repelCooldown` s (default 15). Flash debole o passaggio a Full non lo respingono. Se lo Scatto non colpisce entro 1.5 s torna in Attacco. Il morso ha gli effetti di oggi (`DiverBiteReceiver`), poi fuga e niente attacchi per `biteCooldown` s (default 20).
- `G9` **Pattuglia di zona e uscite.** Ogni viperfish pattuglia attorno al proprio nido (punto di casa e raggio). L'Attacco finisce se per `attackGiveUpTime` s (default 8) non riesce a entrare in Carica (torna Indaga sull'ultimo punto) o se il sub è oltre 2 × `motionSightRange` (torna Neutrale). Due viperfish in due zone separate.
- `G10` **Caccia ai lanternfish.** In Neutrale caccia ancora i lanternfish (`Hunt` di oggi), anche vicino a una cella ferma; si sazia e si allontana. Quando ne mangia uno compare una nuvola di sangue (particelle in un sistema condiviso, niente oggetti creati a runtime), oltre alla dissolvenza in bocca di oggi.
- `G11` **Scala +35%.** Scala 1.35 sulla radice del prefab, distanze ricalibrate (valori in Componenti), morso e manovre corretti.

## Non-goals
- Respawn e stato di morte dopo i morsi: con la UI (M3).
- Luce rossa come upgrade raccolto, cavi su spline, bagliore delle celle oltre la fog, stazioni O₂ alimentate, prato a chiazze: task separate della chiusa 2.
- Udito del viperfish.
- Comunicazione fra i due viperfish.
- Lo `Stalk` di oggi resta nel codice per altre specie; il viperfish della Z2 non lo usa.
- Nessuna UI 2D del sospetto: solo i fotofori.

## Vincoli e assunzioni
- Unity 6, URP, C# nel namespace `Deeploration.Creatures`. Nessuna dipendenza nuova.
- Budget: due viperfish; un raycast di linea di vista per stimolo candidato e uno per il sub a ogni ragionamento.
- **LOD del manager:** fuori inquadratura il viperfish ragiona ogni 4 frame (Mid) o 16 (Far). Percezione e scelte possono seguire il LOD; i timer di Carica e Scatto, il controllo del flash e del morso girano a ogni frame.
- **Colori:** la mappa emissiva del viperfish è arancione e `CreatureGlow` la moltiplica: col blu non funziona. Serve la mappa emissiva in bianco o grigio (il colore lo dà solo `CreatureGlow`). Nessun cambiamento visibile per chi non usa `CreatureStealth`: il colore di riposo di oggi va reimpostato arancione in `CreatureGlow` se la mappa diventa bianca.
- **Flash:** oggi `OnFlashFired` scatta per il flash pieno e per quello debole, e `DiverStimulusSource` emette `Flash` anche al passaggio a Full. La respinta deve distinguere il flash pieno.
- **Quota:** il nuoto guidato non è limitato dal soffitto del nido in Attacco (il casco può essere più alto).
- La camera del sub si legge da `CreatureManager` al momento dell'uso, non allo spawn.

## Architettura
- **Driver a parte, come `CreatureAmbush`:** `CreatureStealth` sul prefab del viperfish decide livello d'allerta e bersaglio, ma non riscrive nuoto ed evitamento ostacoli: chiede a `Creature` di nuotare verso un punto (nuovo ramo «guidato» di `Creature.Move`, che riusa evitamento, quota e spazio personale). Perché: `Creature` è condiviso da quattro specie; la lucertola mostra che un driver per specie tiene il comportamento separato.
- **Percezione tutta nel driver.** Nel profilo del viperfish le reazioni agli stimoli vanno a `Ignore` e `stalkRange` a 0: `Creature` non va più in Inspect, Flee o Stalk da sola, ma può ancora vagare (Wander) e cacciare (Hunt). Il driver filtra gli stimoli `FromPlayer` col proprio cono e linea di vista.
- **In Neutrale `Creature` ragiona da sola** (Wander attorno al nido, Hunt) salvo l'allontanamento dopo aver perso interesse, che è guidato.
- **Macchina a stati esplicita:** Neutrale (Pattuglia, Allontanamento), Indaga, Attacco (Avvicinamento frontale, Carica, Scatto), più Fuga dopo morso o respinta. Il driver scrive su `Creature` stato apparente, aggressività e punto di focus, così mascella, esca e occhi funzionano senza modifiche.
- Flusso: sorgenti (`DiverStimulusSource`, `Grabbable`) → `CreatureManager.Stimuli` → `CreatureStealth` (cono, linea di vista, movimento del sub) → livello d'allerta → bersaglio e stato apparente su `Creature` → fotofori (`CreatureGlow`), mascella, esca, occhi. Morso via `CreatureManager.ReportBite`.

## Componenti

### `CreatureStealth` (nuovo)
- **Responsabilità:** livelli d'allerta, cono visivo, perdita d'interesse, avvicinamento frontale, carica, respinta, ritorno in zona.
- **Ciclo di vita:** `Creature.OnSpawn` lo inizializza con manager, casa e raggio della zona e azzera stato e cooldown (il viperfish esce dal pool del manager). `Creature.Tick` lo chiama a ogni frame con il flag di ragionamento del LOD.
- **Dati configurabili (default da tarare in Play):** `viewHalfAngle` 60°, `maxSightRange` 25 m, `motionSightRange` 12 m, `motionThreshold` 0.3 m/s, `giveUpTime` 5 s, `leaveDistance` 10 m, `inspectDistance` 2 m, `investigateSpeed` 1.8 m/s, `approachSpeed` 3.5 m/s, `frontAngle` 45°, `frontDistance` 3.5 m, `windupTime` 0.8 s, `strikeSpeed` 6 m/s, `strikeMaxDuration` 1.5 s, `attackGiveUpTime` 8 s, `repelCooldown` 15 s, `biteCooldown` 20 s, `helmetHeight` 1.3 m, `mouthOffset` (dalla nuca alla bocca, da misurare sul modello scalato).
- **Riferimenti:** `Creature` sullo stesso oggetto (`RequireComponent`); manager, camera del sub e flashlight letti dal manager al momento dell'uso. Senza camera: «davanti» dalla direzione del sub, warning una volta.

### `Creature` (modifica)
- Ramo di nuoto guidato in `Move` (bersaglio, velocità, quota, con o senza spazio personale, con o senza soffitto del nido).
- API interna per il driver: nuoto guidato, rilascio (torna a ragionare da sola), stato apparente, aggressività e focus per mascella, esca e occhi, morso al sub, fuga.
- Hunt può partire anche dal ramo Neutrale del driver.

### `CreatureGlow` (modifica)
- Con `CreatureStealth` sul prefab: tre colori e ritmi discreti configurabili per livello, transizione di 0.3 s; in caccia resta il colore di Neutrale con l'intensità `huntBoost`. Senza `CreatureStealth` invariato.

### `DiverFlashlight` e `DiverStimulusSource` (modifica)
- Il flash espone se è pieno (sweet spot) o debole; lo stimolo `Flash` porta questa informazione (o un evento dedicato) così che il driver respinga solo col flash pieno.

### `Grabbable` (modifica)
- `FromPlayer` dello stimolo = presa, oppure velocità oltre `stimulusMoveThreshold` (default 0.2 m/s) entro `movedByPlayerWindow` (default 10 s) dall'ultimo rilascio.

### `CreatureManager` (modifica, G10)
- Un `ParticleSystem` di sangue in spazio mondo, opzionale, che emette nel punto della preda quando un predatore la mangia.

### Prefab e profilo del viperfish
- Scala 1.35 sulla radice di `Creature_Viperfish.prefab` (controllare bounds dello `SkinnedMeshRenderer` e offset del figlio). Profilo: reazioni a `Ignore`, `stalkRange` 0, `bodyRadius` 0.27, `biteReach` 0.68, `diverClearance` 2.0, `lungeDistance` ×1.35. Mappa emissiva bianca.

## API pubblica ed eventi
- `enum StealthAlert { Neutral, Investigate, Attack }` in `CreatureEnums.cs`.
- `CreatureStealth.Alert : StealthAlert`: livello attuale, letto da `CreatureGlow`.
- `event Action<StealthAlert> CreatureStealth.OnAlertChanged`: a ogni cambio (audio e H.E.L.M. in futuro).
- `CreatureStealth.LastSeenPoint : Vector3`: punto indagato (gizmo, debug).

## Test e casi limite

| ID | Goal | Dato / Quando | Atteso |
|---|---|---|---|
| T1 | G1 | Neutrale → Indaga → Attacco | Blu lento → ambra medio → rosso rapido, ogni cambio entro 0.5 s; bocca e occhi seguono |
| T2 | G2 | Torcia Full accesa alle spalle del viperfish, 10 m | Resta Neutrale |
| T3 | G2 | Torcia Full nel suo cono a 10 m, tubatura in mezzo | Resta Neutrale |
| T4 | G2, G4 | Torcia Full nel suo cono a 10 m, vista libera, fascio puntato a terra | Indaga, nuota verso la lampada |
| T5 | G3 | Cella ferma a terra nel cono a 5 m | Resta Neutrale; i lanternfish si avvicinano |
| T6 | G3 | Sub fermo in rosso con la cella in mano, nel cono a 8 m | Indaga |
| T7 | G3 | Cella lasciata andare che scivola a 0.5 m/s nel cono | Indaga |
| T8 | G3 | Cella che si assesta da sola a inizio scena nel cono | Resta Neutrale |
| T9 | G3 | Sub in rosso su una chiazza di penne a 4 m nel cono | Indaga; a 10 m resta Neutrale |
| T10 | G2 | Sub fa rumore (atterraggio) alle spalle del viperfish | Resta Neutrale |
| T11 | G5 | In Indaga, sub fermo in rosso, cella appoggiata, 5 s | Neutrale, si allontana di almeno 10 m dal punto restando nella sua zona |
| T12 | G6 | In Indaga, sub cammina nel cono a 8 m | Attacco |
| T13 | G6 | In Neutrale, sub cammina al buio nel cono a 8 m | Resta Neutrale |
| T14 | G6 | In Indaga, sub gira solo la camera | Resta Indaga |
| T15 | G6 | Viperfish fuori inquadratura (LOD Mid), sub fermo | Nessun falso movimento: resta Indaga |
| T16 | G7 | In Attacco, sub di spalle | Il viperfish gli gira attorno fino davanti, senza attraversarlo; nessuna carica da dietro |
| T17 | G7, G9 | Sub che si gira di continuo per tenerlo alle spalle | Resta in Attacco girandogli attorno; dopo 8 s senza carica torna Indaga |
| T18 | G7 | Sub con la faccia contro una parete | Carica da un angolo vicino o a distanza più corta; altrimenti dopo 8 s torna Indaga |
| T19 | G8 | Flash pieno durante la Carica | Fugge, Neutrale, niente attacchi per 15 s |
| T20 | G8 | Flash debole o passaggio a Full durante la Carica | Il morso va a segno |
| T21 | G8 | Sub gira la camera durante la Carica | La Carica continua |
| T22 | G8 | Nessun flash | Morso con gli effetti di oggi, fuga, niente attacchi per 20 s |
| T23 | G8 | Sub si sposta durante lo Scatto | Dopo 1.5 s senza colpire torna in Attacco |
| T24 | G3 | Flash pieno sparato in Neutrale nel suo cono | Indaga sul punto del flash, non fugge |
| T25 | G9 | Dopo perdita d'interesse, morso o respinta | Torna nella sua zona, non in quella dell'altro |
| T26 | G9 | Sub teletrasportato col menu cheat durante l'Attacco | Neutrale |
| T27 | — | Invulnerabilità del menu cheat | Il morso parte, nessun effetto |
| T28 | — | Nessuna camera all'uso | Warning una volta, «davanti» dalla direzione del sub |
| T29 | G10 | Lanternfish attirati da una cella ferma vicino al viperfish | Caccia, nuvola di sangue, sazietà, si allontana |
| T30 | G11 | Viperfish a scala 1.35 | Morso a segno al casco, non si incastra fra le tubature, rispetta lo spazio personale |
| T31 | — | Lanternfish e lucertola in Play dopo le modifiche a `Creature` e `CreatureGlow` | Comportamento e colori invariati |

## Piano
1. **Percezione nel driver.** `CreatureEnums.cs` (`StealthAlert`), nuovo `CreatureStealth.cs` con Neutrale, Indaga, perdita d'interesse e allontanamento; aggancio in `Creature.OnSpawn` e `Tick`; profilo del viperfish con reazioni a `Ignore` e `stalkRange` 0; `Grabbable.cs` con `FromPlayer` a finestra. Verifica in Play da codice (sub spostato a mano), gizmo del cono: T2-T11, T14, T15.
2. **Nuoto guidato in `Creature`.** Ramo in `Move`, stato apparente, aggressività e focus. Verifica: in Indaga raggiunge il punto evitando ostacoli; T31.
3. **Flash pieno distinguibile.** `DiverFlashlight.cs`, `DiverStimulusSource.cs`. Verifica: log dei due tipi di flash in Play.
4. **Attacco.** Avvicinamento frontale, Carica, Scatto, respinta, morso, uscite. Verifica: T12, T13, T16-T28.
5. **Fotofori a tre livelli.** `CreatureGlow.cs`, mappa emissiva bianca, colore di riposo delle altre specie invariato. Verifica: T1 con una cattura in Game view per livello; T31.
6. **Scala +35%.** Prefab e profilo. Verifica: T30.
7. **Sangue sulla preda.** `CreatureManager.cs` e un `ParticleSystem`. Verifica: T29.
8. **Due viperfish in Z2** con nidi in zone separate vicine a due celle. Verifica: T25; poi prova di Manu con mouse e tastiera.
9. Reference: `documentation/Creatures.md` (componente nuovo, modifiche, setup), `Interaction.md` per `Grabbable`, reference della torcia per il flash; catalogo; `python documentation/check_catalog.py` con uscita 0.

## Domande aperte
—

Decise da Manu il 2026-11-14 (proposte approvate):
1. **Sub che lo tiene alle spalle** (T17): resta in Attacco girandogli attorno e dopo 8 s senza riuscire a caricare torna Indaga.
2. **Flash fuori dall'attacco** (T24): in Neutrale e Indaga il flash lo allerta (Indaga sul punto del lampo); solo durante Carica e Scatto lo respinge.
3. **Fotofori durante la caccia ai lanternfish**: colore di Neutrale, più luminosi.
4. **Colore di Neutrale**: blu, distinto dal Cherenkov delle celle e dai lanternfish dalla pulsazione lenta.

## Registro
- 1.0 2026-11-14: prima versione, rivista con la critica di Antigravity (Argon, conversazione `cd5399f9`); approvata da Manu lo stesso giorno.
- 1.1 2026-11-16: passi 3-5 implementati; inspectDistance 5.5 e investigateSpeed 1.2 su richiesta di Manu.
- 1.2 2026-11-16: passi 6-7 implementati (scala 1.35, profilo ricalibrato, sangue in CreatureManager.Eat); passo 8 rimandato (nido di prova nel Crinale).
