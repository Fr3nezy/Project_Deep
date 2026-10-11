# Coral — organismi sessili procedurali (corallo bambù, spugna di vetro)
Ultima verifica: 2026-10-11

## Scopo e confini
Organismi sessili del pool M1, pronti da scatterare sul fondale: corallo bambù (famiglia Isididae) e spugna di vetro tubolare a reticolo (Hexactinellida), ognuno in tre varianti con LOD. Copre lo shader condiviso `Deeplonauts/Coral`, i materiali, i modelli importati e i prefab.

Non coperto: la generazione in Blender, che sta fuori dal repository (vedi *Estensione del sistema*); lo scatter in scena (task M1 successiva, vedi [EnvironmentShading.md](EnvironmentShading.md) per Polybrush); il post-processing (vedi [Rendering.md](Rendering.md)).

Stato: **implementato**; **verificato** con cluster temporanei in `SCN_Gameplay` sotto `PostFX_Style_Analog` (vedi Verifica). Coralli scatterati nel canyon di `SCN_Gameplay` (commit `46d0d40`). **Pianificato**: scatter delle spugne sulla piana fangosa, texture del ramo da Painter, collare a rete dell'apertura delle spugne.

## File e componenti
- `Assets/_Project/Rendering/Shaders/Coral.shader`: shader `Deeplonauts/Coral` (URP, scritto a mano).
- `Assets/_Project/Materials/Environment/Organisms/BambooFan/`:
  - `SM_BambooFan_A.fbx`, `SM_BambooFan_B.fbx`, `SM_BambooFan_C.fbx`: modelli, ognuno con `MSH_BambooFan_<X>_LOD0/1/2`;
  - `M_BambooFan_Branch.mat`, `M_BambooFan_Polyp.mat`: materiali;
  - `T_BambooPolyp_Albedo.png` (RGBA, sRGB), `T_BambooPolyp_Normal.png` (Normal Map), `T_BambooPolyp_Mask.png` (lineare, R = glow): atlante 2×2 dei polipi, 1024².
- `Assets/_Project/Materials/Environment/Organisms/GlassSponge/`:
  - `SM_GlassSponge_A.fbx`, `SM_GlassSponge_B.fbx`, `SM_GlassSponge_C.fbx`: modelli, ognuno con `MSH_GlassSponge_<X>_LOD0/1/2`;
  - `M_GlassSponge.mat`: materiale;
  - `T_GlassSponge_Albedo.png` (sRGB), `T_GlassSponge_Normal.png` (Normal Map), `T_GlassSponge_Mask.png` (lineare, G = profondità dei fori, B = barrette di silice): reticolo a nido d'ape tileabile, 1024², 12 × 12 celle con barrette spesse (devono reggere il pixel filter) con jitter forte, celle allungate lungo il tubo e distorsione del dominio a frequenze intere (resta tileabile).
- `Assets/_Project/Prefabs/Organisms/SM_BambooFan_A.prefab`, `_B.prefab`, `_C.prefab`, `SM_GlassSponge_A.prefab`, `_B.prefab`, `_C.prefab`.

## Dipendenze e flusso dati
1. Blender (`GN_BambooFan_Gen` → `GN_BambooFan_Skin`) esporta una mesh per LOD con due submesh: tubi dei rami e card a X dei polipi.
2. Canali che lo shader legge dalla mesh:
   - UV0: per i rami coordinate in metri (U = circonferenza, V = lunghezza), per i polipi coordinate nell'atlante;
   - UV1.x: fase dei nodi del bambù, che cresce di 1 a ogni nodo (un nodo ogni 1.6 diametri, mai più fitti di 18 mm);
   - colore di vertice R: spessore del ramo (1 = raggio 15 mm o più, salvato in sRGB), solo sui rami;
   - posizione in object space: Y = altezza, base della colonia a Y = 0.
