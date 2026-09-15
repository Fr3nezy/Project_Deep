# Regole di Sviluppo e Documentazione per Agenti AI

Questo repository utilizza una disciplina rigorosa di documentazione e coerenza architetturale. Ogni agente che opera su questo progetto è tenuto a rispettare le seguenti regole tassative:

# Documentazione dei sistemi

- Prima di modificare un sistema, leggere documentation/README.md
  e la relativa reference.
- Ogni sistema nuovo deve avere una reference Markdown in documentation/.
- Ogni modifica a comportamento, API, Inspector, dipendenze o setup
  deve aggiornare la reference nello stesso intervento.
- Ogni nuovo script deve comparire nel catalogo documentation/README.md.
- Documentare tutte le componenti, incluse interfacce, enum e profili.
- Distinguere implementato, verificato e pianificato.
- Non duplicare reference: aggiornare quella canonica.
- Non dichiarare concluso un sistema senza documentazione e verifica.
- Negli spostamenti Unity conservare i .meta e i GUID originali.

## Struttura standard obbligatoria per ogni nuova reference (`documentation/<Sistema>.md`):
```markdown
# Nome sistema
Ultima verifica: YYYY-MM-DD

## Scopo e confini
## File e componenti
## Dipendenze e flusso dati
## Componenti
### NomeClasse
- Responsabilità e ciclo di vita
- Campi Inspector: tipo, default nel codice, significato
- API pubbliche: firme esatte, ritorni ed eventi
- Riferimenti obbligatori e comportamento se mancanti

## Setup in Unity
## Configurazione verificata in prefab e scene
## Estensione del sistema
## Limiti e problemi noti
## Verifica
## Sistemi collegati
```

## Verifica Integrità del Catalogo
Dopo ogni operazione su file C# o documentazione, eseguire da terminale:
```bash
python documentation/check_catalog.py
```
Lo script deve terminare con codice di uscita `0` (nessun errore, nessuno script orfano, nessun percorso inesistente).
