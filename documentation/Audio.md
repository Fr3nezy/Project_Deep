# Audio
Ultima verifica: 2026-11-25

## Scopo e confini
Effetti sonori one-shot di Deeplonauts: passi, torcia, presa, uscita dalla capsula, portello, leva, modulo luce rossa, ricarica O2, cella nella presa, cancello e fotocellula (richiesta di Manu per la demo del 2026-11-25: suoni presi dal web, «a patto che metchino con il gioco», attenzione ai volumi).

Non gestisce l'ambiente (`Ambience_Abyss`, `AudioSource` in loop in scena), le voci di H.E.L.M. ([Comms.md](Comms.md)), il ronzio della torcia ([SuitFeedback.md](SuitFeedback.md)) né i suoni delle creature. Il progetto non ha un `AudioMixer`: il bilanciamento sta nei clip (normalizzati) e nel `volume` di ogni `SfxCue`.

## File e componenti
- `Assets/_Project/Code/Audio/SfxCue.cs`: un effetto one-shot da collegare a un `UnityEvent` (`Play`) o da codice.
- `Assets/_Project/Code/Audio/PlayerSfx.cs`: collega a tre `SfxCue` gli eventi C# del diver (passo, cambio modalità della torcia, presa).
- Clip in `Assets/_Project/Assets/Audio/SFX/Player/` (`SFX_Step_00`..`04`, `SFX_Torch_Click`, `SFX_Grab`, `SFX_Exit_Jump`) e `Assets/_Project/Assets/Audio/SFX/World/` (`SFX_Hatch_Open`, `SFX_Lever_Tick_00`/`01`, `SFX_Lever_Clunk`, `SFX_Module_Install`, `SFX_O2_Refill`, `SFX_Cell_Socket`, `SFX_Gate_Open`, `SFX_Photocell_Beep`). WAV 44.1 kHz mono 16 bit.
- Sorgenti: pack Kenney (licenza CC0) «Impact Sounds», «Interface Sounds», «RPG Audio», «Sci-fi Sounds», scaricati il 2026-11-25. I file originali non sono nel repo.

