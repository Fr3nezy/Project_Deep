# Esporta le istanze di Geometry Nodes di un .blend in <nome>.scatter.json accanto al file.
# Lanciato da Unity (ScatterExport): blender --background <file.blend> --python export_scatter.py
# Solo le istanze il cui oggetto sorgente sta nella collection "ScatterSources" (anche annidata).
# Spazio mondo Blender, quaternione (w, x, y, z): la conversione verso Unity la fa ScatterRoot.
import bpy, json, os, sys

SOURCES = "ScatterSources"

def source_names():
    root = bpy.data.collections.get(SOURCES)
    if root is None:
        return None
    return {o.name for o in root.all_objects}

def main():
    names = source_names()
    if names is None:
        print("SCATTER_ERROR collection '%s' assente" % SOURCES)
        sys.exit(2)
    dg = bpy.context.evaluated_depsgraph_get()
    items = []
    for inst in dg.object_instances:
        if not inst.is_instance:
            continue
        src = inst.object.original.name
        if src not in names:
            continue
        host_obj = inst.parent.original if inst.parent else None
        # niente istanze da copie nascoste del terreno o da sorgenti annidati
        if host_obj is None or host_obj.hide_render or host_obj.name in names:
            continue
        host = host_obj.name
        loc, rot, scl = inst.matrix_world.decompose()
        items.append({"asset": src, "host": host,
                      "p": [round(v, 5) + 0.0 for v in (loc.x, loc.y, loc.z)],  # + 0.0: niente "-0.0" che riscrive il JSON
                      "q": [round(v, 5) + 0.0 for v in (rot.w, rot.x, rot.y, rot.z)],
                      "s": [round(v, 5) + 0.0 for v in (scl.x, scl.y, scl.z)]})
    for img in bpy.data.images:
        if img.name.startswith("Mask_"):
            path = bpy.path.abspath(img.filepath)
            if not os.path.isfile(path):
                print("SCATTER_WARN maschera mancante: %s %s" % (img.name, path))
    items.sort(key=lambda it: (it["host"], it["asset"], it["p"][0], it["p"][1], it["p"][2]))
    out = os.path.splitext(bpy.data.filepath)[0] + ".scatter.json"
    text = json.dumps({"source": os.path.basename(bpy.data.filepath), "instances": items})
    if os.path.exists(out):
        with open(out) as f:
            if f.read() == text:  # scatter invariato: non toccare il file, la scena resta pulita
                print("SCATTER_OK %d invariato" % len(items))
                return
    tmp = out + ".tmp"
    with open(tmp, "w") as f:
        f.write(text)
    os.replace(tmp, out)  # Unity non legge mai un file scritto a metà
    print("SCATTER_OK %d %s" % (len(items), out))

main()
