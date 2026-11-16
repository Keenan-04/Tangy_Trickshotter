"""
Builds the game's sniper rifles and exports one FBX per rifle.

Run from the project root:
    blender -b --factory-startup -P ArtSource/build_rifles.py

Each rifle is one mesh with four material slots (Finish, Metal, Polymer, Lens) and three
child empties the game uses to line the model up, whatever the export axes turn out to be:
    Muzzle  tip of the barrel
    Top     straight above the grip
    Eye     where the shooter's eye sits behind the scope
The grip is at the origin, the barrel points along +Y and +Z is up.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

FIN, MET, POL, LEN = 0, 1, 2, 3
MATERIALS = [
    ("Finish", (0.36, 0.39, 0.43, 1.0), 0.2, 0.5),
    ("Metal", (0.06, 0.06, 0.07, 1.0), 0.9, 0.35),
    ("Polymer", (0.03, 0.03, 0.035, 1.0), 0.0, 0.7),
    ("Lens", (0.15, 0.55, 0.9, 1.0), 0.0, 0.1),
]

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJECT, "Assets", "Trickshot", "Resources", "Rifles")


class Gun:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.muzzle = Vector((0, 1, 0.06))
        self.eye = Vector((0, -0.2, 0.16))

    def _commit(self, tmp, mat, smooth):
        for f in tmp.faces:
            f.material_index = mat
            f.smooth = smooth and len(f.verts) == 4
        me = bpy.data.meshes.new("part")
        tmp.to_mesh(me)
        tmp.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def box(self, c, size, mat, rx=0.0, ry=0.0, bevel=0.003):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        m = Matrix.Translation(c) @ Euler((rx, ry, 0)).to_matrix().to_4x4() @ Matrix.Diagonal((*size, 1))
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
        b = min(bevel, min(size) * 0.3)
        if b > 0.0005:
            bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=b, segments=1, affect='EDGES', profile=0.5)
        self._commit(tmp, mat, False)

    def cyl(self, c, r, length, mat, axis='y', r2=None, seg=18):
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=seg,
                              radius1=r, radius2=r if r2 is None else r2, depth=length)
        if axis == 'y':
            rot = Euler((-math.pi / 2, 0, 0))
        elif axis == 'x':
            rot = Euler((0, math.pi / 2, 0))
        else:
            rot = Euler((0, 0, 0))
        bmesh.ops.transform(tmp, matrix=Matrix.Translation(c) @ rot.to_matrix().to_4x4(), verts=tmp.verts)
        self._commit(tmp, mat, True)

    def scope(self, y, z, length=0.30, tube=0.019, bell=0.03):
        """A scope centred at y, with its axis at height z."""
        self.cyl((0, y, z), tube, length, MET)
        front = y + length / 2
        back = y - length / 2
        self.cyl((0, front + 0.04, z), tube, 0.08, MET, r2=bell)           # objective bell
        self.cyl((0, front + 0.095, z), bell, 0.03, MET)
        self.cyl((0, front + 0.111, z), bell * 0.86, 0.004, LEN)           # front glass
        self.cyl((0, back - 0.03, z), tube * 1.3, 0.06, MET)               # eyepiece
        self.cyl((0, back - 0.061, z), tube * 1.1, 0.004, LEN)             # rear glass
        self.cyl((0, y, z + tube + 0.012), 0.012, 0.024, MET, axis='z')    # elevation turret
        self.cyl((tube + 0.012, y, z), 0.012, 0.024, MET, axis='x')        # windage turret
        for my in (y - length * 0.28, y + length * 0.28):                  # ring mounts
            self.box((0, my, z - tube * 0.9), (0.03, 0.022, tube * 2.4), MET)
        self.eye = Vector((0, back - 0.13, z))

    def bolt(self, y, z, side=0.045):
        self.cyl((side, y, z), 0.007, 0.05, MET, axis='x')
        self.cyl((side + 0.032, y, z - 0.008), 0.013, 0.022, POL, axis='x')

    def grip(self, y=-0.02, rx=-0.28):
        self.box((0, y, -0.068), (0.038, 0.052, 0.13), POL, rx=rx, bevel=0.006)
        self.box((0, y + 0.065, -0.036), (0.012, 0.09, 0.01), MET)         # trigger guard
        self.box((0, y + 0.108, -0.016), (0.012, 0.01, 0.045), MET)
        self.box((0, y + 0.062, -0.014), (0.006, 0.01, 0.028), MET, rx=0.3)  # trigger


def vanta():
    """Bullpup heavy bolt-action: the action and magazine sit behind the grip."""
    g = Gun("Vanta")
    g.box((0, -0.16, 0.055), (0.058, 0.64, 0.10), FIN, bevel=0.008)
    g.box((0, -0.33, 0.118), (0.048, 0.20, 0.03), POL, bevel=0.008)
    g.box((0, -0.492, 0.045), (0.064, 0.03, 0.14), POL, bevel=0.008)
    g.box((0, -0.25, -0.055), (0.042, 0.10, 0.13), MET, rx=0.12)
    g.grip(y=-0.01)
    g.box((0, 0.29, 0.05), (0.052, 0.28, 0.075), FIN, bevel=0.006)
    for vy in (0.2, 0.26, 0.32, 0.38):                                      # vents
        g.box((0, vy, 0.06), (0.056, 0.03, 0.016), POL)
    g.cyl((0, 0.50, 0.06), 0.013, 0.40, MET)
    g.box((0, 0.72, 0.06), (0.046, 0.08, 0.03), MET)
    g.box((0, 0.72, 0.06), (0.05, 0.016, 0.018), POL)
    g.box((0, 0.02, 0.111), (0.028, 0.46, 0.012), MET)
    g.scope(0.0, 0.165)
    g.bolt(-0.28, 0.075)
    for sx in (-0.03, 0.03):                                                # folded bipod
        g.box((sx, 0.36, -0.0), (0.012, 0.22, 0.012), MET)
    g.muzzle = Vector((0, 0.76, 0.06))
    return g


def arbalest():
    """Modular chassis bolt-action with a skeleton stock."""
    g = Gun("Arbalest")
    g.box((0, 0.02, 0.06), (0.05, 0.30, 0.07), FIN, bevel=0.006)
    g.box((0, 0.38, 0.06), (0.048, 0.42, 0.054), FIN, bevel=0.01)
    for vy in (0.24, 0.31, 0.38, 0.45, 0.52):
        g.box((0, vy, 0.06), (0.052, 0.04, 0.014), POL)
    g.box((0, 0.38, 0.03), (0.02, 0.36, 0.01), MET)
    g.cyl((0, 0.70, 0.06), 0.012, 0.50, MET)
    g.cyl((0, 0.985, 0.06), 0.02, 0.07, MET)
    g.cyl((0, 0.985, 0.06), 0.022, 0.012, POL)
    g.grip(y=-0.03, rx=-0.3)
    g.box((0, 0.10, -0.05), (0.04, 0.085, 0.12), MET, rx=0.06)
    g.box((0, -0.14, 0.05), (0.05, 0.04, 0.08), MET)                        # folding hinge
    g.box((0, -0.31, 0.068), (0.03, 0.32, 0.03), FIN)
    g.box((0, -0.29, -0.012), (0.022, 0.30, 0.02), FIN, rx=0.2)
    g.box((0, -0.48, 0.03), (0.045, 0.03, 0.15), POL, bevel=0.008)
    g.box((0, -0.30, 0.102), (0.044, 0.16, 0.034), POL, bevel=0.008)
    g.box((0, 0.12, 0.101), (0.028, 0.52, 0.012), MET)
    g.scope(0.03, 0.155, length=0.34)
    g.bolt(-0.08, 0.07)
    g.muzzle = Vector((0, 1.02, 0.06))
    return g


def longbow():
    """Classic long-barrel bolt-action: full stock, heavy barrel, tank brake, bipod down."""
    g = Gun("Longbow")
    g.box((0, -0.05, 0.03), (0.055, 0.55, 0.08), FIN, bevel=0.012)
    g.box((0, 0.38, 0.035), (0.05, 0.34, 0.06), FIN, bevel=0.012)
    g.box((0, -0.42, 0.01), (0.05, 0.24, 0.13), FIN, bevel=0.014)
    g.box((0, -0.2, -0.05), (0.042, 0.06, 0.12), FIN, rx=-0.35, bevel=0.01)
    g.box((0, -0.552, 0.01), (0.055, 0.025, 0.14), POL, bevel=0.006)
    g.box((0, -0.40, 0.09), (0.045, 0.18, 0.03), POL, bevel=0.008)
    g.box((0, -0.085, -0.016), (0.012, 0.11, 0.01), MET)
    g.box((0, -0.11, 0.0), (0.006, 0.01, 0.03), MET, rx=0.3)
    g.cyl((0, 0.02, 0.08), 0.022, 0.30, MET)
    g.cyl((0, 0.55, 0.08), 0.016, 0.78, MET)
    g.box((0, 0.99, 0.08), (0.05, 0.10, 0.036), MET)
    for by in (0.965, 1.0):
        g.box((0, by, 0.08), (0.054, 0.014, 0.022), POL)
    g.box((0, 0.07, -0.04), (0.04, 0.09, 0.08), MET)
    g.box((0, 0.50, 0.0), (0.05, 0.04, 0.02), MET)
    for sx, ry in ((-0.065, 0.32), (0.065, -0.32)):                         # bipod legs, down
        g.box((sx, 0.50, -0.125), (0.012, 0.012, 0.26), MET, ry=ry)
        g.box((sx * 1.62, 0.50, -0.25), (0.03, 0.02, 0.012), POL)
    g.box((0, 0.02, 0.107), (0.026, 0.24, 0.01), MET)
    g.scope(0.02, 0.165, length=0.36, bell=0.033)
    g.bolt(-0.1, 0.085)
    g.muzzle = Vector((0, 1.04, 0.08))
    return g


def hammer():
    """Semi-automatic anti-materiel rifle: long box receiver, big magazine, arrowhead brake."""
    g = Gun("Hammer")
    g.box((0, 0.12, 0.075), (0.06, 0.62, 0.075), FIN, bevel=0.005)
    g.box((0, 0.0, 0.015), (0.05, 0.30, 0.06), FIN, bevel=0.005)
    g.box((0, 0.11, -0.085), (0.045, 0.13, 0.16), MET, rx=0.08)
    g.grip(y=-0.07, rx=-0.3)
    g.box((0, -0.33, 0.04), (0.05, 0.28, 0.11), FIN, bevel=0.008)
    g.box((0, -0.482, 0.04), (0.058, 0.03, 0.15), POL, bevel=0.008)
    g.cyl((0, -0.40, -0.06), 0.01, 0.1, MET, axis='z')
    g.box((0, 0.50, 0.075), (0.066, 0.14, 0.085), FIN, bevel=0.006)
    g.cyl((0, 0.755, 0.075), 0.017, 0.65, MET)
    for fy in (0.62, 0.70, 0.78, 0.86, 0.94):                               # cooling rings
        g.cyl((0, fy, 0.075), 0.021, 0.02, MET)
    g.box((0, 1.07, 0.075), (0.045, 0.04, 0.035), MET)
    g.box((0, 1.115, 0.075), (0.085, 0.03, 0.04), MET)
    g.box((0, 1.15, 0.075), (0.06, 0.03, 0.036), MET)
    g.box((0, 0.02, 0.118), (0.03, 0.36, 0.012), MET)
    g.box((0, 0.33, 0.14), (0.02, 0.16, 0.012), MET)                        # carry handle
    for hy in (0.26, 0.40):
        g.box((0, hy, 0.126), (0.02, 0.014, 0.03), MET)
    g.scope(0.0, 0.172, length=0.32)
    g.cyl((0.047, 0.17, 0.08), 0.008, 0.04, MET, axis='x')                  # charging handle
    for sx in (-0.036, 0.036):
        g.box((sx, 0.64, 0.025), (0.012, 0.26, 0.012), MET)
    g.muzzle = Vector((0, 1.17, 0.075))
    return g


def lumen():
    """Angular energy-era rifle with an integrated optic and a lit core between twin rails."""
    g = Gun("Lumen")
    g.box((0, 0.05, 0.055), (0.055, 0.50, 0.085), FIN, bevel=0.016)
    g.box((0, 0.45, 0.06), (0.05, 0.34, 0.06), FIN, bevel=0.014)
    g.box((0, 0.36, 0.008), (0.03, 0.30, 0.035), POL, rx=0.1, bevel=0.008)
    for sx in (-0.0285, 0.0285):                                            # light strips
        g.box((sx, 0.25, 0.062), (0.004, 0.5, 0.012), LEN, bevel=0)
    for rz in (0.086, 0.034):                                               # twin rails
        g.box((0, 0.73, rz), (0.022, 0.30, 0.012), MET)
    g.cyl((0, 0.73, 0.06), 0.008, 0.30, LEN)
    g.box((0, 0.885, 0.06), (0.04, 0.03, 0.078), MET, bevel=0.006)
    g.box((0, -0.31, 0.042), (0.04, 0.30, 0.05), FIN, rx=-0.12, bevel=0.012)
    g.box((0, -0.29, -0.02), (0.025, 0.24, 0.02), MET, rx=0.25)
    g.box((0, -0.47, 0.035), (0.05, 0.03, 0.15), POL, bevel=0.008)
    g.grip(y=-0.04, rx=-0.35)
    g.cyl((0, 0.09, -0.03), 0.04, 0.046, MET, axis='x', seg=24)             # drum magazine
    g.cyl((0, 0.09, -0.03), 0.018, 0.05, LEN, axis='x')
    g.box((0, 0.02, 0.135), (0.045, 0.26, 0.05), MET, bevel=0.01)           # integrated optic
    g.box((0, 0.152, 0.137), (0.034, 0.004, 0.034), LEN, bevel=0)
    g.box((0, -0.112, 0.137), (0.03, 0.004, 0.03), LEN, bevel=0)
    g.eye = Vector((0, -0.24, 0.137))
    g.muzzle = Vector((0, 0.90, 0.06))
    return g


def make_materials():
    mats = []
    for name, color, metallic, rough in MATERIALS:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        m.diffuse_color = color
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Metallic"].default_value = metallic
            bsdf.inputs["Roughness"].default_value = rough
        mats.append(m)
    return mats


def to_object(gun, mats):
    me = bpy.data.meshes.new(gun.name)
    bmesh.ops.remove_doubles(gun.bm, verts=gun.bm.verts, dist=0.00001)
    gun.bm.to_mesh(me)
    gun.bm.free()
    for m in mats:
        me.materials.append(m)
    obj = bpy.data.objects.new(gun.name, me)
    bpy.context.scene.collection.objects.link(obj)
    for label, pos in (("Muzzle", gun.muzzle), ("Top", Vector((0, 0, 0.3))), ("Eye", gun.eye)):
        e = bpy.data.objects.new(label, None)
        e.empty_display_size = 0.03
        e.location = pos
        e.parent = obj
        bpy.context.scene.collection.objects.link(e)
    return obj


def export(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    for child in obj.children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(OUT_DIR, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    return path


def render_preview(objects, path):
    scene = bpy.context.scene
    for i, obj in enumerate(objects):
        obj.location = (0, 0, -0.52 * i)
        for child in obj.children:  # FBX-style names collide; give previews unique names
            child.hide_render = True

    cam_data = bpy.data.cameras.new("Preview")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 2.9
    cam = bpy.data.objects.new("Preview", cam_data)
    scene.collection.objects.link(cam)
    centre = Vector((0, 0.28, -0.52 * (len(objects) - 1) / 2 + 0.03))
    direction = Vector((1.0, 0.45, 0.25)).normalized()
    cam.location = centre + direction * 6
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 4
    sun.rotation_euler = (math.radians(50), 0, math.radians(110))
    scene.collection.objects.link(sun)

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.58, 0.62, 1)
    scene.world = world

    scene.render.resolution_x = 1300
    scene.render.resolution_y = 1300
    scene.render.filepath = path
    for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH', 'CYCLES'):
        try:
            scene.render.engine = engine
            if engine == 'CYCLES':
                scene.cycles.samples = 16
            bpy.ops.render.render(write_still=True)
            print("PREVIEW rendered with", engine)
            return
        except Exception as ex:  # try the next engine
            print("PREVIEW engine failed:", engine, ex)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    os.makedirs(OUT_DIR, exist_ok=True)

    mats = make_materials()
    objects = []
    for build in (vanta, arbalest, longbow, hammer, lumen):
        gun = build()
        length = gun.muzzle.y + 0.5
        obj = to_object(gun, mats)
        path = export(obj)
        print("EXPORTED %s  tris~%d  %s" % (obj.name, sum(len(p.vertices) - 2 for p in obj.data.polygons), path))
        objects.append(obj)

    render_preview(objects, os.path.join(PROJECT, "ArtSource", "rifles_preview.png"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "ArtSource", "Rifles.blend"))


main()
