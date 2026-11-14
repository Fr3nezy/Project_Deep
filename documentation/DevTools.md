# Dev Tools
Ultima verifica: 2026-11-14

## Scopo e confini
Strumenti per i test e le demo:
- il menu cheat (`CheatMenu`), che funziona anche in build;
- i punti di teletrasporto (`DevWarpPoint`);
- la partenza fuori dalla capsula. Vale solo in Editor: la build parte sempre dal risveglio.

Non coperti:
- salvataggio dello stato dei puzzle;
- salto di chiuse già aperte;
- teletrasporto durante l'uscita dal portello.

## File e componenti
- `Assets/_Project/Code/DevTools/CheatMenu.cs`: contiene
  - il menu IMGUI (tasto F1) con invulnerabilità, ossigeno infinito, ricarica O₂, sblocco del flash e teletrasporto;
  - la partenza fuori capsula.
- `Assets/_Project/Code/DevTools/DevWarpPoint.cs`: marker di destinazione. Ha nome e ordine nel menu, un gizmo sempre visibile e la voce di menu contestuale «Parti da qui».

## Dipendenze e flusso dati
- `CheatMenu.Start`, che gira dopo tutti gli `Awake`:
  - cerca in scena `DiverController`;
  - sul diver prende `CharacterController`, `OxygenSystem`, `DiverRespawn`, `StarterAssetsInputs` e `DiverFlashlight` (nei figli);
  - trova `PlayerWakeUpSequence` e `HatchExit`, e raccoglie tutti i `DevWarpPoint`.
- Il teletrasporto (`WarpTo`) segue questi passi:
  1. `PlayerWakeUpSequence.Skip()` (vedi [Prologue.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Prologue.md)).
  2. Raycast in basso da 2 m sopra il marker, sul layer `Seafloor`, con la correzione del pivot di `HatchExit`.
  3. `DiverController.PlaceStanding(punto, yaw del marker, HatchExit.EyeHeight)` (vedi [PlayerMovement.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/PlayerMovement.md)).
  4. `DiverRespawn.SetCheckpoint(marker)`.
- I cheat dell'ossigeno scrivono `OxygenSystem.Invulnerable` e `OxygenSystem.InfiniteOxygen` (vedi [Oxygen.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Oxygen.md)). Con l'invulnerabilità `DiverBiteReceiver.ReceiveBite` esce subito (vedi [Creatures.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Creatures.md)). Nessun'altra fonte di danno: la mina passa da `SetNormalized(0)`, che con l'invulnerabilità viene ignorato.
- Il flash si sblocca con `DiverFlashlight.UnlockFlash()`.