3. Spugna: Blender (`GN_GlassSponge_Gen` → `GN_GlassSponge_Skin`) esporta una mesh per LOD con una sola submesh. UV0: U ripete la texture un numero intero di volte attorno a ogni tubo (`Tiles Around` = 1, cioè 12 celle per giro e celle di circa 3 cm: a 3 ripetizioni le celle da 9 mm diventavano rumore sotto `PostFX_Style_Analog`; niente cucitura), V nella stessa misura di cella lungo il tubo; ogni tubo ha un offset casuale e una torsione a spirale (`Twist` 0.25), così due tubi non mostrano lo stesso disegno. UV1.x = numero casuale per tubo (`UV_Tube`), usato per sfasare gli impulsi del glow. Niente colore di vertice.
4. Il `ModelImporter` crea da solo il `LODGroup` grazie ai suffissi `_LOD0/1/2`; i prefab lo tarano.

```mermaid
flowchart LR
    Gen[GN_BambooFan_Gen] --> Skin[GN_BambooFan_Skin]
    Skin -->|FBX: UV0, UV1 fase nodi, colore R spessore| Model[SM_BambooFan_X.fbx]
    Model --> Prefab[SM_BambooFan_X.prefab + LODGroup]
    Atlas[T_BambooPolyp_*] --> Polyp[M_BambooFan_Polyp]
    Shader[Deeplonauts/Coral] --> Branch[M_BambooFan_Branch]
    Shader --> Polyp
    Branch --> Prefab
    Polyp --> Prefab
```

## Componenti

### Shader `Deeplonauts/Coral`
- Responsabilità: superficie lit URP con due modalità.
  - Ramo (`_CORAL_BRANCH`): albedo = `_BaseMap` × `_BaseColor` × gradiente da `_BottomColor` a `_TopColor` sull'altezza in object space, più le bande dei nodi calcolate per pixel da `frac(UV1.x)`. Le bande si attenuano sui rametti sottili in base al colore di vertice R.
  - Polipo (`_ALPHATEST_ON`): atlante con alpha clip, doppia faccia con normale ribaltata sul retro.
  - Entrambe: normal map opzionale, emissione = `_GlowColor` × `_GlowStrength` × `_MaskMap.r`, ondeggiamento con la corrente, bordo luminoso opzionale (`_RimStrength`), tinta delle facce posteriori (`_BackfaceTint`) glow del reticolo (maschera B: base costante più un impulso che risale lungo UV0.y con fronte netto e coda esponenziale; fase per istanza dalla posizione mondo e per tubo da UV1.x) e modalità traslucida (`_CORAL_TRANSLUCENT`: colore finale = mescola tra lo sfondo della Opaque Texture, con rifrazione e finta parete interna, e il colore illuminato, in base a opacità per angolo e maschera G dei fori). Aggiunti il 2026-10-11 per la spugna, con default neutri.
- Ondeggiamento: spostamento in world space lungo `_SwayDirection`, ampiezza `_SwayAmount` × (Y / `_SwayHeight`)², somma di due seni a `_SwayFrequency` e 2.3×. La fase dipende dalla posizione mondo dell'oggetto, quindi istanze diverse non si muovono all'unisono. Lo stesso spostamento è applicato in tutti i pass, così ombre e profondità restano allineate.
- Pass: `ForwardLit` (luce principale e aggiuntive, ombre, SSAO, nebbia, cookie, LOD cross-fade), `ShadowCaster`, `DepthOnly`, `DepthNormals` (questi tre con alpha clip quando attivo). Compatibile con SRP Batcher e GPU instancing.
- Proprietà:

