# Prepara lo scatter a strati in un .blend della mappa (docs/specs/ProceduralScatter.md, v1.1).
# Uso: blender --background <mappa.blend> --python scatter_setup.py [-- --rebuild-group]
# Idempotente: crea solo cio' che manca. --rebuild-group ricostruisce i nodi di GN_ScatterLayer
# tenendo l'interfaccia, quindi i valori dei modificatori restano.
import bpy, math, os, sys
import numpy as np

SURFACE = "Cliff"
SOURCES = "ScatterSources"
# Proiezione delle maschere (Unity x -215..185, z -345..55): U = (215 - x_b) / 400, V = (345 - y_b) / 400
PROJ_X, PROJ_Y, PROJ_SIZE = 215.0, 345.0, 400.0
MASK_RES = 4096
MASK_DIR = "//MapBlocking_Masks~/"  # cartella con ~: Unity la ignora, git la versiona
REBUILD = "--rebuild-group" in sys.argv or not bpy.app.background  # da interfaccia ricostruisce sempre

# ---------------------------------------------------------------- utilita'

def collection(name, parent):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in parent.children:
        parent.children.link(c)
    return c

def layer_collection(name, lc=None):
    lc = lc or bpy.context.view_layer.layer_collection
    if lc.name == name:
        return lc
    for ch in lc.children:
        r = layer_collection(name, ch)
        if r:
            return r
    return None

# ---------------------------------------------------------------- BiomeUV sul terreno

def setup_biome_uv(cl):
    # UV statica: serve solo per sceglierla in Texture Paint (Single Image). I valori esatti li
    # scrive GN_BiomeUV in coda allo stack: la Subdivision con i crease sposta le posizioni ma non le UV.
    me = cl.data
    if "BiomeUV" not in me.uv_layers:
        uv = me.uv_layers.new(name="BiomeUV")
        mw = cl.matrix_world
        for lp in me.loops:
            w = mw @ me.vertices[lp.vertex_index].co
            uv.data[lp.index].uv = ((PROJ_X - w.x) / PROJ_SIZE, (PROJ_Y - w.y) / PROJ_SIZE)
    # Texture Paint (Single Image) dipinge sulla UV attiva; il materiale resta su quella di render (UVMap).
    me.uv_layers.active = me.uv_layers["BiomeUV"]
    ng = bpy.data.node_groups.get("GN_BiomeUV")
    if ng is None:
        ng = bpy.data.node_groups.new("GN_BiomeUV", "GeometryNodeTree")
        ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
        N, L = ng.nodes, ng.links
        gi = N.new("NodeGroupInput"); gi.location = (-800, 0)
        so = N.new("GeometryNodeSelfObject"); so.location = (-800, -200)
        oi = N.new("GeometryNodeObjectInfo"); oi.location = (-600, -200); oi.transform_space = "ORIGINAL"
        pos = N.new("GeometryNodeInputPosition"); pos.location = (-600, -400)
        tp = N.new("FunctionNodeTransformPoint"); tp.location = (-400, -300)
        ma = N.new("ShaderNodeVectorMath"); ma.location = (-200, -300); ma.operation = "MULTIPLY_ADD"; ma.label = "Proiezione dall'alto"
        ma.inputs[1].default_value = (-1 / PROJ_SIZE, -1 / PROJ_SIZE, 0)
        ma.inputs[2].default_value = (PROJ_X / PROJ_SIZE, PROJ_Y / PROJ_SIZE, 0)
        st = N.new("GeometryNodeStoreNamedAttribute"); st.location = (0, 0); st.data_type = "FLOAT2"; st.domain = "CORNER"
        st.inputs["Name"].default_value = "BiomeUV"
        go = N.new("NodeGroupOutput"); go.location = (200, 0)
        L.new(so.outputs[0], oi.inputs["Object"]); L.new(pos.outputs[0], tp.inputs["Vector"])
        L.new(oi.outputs["Transform"], tp.inputs["Transform"]); L.new(tp.outputs[0], ma.inputs[0])
        L.new(gi.outputs[0], st.inputs["Geometry"]); L.new(ma.outputs[0], st.inputs["Value"]); L.new(st.outputs[0], go.inputs[0])
    if "BiomeUV" not in cl.modifiers:
        cl.modifiers.new("BiomeUV", "NODES").node_group = ng  # ultimo dello stack

