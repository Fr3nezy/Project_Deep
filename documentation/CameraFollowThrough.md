# Camera & Visual Follow-Through
Ultima verifica: 2026-10-04

## Scopo e confini
Gestisce il ritardo e l'inerzia rotazionale (lag/follow-through) per elementi solidali alla visuale del diver, come la visiera del casco e la torcia.
Simula il peso e la massa di uno scafandro da palombaro e della torcia subacquea pesante: quando la visuale o il corpo ruotano rapidamente, questi oggetti non ruotano istantaneamente insieme alla camera ma inseguono con un ritardo angolare controllato.

## File e componenti
- `Assets/_Project/Code/Player/Camera/RotationalFollowThrough.cs`: Componente generalizzato per ritardo rotazionale su yaw/pitch con stato quaternion indipendente e clamping angolare massimo.
- `Assets/_Project/Code/Player/Camera/TorchFollowThrough.cs`: Implementazione preesistente dedicata all'inseguimento morbido della torcia. Conservata per retrocompatibilità con scene/prefab precedenti.

## Dipendenze e flusso dati
- **Lead Transform**: Dipende da una Transform sorgente di guida (tipicamente `PlayerCameraRoot` o la Main Camera).
- **Execution Order**: `RotationalFollowThrough` ha ordine 200 e aggiorna in `LateUpdate()` dopo `DiverController` (0), `CinemachineBrain` e `PlayerWakeUpSequence` (100).
- **Stato Quaternion Interno**: Mantiene uno stato di rotazione indipendente evitando che la rotazione del parent azzeri l'accumulo angolare nel frame.

```mermaid
flowchart LR
    Lead[cameraTarget / PlayerCameraRoot] -->|Rotazione target in LateUpdate| RFT[RotationalFollowThrough]
    RFT -->|Quaternion.Slerp smorzato| Object[HelmetVisualPivot / TorchPivot]
```

## Componenti

### RotationalFollowThrough
- **Responsabilità e ciclo di vita**:
  - `[DefaultExecutionOrder(200)]`: gira dopo `CinemachineBrain` (100, impostato nelle Script Execution Order del progetto) e `PlayerWakeUpSequence` (100). Così legge la rotazione finale del target nel frame, dopo che `DiverController.RotateView` (0) e `PlayerWakeUpSequence` l'hanno scritta, e la camera padre è già stata posizionata dal brain.
  - `Awake()`: Se `rotationTarget` è nullo usa `Camera.main.transform`. Inizializza `smoothedRotation = transform.rotation` e salva `targetOffset = Inverse(rotationTarget.rotation) * transform.rotation`.
  - `LateUpdate()`: `desired = rotationTarget.rotation * targetOffset`; `smoothedRotation = Slerp(smoothedRotation, desired, 1 - exp(-rotationSharpness * dt))`; se lo scarto supera `maxLagAngle` lo riporta entro il limite con `RotateTowards`; assegna `transform.rotation`.
- **Campi Inspector**:
  - `Transform rotationTarget` (default: `null` → `Camera.main`): Transform di riferimento da inseguire.
  - `float rotationSharpness` (default: `7f`, min `0.01`): Rapidità dell'inseguimento esponenziale.
  - `float maxLagAngle` (default: `12f`, range `0–45`): Scarto angolare massimo in gradi rispetto alla rotazione desiderata.
- **API pubbliche**:
  - Nessuna; controller autonomo in `LateUpdate`.
- **Riferimenti obbligatori e comportamento se mancanti**:
  - `rotationTarget`: se nullo e non esiste `Camera.main`, `LateUpdate` non fa nulla.

### TorchFollowThrough
- **Responsabilità e ciclo di vita**:
  - `Awake()`: Fallback su `transform.parent` se `leadTransform` è nullo.
  - `LateUpdate()`: Applica `Quaternion.Slerp` con `followSpeed * Time.deltaTime` e vincola l'offset con `maxAngleOffset`.
- **Campi Inspector**:
  - `Transform leadTransform`: Transform da inseguire.
  - `float followSpeed` (default: `12f`): Velocità di interpolazione.
  - `float maxAngleOffset` (default: `18f`): Deviazione massima tollerata in gradi.
- **Riferimenti obbligatori e comportamento se mancanti**:
  - Se `leadTransform` è nullo e il componente non ha parent, genera warning e si disabilita.

## Setup in Unity
1. Posizionare un GameObject pivot figlio del diver (es. `HelmetVisualPivot` o `TorchPivot`).
2. Aggiungere il componente `RotationalFollowThrough` al pivot.
3. Assegnare il `PlayerCameraRoot` al campo `Rotation Target`.
4. Posizionare le mesh grafiche (visiera, torcia) come figlie di questo pivot.

## Configurazione verificata in prefab e scene
- In `GameplayLoop_Blockout.unity` (verificato 2026-10-04): `Player/MainCamera/HelmetVisualPivot` ha `RotationalFollowThrough` con `rotationTarget = PlayerCameraRoot`, `rotationSharpness = 12`, `maxLagAngle = 6`; `PlayerCapsule/TorchPivot` usa lo stesso target.
  - Misura frame per frame del risveglio (200 frame a 50 fps): scarto casco–camera massimo 5,6° (primi frame, a schermo nero), poi sotto 1,5°. Prima del cambio di ordine a 200 lo scarto era ~30° (roll 28° + pitch 12°) e il casco attraversava il near plane.
- In [Player.prefab](file:///Z:/_PROJECTS/Unity/Project_Deep/Assets/_Project/Prefabs/Player.prefab):
  - Il nodo `HelmetVisualPivot` ha `RotationalFollowThrough` con `rotationTarget` collegato a `PlayerCameraRoot`.
  - Il nodo `TorchPivot` gestisce la torcia mantenendo il lag rispetto alla visuale per accentuare la claustrofobia dell'abisso.

## Estensione del sistema
- Aggiunta di sway laterale/rollio durante i passi agganciandosi all'evento `OnFootstep` di `DiverController`.

## Limiti e problemi noti
- Bug risolto 2026-10-04: con ordine 100, pari a `CinemachineBrain` e `PlayerWakeUpSequence`, il follow-through poteva leggere `PlayerCameraRoot` dopo che `DiverController.RotateView` ne aveva azzerato il roll e prima che `PlayerWakeUpSequence` lo riapplicasse. La camera prendeva roll e pitch del risveglio, il casco no, e la geometria del casco entrava nel near plane. Un nuovo script che scrive la rotazione di `PlayerCameraRoot` in `LateUpdate` deve avere ordine inferiore a 200.
- Anche in piedi alcuni vertici del casco ai bordi del campo visivo restano più vicini del near plane (0,14) della camera: comportamento preesistente, non legato al lag.
- Se il pivot si trova nella stessa gerarchia di Cinemachine, un'inversione nell'ordine di esecuzione dei `LateUpdate` può produrre jitter visivo. Si consiglia di mantenere i nodi grafici follow-through su pivot dedicati non controllati direttamente da Cinemachine.

## Sistemi collegati
- [PlayerMovement.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/PlayerMovement.md)
- [Flashlight.md](file:///Z:/_PROJECTS/Unity/Project_Deep/documentation/Flashlight.md)