| Proprietà | Tipo | Default | Significato |
|---|---|---|---|
| `_BaseMap` | 2D | white | Albedo (atlante per i polipi, tileabile per i rami) |
| `_BaseColor` | Color | (1,1,1,1) | Tinta e moltiplicatore dell'alpha |
| `_BumpMap` / `_BumpScale` | 2D / Float | bump / 1 | Normal map |
| `_MaskMap` | 2D | black | R = intensità del glow |
| `_Smoothness` | Range 0–1 | 0.25 | Lucidità |
| `_TexScale` | Float | 1 | Moltiplicatore di UV0 (per i rami: ripetizioni per metro) |
| `_Cull` | Enum CullMode | 2 (Back) | 0 sui polipi (doppia faccia) |
| `_AlphaClip` | Toggle `_ALPHATEST_ON` | 0 | Attiva l'alpha clip |
| `_Cutoff` | Range 0–1 | 0.5 | Soglia dell'alpha |
| `_Branch` | Toggle `_CORAL_BRANCH` | 0 | Modalità ramo |
| `_BottomColor` / `_TopColor` | Color | (0.70,0.64,0.50) / (0.92,0.90,0.84) | Gradiente in altezza |
| `_GradientHeight` | Float | 1 | Altezza (m) a cui si arriva a `_TopColor` |
| `_NodeColor` | Color | (0.16,0.09,0.08) | Colore delle bande |
| `_NodeWidth` / `_NodeSoftness` | Range | 0.05 / 0.02 | Mezza larghezza della banda e sfumatura, in fase |
| `_NodeFadeMin` / `_NodeFadeMax` | Range 0–1 | 0.25 / 0.58 | Colore R (sRGB) a cui le bande sono spente / piene: circa 0.75 e 4.5 mm di raggio |
| `_GlowColor` | HDR Color | (0.25,0.85,1) | Colore del glow |
| `_GlowStrength` | Range 0–4 | 0.15 | Intensità del glow |
| `_LatticeGlowColor` | HDR Color | (0.55,0.85,1) | Colore del glow del reticolo (maschera B) |
| `_LatticeGlow` | Range 0–2 | 0 | Glow costante sulle barrette |
| `_PulseStrength` | Range 0–4 | 0 | Intensità dell'impulso che risale il tubo |
| `_PulseSpeed` | Float | 0.3 | Velocità dell'impulso, in unità di UV0.y al secondo |
| `_PulseSpacing` | Float | 3 | Distanza fra due impulsi, in UV0.y |
| `_PulseTail` | Range 0.01–1 | 0.15 | Lunghezza della coda, in frazione di `_PulseSpacing` |
| `_RimColor` | Color | (0.85,0.95,1) | Colore del bordo luminoso |
| `_RimStrength` | Range 0–4 | 0 | Intensità del bordo, moltiplicata per quanto il pixel è già illuminato (al buio resta spento) |
| `_RimPower` | Range 0.5–8 | 3 | Strettezza del bordo (Fresnel) |
| `_BackfaceTint` | Color | (1,1,1) | Moltiplica l'albedo delle facce posteriori (interni dei tubi visti dall'apertura) |
| `_Translucent` | Toggle `_CORAL_TRANSLUCENT` | 0 | Parete sottile traslucida: richiede coda Transparent (3000) e la Opaque Texture di URP (attiva in `PC_RPAsset`) |
| `_OpacityFacing` / `_OpacityGrazing` | Range 0–1 | 0.55 / 0.95 | Opacità della parete vista di fronte / di taglio |
| `_HoleTransmission` | Range 0–1 | 0.6 | Quanto i fori (maschera G) lasciano passare lo sfondo |
| `_TransmissionTint` | Color | (0.85,0.92,1) | Tinta dello sfondo visto attraverso |
| `_Refraction` | Range 0–0.1 | 0.02 | Spostamento dello sfondo dato dalla normal map (in UV di schermo) |
| `_InteriorFill` | Range 0–1 | 0.55 | Finta parete interna dietro i fori: la Opaque Texture non contiene l'oggetto stesso, quindi senza questo si vedrebbe il fondale invece del retro del tubo |
| `_SwayDirection` | Vector | (1,0,0,0) | Direzione della corrente in world space; (0,0,0) spegne l'ondeggiamento |
| `_SwayAmount` | Range 0–0.3 | 0.03 | Spostamento (m) alla quota `_SwayHeight` |
| `_SwayFrequency` | Range 0–2 | 0.2 | Frequenza principale (Hz) |
| `_SwayHeight` | Float | 1 | Quota (m) di normalizzazione dell'ondeggiamento |

