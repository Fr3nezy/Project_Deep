# Landmark — sagome visibili nella nebbia e luci di segnalazione
Ultima verifica: 2026-10-09

## Scopo e confini
Rende un oggetto lontano (la torre di `SCN_Gameplay`) leggibile come punto di riferimento anche oltre la distanza in cui la nebbia di scena lo cancellerebbe, e aggiunge luci rosse che si accendono in sequenza dall'alto verso il basso.

La nebbia di `SCN_Gameplay` è Exponential Squared con densità 0.065: già a 30 m la visibilità è circa il 2%, quindi a 600 m un materiale normale coincide con lo sfondo. Il materiale landmark non toglie la nebbia: la limita a un massimo e, con la distanza, sostituisce il colore illuminato con un colore di sagoma. Da vicino si comporta come un materiale lit qualunque.

Coperto qui: lo shader `Deeplonauts/Landmark`, i due materiali e le luci della torre.
Non coperto: gli shader di fondale e rocce (vedi [EnvironmentShading.md](EnvironmentShading.md)), il post-processing (vedi [Rendering.md](Rendering.md)).

Stato: **implementato**, verificato con render da script nell'Editor (vedi Verifica). **Non verificato** in Play Mode; la torre è ancora una mesh di blockout.

## File e componenti
- `Assets/_Project/Rendering/Shaders/Landmark.shader` (shader `Deeplonauts/Landmark`): shader HLSL scritto a mano per URP, con i pass `ForwardLit`, `ShadowCaster`, `DepthOnly` e `DepthNormals`.
- `Assets/_Project/Materials/Environment/Landmark/M_Landmark_Tower.mat`: corpo della torre.
- `Assets/_Project/Materials/Environment/Landmark/M_Landmark_Beacon.mat`: luci di segnalazione.

Nessuno script C#: la sequenza delle luci è calcolata nello shader dal tempo e dalla quota.

## Dipendenze e flusso dati
1. Vertex: posizione, normale e fattore di nebbia URP (`ComputeFogFactor`).
2. Fragment: illuminazione PBR di URP (`UniversalFragmentPBR`: luce principale, luci aggiuntive in Forward+, ombre, SSAO) con GI dall'ambient probe (`SampleSH`).
3. `visibility = ComputeFogIntensity(fogFactor)`, cioè 1 vicino e 0 nella nebbia piena.
4. `color = lerp(_FarColor, lit, visibility)`: con la distanza il colore illuminato diventa il colore di sagoma.
5. `color += _EmissionColor * beacon`, dove `beacon` è 1 senza sequenza oppure l'impulso della sequenza.
6. `color = lerp(color, unity_FogColor, min(1 - visibility, _MaxFog))`: la nebbia non supera mai `_MaxFog`.

Sequenza: `phase` va da 0 a `_BeaconTop` a 1 a `_BeaconBottom` (quota mondo del frammento). Con `t = frac(_Time.y / _BeaconPeriod)` e `d = frac(t - phase * _BeaconSweep)`, l'impulso è `lerp(_BeaconBase, 1, exp(-d * _BeaconFade))`. Il fronte scende dalla cima al fondo nella frazione `_BeaconSweep` del periodo, poi c'è una pausa. Ogni luce si accende di colpo e si spegne in modo esponenziale. Siccome usa la quota del frammento e non un indice, più mesh con lo stesso materiale si accendono in ordine senza script e senza property block, e lo SRP Batcher resta attivo.

## Componenti

### Deeplonauts/Landmark (shader)
- Responsabilità: superficie opaca illuminata con nebbia limitata ed emissione opzionale in sequenza.
- Campi Inspector (proprietà del materiale):