# ---------------------------------------------------------------- salvataggio maschere al Ctrl+S

SAVE_SCRIPT = '''# Salva le maschere dello scatter (Mask_*) prima di ogni salvataggio del .blend.
# Blender lo esegue solo con Auto Run Python Scripts attivo per questo file; senza, salva a mano con Alt+S.
import bpy
from bpy.app.handlers import persistent

@persistent
def scatter_save_masks(*_):
    for img in bpy.data.images:
        if img.name.startswith("Mask_") and img.is_dirty and img.filepath:
            img.save()

bpy.app.handlers.save_pre[:] = [h for h in bpy.app.handlers.save_pre
                                if getattr(h, "__name__", "") != "scatter_save_masks"]
bpy.app.handlers.save_pre.append(scatter_save_masks)
'''

def setup_save_script():
    t = bpy.data.texts.get("scatter_masks_save.py") or bpy.data.texts.new("scatter_masks_save.py")
    t.clear(); t.write(SAVE_SCRIPT); t.use_module = True

# ---------------------------------------------------------------- interfaccia del gruppo

def iface(ng):
    """Crea (o ritrova per nome) pannelli e input. Ritrovarli tiene gli identificatori e i valori dei modificatori."""
    it = ng.interface
    def panel(name):
        for i in it.items_tree:
            if i.item_type == "PANEL" and i.name == name:
                return i
        return it.new_panel(name)
    def sock(name, stype, parent=None, default=None, mn=None, mx=None, subtype=None, desc=""):
        for i in it.items_tree:
            if i.item_type == "SOCKET" and i.in_out == "INPUT" and i.name == name:
                return i
        s = it.new_socket(name, in_out="INPUT", socket_type=stype, parent=parent, description=desc)
        if subtype and hasattr(s, "subtype"):
            s.subtype = subtype
        if mn is not None: s.min_value = mn
        if mx is not None: s.max_value = mx
        if default is not None: s.default_value = default
        return s
    if not any(i.item_type == "SOCKET" and i.in_out == "OUTPUT" for i in it.items_tree):
        it.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    p = panel("Asset")
    sock("Superficie", "NodeSocketObject", p, desc="Terreno su cui spargere (Cliff)")
    sock("Collection", "NodeSocketCollection", p, desc="Sorgenti dello strato, in ScatterSources")
    sock("Seed", "NodeSocketInt", p, 0)
    p = panel("Densità e maschera")
    sock("Densità", "NodeSocketFloat", p, 0.01, 0.0, 10.0, desc="Istanze per m² dove la maschera è bianca")
    sock("Maschera", "NodeSocketImage", p, desc="Mask_*: bianco aggiunge, nero cancella. Senza maschera lo strato è vuoto")
    sock("Distanza minima", "NodeSocketFloat", p, 0.0, 0.0, 50.0, "DISTANCE", "Spazio minimo fra le istanze dello strato")
    p = panel("Vicinanza")
    sock("Regola", "NodeSocketMenu", p, desc="Nessuna / Vicino al bersaglio / Lontano dal bersaglio")
    sock("Bersaglio", "NodeSocketCollection", p, desc="Collection di strati (es. Scatter_Rocks). Non deve contenere questo strato")
    sock("Distanza min", "NodeSocketFloat", p, 0.2, 0.0, 100.0, "DISTANCE")
    sock("Distanza max", "NodeSocketFloat", p, 2.0, 0.0, 100.0, "DISTANCE")
    sock("Sfuma", "NodeSocketBool", p, True, desc="Con Vicino: più fitto attaccato al bersaglio, a zero a Distanza max")
    p = panel("Variazione")
    sock("Scala", "NodeSocketFloat", p, 1.0, 0.0, 100.0)
    sock("Scala casuale", "NodeSocketFloat", p, 0.3, 0.0, 1.0, "FACTOR", "± sulla scala")
    sock("Rotazione casuale", "NodeSocketVector", p, (0.0, 0.0, math.tau), subtype="EULER")
    sock("Allinea alla normale", "NodeSocketBool", p, True)
    sock("Surface Offset", "NodeSocketFloat", p, 0.0, -10.0, 10.0, "DISTANCE", "Lungo l'asse su dell'istanza, proporzionale alla scala")
    p = panel("Filtri")
    sock("Pendenza max", "NodeSocketFloat", p, math.radians(40), 0.0, math.pi, "ANGLE")