- Riferimenti obbligatori: nessuno. Senza UV1 le bande prendono la fase 0 (banda piena ovunque sui rami spessi); senza colore di vertice le bande restano spente.

### M_BambooFan_Branch
Shader `Deeplonauts/Coral`, `_Branch` = 1, `_Cull` = Back, `_GlowStrength` = 0, `_Smoothness` = 0.3, `_BottomColor` (0.62, 0.61, 0.57), `_TopColor` (0.84, 0.85, 0.84), instancing attivo. Nessuna texture: colore dal gradiente e dalle bande. Gradiente tarato in Play il 2026-10-11: i default dello shader (avorio 0.92) saturavano con la torcia al massimo a meno di 2 m e il dithering cancellava le bande; un avorio caldo più scuro perdeva il bianco freddo; scelto un bianco neutro-freddo a metà strada.

### M_BambooFan_Polyp
Shader `Deeplonauts/Coral`, `_AlphaClip` = 1, `_Cutoff` = 0.5, `_Cull` = Off, atlante su `_BaseMap`, `_BumpMap`, `_MaskMap`, `_BaseColor` (0.88, 0.89, 0.91), `_GlowStrength` = 0.3, `_Smoothness` = 0.25 (stesso motivo del ramo: con il bianco pieno e 0.35 di smoothness i polipi saturavano e sembravano lucine), render queue 2450 (AlphaTest), tag `RenderType` = `TransparentCutout`, **pass `ShadowCaster` disattivato** (le card dei polipi non proiettano ombra), instancing attivo.

### Modelli SM_BambooFan_A / B / C
Import: `bakeAxisConversion` attivo, normali importate, tangenti MikkTSpace, `generateSecondaryUV` **spento** (altrimenti sovrascriverebbe UV1), camere, luci, blend shape e animazione spenti, materiali rimappati sui due `.mat` qui sopra.

| Variante | Forma | Misure (L × H × P) | Triangoli LOD0 / LOD1 / LOD2 |
|---|---|---|---|
| A | ventaglio adulto | 1.14 × 0.98 × 0.31 m | 5134 / 2960 / 1306 |
| B | colonia giovane | 0.70 × 0.56 × 0.13 m | 1958 / 1114 / 444 |
| C | candelabro a dita parallele | 0.61 × 0.94 × 0.14 m | 4666 / 2714 / 816 |

La LOD2 non ha polipi (una sola submesh, il ramo).

### Prefab SM_BambooFan_A / B / C
Varianti del modello. `LODGroup`: passaggi a 0.30, 0.10 e 0.02 di altezza relativa sullo schermo, `fadeTransitionWidth` 0.15, `fadeMode` CrossFade con `animateCrossFading`. Ombre proiettate da LOD0 e LOD1 (solo i rami, vedi materiale polipi), spente su LOD2.

