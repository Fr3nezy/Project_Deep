bl_info = {
    "name": "Mirror Collection",
    "author": "Manu",
    "version": (1, 0, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > Mirror, Outliner > Collection context menu",
    "description": "Duplicate the active collection mirrored across a world axis, without negative scale",
    "category": "Object",
}

import bpy
from mathutils import Matrix, Vector

GEO_TYPES = {'MESH', 'CURVE', 'SURFACE', 'FONT', 'META'}
AXIS_INDEX = {'X': 0, 'Y': 1, 'Z': 2}


def mirror_name(name, fallback="_Mirror"):
    for a, b in ((".L", ".R"), (".R", ".L"), ("_L", "_R"), ("_R", "_L")):
        if name.endswith(a):
            return name[:-2] + b
    return name + fallback


def is_hidden(o):
    try:
        return o.hide_viewport or o.hide_get()
    except RuntimeError:  # not in the view layer
        return True


def reflection(axis, pivot):
    s = Matrix.Identity(4)
    s[axis][axis] = -1.0
    return Matrix.Translation(pivot) @ s @ Matrix.Translation(-pivot)


def local_flip(world, axis):
    """Flip along the local axis closest to the mirror normal: the result keeps a positive determinant."""
    m = world.to_3x3()
    k = max(range(3), key=lambda i: abs(m.col[i].normalized()[axis]) if m.col[i].length > 1e-9 else -1.0)
    f = Matrix.Identity(4)
    f[k][k] = -1.0
    return f


def mirror_collection(context, src, axis, pivot, same_collection, skip_hidden):
    """Mirror `src` across the world plane through `pivot`. Modifiers are applied on the copies.
    Returns (made, skipped, orphaned): clones by source object, unsupported/empty names, children whose parent is outside."""
    dg = context.evaluated_depsgraph_get()
    scene = context.scene
    refl = reflection(axis, pivot)

    cmap = {}
    if same_collection:
        for c in (src, *src.children_recursive):
            cmap[c] = c
    else:
        def clone_tree(c, parent):
            n = bpy.data.collections.new(mirror_name(c.name))
            parent.children.link(n)
            cmap[c] = n
            for ch in c.children:
                clone_tree(ch, n)

        parent = next((c for c in (scene.collection, *bpy.data.collections) if src.name in c.children),
                      scene.collection)
        clone_tree(src, parent)

    made, mats, skipped, orphaned = {}, {}, [], []
    for o in src.all_objects:
        if o.type not in GEO_TYPES | {'EMPTY'} or (skip_hidden and is_hidden(o)):
            skipped.append(o.name)
            continue
        world = o.matrix_world.copy()
        flip = local_flip(world, axis)
        if o.type == 'EMPTY':
            c = o.copy()
            c.name = mirror_name(o.name)
        else:
            mesh = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
            if mesh is None or not mesh.vertices:
                if mesh is not None:
                    bpy.data.meshes.remove(mesh)
                skipped.append(o.name)
                continue
            mesh.transform(flip, shape_keys=True)
            mesh.flip_normals()
            c = bpy.data.objects.new(mirror_name(o.name), mesh)
            mesh.name = c.name
        mats[c] = refl @ world @ flip
        for col in o.users_collection:
            if col in cmap:
                cmap[col].objects.link(c)
        made[o] = c

    for c in made.values():
        c.parent = None
        c.matrix_world = mats[c]
    for o, c in made.items():
        if o.parent is None:
            continue
        p = made.get(o.parent)
        if p is not None and o.parent_type == 'OBJECT':
            c.parent = p
            c.matrix_parent_inverse = p.matrix_world.inverted()
        else:
            orphaned.append(o.name)

    for o in context.selected_objects:
        o.select_set(False)
    for c in made.values():
        c.select_set(True)
    return made, skipped, orphaned


class OBJECT_OT_mirror_collection(bpy.types.Operator):
    bl_idname = "object.mirror_collection"
    bl_label = "Mirror Collection"
    bl_description = ("Duplicate the active collection mirrored across a world axis. "
                      "Modifiers are applied on the copies; no negative scale, normals stay outward")
    bl_options = {'REGISTER', 'UNDO'}

    axis: bpy.props.EnumProperty(
        name="Axis", items=[('X', "X", ""), ('Y', "Y", ""), ('Z', "Z", "")], default='X')
    pivot: bpy.props.EnumProperty(
        name="Pivot",
        items=[('WORLD', "World origin", ""), ('CURSOR', "3D cursor", ""), ('OBJECT', "Active object", "")],
        default='WORLD')
    same_collection: bpy.props.BoolProperty(
        name="Keep in same collection",
        description="Add the copies to the source collection (lets you mirror again on another axis for a 4-way layout)",
        default=False)
    skip_hidden: bpy.props.BoolProperty(
        name="Skip hidden objects", description="Helpers and hidden meshes are not copied", default=True)

    @classmethod
    def poll(cls, context):
        return (context.mode == 'OBJECT'
                and context.view_layer.active_layer_collection.collection != context.scene.collection)

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        if self.pivot == 'CURSOR':
            pivot = context.scene.cursor.location.copy()
        elif self.pivot == 'OBJECT':
            if context.active_object is None:
                self.report({'ERROR'}, "No active object for the pivot")
                return {'CANCELLED'}
            pivot = context.active_object.matrix_world.translation.copy()
        else:
            pivot = Vector()
        src = context.view_layer.active_layer_collection.collection
        made, skipped, orphaned = mirror_collection(
            context, src, AXIS_INDEX[self.axis], pivot, self.same_collection, self.skip_hidden)
        self.report({'INFO'}, f"{src.name}: {len(made)} mirrored, {len(skipped)} skipped")
        if orphaned:
            self.report({'WARNING'}, f"Parent outside the collection, copy unparented: {', '.join(orphaned)}")
        return {'FINISHED'}


class VIEW3D_PT_mirror_collection(bpy.types.Panel):
    bl_label = "Mirror Collection"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Mirror"

    def draw(self, context):
        col = context.view_layer.active_layer_collection.collection
        self.layout.label(text=col.name, icon='OUTLINER_COLLECTION')
        self.layout.operator(OBJECT_OT_mirror_collection.bl_idname)


def outliner_menu(self, context):
    self.layout.separator()
    self.layout.operator(OBJECT_OT_mirror_collection.bl_idname)


classes = (OBJECT_OT_mirror_collection, VIEW3D_PT_mirror_collection)


def register():
    for c in classes:
        bpy.utils.register_class(c)
    bpy.types.OUTLINER_MT_collection.append(outliner_menu)


def unregister():
    bpy.types.OUTLINER_MT_collection.remove(outliner_menu)
    for c in reversed(classes):
        bpy.utils.unregister_class(c)


if __name__ == "__main__":
    register()
