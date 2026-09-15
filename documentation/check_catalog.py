#!/usr/bin/env python3
"""
check_catalog.py - Verifica integrità catalogo codice e documentazione di Project Deep.
Nessuna dipendenza esterna richiesta (solo libreria standard Python 3).
"""

import os
import re
import sys

def main():
    # Posizionati sulla root del progetto
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.abspath(os.path.join(script_dir, ".."))
    os.chdir(project_root)

    code_dir = os.path.join("Assets", "_Project", "Code")
    readme_path = os.path.join("documentation", "README.md")

    print(f"=== CHECK CATALOG PROJECT DEEP ===")
    print(f"Root: {project_root}")
    print(f"Code Dir: {code_dir}")
    print(f"Catalog: {readme_path}\n")

    errors = []

    if not os.path.isdir(code_dir):
        print(f"ERRORE CRITICO: Directory codice non trovata: {code_dir}")
        sys.exit(1)

    if not os.path.isfile(readme_path):
        print(f"ERRORE CRITICO: Catalogo non trovato: {readme_path}")
        sys.exit(1)

    # 1. Scansiona tutti i file .cs fisici su disco
    disk_scripts = set()
    missing_metas = []
    for root, dirs, files in os.walk(code_dir):
        for f in files:
            if f.endswith(".cs"):
                rel_path = os.path.relpath(os.path.join(root, f), project_root).replace("\\", "/")
                disk_scripts.add(rel_path)
                meta_path = os.path.join(root, f + ".meta")
                if not os.path.isfile(meta_path):
                    missing_metas.append(rel_path)

    print(f"-> Script C# rilevati su disco: {len(disk_scripts)}")
    if missing_metas:
        for m in missing_metas:
            errors.append(f"Meta mancante per script: {m}")

    # 2. Parsing catalogo README.md
    with open(readme_path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()

    # Pattern tabella: | `percorso_script` | Sistema | [doc.md](link) |
    row_pattern = re.compile(
        r"\|\s*`([^`]+\.cs)`\s*\|\s*([^|]+)\s*\|\s*\[([^\]]+)\]\(([^)]+)\)\s*\|"
    )

    cataloged_scripts = set()
    referenced_docs = set()
    duplicate_catalog = set()

    for line in content.splitlines():
        match = row_pattern.search(line)
        if match:
            cs_path = match.group(1).strip().replace("\\", "/")
            system_name = match.group(2).strip()
            doc_label = match.group(3).strip()
            doc_target = match.group(4).strip()

            if cs_path in cataloged_scripts:
                duplicate_catalog.add(cs_path)
            cataloged_scripts.add(cs_path)

            # Risolvi target del documento
            # Può essere file:///Z:/.../documentation/File.md oppure File.md o documentation/File.md
            clean_target = doc_target
            if clean_target.startswith("file:///"):
                # Rimuovi schema file:///
                clean_target = clean_target.replace("file:///", "")
                # Se windows path tipo Z:/.../documentation/... normalizza
                if os.path.isabs(clean_target):
                    rel_doc = os.path.relpath(clean_target, project_root).replace("\\", "/")
                else:
                    rel_doc = clean_target
            else:
                if not clean_target.startswith("documentation/"):
                    rel_doc = f"documentation/{clean_target}"
                else:
                    rel_doc = clean_target

            referenced_docs.add((rel_doc, cs_path))

    print(f"-> Script C# catalogati in README.md: {len(cataloged_scripts)}")

    # 3. Validazione incrociata
    if duplicate_catalog:
        for dup in duplicate_catalog:
            errors.append(f"Script duplicato nel catalogo: {dup}")

    # Script su disco ma non nel catalogo
    unmapped = disk_scripts - cataloged_scripts
    if unmapped:
        for u in sorted(unmapped):
            errors.append(f"Script presente su disco ma NON nel catalogo: {u}")

    # Script nel catalogo ma non su disco
    phantom = cataloged_scripts - disk_scripts
    if phantom:
        for p in sorted(phantom):
            errors.append(f"Script presente nel catalogo ma NON su disco: {p}")

    # Verifica esistenza file di documentazione
    missing_docs = set()
    for doc_path, source_script in referenced_docs:
        if not os.path.isfile(doc_path):
            missing_docs.add((doc_path, source_script))

    if missing_docs:
        for md, sc in sorted(missing_docs):
            errors.append(f"Documento reference inesistente: {md} (referenziato da {sc})")

    # 4. Esito
    print(f"-> Documenti reference verificati: {len(set(d[0] for d in referenced_docs))}")
    if errors:
        print("\n[ERRORE] CONVALIDA FALLITA:")
        for e in errors:
            print(f"   - {e}")
        sys.exit(1)
    else:
        print(f"\n[SUCCESSO] CONVALIDA RIUSCITA: Tutti i {len(cataloged_scripts)} script sono catalogati e collegati a reference valide!")
        sys.exit(0)

if __name__ == "__main__":
    main()