# ---------------------------------------------------------------- nodi del gruppo

def build_nodes(ng):
    N, L = ng.nodes, ng.links
    N.clear()
    def frame(label, x, y):
        f = N.new("NodeFrame"); f.label = label; f.label_size = 20; f.location = (x, y); return f
    def node(t, x, y, fr=None, label=None, **props):
        n = N.new(t); n.location = (x, y)
        if fr is not None: n.parent = fr
        if label: n.label = label
        for k, v in props.items(): setattr(n, k, v)
        return n
    def gin(x, y, fr, *names):
        g = node("NodeGroupInput", x, y, fr)
        for o in g.outputs:  # mostra solo le uscite che servono: nodo compatto e leggibile
            o.hide = o.name not in names and o.name != ""
        return g
    def math_(op, x, y, fr, a=None, b=None):
        m = node("ShaderNodeMath", x, y, fr, operation=op)
        if a is not None: m.inputs[0].default_value = a
        if b is not None: m.inputs[1].default_value = b
        return m
    def cmp(op, x, y, fr):
        return node("FunctionNodeCompare", x, y, fr, data_type="FLOAT", operation=op)
    def band(x, y, fr, op="AND"):
        return node("FunctionNodeBooleanMath", x, y, fr, operation=op)

    # 1. punti sul terreno, sempre uguali: la maschera filtra dopo (stabilità, G5)
    f1 = frame("1 · Punti sul terreno", -2400, 600)
    g1 = gin(-2400, 600, f1, "Superficie", "Densità", "Distanza minima", "Seed")
    oi = node("GeometryNodeObjectInfo", -2150, 600, f1, transform_space="RELATIVE")
    dist = node("GeometryNodeDistributePointsOnFaces", -1900, 600, f1, distribute_method="POISSON")
    L.new(g1.outputs["Superficie"], oi.inputs["Object"]); L.new(oi.outputs["Geometry"], dist.inputs["Mesh"])
    L.new(g1.outputs["Densità"], dist.inputs["Density Max"]); L.new(g1.outputs["Distanza minima"], dist.inputs["Distance Min"])
    L.new(g1.outputs["Seed"], dist.inputs["Seed"])

    # 2. maschera dipinta: il punto resta con probabilità = valore del pixel
    f2 = frame("2 · Maschera dipinta", -2400, 100)
    g2 = gin(-2400, 100, f2, "Maschera", "Seed")
    pos = node("GeometryNodeInputPosition", -2400, -100, f2)
    proj = node("ShaderNodeVectorMath", -2150, -100, f2, label="Proiezione dall'alto (BiomeUV)", operation="MULTIPLY_ADD")
    proj.inputs[1].default_value = (-1 / PROJ_SIZE, -1 / PROJ_SIZE, 0)
    proj.inputs[2].default_value = (PROJ_X / PROJ_SIZE, PROJ_Y / PROJ_SIZE, 0)
    tex = node("GeometryNodeImageTexture", -1900, 100, f2, interpolation="Linear", extension="CLIP")
    rnd = node("FunctionNodeRandomValue", -1900, -150, f2, data_type="FLOAT")
    seedm = math_("ADD", -2400, -250, f2, b=101)  # seed diverso da quello dei punti
    keep_mask = cmp("LESS_THAN", -1650, 0, f2)
    L.new(pos.outputs[0], proj.inputs[0]); L.new(proj.outputs[0], tex.inputs["Vector"]); L.new(g2.outputs["Maschera"], tex.inputs["Image"])
    L.new(g2.outputs["Seed"], seedm.inputs[0]); L.new(seedm.outputs[0], rnd.inputs["Seed"])
    L.new(rnd.outputs[0], keep_mask.inputs[0]); L.new(tex.outputs["Color"], keep_mask.inputs[1])

    # 3. pendenza: Normal.z >= cos(Pendenza max), esclude anche sottosquadri e soffitti
    f3 = frame("3 · Pendenza", -2400, -500)
    g3 = gin(-2400, -500, f3, "Pendenza max")
    cosm = math_("COSINE", -2150, -500, f3)
    sep = node("ShaderNodeSeparateXYZ", -2150, -650, f3)
    keep_slope = cmp("GREATER_EQUAL", -1900, -550, f3)
    L.new(g3.outputs["Pendenza max"], cosm.inputs[0]); L.new(dist.outputs["Normal"], sep.inputs[0])
    L.new(sep.outputs["Z"], keep_slope.inputs[0]); L.new(cosm.outputs[0], keep_slope.inputs[1])

    # 4. vicinanza a un'altra collection di strati
    f4 = frame("4 · Vicinanza", -2400, -900)
    g4 = gin(-2400, -900, f4, "Regola", "Bersaglio", "Distanza min", "Distanza max", "Sfuma", "Seed")
    ci_t = node("GeometryNodeCollectionInfo", -2150, -900, f4, transform_space="RELATIVE")
    real = node("GeometryNodeRealizeInstances", -1900, -900, f4)
    prox = node("GeometryNodeProximity", -1650, -900, f4)
    try: prox.target_element = "FACES"
    except Exception: pass
    pos4 = node("GeometryNodeInputPosition", -1900, -1100, f4)
    dsz = node("GeometryNodeAttributeDomainSize", -1650, -1150, f4, component="MESH")
    has = node("FunctionNodeCompare", -1400, -1150, f4, data_type="INT", operation="GREATER_THAN")
    L.new(g4.outputs["Bersaglio"], ci_t.inputs["Collection"]); L.new(ci_t.outputs[0], real.inputs[0])
    L.new(real.outputs[0], prox.inputs[0]); L.new(pos4.outputs[0], prox.inputs["Sample Position"])
    L.new(real.outputs[0], dsz.inputs[0]); L.new(dsz.outputs["Face Count"], has.inputs[0])
    d = prox.outputs["Distance"]
    ge_min = cmp("GREATER_EQUAL", -1400, -850, f4); L.new(d, ge_min.inputs[0]); L.new(g4.outputs["Distanza min"], ge_min.inputs[1])
    le_max = cmp("LESS_EQUAL", -1400, -1000, f4); L.new(d, le_max.inputs[0]); L.new(g4.outputs["Distanza max"], le_max.inputs[1])
    # sfuma: tieni se random < 1 - (d - min) / (max - min)
    span = math_("SUBTRACT", -1150, -1250, f4); L.new(g4.outputs["Distanza max"], span.inputs[0]); L.new(g4.outputs["Distanza min"], span.inputs[1])
    off = math_("SUBTRACT", -1150, -1100, f4); L.new(d, off.inputs[0]); L.new(g4.outputs["Distanza min"], off.inputs[1])
    t = math_("DIVIDE", -900, -1150, f4); L.new(off.outputs[0], t.inputs[0]); L.new(span.outputs[0], t.inputs[1])
    inv = math_("SUBTRACT", -650, -1150, f4, a=1.0); L.new(t.outputs[0], inv.inputs[1])
    seedf = math_("ADD", -900, -1350, f4, b=202); L.new(g4.outputs["Seed"], seedf.inputs[0])
    rndf = node("FunctionNodeRandomValue", -650, -1350, f4, data_type="FLOAT"); L.new(seedf.outputs[0], rndf.inputs["Seed"])
    fall = cmp("LESS_THAN", -400, -1250, f4); L.new(rndf.outputs[0], fall.inputs[0]); L.new(inv.outputs[0], fall.inputs[1])
    nosf = band(-400, -1100, f4, "NOT"); L.new(g4.outputs["Sfuma"], nosf.inputs[0])
    sfok = band(-150, -1150, f4, "OR"); L.new(nosf.outputs[0], sfok.inputs[0]); L.new(fall.outputs[0], sfok.inputs[1])
    near1 = band(-1150, -900, f4); L.new(ge_min.outputs[0], near1.inputs[0]); L.new(le_max.outputs[0], near1.inputs[1])
    near2 = band(-900, -900, f4); L.new(near1.outputs[0], near2.inputs[0]); L.new(has.outputs[0], near2.inputs[1])
    # la distanza non ha segno: un punto sotto una roccia può stare oltre Distanza min. Raggio in su: se colpisce, è coperto
    ray = node("GeometryNodeRaycast", -1900, -500, f4, label="Coperto dal bersaglio")
    ray.inputs["Ray Direction"].default_value = (0, 0, 1); ray.inputs["Ray Length"].default_value = 10.0
    L.new(real.outputs[0], ray.inputs["Target Geometry"]); L.new(pos4.outputs[0], ray.inputs["Source Position"])
    free = band(-1400, -650, f4, "NOT"); L.new(ray.outputs["Is Hit"], free.inputs[0])
    near3 = band(100, -950, f4); L.new(near2.outputs[0], near3.inputs[0]); L.new(sfok.outputs[0], near3.inputs[1])
    near = band(100, -800, f4); L.new(near3.outputs[0], near.inputs[0]); L.new(free.outputs[0], near.inputs[1])
    nohas = band(-1150, -750, f4, "NOT"); L.new(has.outputs[0], nohas.inputs[0])
    far1 = band(-900, -750, f4, "OR"); L.new(ge_min.outputs[0], far1.inputs[0]); L.new(nohas.outputs[0], far1.inputs[1])
    far = band(-650, -700, f4); L.new(far1.outputs[0], far.inputs[0]); L.new(free.outputs[0], far.inputs[1])
    ms = node("GeometryNodeMenuSwitch", 350, -850, f4, label="Regola", data_type="BOOLEAN")
    ms.enum_items.clear()
    for name in ("Nessuna", "Vicino", "Lontano"):
        ms.enum_items.new(name)
    ms.inputs[1].default_value = True
    L.new(near.outputs[0], ms.inputs[2]); L.new(far.outputs[0], ms.inputs[3])
    L.new(g4.outputs["Regola"], ms.inputs[0])
    for n in N:  # la vicinanza è il frame più alto: sotto la pendenza, senza accavallarsi
        if n.parent == f4: n.location.y -= 450

    # 5. filtro: elimina i punti che non passano maschera, pendenza e vicinanza
    f5 = frame("5 · Filtro dei punti", 700, 300)
    a1 = band(700, 100, f5); L.new(keep_mask.outputs[0], a1.inputs[0]); L.new(keep_slope.outputs[0], a1.inputs[1])
    a2 = band(900, 100, f5); L.new(a1.outputs[0], a2.inputs[0]); L.new(ms.outputs[0], a2.inputs[1])
    nk = band(1100, 100, f5, "NOT"); L.new(a2.outputs[0], nk.inputs[0])
    dele = node("GeometryNodeDeleteGeometry", 1100, 400, f5, domain="POINT")
    L.new(dist.outputs["Points"], dele.inputs["Geometry"]); L.new(nk.outputs[0], dele.inputs["Selection"])

    # 6. istanze: asset casuale della collection, rotazione e scala casuali, offset locale
    f6 = frame("6 · Istanze e variazione", 1500, 600)
    g6 = gin(1500, 300, f6, "Collection", "Seed", "Scala", "Scala casuale", "Rotazione casuale", "Allinea alla normale", "Surface Offset")
    ci = node("GeometryNodeCollectionInfo", 1800, 650, f6, transform_space="RELATIVE")
    ci.inputs["Separate Children"].default_value = True; ci.inputs["Reset Children"].default_value = True
    ridx = node("FunctionNodeRandomValue", 1800, 450, f6, data_type="INT"); ridx.inputs["Min"].default_value = 0; ridx.inputs["Max"].default_value = 9999
    s1 = math_("ADD", 1800, 250, f6, b=303); s2 = math_("ADD", 1800, 100, f6, b=404); s3 = math_("ADD", 1800, -50, f6, b=505)
    for s in (s1, s2, s3): L.new(g6.outputs["Seed"], s.inputs[0])
    L.new(s1.outputs[0], ridx.inputs["Seed"])
    sw = node("GeometryNodeSwitch", 2050, 250, f6, input_type="ROTATION", label="Allinea alla normale")
    L.new(g6.outputs["Allinea alla normale"], sw.inputs["Switch"]); L.new(dist.outputs["Rotation"], sw.inputs["True"])
    rrot = node("FunctionNodeRandomValue", 2050, 50, f6, data_type="FLOAT_VECTOR"); L.new(g6.outputs["Rotazione casuale"], rrot.inputs["Max"]); L.new(s2.outputs[0], rrot.inputs["Seed"])
    e2r = node("FunctionNodeEulerToRotation", 2300, 50, f6)
    rot = node("FunctionNodeRotateRotation", 2550, 200, f6, rotation_space="LOCAL")
    L.new(rrot.outputs[0], e2r.inputs[0]); L.new(sw.outputs[0], rot.inputs["Rotation"]); L.new(e2r.outputs[0], rot.inputs["Rotate By"])
    neg = math_("MULTIPLY", 2050, -300, f6, b=-1.0); L.new(g6.outputs["Scala casuale"], neg.inputs[0])
    rsc = node("FunctionNodeRandomValue", 2300, -300, f6, data_type="FLOAT")
    L.new(neg.outputs[0], rsc.inputs["Min"]); L.new(g6.outputs["Scala casuale"], rsc.inputs["Max"]); L.new(s3.outputs[0], rsc.inputs["Seed"])
    one = math_("ADD", 2550, -300, f6, b=1.0); L.new(rsc.outputs[0], one.inputs[0])
    scl = math_("MULTIPLY", 2800, -300, f6); L.new(one.outputs[0], scl.inputs[0]); L.new(g6.outputs["Scala"], scl.inputs[1])
    iop = node("GeometryNodeInstanceOnPoints", 2800, 500, f6)
    iop.inputs["Pick Instance"].default_value = True
    L.new(dele.outputs[0], iop.inputs["Points"]); L.new(g6.outputs["Collection"], ci.inputs["Collection"]); L.new(ci.outputs[0], iop.inputs["Instance"])
    L.new(ridx.outputs[0], iop.inputs["Instance Index"]); L.new(rot.outputs[0], iop.inputs["Rotation"]); L.new(scl.outputs[0], iop.inputs["Scale"])
    comb = node("ShaderNodeCombineXYZ", 2800, 100, f6); L.new(g6.outputs["Surface Offset"], comb.inputs["Z"])
    tr = node("GeometryNodeTranslateInstances", 3050, 500, f6, label="Surface Offset (locale)")
    tr.inputs["Local Space"].default_value = True
    L.new(iop.outputs[0], tr.inputs["Instances"]); L.new(comb.outputs[0], tr.inputs["Translation"])
    go = node("NodeGroupOutput", 3350, 500)
    L.new(tr.outputs[0], go.inputs[0])