### M_GlassSponge
Shader `Deeplonauts/Coral`, modalità ramo e alpha clip spente, **modalità traslucida attiva** (coda 3000, tag `RenderType` = `Transparent`, `_OpacityFacing` 0.8, `_OpacityGrazing` 1, `_HoleTransmission` 0.55, `_InteriorFill` 0.7, `_Refraction` 0.02), glow del reticolo tarato da Manu in Play (`_LatticeGlow` 0.006, `_PulseStrength` 0.08, `_PulseSpeed` 1, `_PulseSpacing` 4, `_PulseTail` 0.07), `_Cull` = Off (si vede l'interno dei tubi), `_BaseMap`/`_BumpMap`/`_MaskMap` (G = fori, B = barrette) del reticolo, `_BaseColor` (0.92, 0.94, 0.95), `_Smoothness` 0.35, `_RimStrength` 1.2, `_RimPower` 3, `_RimColor` (0.85, 0.95, 1), `_BackfaceTint` (0.78, 0.80, 0.84), `_GlowStrength` 0, `_SwayAmount` 0.05 con `_SwayDirection` (1,0,0) e `_SwayFrequency` 0.2: licenza artistica scelta da Manu il 2026-10-11 per dare vita all'ambiente (lo scheletro di spicole di silice fuse in realtà è rigido), instancing attivo.

### Modelli e prefab SM_GlassSponge_A / B / C
Stesse impostazioni di import del corallo, materiale rimappato su `M_GlassSponge`. `LODGroup` dei prefab: passaggi a 0.25, 0.08 e 0.015, cross-fade animato; ombre su LOD0 e LOD1, spente su LOD2. Pivot alla base (Y min −0.02: la base affonda di 2 cm nel fango).

| Variante | Forma | Misure (L × H × P) | Triangoli LOD0 / LOD1 / LOD2 |
|---|---|---|---|
| A | mazzo di 9 tubi a fuso | 0.50 × 0.50 × 0.44 m | 5508 / 2080 / 840 |
| B | gruppo giovane di 4 tubi | 0.24 × 0.27 × 0.21 m | 2748 / 1040 / 420 |
| C | 6 tubi alti e stretti | 0.41 × 0.68 × 0.33 m | 3852 / 1456 / 588 |

## Setup in Unity
1. Trascinare un prefab di `Prefabs/Organisms/` sul fondale; il pivot è alla base.
2. Scatter consigliato (task M1 successiva): cluster su roccia, creste e bordi del canyon; ventagli con la stessa rotazione (perpendicolari alla corrente) ±15°; mix indicativo 60% B, 30% A, 10% C, scala 0.8–1.2.
3. Spugne: sulla piana fangosa, isolate o a coppie, rotazione Y libera.
4. Per cambiare la corrente: `_SwayDirection` e `_SwayAmount` su entrambi i materiali, con gli stessi valori, così rami e polipi si muovono insieme.

## Configurazione verificata in prefab e scene
Prefab e materiali come descritti sopra. Nessuna istanza salvata in scena: il test del 2026-10-11 in `SCN_Gameplay` era temporaneo e la scena è stata riaperta senza salvare.

## Estensione del sistema
- **Rigenerare le mesh:** in Blender, file `C:\Users\manue\Desktop\Coral.blend`. Per ogni variante `SKL_BambooFan_<X>` contiene il generatore (`GN_BambooFan_Gen`) e le tre `MSH_BambooFan_<X>_LOD*` lo vestono (`GN_BambooFan_Skin`, polipi inclusi). L'exporter della collezione `EXP_BambooFan_<X>` scrive `Desktop\Deeplonauts\Export\BambooFan\SM_BambooFan_<X>.fbx`, da copiare sopra il file in `Organisms/BambooFan/` mantenendo il `.meta`.
- **Texture custom dei polipi:** sostituire i PNG dell'atlante mantenendo nomi e layout (4 tile da 512, polipo dritto con la base in basso al centro, alpha attorno). Le sorgenti high-poly sono nella scena `TEX_BambooPolyp` del `.blend`.
- **Texture custom dei rami:** materiale tileabile (Painter o Designer) su `_BaseMap`/`_BumpMap` di `M_BambooFan_Branch`, con `_TexScale` = ripetizioni per metro. Le bande dei nodi restano dello shader.
- **Spugna di vetro:** nello stesso `Coral.blend`, collezione `GlassSponge`: `SKL_GlassSponge_<X>` con `GN_GlassSponge_Gen` (numero di tubi, apertura, schiacciamento a ventaglio, profilo a fuso con collo e svasatura, curvatura, base), `MSH_GlassSponge_<X>_LOD*` con `GN_GlassSponge_Skin` (lati, anelli, ripetizioni della texture attorno al tubo). Exporter in `EXP_GlassSponge_<X>` → `Export\GlassSponge\`. Texture generate da script in `Painter\MTL_GlassSponge\`.
- **Altri organismi:** riusare lo shader. Le bande si spengono togliendo `_Branch`, il glow con `_GlowStrength` = 0.

## Limiti e problemi noti
- L'ondeggiamento sposta i vertici solo in base all'altezza: tutta la colonia si inclina insieme, i rami non si piegano in modo indipendente.
- Le bande usano la fase per vertice interpolata: su rami campionati ogni 4.5 cm (LOD0) restano nitide, ma la LOD2 (10 cm) può mostrare bande leggermente oblique.
- Il colore di vertice R è esportato in sRGB, quindi `_NodeFadeMin`/`_NodeFadeMax` sono valori sRGB.
- Nessun bake di AO: le giunture dei rami non si scuriscono.
- La LOD2 ha i rametti a segmenti dritti, da usare solo da lontano.
- Spugna: le pareti dei tubi non hanno spessore (superficie singola a doppia faccia), il bordo dell'apertura è una linea sottile. Manca il collare a rete delle reference.
- Spugna: i tubi del mazzo possono compenetrarsi, come in natura, ma senza fusione nel punto di contatto.
- Modalità traslucida: l'oggetto sta nella coda Transparent, quindi non entra nella depth texture (niente SSAO su di lui) e attraverso la parete non si vedono gli altri tubi dello stesso mazzo né altri oggetti trasparenti. La Opaque Texture è a metà risoluzione (`_2xBilinear`): lo sfondo attraverso è sfocato, cosa che qui aiuta l'effetto lattiginoso.

## Verifica
Eseguita il 2026-10-11 in Unity 6000.3.19f1:
- `Deeplonauts/Coral` compila senza messaggi; console senza errori dopo import e render;
- import: 3 LOD per modello, `LODGroup` creato dall'importer, UV1 e colori presenti, pivot a Y ≈ 0 (−0.001), assi corretti (altezza su Y);
- colore R dei vertici del ramo nell'intervallo 0.21–1, coerente con le soglie delle bande;
- render da una camera temporanea con torcia simulata (spot 6, raggio 15 m, 57°), con e senza post-processing: rametti e polipi leggibili a 3–4 m sotto `PostFX_Style_Analog`, glow dei polipi visibile fuori dal cono della torcia.

- Spugna (2026-10-11): 3 LOD per modello con `LODGroup` automatico, pivot alla base; render di prova con 4 spugne sul fango, torcia simulata e `PostFX_Style_Analog`: silhouette a mazzo leggibile, reticolo reso come porosità fine dal dithering. Interni troppo neri alla prima prova, `_BackfaceTint` alzato da 0.55 a 0.78. Seconda prova con la modalità traslucida: senza `_InteriorFill` i fori mostravano il fondale e i tubi in ombra sembravano vuoti; con finta parete interna 0.55 e opacità 0.8/1 le spugne restano lattiginose, la trasparenza si concentra nei fori. Nella prova le spugne sparivano al primo render della camera nuova: era il cross-fade animato delle LOD che partiva da zero, non lo shader. Terza prova, al buio sulle spugne piazzate da Manu: con glow base 0.06 il reticolo sembrava illuminato, portato a 0.015; impulsi prima quasi sincroni (fronte orizzontale da linea di scansione), sfasati con UV1.x per tubo; coda ridotta a 0.07 e spaziatura 4. Il glow del reticolo è una **licenza artistica**: le spugne di vetro non sono note per emettere luce, ma le loro spicole conducono la luce come fibre ottiche.

Non verificato: transizioni del cross-fade in movimento, costo con decine di istanze, spugne in Play.

## Sistemi collegati
- [EnvironmentShading.md](EnvironmentShading.md): fondale, rocce e scatter con Polybrush.
- [Rendering.md](Rendering.md): post-processing `PostFX_Style_Analog`.
- [Landmark.md](Landmark.md): altro shader scritto a mano del progetto.
