# Cable — cavo fisico trascinabile
Ultima verifica: 2026-11-21

## Scopo e confini
Cavo che il sub tira da un rocchetto fisso fino a una presa: la spina è un `Grabbable` qualunque, la presa un `GrabSocket` qualunque, il cavo in mezzo è una corda fisica a particelle che si posa sul fondale, scorre sulle gobbe e, a fine lunghezza, trattiene la spina.

Coperto qui: `PhysicsCable`, il prefab `CableReel`, i materiali del cavo e i due cavi della Z3.
Non coperto: presa e trascinamento (`PlayerGrab`, `Grabbable`, `GrabSocket`, `SocketGroup` in [Interaction.md](Interaction.md)), stazioni O₂ ([Environment.md](Environment.md)).

## File e componenti
- `Assets/_Project/Code/Interaction/PhysicsCable.cs`: simulazione e disegno del cavo.
- `Assets/_Project/Prefabs/Puzzle/CableReel.prefab`: rocchetto placeholder + ancoraggio + spina + cavo.
- `Assets/_Project/Materials/Environment/M_Cable.mat` (URP Lit, (0.32, 0.24, 0.05)), `M_CableReel.mat` ((0.18, 0.18, 0.17)), `M_CablePlug.mat` ((0.55, 0.38, 0.05)).

## Dipendenze e flusso dati
1. `FixedUpdate`: integrazione Verlet delle particelle libere (velocità smorzata + gravità ridotta), estremi fissati su `anchor` e sul punto d'attacco della spina.
2. `iterations` passate di vincoli **solo in trazione**: un segmento più corto di quello a riposo non spinge, quindi il cavo si raccoglie senza esplodere.
3. Collisioni per particella: `SphereCastNonAlloc` dalla posizione precedente (trigger ignorati, collider della spina ignorato), arresto al contatto e scorrimento del moto restante sul piano della superficie, poi `Depenetrate` (`OverlapSphereNonAlloc` + `Physics.ComputePenetration` con una sfera di prova nascosta su Ignore Raycast).
4. Lunghezza del percorso → `Excess` oltre la lunghezza a riposo → forza sulla spina verso la particella precedente, `min(Excess × tensionStiffness, maxTension)`.
5. `LateUpdate`: `LineRenderer` in world space, estremi sui transform interpolati.

Con `maxTension` (900 N) sopra `PlayerGrab.maxForce` (400 N) il sub non riesce a portare la spina oltre la lunghezza: la distanza dalla mano cresce e `PlayerGrab` rilascia a `breakDistance` (2.5 m). Nessun codice in più per il fine corsa.

## Componenti
### PhysicsCable
- Responsabilità e ciclo di vita: `MonoBehaviour` sealed, `[RequireComponent(typeof(LineRenderer))]`, namespace `Deeploration.Interaction`. In `Awake` crea `ceil(maxLength / segmentLength) + 1` particelle stese in linea retta tra ancoraggio e spina, ognuna sollevata sopra il fondale con un raycast, e la sfera di prova `CableProbe` (`HideAndDontSave`, layer 2, trigger). Simula in `FixedUpdate`, disegna in `LateUpdate`. Numero di particelle fissato all'avvio: cambiare `maxLength` in Play non ha effetto.
- Campi Inspector:

| Campo | Tipo | Default | Significato |
|---|---|---|---|
| `anchor` | Transform | null = questo transform | Punto fisso da cui esce il cavo |
| `plug` | Grabbable | null | Spina all'altro capo (con Rigidbody) |
| `plugAttachLocal` | Vector3 | (0, 0, −0.15) | Punto d'attacco del cavo nello spazio della spina |
| `maxLength` | float ≥ 1 | 30 | Lunghezza a riposo, m |
| `segmentLength` | float 0.1–1 | 0.25 | Distanza tra particelle, m |
| `iterations` | int 1–60 | 24 | Passate dei vincoli per passo fisico |
| `gravity` | float ≥ 0 | 2.5 | Gravità in acqua, m/s² |
| `damping` | float 0–1 | 0.08 | Smorzamento della velocità per passo |
| `collisionMask` | LayerMask | tutto | Layer con cui il cavo collide |
| `radius` | float ≥ 0.005 | 0.04 | Raggio di collisione della particella, m |
| `skin` | float ≥ 0 | 0.01 | Margine dalle superfici, m |
| `friction` | float 0–1 | 0.6 | Quota di velocità persa a ogni contatto |
| `tensionStiffness` | float ≥ 0 | 1500 | N per metro oltre la lunghezza |
| `maxTension` | float ≥ 0 | 900 | Forza massima sulla spina, N |
| `currentLength` | float | — | Debug, sola lettura |

- API pubbliche: `float CurrentLength { get; }` (lunghezza del percorso, m), `float Excess { get; }` (metri oltre la lunghezza a riposo, 0 se lento), `Grabbable Plug { get; }`.
- Riferimenti obbligatori: `plug`. Senza spina `FixedUpdate` e `LateUpdate` non fanno nulla (il cavo resta fermo dove è stato creato). Spina cinematica (inserita in una presa): niente forza di tensione, il cavo continua a posarsi.