def setup_group():
    ng = bpy.data.node_groups.get("GN_ScatterLayer")
    fresh = ng is None
    if fresh:
        ng = bpy.data.node_groups.new("GN_ScatterLayer", "GeometryNodeTree")
        ng.description = "Uno strato dello scatter: punti sul terreno filtrati da maschera, pendenza e vicinanza, poi istanze"
    iface(ng)
    if fresh or REBUILD:
        build_nodes(ng)
    return ng

# ---------------------------------------------------------------- maschere

def mask(name, fill=None):
    img = bpy.data.images.get(name)
    path = MASK_DIR + name + ".png"
    if img is None and os.path.isfile(bpy.path.abspath(path)):
        img = bpy.data.images.load(path)  # maschera già dipinta su disco: mai sovrascriverla
        img.colorspace_settings.name = "Non-Color"; img.use_fake_user = True
    if img is None:
        img = bpy.data.images.new(name, MASK_RES, MASK_RES, alpha=False)
        img.colorspace_settings.name = "Non-Color"
        px = np.zeros((MASK_RES, MASK_RES, 4), dtype=np.float32); px[..., 3] = 1.0
        if fill is not None:
            v = fill()  # (MASK_RES, MASK_RES) float, riga = V
            px[..., 0] = px[..., 1] = px[..., 2] = v
        img.pixels.foreach_set(px.ravel())
        os.makedirs(bpy.path.abspath(MASK_DIR), exist_ok=True)
        img.filepath_raw = path; img.file_format = "PNG"
        img.save()
        img.use_fake_user = True
    return img

