# Stampi di terreno non distruttivi: le mesh nella collezione TerrainStamps scavano il terreno fino alla loro faccia superiore.
# Uso: blender --background <mappa.blend> --python terrain_stamp.py, oppure dal Text Editor con Run Script.
# Idempotente: ricostruisce i nodi di GN_TerrainStamp tenendo l'interfaccia, quindi i valori del modificatore restano.
# Solo scavo: dove il terreno sta gia' sotto lo stampo non cambia niente. Fuori dagli stampi e dalla sfumatura la mesh
# resta identica (la suddivisione lineare non sposta i punti) e tiene le normali custom originali.
import bpy

SURFACE = "Cliff"
STAMPS = "TerrainStamps"
GROUP = "GN_TerrainStamp"
RAY_TOP = 1000.0  # quota locale da cui parte il raggio verso il basso

SOCKETS = ("Geometry", "Stampi", "Suddivisioni", "Sfumatura", "Levigatura")

def iface(ng):
    # Toglie i socket di versioni precedenti (es. Morbidezza) e aggiunge quelli mancanti; ritorna i nomi aggiunti.
    for it in list(ng.interface.items_tree):
        if it.item_type == "SOCKET" and it.name not in SOCKETS:
            ng.interface.remove(it)
    have = {(it.name, it.in_out) for it in ng.interface.items_tree if it.item_type == "SOCKET"}
    added = {n for n, io in [("Suddivisioni", "INPUT"), ("Sfumatura", "INPUT"), ("Levigatura", "INPUT")] if (n, io) not in have}
    s = ng.interface.new_socket
    if ("Geometry", "INPUT") not in have:
        s("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    if ("Stampi", "INPUT") not in have:
        c = s("Stampi", in_out="INPUT", socket_type="NodeSocketCollection")
        c.description = "Collezione di mesh: il terreno sotto ciascuna scende fino alla sua faccia superiore"
    if ("Suddivisioni", "INPUT") not in have:
        lv = s("Suddivisioni", in_out="INPUT", socket_type="NodeSocketInt")
        lv.default_value, lv.min_value, lv.max_value = 2, 0, 5  # 2: ~29k triangoli su tutta la mappa, 3: ~116k
        lv.description = "Suddivisione lineare prima dello scavo: ogni livello divide per due la distanza fra i vertici"
    if ("Sfumatura", "INPUT") not in have:
        fo = s("Sfumatura", in_out="INPUT", socket_type="NodeSocketFloat")
        fo.default_value, fo.min_value, fo.max_value = 4.0, 0.0, 30.0
        fo.description = "Metri orizzontali fuori dall'impronta in cui lo scavo sfuma fino a zero"
    if ("Levigatura", "INPUT") not in have:
        sm = s("Levigatura", in_out="INPUT", socket_type="NodeSocketInt")
        sm.default_value, sm.min_value, sm.max_value = 15, 0, 50
        sm.description = "Passate di levigatura della posizione, solo dove il terreno e' stato scavato"
    if ("Geometry", "OUTPUT") not in have:
        s("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    return added

def build_nodes(ng):
    N, L = ng.nodes, ng.links
    N.clear()
    def node(t, x, y, label=None, **props):
        n = N.new(t); n.location = (x, y)
        if label: n.label = label
        for k, v in props.items(): setattr(n, k, v)
        return n
    def vmath(op, x, y, label=None):
        return node("ShaderNodeVectorMath", x, y, label, operation=op)
    def math(op, x, y, label=None):
        return node("ShaderNodeMath", x, y, label, operation=op)
    gi = node("NodeGroupInput", -2000, 0)
    go = node("NodeGroupOutput", 2200, 0)
    # Normali originali (il Cliff ha normali custom): catturate prima della suddivisione, rimesse dopo.
    cap_n = node("GeometryNodeCaptureAttribute", -1800, 0, "Normali originali", domain="CORNER")
    cap_n.capture_items.new("VECTOR", "Normale")
    L.new(node("GeometryNodeInputNormal", -2000, -150).outputs["Normal"], cap_n.inputs["Normale"])
    L.new(gi.outputs["Geometry"], cap_n.inputs["Geometry"])
    sub = node("GeometryNodeSubdivideMesh", -1600, 0)
    L.new(cap_n.outputs["Geometry"], sub.inputs["Mesh"]); L.new(gi.outputs["Suddivisioni"], sub.inputs["Level"])
    # Stampi: solo le facce rivolte in su, poi una copia schiacciata a z = 0 che ricorda la quota.
    ci = node("GeometryNodeCollectionInfo", -1800, -500, transform_space="RELATIVE")
    ri = node("GeometryNodeRealizeInstances", -1600, -500)
    L.new(gi.outputs["Stampi"], ci.inputs["Collection"]); L.new(ci.outputs[0], ri.inputs[0])
    up = node("ShaderNodeSeparateXYZ", -1600, -700)
    L.new(node("GeometryNodeInputNormal", -1800, -700).outputs["Normal"], up.inputs[0])
    upk = math("GREATER_THAN", -1400, -700); upk.inputs[1].default_value = 0.5
    L.new(up.outputs["Z"], upk.inputs[0])
    tops = node("GeometryNodeSeparateGeometry", -1400, -500, "Facce superiori", domain="FACE")
    L.new(ri.outputs[0], tops.inputs["Geometry"]); L.new(upk.outputs[0], tops.inputs["Selection"])
    pz_s = node("ShaderNodeSeparateXYZ", -1400, -900)
    L.new(node("GeometryNodeInputPosition", -1600, -900).outputs[0], pz_s.inputs[0])
    cap_h = node("GeometryNodeCaptureAttribute", -1200, -500, "Quota stampo", domain="POINT")
    cap_h.capture_items.new("FLOAT", "Quota")
    L.new(tops.outputs["Selection"], cap_h.inputs["Geometry"]); L.new(pz_s.outputs["Z"], cap_h.inputs["Quota"])
    flat_pos = vmath("MULTIPLY", -1200, -800); flat_pos.inputs[1].default_value = (1, 1, 0)
    L.new(node("GeometryNodeInputPosition", -1400, -1050).outputs[0], flat_pos.inputs[0])
    flat = node("GeometryNodeSetPosition", -1000, -500, "Impronta a z = 0")
    L.new(cap_h.outputs["Geometry"], flat.inputs["Geometry"]); L.new(flat_pos.outputs[0], flat.inputs["Position"])
    # Terreno: quota dello stampo sopra il vertice (raggio) o del bordo più vicino (fuori dall'impronta).
    pos = node("GeometryNodeInputPosition", -1000, -1200)
    q = vmath("MULTIPLY", -800, -1200, "Vertice a z = 0"); q.inputs[1].default_value = (1, 1, 0)
    L.new(pos.outputs[0], q.inputs[0])
    src = vmath("MULTIPLY_ADD", -800, -1400, "Partenza raggio")
    src.inputs[1].default_value = (1, 1, 0); src.inputs[2].default_value = (0, 0, RAY_TOP)
    L.new(pos.outputs[0], src.inputs[0])
    rc = node("GeometryNodeRaycast", -600, -500, data_type="FLOAT")
    rc.inputs["Ray Direction"].default_value = (0, 0, -1); rc.inputs["Ray Length"].default_value = 2 * RAY_TOP
    L.new(tops.outputs["Selection"], rc.inputs["Target Geometry"]); L.new(src.outputs[0], rc.inputs["Source Position"])
    hz = node("ShaderNodeSeparateXYZ", -400, -500); L.new(rc.outputs["Hit Position"], hz.inputs[0])
    prox = node("GeometryNodeProximity", -600, -800, target_element="FACES")
    L.new(flat.outputs[0], prox.inputs["Geometry"]); L.new(q.outputs[0], prox.inputs["Sample Position"])
    near = node("GeometryNodeSampleNearestSurface", -600, -1000, data_type="FLOAT")
    L.new(flat.outputs[0], near.inputs["Mesh"]); L.new(cap_h.outputs["Quota"], near.inputs["Value"])
    L.new(q.outputs[0], near.inputs["Sample Position"])
    h = node("GeometryNodeSwitch", -200, -600, "Quota bersaglio", input_type="FLOAT")
    L.new(rc.outputs["Is Hit"], h.inputs["Switch"]); L.new(near.outputs["Value"], h.inputs["False"])
    L.new(hz.outputs["Z"], h.inputs["True"])
    w = node("ShaderNodeMapRange", -200, -900, "Peso sfumatura", interpolation_type="SMOOTHSTEP", clamp=True)
    w.inputs["From Min"].default_value = 0.0; w.inputs["To Min"].default_value = 1.0; w.inputs["To Max"].default_value = 0.0
    L.new(prox.outputs["Distance"], w.inputs["Value"]); L.new(gi.outputs["Sfumatura"], w.inputs["From Max"])
    pz = node("ShaderNodeSeparateXYZ", -400, -1300); L.new(pos.outputs[0], pz.inputs[0])
    dz = math("SUBTRACT", 0, -700); L.new(h.outputs[0], dz.inputs[0]); L.new(pz.outputs["Z"], dz.inputs[1])
    cut = math("MINIMUM", 200, -700, "Solo scavo"); cut.inputs[1].default_value = 0.0
    L.new(dz.outputs[0], cut.inputs[0])
    d = math("MULTIPLY", 400, -700); L.new(cut.outputs[0], d.inputs[0]); L.new(w.outputs["Result"], d.inputs[1])
    # Lo scostamento va catturato: dopo Set Position il raggio ricalcolato darebbe zero.
    cap_d = node("GeometryNodeCaptureAttribute", 600, 0, "Scavo", domain="POINT")
    cap_d.capture_items.new("FLOAT", "Scavo")
    L.new(sub.outputs[0], cap_d.inputs["Geometry"]); L.new(d.outputs[0], cap_d.inputs["Scavo"])
    off = node("ShaderNodeCombineXYZ", 800, -300); L.new(cap_d.outputs["Scavo"], off.inputs["Z"])
    sp = node("GeometryNodeSetPosition", 1000, 0)
    L.new(cap_d.outputs["Geometry"], sp.inputs["Geometry"]); L.new(off.outputs[0], sp.inputs["Offset"])
    # Normali: originali dove il terreno non si muove, ricalcolate dove scava (pieno da 0.3 m di scavo).
    ab = math("ABSOLUTE", 800, -500); L.new(cap_d.outputs["Scavo"], ab.inputs[0])
    f = math("DIVIDE", 1000, -500); f.use_clamp = True; f.inputs[1].default_value = 0.3
    L.new(ab.outputs[0], f.inputs[0])
    diff = vmath("SUBTRACT", 1000, -700)
    L.new(node("GeometryNodeInputNormal", 800, -800).outputs["Normal"], diff.inputs[0])
    L.new(cap_n.outputs["Normale"], diff.inputs[1])
    mix = vmath("MULTIPLY_ADD", 1200, -600)
    L.new(diff.outputs[0], mix.inputs[0]); L.new(f.outputs[0], mix.inputs[1]); L.new(cap_n.outputs["Normale"], mix.inputs[2])
    nn = vmath("NORMALIZE", 1400, -600); L.new(mix.outputs[0], nn.inputs[0])
    # Levigatura solo dove scava: fuori il terreno resta identico e il bordo aperto della mappa non si restringe.
    pos2 = node("GeometryNodeInputPosition", 1000, 300)
    blur = node("GeometryNodeBlurAttribute", 1200, 300, "Levigatura", data_type="FLOAT_VECTOR")
    L.new(pos2.outputs[0], blur.inputs["Value"]); L.new(gi.outputs["Levigatura"], blur.inputs["Iterations"])
    dpos = vmath("SUBTRACT", 1400, 300); L.new(blur.outputs[0], dpos.inputs[0]); L.new(pos2.outputs[0], dpos.inputs[1])
    lev = vmath("MULTIPLY_ADD", 1600, 300)
    L.new(dpos.outputs[0], lev.inputs[0]); L.new(f.outputs[0], lev.inputs[1]); L.new(pos2.outputs[0], lev.inputs[2])
    sp2 = node("GeometryNodeSetPosition", 1800, 0)
    L.new(sp.outputs[0], sp2.inputs["Geometry"]); L.new(lev.outputs[0], sp2.inputs["Position"])
    smn = node("GeometryNodeSetMeshNormal", 2000, 0, mode="FREE", domain="CORNER")
    L.new(sp2.outputs[0], smn.inputs["Mesh"]); L.new(nn.outputs[0], smn.inputs["Custom Normal"])
    L.new(smn.outputs[0], go.inputs["Geometry"])

SAVE_SCRIPT = '''# Esclude TerrainStamps dal view layer solo durante il salvataggio: lo scavo resta, i prismi non arrivano in Unity.
# Blender lo esegue solo con Auto Run Python Scripts attivo per questo file; senza, togli a mano la spunta prima di Ctrl+S.
import bpy
from bpy.app.handlers import persistent

def _stamps_layers():
    def walk(lc):
        if lc.collection.name == "TerrainStamps":
            yield lc
        for ch in lc.children:
            yield from walk(ch)
    for scene in bpy.data.scenes:
        for vl in scene.view_layers:
            yield from walk(vl.layer_collection)

_restore = []

@persistent
def terrain_stamps_pre(*_):
    _restore.clear()
    for lc in _stamps_layers():
        if not lc.exclude:
            lc.exclude = True
            _restore.append(lc)

@persistent
def terrain_stamps_post(*_):
    for lc in _restore:
        lc.exclude = False
    _restore.clear()

for hs, fn in ((bpy.app.handlers.save_pre, terrain_stamps_pre), (bpy.app.handlers.save_post, terrain_stamps_post)):
    hs[:] = [h for h in hs if getattr(h, "__name__", "") != fn.__name__]
    hs.append(fn)
'''

def setup_save_script():
    t = bpy.data.texts.get("terrain_stamps_save.py") or bpy.data.texts.new("terrain_stamps_save.py")
    t.clear(); t.write(SAVE_SCRIPT); t.use_module = True
    exec(compile(SAVE_SCRIPT, t.name, "exec"), {})  # registra subito, senza riaprire il file

def main():
    cl = bpy.data.objects[SURFACE]
    stamps = bpy.data.collections.get(STAMPS) or bpy.data.collections.new(STAMPS)
    if stamps.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(stamps)
    ng = bpy.data.node_groups.get(GROUP) or bpy.data.node_groups.new(GROUP, "GeometryNodeTree")
    added = iface(ng)
    build_nodes(ng)
    setup_save_script()
    mod = cl.modifiers.get("TerrainStamp")
    if mod is None:
        mod = cl.modifiers.new("TerrainStamp", "NODES")
        mod.node_group = ng
        # Dopo Triangulate e prima di BiomeUV: Bevel non lavora sulla mesh densa e BiomeUV vede la forma finale.
        bi = cl.modifiers.find("BiomeUV")
        if bi >= 0:
            cl.modifiers.move(len(cl.modifiers) - 1, bi)
    for it in ng.interface.items_tree:
        if it.item_type != "SOCKET" or it.in_out != "INPUT":
            continue
        inp = getattr(mod.properties.inputs, it.identifier, None)  # Blender 5.3: niente più mod[id]
        if it.name == "Stampi":
            inp.value = stamps
        elif it.name in added:
            inp.value = it.default_value  # un socket aggiunto a un modificatore esistente parte da 0
    cl.update_tag()

main()
