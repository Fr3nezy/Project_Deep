# Viperfish stealth: prove a mano

Prove che Manu deve fare in Play con mouse e tastiera, perché i test da codice non le coprono. Spec: [ViperfishStealth.md](ViperfishStealth.md). Ogni agente che riprende il lavoro sul viperfish le ricorda a Manu e aggiunge qui quelle dei passi nuovi. Spunta una prova quando l'hai fatta; se fallisce scrivi cosa hai visto sotto la riga.

Per vedere lo stato senza i colori dei fotofori (arrivano col passo 5): seleziona il viperfish in Hierarchy durante il Play. Il gizmo del cono è blu in Neutrale, arancione in Indaga, rosso in Attacco; in Indaga una linea va al punto indagato, quando si allontana una linea ciano va al punto d'uscita. Anche la bocca aiuta: socchiusa in Indaga, aperta in Attacco.

Dove: i 4 viperfish di `Nest_Lanternfish (1)` vicino alla capsula, attorno a (-8, -1, -40). I due viperfish della Z2 arrivano col passo 8.

## Passi 1-2 (commit del 2026-11-14)

Già verificati da codice: T2-T15 della spec (cono, ostacoli, rumore, cella, prato, movimento, LOD) e T31 sui lanternfish. Qui resta quello che si giudica solo giocando.

- [ ] **Avvicinamento in Indaga.** Accendi la torcia Full davanti a un viperfish (dentro il suo cono, a 8-10 m) e resta fermo. Atteso: nuota verso la lampada e ci gira attorno a circa 2 m senza attraversarti, senza incastrarsi nelle rocce, guardando la lampada.
- [ ] **Perdita d'interesse.** Mentre indaga, passa alla luce rossa e resta immobile. Atteso: dopo circa 5 s se ne va, nuota per una decina di metri verso il suo nido e poi riprende a vagare. Deve sembrare naturale, non un dietrofront di scatto.
- [ ] **Movimento = attacco.** Mentre indaga e ti guarda, cammina. Atteso: passa all'Attacco (gizmo rosso, bocca aperta) e ti viene addosso. Oggi l'Attacco è provvisorio: ti insegue soltanto, niente carica, morso o respinta col flash. Dopo 8 s torna a indagare.
- [ ] **Camminare al buio non basta.** Con la luce rossa, cammina davanti a un viperfish in Neutrale. Atteso: non reagisce. Attenzione al prato di penne: la scia luminosa sotto i piedi lo allerta (voluto, T9).
- [ ] **Girare la camera non è movimento.** Mentre indaga, resta fermo e guarda in giro col mouse. Atteso: resta in Indaga.
- [ ] **Torcia alle spalle.** Torcia Full puntata in qualsiasi direzione con il viperfish girato di spalle. Atteso: non ti nota finché non si gira verso la lampada.
- [ ] **Cella nucleare.** Prendi una cella e portala nel cono di un viperfish con la luce rossa. Atteso: Indaga. Appoggiala e resta fermo: dopo 5 s perde interesse. Una cella ferma a terra non lo allerta.
- [ ] **Caccia.** Lascia un viperfish in Neutrale vicino ai lanternfish. Atteso: li caccia come prima e si sazia; se lo illumini mentre caccia smette e viene a indagare.
- [ ] **Velocità e distanze.** Giudizio di Manu: Indaga a 1.8 m/s, Attacco a 3.5 m/s, cono di 60°, portata della torcia 25 m. Da tarare se è troppo facile o troppo difficile.

## Passi 3-8

Da aggiungere quando vengono implementati (flash pieno, attacco frontale con carica e morso, fotofori a tre colori, scala +35%, sangue, due viperfish in Z2).