## Setup in Unity
1. Trascinare `CableReel.prefab` in scena, rocchetto appoggiato al fondale, ruotato verso la presa.
2. Mettere `CablePlug` a 1-3 m dal rocchetto, sopra il terreno: se coincide con l'ancoraggio il cavo parte raccolto in un punto.
3. Misurare il percorso reale fino alla presa e impostare `maxLength` al percorso + 15-35 %: la lunghezza è la leva del puzzle.
4. Sulla presa (`GrabSocket`) mettere `acceptedItemId = cable_plug` **come override di scena**: `O2Station.prefab` e `PowerStation.prefab` restano su `power_cell` per la chiusa 1.
5. Cablare `onSocketed` della presa su ciò che il cavo alimenta.

## Configurazione verificata in prefab e scene
- `CableReel.prefab`: radice con `LineRenderer` (`M_Cable`, larghezza 0.06, ombre spente) e `PhysicsCable` (`collisionMask` = Default, Obstacle, Seafloor); `Reel` cilindro con `BoxCollider`; `CableAnchor` a y 0.95; `CablePlug` a (0, 0.2, 2) con `Rigidbody` massa 4, `Grabbable` (`itemId = cable_plug`, `stimulusReach = 0` → la spina non attira creature, prompt «Tieni premuto il tasto sinistro per tirare il cavo»), `Visual` cubo 0.16 × 0.16 × 0.3 con `BoxCollider`, `Pins` senza collider.
- `SCN_Gameplay`, `Zones/Z3_Viperfish/Z3_Puzzle` (2026-11-21):
  - `Cavo_A_bocca`: rocchetto a (53, −21.0, −145.5), in testa al nastro; presa di `Stazione_O2_A_bocca` a 12.9 m in linea d'aria; `maxLength` 24.
  - `Cavo_B_gradone`: rocchetto a (90, −4.0, −98.7) sul gradone, unico punto piano alla quota della presa entro 22 m; presa di `Stazione_O2_B_gradone` a 22 m; `maxLength` 30.
  - Le due `PowerSocket` delle stazioni hanno `acceptedItemId = cable_plug` e `requireSeating` spento (snap all'ingresso nel trigger da 1.45 m, magnete 0.7 m invariato); `onSocketed` invariato (`SafeOxygenStressZone.PowerOn` + `Beacon_Unpowered.SetActive(false)`).

## Estensione del sistema
- Cavi fissi decorativi della compagnia: stesso componente con la spina cinematica già nella presa.
- Cavi che si accendono a facility attiva: emissione sul materiale del `LineRenderer` legata a `onSocketed`.
- Tubo vero al posto del `LineRenderer` (che è sempre rivolto alla camera): mesh estrusa sulle particelle.

## Limiti e problemi noti
- **Spina che ruota in modo strano quando la si prende** (playtest di Manu, 2026-11-21): probabile coppia tra presa libera (`GrabMode.Free`, forza applicata al punto d'appoggio) e tensione del cavo applicata a `plugAttachLocal` sul retro. Da provare: `KeepUpright` o un nuovo modo che allinei la spina alla direzione di trascinamento, `angularDamping` più alto sulla spina, tensione applicata al baricentro.
- **Inserimento mai provato in gioco**: manca la mesh del socket per la spina; le prese delle stazioni sono ancora quelle della cella.
- Il cavo non collide con il sub (Player escluso dalla maschera) né con le creature: ci passano attraverso.
- Una particella finita molto sotto un `MeshCollider` a foglio non viene vista da `ComputePenetration` e non risale; non succede durante la simulazione perché ogni spostamento è spazzato da una posizione fuori, solo con teletrasporti della spina attraverso il terreno.
- Le particelle in eccesso, a cavo lento, si raccolgono dentro il collider della spina (non visibili).
- Rocchetto, spina e materiali sono placeholder di blocking.
- La motivazione narrativa (rocchetti come cassette di giunzione ancora sotto tensione della linea dell'escavatore) è una proposta da confermare con Manu.

## Verifica
2026-11-21, Play mode con passi chiamati a mano (editor senza focus):
- Stesa iniziale sopra il fondale, nessun NaN.
- Spina portata lungo il fondale fino alle prese: A 15.6 m su 24, B 24.4 m su 30; tutti i segmenti ≤ 0.252 m su 0.25 dopo la correzione dello scorrimento (prima il cavo si incollava alle gobbe con segmenti fino a 18 m).
- Spina portata 15 m oltre: `Excess` 20.8 m, forza 900 N.
- Riavvolgimento tornando indietro: lunghezza da 33.6 a 3.2 m.
- Costo: 0.3 ms per passo fisico per un cavo da 97 particelle.
- Playtest di Manu (2026-11-21): base approvata; spina che ruota in modo strano in mano; inserimento non provato (manca la mesh del socket).
- Non verificato: presa reale col sub (strattone, rilascio a `breakDistance`), inserimento con `requireSeating` e magnete, resa del `LineRenderer` in Game view.

## Sistemi collegati
[Interaction.md](Interaction.md), [Environment.md](Environment.md), [Landmark.md](Landmark.md).