def z2_fill(cl):
    """Contenuto iniziale di prova: la Z2 di oggi (riquadro Blender x 55..150, y 145..240), solo fondo sotto quota 0."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(cl.evaluated_get(dg), dg)
    mw = cl.matrix_world; inv = mw.inverted()
    v = np.zeros((MASK_RES, MASK_RES), dtype=np.float32)
    step = 0.5
    px_per_m = MASK_RES / PROJ_SIZE
    for x in np.arange(55, 150, step):
        for y in np.arange(145, 240, step):
            o = inv @ __import__("mathutils").Vector((x, y, 200)); dvec = (inv.to_3x3() @ __import__("mathutils").Vector((0, 0, -1))).normalized()
            hit = bvh.ray_cast(o, dvec)
            if hit[0] is None or (mw @ hit[0]).z >= 0:
                continue
            u = int((PROJ_X - x) * px_per_m); r = int((PROJ_Y - y) * px_per_m)
            v[max(r - 3, 0):r + 3, max(u - 3, 0):u + 3] = 1.0
    return v

# ---------------------------------------------------------------- strati

def set_input(mod, name, value):
    for it in mod.node_group.interface.items_tree:
        if it.item_type == "SOCKET" and it.in_out == "INPUT" and it.name == name:
            getattr(mod.properties.inputs, it.identifier).value = value  # Blender 5.3: niente più mod[id]
            return
    raise KeyError(name)

def layer(name, coll, ng, surface, inputs):
    ob = bpy.data.objects.get(name)
    if ob is not None:
        return ob
    pc = bpy.data.pointclouds.new(name)  # Point Cloud: non arriva nell'FBX di Unity (G10)
    ob = bpy.data.objects.new(name, pc)
    coll.objects.link(ob)
    ob.lock_location = ob.lock_rotation = ob.lock_scale = (True, True, True)
    mod = ob.modifiers.new("Scatter", "NODES"); mod.node_group = ng
    set_input(mod, "Superficie", surface)
    for k, v in inputs.items():
        set_input(mod, k, v)
    return ob

def sources_split(src_root):
    """Una sotto-collection di sorgenti per strato."""
    groups = {"ScatterSources_RocksSmall": "SmallRock", "ScatterSources_RocksMedium": "MediumRock", "ScatterSources_Pens": "SeaPen"}
    out = {}
    for cname, key in groups.items():
        c = collection(cname, src_root)
        for o in list(src_root.all_objects):
            if key in o.name and o.name not in c.objects:
                for uc in list(o.users_collection):
                    uc.objects.unlink(o)
                c.objects.link(o)
        out[cname] = c
    old = bpy.data.collections.get("ScatterSources_Rocks")
    if old is not None and not old.all_objects:
        bpy.data.collections.remove(old)
    # flora dagli FBX del progetto: LOD2 basta per vedere lo scatter, Unity usa il prefab con i suoi LOD
    org = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Materials", "Environment", "Organisms"))
    for cname, folder, base in (("ScatterSources_Bamboo", "BambooFan", "SM_BambooFan"), ("ScatterSources_Sponges", "GlassSponge", "SM_GlassSponge")):
        c = collection(cname, src_root)
        for v in "ABC":
            name = "%s_%s" % (base, v)
            if name not in bpy.data.objects:
                import_lod(os.path.join(org, folder, name + ".fbx"), name, c)
        out[cname] = c
    return out

def import_lod(path, name, coll):
    """Importa l'FBX, tiene solo il LOD2 con la rotazione applicata (Z su, origine a terra come le penne)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = set(bpy.data.objects) - before
    keep = next(o for o in new if o.name.endswith("_LOD2"))
    for o in new - {keep}:
        bpy.data.objects.remove(o)
    keep.data.transform(keep.matrix_basis); keep.matrix_basis.identity()
    keep.name = keep.data.name = name
    for uc in list(keep.users_collection):
        uc.objects.unlink(keep)
    coll.objects.link(keep)