| Proprietà | Tipo | Default nello shader | Significato |
|---|---|---|---|
| `_BaseColor` | Color | (0.35, 0.35, 0.35, 1) | Albedo |
| `_Metallic` | Range 0–1 | 0 | Metallicità |
| `_Smoothness` | Range 0–1 | 0.2 | Levigatezza |
| `_FarColor` | Color | nero | Colore verso cui va la superficie illuminata quando la nebbia la coprirebbe |
| `_MaxFog` | Range 0–1 | 0.75 | Copertura massima della nebbia: 1 equivale a un materiale normale, 0 ignora la nebbia |
| `_EmissionColor` | Color HDR | nero | Emissione, sommata dopo la sagoma e prima della nebbia |
| `_BeaconOn` | Toggle | 0 | Attiva la sequenza (con 0 l'emissione è costante) |
| `_BeaconTop` | Float | 75 | Quota mondo dove la sequenza parte |
| `_BeaconBottom` | Float | −70 | Quota mondo dove la sequenza finisce |
| `_BeaconPeriod` | Float | 4 | Durata di un ciclo (s) |
| `_BeaconSweep` | Range 0.05–1 | 0.6 | Frazione del periodo usata dalla discesa; il resto è pausa |
| `_BeaconFade` | Float | 10 | Velocità di spegnimento: più alto, impulso più corto (con 10 e periodo 4 s si dimezza in circa 0.28 s) |
| `_BeaconBase` | Range 0–1 | 0.03 | Livello minimo delle luci spente |

- API pubbliche: nessuna. Le proprietà sono nel `CBUFFER UnityPerMaterial` (compatibile con lo SRP Batcher).
- Riferimenti obbligatori: nessuno. Senza nebbia di scena attiva `visibility` vale 1 e lo shader è un lit semplice.

### M_Landmark_Tower
`_BaseColor` (0.16, 0.16, 0.16), scurito da Manu il 2026-10-09 (prima 0.3, 0.3, 0.32), `_FarColor` nero, `_MaxFog` 0.5, nessuna emissione.

### M_Landmark_Beacon
`_BaseColor` e `_FarColor` (0.1, 0.02, 0.02), `_MaxFog` 0.35, `_EmissionColor` (1, 0.05, 0.03) × 6 HDR, `_BeaconOn` 1, `_BeaconTop` 76.6 (cima della torre), `_BeaconBottom` −62.6 (fondo della torre + 10 m), `_BeaconFade` 10, il resto ai default.

## Setup in Unity
1. Assegnare un materiale con shader `Deeplonauts/Landmark` all'oggetto. Regolare `_MaxFog` guardando la sagoma dalla distanza di gioco: 0.5 dà una sagoma scura leggibile sul fondo blu.
2. Per le luci: mesh piccole (sfere da 2 m) sulla superficie, con un materiale che abbia `_BeaconOn` attivo e `_BeaconTop`/`_BeaconBottom` pari alle quote mondo dell'oggetto. Ombre spente sulle luci.
3. Se la torre si sposta in verticale o cambia altezza, aggiornare `_BeaconTop` e `_BeaconBottom`: sono quote mondo, non relative all'oggetto.

## Configurazione verificata in prefab e scene
`SCN_Gameplay`:
- `MapBlocking/Towe` (mesh di blockout di 18 vertici, 11.5 × 149 × 9.4 m, centro a circa (−60, 2, −599)) con `M_Landmark_Tower`.
- `MapBlocking/Towe_Beacons`: 32 sfere `Beacon_<fila>_<lato>`, 8 file da 2 a 12 m sotto la cima fino a 12 m sopra il fondo, una per lato (N, S, E, W), posizionate sulla superficie con un raycast sulla mesh e scostate di 0.6 m lungo la normale. `M_Landmark_Beacon`, ombre spente, nessun collider.
- Camera del player: far clip 800 (la torre è a circa 600 m dallo spawn).

## Estensione del sistema
- Altri landmark: stesso shader, `_MaxFog` più alto per quelli meno importanti.
- Colore della sequenza: `_EmissionColor` sul materiale delle luci.
- Una luce fissa in cima (faro): un secondo materiale con `_BeaconOn` 0 e `_MaxFog` basso.
- GI da Adaptive Probe Volumes: oggi lo shader usa solo l'ambient probe. Per i landmark vicini al gameplay si può passare a `SAMPLE_GI` con le varianti di `ProbeVolumeVariants.hlsl`.

## Limiti e problemi noti
- Niente texture né normal map: pensato per la torre di blockout. Per la mesh definitiva vanno aggiunti `_BaseMap` e `_BumpMap`.
- GI solo dall'ambient probe (vedi sopra), niente lightmap, decal e light layers.
- Il tetto della nebbia vale per ogni distanza: un oggetto con `_MaxFog` basso si vede anche dietro la nebbia a distanza intermedia, dove ci si aspetterebbe che fosse già sparito. Voluto per la torre, da valutare per altri oggetti.
- La sagoma è leggibile solo finché `_FarColor` resta più scuro dello sfondo (colore della nebbia). Se si schiarisce la nebbia va rivisto.
- Nel render da script nell'Editor `_Time` non avanza dentro la stessa chiamata: la sequenza animata va guardata in Play Mode o con la Scene View in "Always Refresh".

## Verifica
Eseguita il 2026-10-09 in Unity 6000.3.19f1 (URP 17.3.0):
- import dello shader senza messaggi, `isSupported` vero;
- render dalla camera del player (stesse impostazioni, post-processing acceso) a 560 m: con `_MaxFog` 0.75 la torre è quasi invisibile, con 0.5 è una sagoma scura leggibile; le 8 file di luci sono visibili;
- render a 120 m e a 220 m: torre e luci leggibili, nessuno stacco tra sagoma e superficie illuminata;
- sequenza: render HDR senza post-processing, massimo del rosso per fascia orizzontale. Con la sequenza accesa una sola fascia è a 2.64 e le altre a 0.12; con la sequenza spenta tutte a 3.91; con `_BeaconBase` 0 le fasce spente scendono a 0.01.

Non verificato: Play Mode (animazione a schermo, torcia vicino alla torre), comportamento con la mesh definitiva.

## Sistemi collegati
- [EnvironmentShading.md](EnvironmentShading.md): shader del fondale e delle rocce.
- [Rendering.md](Rendering.md): post-processing (bloom e posterizzazione agiscono sulle luci).
- [Environment.md](Environment.md): oggetti ambientali.