## Dipendenze e flusso dati
- `DiverController.OnFootstep`, `DiverFlashlight.OnModeChanged`, `PlayerGrab.OnGrabbed` (eventi C#) → `PlayerSfx` → `SfxCue.Play`.
- `UnityEvent` di scena (`HatchDoor.onOpenStarted`, `HatchExit.onExitStarted`, `LeverSwitch.onTick`/`onPulled`, `SimpleInteractable.onInteracted`, `OxygenRefillStation.onDocked`, `GrabSocket.onSocketed`, `Photocell.onTriggered`) → `SfxCue.Play` come listener persistente.

## Componenti
### SfxCue
- **Responsabilità e ciclo di vita**: `MonoBehaviour` sealed, namespace `Deeploration.Audio`. In `Awake()` aggiunge al proprio oggetto un `AudioSource` (`playOnAwake` falso, `spatialBlend` dal campo, rolloff lineare, `minDistance` 1, `maxDistance` dal campo, doppler 0). `Play()` sceglie un clip a caso (mai lo stesso due volte di fila se ce n'è più d'uno), applica uno scarto casuale di pitch e lo suona con `PlayOneShot(clip, volume)`: più `Play()` ravvicinati si sovrappongono.
- **Campi Inspector**: `AudioClip[] clips` (vuoto = muto), `float volume` (`0.7`, 0..1), `float pitchJitter` (`0.05` = ±5%, 0..0.3), `float spatialBlend` (`1`; 0 = suono della tuta in 2D, 1 = nel mondo in 3D), `float maxDistance` (`25` m, oltre non si sente).
- **API pubbliche**: `void Play()`.
- **Riferimenti**: nessuno obbligatorio. Un clip `null` nell'array salta quel `Play()`. Su un oggetto disattivato non suona: per oggetti che si spengono quando l'evento scatta (es. il modulo luce rossa raccolto) il cue va su un oggetto che resta acceso.

### PlayerSfx
- **Responsabilità e ciclo di vita**: `MonoBehaviour` sealed, namespace `Deeploration.Audio`. In `Awake()` risolve i riferimenti vuoti: `DiverController` e `PlayerGrab` nei genitori, `DiverFlashlight` con `FindFirstObjectByType`. In `OnEnable`/`OnDisable` si iscrive/disiscrive a `OnFootstep`, `OnModeChanged`, `OnGrabbed` e suona il `SfxCue` corrispondente.
- **Campi Inspector**: `DiverController diver`, `DiverFlashlight flashlight`, `PlayerGrab grab` (vuoti = cercati), `SfxCue footstep`, `SfxCue torchClick`, `SfxCue grabbed` (vuoto = quel suono spento).
- **API pubbliche**: nessuna.
- **Riferimenti**: senza un riferimento risolto il suo suono non parte; nessun errore.

## Setup in Unity
1. Oggetto figlio dove deve nascere il suono (per i suoni del mondo, vicino alla sorgente; per la tuta, sotto `PlayerCapsule/Sfx`), `SfxCue` con clip, volume, `spatialBlend` e `maxDistance`.
2. Sull'evento di scena: listener persistente `SfxCue.Play` (Inspector, o `UnityEventTools.AddVoidPersistentListener`; sulle istanze di prefab poi `PrefabUtility.RecordPrefabInstancePropertyModifications`).
3. Nuovo clip: tenerlo al livello degli altri (vedi «Clip e livelli») e regolare solo il `volume` del cue.

### Clip e livelli
Costruiti con `C:/Users/manue/Desktop/Deeplonauts/Audio/SFX_Build/build_sfx.py` (Python con numpy, scipy, soundfile; sorgenti Kenney estratte in `SFX_Build/kenney_src`, livelli misurati con `rms.py`), fuori dal repo: `python -P -E build_sfx.py <kenney_src> <out>`, poi copiare `Player/` e `World/` in `Assets/_Project/Assets/Audio/SFX/`. Trattamento per tutti i suoni del mondo e della tuta: pitch abbassato per ricampionamento (0.55..0.9: più grave e più lento, dà massa sott'acqua), passa-basso Butterworth 4° ordine (600..3500 Hz: ovattato), mix di più sorgenti con offset, dissolvenza in coda. Normalizzazione sul livello percepito: RMS massimo su finestre da 50 ms a −14 dBFS, poi limiter morbido `0.9·tanh(x/0.9)`. Riferimenti misurati: voci di H.E.L.M. ~−15 dB (RMS 50 ms max, `AudioSource` a volume 1), `AMB_Abyss` ~−5 dB a volume `0.048` in scena (≈ −31 dB effettivi).

| Clip | Sorgenti Kenney | Trattamento |
|---|---|---|
| `SFX_Step_00`..`04` | `footstep_concrete_00N` + `impactSoft_medium_00N` | pitch 0.72, passa-basso 1100 Hz |
| `SFX_Torch_Click` | `switch_002` | pitch 0.85, 3500 Hz |
| `SFX_Grab` | `impactPlate_light_001` + `cloth2` | pitch 0.8 |
| `SFX_Exit_Jump` (2.6 s) | spinta `cloth3` + `impactSoft_medium_002` a 0.3 s; atterraggio `impactSoft_heavy_001` + `footstep_snow_002` + `impactMetal_light_002` a 1.85 s | atterraggio cucito nel clip a `crouchDuration` 0.45 + `jumpDuration` 1.4 di `HatchExit` |
| `SFX_Hatch_Open` (1.8 s) | `metalLatch` + `doorOpen_001` (sci-fi) + `creak2` | durata di `HatchDoor.openDuration` 1.6 s |
| `SFX_Lever_Tick_00`/`01` | `metalClick` | pitch 0.75/0.81, tagliati a 0.18 s |
| `SFX_Lever_Clunk` | `metalLatch` + `impactMetal_heavy_002` | pitch 0.6..0.65 |
| `SFX_Module_Install` | `metalLatch` + `confirmation_002` | cinguettio ovattato a 2600 Hz |
| `SFX_O2_Refill` (2.7 s) | `metalClick` + `thrusterFire_002` in loop | sibilo 120..1500 Hz, attacco 0.05 s, rilascio 0.6 s |
| `SFX_Cell_Socket` | `impactMetal_heavy_000` + `metalLatch` + `spaceEngineLow_001` | ronzio sotto 500 Hz che sale |
| `SFX_Gate_Open` (3.8 s) | `metalLatch` + `doorOpen_000` (sci-fi) + `spaceEngineLow_003` + `impactMetal_heavy_004` a 3.0 s | pitch 0.55..0.6, rimbombo sotto 350 Hz |
| `SFX_Photocell_Beep` | `confirmation_001` | pitch 0.9, 3000 Hz |

## Configurazione verificata in prefab e scene
`SCN_Gameplay` (2026-11-25):

| Oggetto | Clip | `volume` | `spatialBlend` / `maxDistance` | Evento |
|---|---|---|---|---|
| `Player/PlayerCapsule/Sfx/Sfx_Step` | `SFX_Step_00`..`04` | 0.3 | 0 (2D), jitter 0.08 | `PlayerSfx.footstep` |
| `Player/PlayerCapsule/Sfx/Sfx_TorchClick` | `SFX_Torch_Click` | 0.35 | 0 | `PlayerSfx.torchClick` |
| `Player/PlayerCapsule/Sfx/Sfx_Grab` | `SFX_Grab` | 0.4 | 0 | `PlayerSfx.grabbed` |
| `Player/PlayerCapsule/Sfx/Sfx_ModuleInstall` | `SFX_Module_Install` | 0.6 | 0 | `Chiusa1_Puzzle/RedLightModule` `SimpleInteractable.onInteracted` |
| `World/UnderwaterLander_Crashed/HatchExitVolume/Sfx_ExitJump` | `SFX_Exit_Jump` | 0.7 | 0 | `HatchExit.onExitStarted` |
| `World/UnderwaterLander_Crashed/MSH_Hatch_Door/Sfx_HatchOpen` | `SFX_Hatch_Open` | 0.8 | 1 / 20 m | `HatchDoor.onOpenStarted` |
| `World/UnderwaterLander_Crashed/MSH_LeverHandle/Sfx_LeverTick` | `SFX_Lever_Tick_00`/`01` | 0.35 | 1 / 15 m, jitter 0.06 | `LeverSwitch.onTick` |
| `World/UnderwaterLander_Crashed/MSH_LeverHandle/Sfx_LeverClunk` | `SFX_Lever_Clunk` | 0.8 | 1 / 15 m | `LeverSwitch.onPulled` |
| `…/O2Station/SafeOxygenStressZone/Sfx_O2Refill` (Z0 `Chiusa1_Puzzle` e Z3 `Stazione_O2_A_bocca`) | `SFX_O2_Refill` | 0.45 | 1 / 20 m | `OxygenRefillStation.onDocked` |
| `…/PowerSocket/Sfx_CellSocket` (Z0 `Chiusa1_Puzzle/PowerStation`, Z3 `Stazione_O2_A_bocca/PowerStation`, Z3 `Centrale_Socket/PowerStation_1` e `_2`) | `SFX_Cell_Socket` | 0.7 | 1 / 20 m, jitter 0.04 | `GrabSocket.onSocketed` |
| `Zones/Z0_Tutorial/Chiusa1_Gate/Gate/Sfx_GateOpen` | `SFX_Gate_Open` | 0.9 | 1 / 40 m | `Chiusa1_Gate/PhotocellUnit/Photocell.onTriggered` |
| `Zones/Z0_Tutorial/Chiusa1_Gate/PhotocellUnit/Sfx_PhotocellBeep` | `SFX_Photocell_Beep` | 0.5 | 1 / 20 m | `Photocell.onTriggered` |

`PlayerSfx` sta su `Player/PlayerCapsule/Sfx` con `diver` e `grab` assegnati, `flashlight` risolta a runtime.

## Estensione del sistema
- Nuovo suono a evento di scena: un `SfxCue` e un listener persistente, nessun codice.
- Nuovo suono da evento C#: un campo `SfxCue` in `PlayerSfx` (se del diver) o un `UnityEvent` nel componente che lo genera, come `LeverSwitch.onTick`.
- Un `AudioMixer` (gruppi SFX/voce/ambiente) toglierebbe il bilanciamento a mano dai singoli cue: da valutare dopo la demo.

## Limiti e problemi noti
- Bilanciamento fatto su misure (RMS), non ancora ascoltato in cuffia da Manu: i `volume` dei cue vanno ritoccati al primo playtest.
- I suoni non sono filtrati in base alla distanza o alla profondità (niente passa-basso dinamico): il trattamento sott'acqua è nel clip.
- `SFX_Exit_Jump` ha i tempi di `HatchExit` cuciti dentro: cambiando `crouchDuration` o `jumpDuration` l'atterraggio va rifatto nel clip.
- Gli scatti della leva (`onTick`) suonano anche quando la leva torna giù da sola, a ogni `tickDegrees` attraversato.
- `PlayerSfx.grabbed` suona anche afferrando la leva.

## Verifica
- 2026-11-25, Play da codice: compilazione senza errori; invocando gli eventi di scena (`onTick`, `onPulled` → apertura del portello, `onExitStarted`, `onDocked`, `onInteracted` del modulo, `onSocketed` della cella Z0, `onTriggered` della fotocellula) e gli eventi C# (`OnFootstep`, `OnModeChanged`, `OnGrabbed`) ogni `SfxCue` collegato ha l'`AudioSource` in riproduzione; nessun errore in console.
- Non verificato: ascolto reale dei suoni e dei volumi, passi camminando davvero, cue Z3 in gioco.

## Sistemi collegati
- [Interaction.md](Interaction.md): `PlayerGrab.OnGrabbed`, `LeverSwitch.onTick`, `GrabSocket`, `SimpleInteractable`.
- [PlayerMovement.md](PlayerMovement.md): `DiverController.OnFootstep`.
- [Comms.md](Comms.md): voci di H.E.L.M., `OxygenComms`.
- [SuitFeedback.md](SuitFeedback.md): ronzio e modalità della torcia.