def main():
    scn = bpy.context.scene
    cl = bpy.data.objects[SURFACE]
    setup_biome_uv(cl)
    setup_save_script()
    ng = setup_group()
    src_root = bpy.data.collections[SOURCES]
    src = sources_split(src_root)
    lc = layer_collection(SOURCES)
    if lc: lc.exclude = True
    root = collection("Scatter", scn.collection)
    rocks = collection("Scatter_Rocks", root)
    rs, rm = collection("Scatter_Rocks_Small", rocks), collection("Scatter_Rocks_Medium", rocks)
    flora = collection("Scatter_Flora", root)
    bpy.context.view_layer.update()
    m_rocks = mask("Mask_Rocks", lambda: z2_fill(cl))
    m_pens = mask("Mask_Pens", lambda: z2_fill(cl))
    layer("SCT_Rocks_Small", rs, ng, cl, {"Collection": src["ScatterSources_RocksSmall"], "Maschera": m_rocks, "Densità": 0.004, "Seed": 1})
    layer("SCT_Rocks_Medium", rm, ng, cl, {"Collection": src["ScatterSources_RocksMedium"], "Maschera": m_rocks, "Densità": 0.004, "Seed": 2, "Surface Offset": -0.15})
    layer("SCT_Pens", flora, ng, cl, {"Collection": src["ScatterSources_Pens"], "Maschera": m_pens, "Densità": 0.003, "Seed": 3,
                                       "Regola": "Lontano", "Bersaglio": rocks, "Distanza min": 0.3})
    # bambù e spugne attorno alle rocce, come il dressing a mano del crinale
    m_bamboo = mask("Mask_Bamboo", lambda: z2_fill(cl))
    m_sponges = mask("Mask_Sponges", lambda: z2_fill(cl))
    layer("SCT_Bamboo", flora, ng, cl, {"Collection": src["ScatterSources_Bamboo"], "Maschera": m_bamboo, "Densità": 0.15, "Seed": 4,
                                         "Regola": "Vicino", "Bersaglio": rocks, "Distanza min": 0.1, "Distanza max": 2.0,
                                         "Distanza minima": 0.6, "Allinea alla normale": False})
    layer("SCT_Sponges", flora, ng, cl, {"Collection": src["ScatterSources_Sponges"], "Maschera": m_sponges, "Densità": 0.25, "Seed": 5,
                                          "Regola": "Vicino", "Bersaglio": rocks, "Distanza min": 0.05, "Distanza max": 1.5,
                                          "Distanza minima": 0.4})
    # vecchio strato della prova in Z2: spento, non cancellato (si toglie a mano). L'export salta gli host hide_render.
    o = bpy.data.objects.get("Scatter_Z2")
    if o:
        o.hide_render = o.hide_viewport = True
        for m in o.modifiers: m.show_viewport = m.show_render = False
    if bpy.app.background:  # da GUI non salva: Manu guarda e salva con Ctrl+S
        bpy.ops.wm.save_mainfile()
    print("SETUP_OK")

main()