## Componenti
### CheatMenu
- **Responsabilità e ciclo di vita:**
  - `Start`: risolve i riferimenti e ordina i marker per `Order`, poi per nome. In Editor, con `startOutside` e `startPoint`, chiama `WarpTo(startPoint)`.
  - `Update`: il tasto apre e chiude il menu (`Keyboard.current`; il progetto ha solo l'Input System nuovo, `activeInputHandler: 1`).
  - `OnGUI`: disegna il pannello.
  - `OnDisable`: chiude il menu, ripristinando `timeScale` e il cursore.
  - `OnDrawGizmos`: con la partenza attiva, ridisegna in azzurro il marker di partenza con l'etichetta «PARTENZA».
- **Campi Inspector:**
  - `bool startOutside` (falso): parte in piedi su `startPoint` invece di svegliarsi nella capsula. Vale solo in Editor (`Application.isEditor`).
  - `DevWarpPoint startPoint` (null): marker di partenza.
  - `Key toggleKey` (`F1`): apre e chiude il menu.
  - `bool pauseWhileOpen` (vero): a menu aperto `Time.timeScale = 0`; alla chiusura torna il valore precedente.
  - `LayerMask groundMask` (`1 << 10`, `Seafloor`): superficie su cui posare il diver.
  - `float fallbackEyeHeight` (1.375): altezza degli occhi se in scena non c'è un `HatchExit`.
- **API pubbliche:**
  - `bool IsOpen { get; }`.
  - `void SetOpen(bool value)`: a menu aperto `StarterAssetsInputs.cursorLocked` e `cursorInputForLook` sono falsi, `look` è azzerato e il cursore libero e visibile. Alla chiusura cursore bloccato e input del look riattivato.
  - `void WarpTo(DevWarpPoint point)`: diver in piedi sul marker, posato sul fondale se il raycast colpisce, altrimenti alla quota del marker. Il marker diventa il checkpoint del respawn.
  - `void SetStartPoint(DevWarpPoint point)` (solo Editor): imposta la partenza e attiva `startOutside`, con Undo.
- **Riferimenti mancanti:**
  - Senza `DiverController` dà un warning e il menu non ha effetto.
  - Senza `OxygenSystem` o `DiverFlashlight` le voci relative non compaiono.
  - Senza marker il menu scrive «Nessun DevWarpPoint in scena.»

### DevWarpPoint
- **Responsabilità e ciclo di vita:** solo dati e gizmo, nessuna logica a runtime. Il diver arriva rivolto lungo lo Z locale del marker (conta solo lo yaw).
- **Campi Inspector:**
  - `string label` (vuoto = nome dell'oggetto): nome nel menu.
  - `int order` (0): posizione nella lista, crescente.
- **API pubbliche:**
  - `string Label { get; }`, `int Order { get; }`.
  - `static void DrawMarker(Transform t, Color color, string text)`: box 1 × 2 × 1 m, freccia all'altezza degli occhi, etichetta (`Handles.Label`, solo Editor). Ambra per tutti i marker.
- **Menu contestuale** «Parti da qui» (solo Editor): chiama `CheatMenu.SetStartPoint(this)` sul primo `CheatMenu` in scena; se manca, dà un warning.

## Setup in Unity
1. Un oggetto con `CheatMenu` per scena.
2. Un figlio vuoto con `DevWarpPoint` per ogni destinazione. Posizione sul fondale o poco sopra, Z verso la direzione di arrivo, `label` e `order`.
3. Partenza fuori capsula, da fuori Play: tasto destro sul componente `DevWarpPoint` → «Parti da qui». In alternativa `startOutside` e `startPoint` su `CheatMenu`. Per tornare al risveglio basta togliere `startOutside`.

## Configurazione verificata in prefab e scene
`SCN_Gameplay`, 2026-11-14:
- radice `_DevTools` con `CheatMenu`; `startOutside` falso, `startPoint` = `Warp_Chiusa_1_oltre`;
- quattro figli `DevWarpPoint`, nessuno dentro la geometria:

| Oggetto | `label` | `order` | Posizione | Yaw |
|---|---|---|---|---|
| `Warp_Capsula` | Capsula | 0 | (−4.62, 0.93, −6.06), sul `HatchExitPoint` | 191 |
| `Warp_Stazione_O2_1` | Stazione O₂ 1 | 1 | (−11.96, −9, −75.7), 3 m a nord della stazione | 180 |
| `Warp_Chiusa_1_davanti` | Chiusa 1 · davanti | 2 | (−18.49, −11, −87) | 180 |
| `Warp_Chiusa_1_oltre` | Chiusa 1 · oltre | 3 | (−18.49, −11, −97), oltre il gate | 180 |

## Estensione del sistema
- Nuova destinazione: aggiungere un `DevWarpPoint`, senza codice. Chiusa 2 e chiusa 3 vanno aggiunte quando esistono in scena.
- Nuovo cheat: una riga in `OnGUI` che chiama l'API pubblica del sistema. Lo stato del cheat va nel sistema stesso (come `OxygenSystem.Invulnerable`), non in campi statici: con il domain reload spento sopravvivrebbero fra un Play e l'altro.

## Limiti e problemi noti
- Teletrasporto durante l'uscita dal portello (2.45 s): `HatchExit` continua a muovere il diver e vince.
- Il respawn usa la posizione del marker, non quella posata: un marker sotto il fondale fa rinascere dentro il terreno.
- Senza checkpoint, `DiverRespawn` riporta il diver sul sedile della capsula con il controller acceso, dove resta bloccato. Problema già esistente, non toccato. Il teletrasporto e la partenza fuori capsula lo evitano impostando il checkpoint.
- L'invulnerabilità blocca solo l'ossigeno e i morsi. Lo stress da buio e da look continua: alza il consumo, ma il consumo non scala il serbatoio.
- Il menu IMGUI usa la skin di default, non è diegetico.
- Il tasto F1 è letto solo con `Keyboard.current`: niente gamepad.

## Verifica
Verificato in Play Mode il 2026-11-14 in `SCN_Gameplay`, chiamando le API da codice e non con mouse e tastiera:
- **Partenza su `Warp_Chiusa_1_oltre`:** al primo frame
  - diver a (−18.49, −11.46, −97), yaw 180, a terra;
  - `CharacterController` e `DiverController` accesi, `LockMovement` falso, `ViewRoll` 0, camera a (0, 1.375, 0);
  - `PlayerWakeUpSequence` spento, overlay ad alpha 0, checkpoint = il marker.
- **Invulnerabile:** da 0.50, `SetNormalized(0)`, `Consume(30)` e `ReceiveBite` lasciano 0.50 con `BiteCount` 0; `SetNormalized(1)` porta a 1.00.
- **Ossigeno infinito:** 200 `Update` lasciano 0.500 (consumo calcolato 0.804/s); `Consume(10)` porta a 0.475. Senza cheat 200 `Update` portano da 0.475 a 0.467.
- **Menu:** aperto, `timeScale` 0, input del look spento, cursore libero; chiuso, `timeScale` 1, input del look acceso, cursore bloccato.
- **Teletrasporto alla capsula:** (−4.62, 0.33, −6.06), yaw 191, checkpoint `Warp_Capsula`.
- **Sblocca flash:** `FlashUnlocked` da falso a vero.
- **Regressione:** con `startOutside` spento parte il risveglio normale: diver seduto a (−3.92, 0.10, −3.33), controller spento, `LockMovement` vero, risveglio acceso, overlay ad alpha 1.

Non verificati:
- il pannello IMGUI a schermo e il tasto F1 premuto davvero;
- l'uscita dal portello con la nuova `PlaceStanding`;
- il menu in build.

## Sistemi collegati
- [Oxygen.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Oxygen.md): `InfiniteOxygen`, `Invulnerable`, `DiverRespawn.SetCheckpoint`.
- [PlayerMovement.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/PlayerMovement.md): `DiverController.PlaceStanding`.
- [Prologue.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Prologue.md): `PlayerWakeUpSequence.Skip`.
- [Environment.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Environment.md): `HatchExit.EyeHeight` e la fine dell'uscita.
- [Creatures.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Creatures.md): `DiverBiteReceiver.ReceiveBite`.
- [Flashlight.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Flashlight.md): `DiverFlashlight.UnlockFlash`.
