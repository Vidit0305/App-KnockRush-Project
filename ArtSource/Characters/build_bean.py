"""
KnockRush - procedural "Bean" character builder (Blender 4.5 LTS).

Builds the original KnockRush bean character entirely from code so it is reproducible:
  * rounded bean body, big expressive eyes, short arms and stubby feet (one mesh)
  * vertex colours: RGB = albedo for untinted parts, A = tint mask (1 = player colour)
  * shared skeleton "BeanRig" (Root, Hips, Chest, Arm.L/R, Leg.L/R) used by every costume
  * 13 animations: Idle, Walk, Run, Jump, Fall, Land, Dive, Hit, Stumble, Grab,
    Eliminated, Celebrate, VictoryDance
  * exports FBX for Unity (Generic rig). The model faces -Z after import; the PlayerCharacter
    prefab rotates it 180 degrees so it faces +Z (Unity's forward).

Usage:
  blender -b --factory-startup --python build_bean.py -- <output.fbx> [<output.blend>]
"""

import math
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

FPS = 30

# Character faces -Y in Blender (=> +Z in Unity). Its LEFT side is +X.
BODY_CENTER_Z = 0.86
BODY_RADII = (0.44, 0.40, 0.66)       # x (width), y (depth), z (half height)
EYE_Z = 1.14
EYE_X = 0.15
SHOULDER = (0.41, 0.0, 0.98)
HIP_Z = 0.45
FOOT = (0.17, -0.04, 0.10)

WHITE = (1.0, 1.0, 1.0)
PUPIL = (0.04, 0.04, 0.07)
FEET_SHADE = (0.72, 0.72, 0.72)
HAND_SHADE = (0.92, 0.92, 0.92)


# ---------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0


def smoothstep(edge0, edge1, x):
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------
# Mesh construction (single bmesh, parts tagged with vertex groups + colours)
# ---------------------------------------------------------------------------
class Part:
    def __init__(self, name, color, alpha, group):
        self.name = name
        self.color = color
        self.alpha = alpha
        self.group = group
        self.verts = []


def add_ellipsoid(bm, part, center, radii, segments=32, rings=16, rotation=None, deform=None):
    created = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    verts = created['verts']
    rot = rotation.to_matrix() if rotation is not None else Matrix.Identity(3)
    for v in verts:
        local = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
        if deform is not None:
            local = deform(local)
        v.co = Vector(center) + rot @ local
    part.verts.extend(verts)
    return verts


def body_deform(local):
    # Bean silhouette: slightly narrower top, fuller belly, flatter bottom.
    t = local.z / BODY_RADII[2]  # -1..1
    width = 1.0 - 0.10 * max(0.0, t) + 0.05 * max(0.0, -t) * (1.0 - abs(t))
    belly = 1.0 + 0.06 * math.exp(-((t + 0.15) ** 2) / 0.18)
    out = Vector((local.x * width * belly, local.y * width * belly, local.z))
    if t < -0.75:
        out.z = -BODY_RADII[2] * (0.75 + (abs(t) - 0.75) * 0.6)
    return out


def build_mesh():
    bm = bmesh.new()
    parts = []

    body = Part('Body', WHITE, 1.0, None)
    add_ellipsoid(bm, body, (0, 0, BODY_CENTER_Z), BODY_RADII, 40, 24, deform=body_deform)
    parts.append(body)

    for side, sx in (('L', 1.0), ('R', -1.0)):
        # Eyes: big white ovals slightly embedded in the face, black pupils in front.
        eye = Part(f'Eye.{side}', WHITE, 0.0, 'Chest')
        eye_center = (sx * EYE_X, -0.335, EYE_Z)
        add_ellipsoid(bm, eye, eye_center, (0.115, 0.07, 0.145), 24, 12,
                      rotation=Euler((0, 0, -sx * math.radians(14))))
        parts.append(eye)

        pupil = Part(f'Pupil.{side}', PUPIL, 0.0, 'Chest')
        add_ellipsoid(bm, pupil, (sx * (EYE_X - 0.01), -0.392, EYE_Z - 0.01), (0.055, 0.03, 0.07), 16, 8,
                      rotation=Euler((0, 0, -sx * math.radians(14))))
        parts.append(pupil)

        highlight = Part(f'Highlight.{side}', WHITE, 0.0, 'Chest')
        add_ellipsoid(bm, highlight, (sx * (EYE_X - 0.03), -0.418, EYE_Z + 0.025), (0.018, 0.01, 0.022), 8, 6)
        parts.append(highlight)

        # Arms: short rounded stubs hanging from the shoulders.
        arm = Part(f'Arm.{side}', HAND_SHADE, 1.0, f'Arm.{side}')
        shoulder = Vector((sx * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
        arm_center = shoulder + Vector((sx * 0.10, 0.0, -0.17))
        add_ellipsoid(bm, arm, arm_center, (0.09, 0.09, 0.23), 16, 10,
                      rotation=Euler((0, sx * math.radians(22), 0)))
        parts.append(arm)

        # Feet: stubby rounded feet pointing forward.
        foot = Part(f'Foot.{side}', FEET_SHADE, 1.0, f'Leg.{side}')
        add_ellipsoid(bm, foot, (sx * FOOT[0], FOOT[1], FOOT[2]), (0.12, 0.17, 0.10), 16, 10)
        parts.append(foot)

    # Smile: a thin curved capsule under the eyes.
    mouth = Part('Mouth', PUPIL, 0.0, 'Chest')
    for i in range(7):
        t = (i - 3) / 3.0
        x = t * 0.075
        z = 0.965 + 0.02 * (t * t)
        add_ellipsoid(bm, mouth, (x, -0.378 + 0.01 * abs(t), z), (0.018, 0.012, 0.014), 8, 6)
    parts.append(mouth)

    bm.normal_update()
    mesh = bpy.data.meshes.new('BeanMesh')
    bm.to_mesh(mesh)

    # Map bmesh verts -> indices (indices are stable after to_mesh).
    index_parts = []
    for part in parts:
        index_parts.append((part, [v.index for v in part.verts]))
    bm.free()

    obj = bpy.data.objects.new('Bean', mesh)
    bpy.context.scene.collection.objects.link(obj)

    # Smooth shading.
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Vertex colours (point domain, byte colour => FBX vertex colours).
    color_attr = mesh.color_attributes.new(name='Col', type='BYTE_COLOR', domain='POINT')
    mesh.color_attributes.active_color = color_attr
    for part, indices in index_parts:
        rgba = (part.color[0], part.color[1], part.color[2], part.alpha)
        for i in indices:
            color_attr.data[i].color_srgb = rgba

    return obj, index_parts


# ---------------------------------------------------------------------------
# Armature + skinning
# ---------------------------------------------------------------------------
BONES = {
    # name: (head, tail, parent, deform)
    'Root': ((0, 0, 0), (0, 0, 0.25), None, False),
    'Hips': ((0, 0, HIP_Z), (0, 0, 0.84), 'Root', True),
    'Chest': ((0, 0, 0.84), (0, 0, 1.42), 'Hips', True),
    'Arm.L': ((SHOULDER[0], 0, SHOULDER[2]), (SHOULDER[0] + 0.12, 0, SHOULDER[2] - 0.34), 'Chest', True),
    'Arm.R': ((-SHOULDER[0], 0, SHOULDER[2]), (-SHOULDER[0] - 0.12, 0, SHOULDER[2] - 0.34), 'Chest', True),
    'Leg.L': ((FOOT[0], 0, HIP_Z), (FOOT[0], 0, 0.06), 'Hips', True),
    'Leg.R': ((-FOOT[0], 0, HIP_Z), (-FOOT[0], 0, 0.06), 'Hips', True),
}


def build_armature():
    data = bpy.data.armatures.new('BeanRigData')
    rig = bpy.data.objects.new('BeanRig', data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)

    bpy.ops.object.mode_set(mode='EDIT')
    edit_bones = {}
    for name, (head, tail, parent, deform) in BONES.items():
        bone = data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.roll = 0.0
        bone.use_deform = deform
        edit_bones[name] = bone
    for name, (_, _, parent, _) in BONES.items():
        if parent:
            edit_bones[name].parent = edit_bones[parent]
            edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')

    for pbone in rig.pose.bones:
        pbone.rotation_mode = 'QUATERNION'
    return rig


def skin(mesh_obj, rig, index_parts):
    groups = {name: mesh_obj.vertex_groups.new(name=name)
              for name, spec in BONES.items() if spec[3]}
    mesh = mesh_obj.data

    for part, indices in index_parts:
        if part.group is not None:
            groups[part.group].add(indices, 1.0, 'REPLACE')
            continue
        # Body: blend Hips -> Chest by height.
        for i in indices:
            z = mesh.vertices[i].co.z
            w_chest = smoothstep(0.70, 1.02, z)
            if w_chest < 0.999:
                groups['Hips'].add([i], 1.0 - w_chest, 'REPLACE')
            if w_chest > 0.001:
                groups['Chest'].add([i], w_chest, 'REPLACE')

    mesh_obj.parent = rig
    modifier = mesh_obj.modifiers.new(name='Armature', type='ARMATURE')
    modifier.object = rig


# ---------------------------------------------------------------------------
# Animation helpers (rotations/offsets are given in ARMATURE space and converted)
# ---------------------------------------------------------------------------
def deg(x, y=0.0, z=0.0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ')


class Clip:
    def __init__(self, rig, name, length):
        self.rig = rig
        self.length = length
        self.action = bpy.data.actions.new(name)
        self.action.use_fake_user = True
        rig.animation_data_create()
        rig.animation_data.action = self.action
        for pbone in rig.pose.bones:
            pbone.location = (0, 0, 0)
            pbone.rotation_quaternion = (1, 0, 0, 0)
            pbone.scale = (1, 1, 1)
        # Key every bone at rest at the start and end so no bone inherits another clip's pose.
        for frame in (0, length):
            for pbone in rig.pose.bones:
                for path in ('location', 'rotation_quaternion', 'scale'):
                    pbone.keyframe_insert(path, frame=frame)

    def key(self, bone, frame, rot=None, loc=None, scale=None):
        pbone = self.rig.pose.bones[bone]
        rest = pbone.bone.matrix_local.to_quaternion()
        if rot is not None:
            q_arm = rot.to_quaternion() if isinstance(rot, Euler) else rot
            pbone.rotation_quaternion = rest.inverted() @ q_arm @ rest
            pbone.keyframe_insert('rotation_quaternion', frame=frame)
        if loc is not None:
            pbone.location = rest.inverted() @ Vector(loc)
            pbone.keyframe_insert('location', frame=frame)
        if scale is not None:
            # Scale is bone-local; bones point up (Y) so (side, up, depth).
            pbone.scale = scale
            pbone.keyframe_insert('scale', frame=frame)

    def finish(self, cyclic):
        # Looping is configured in Unity's import settings (CharacterModelPostprocessor);
        # cyclic clips repeat their first key at the end so the loop is seamless.
        self.cyclic = cyclic
        for fcurve in self.action.fcurves:
            for kp in fcurve.keyframe_points:
                kp.interpolation = 'BEZIER'
        self.rig.animation_data.action = None


def cycle(clip, frames, bone, values, kind='rot'):
    """Keys a looping sequence of values evenly across frames (first value repeated at end)."""
    count = len(values)
    for i, value in enumerate(values + [values[0]]):
        frame = round(frames * i / count)
        if kind == 'rot':
            clip.key(bone, frame, rot=value)
        elif kind == 'loc':
            clip.key(bone, frame, loc=value)
        else:
            clip.key(bone, frame, scale=value)


def build_animations(rig):
    clips = []

    # 1. Idle - gentle breathing and arm sway.
    c = Clip(rig, 'Idle', 60)
    cycle(c, 60, 'Hips', [(0, 0, 0), (0, 0, -0.015)], 'loc')
    cycle(c, 60, 'Chest', [(1, 1, 1), (1.02, 1.03, 1.02)], 'scale')
    cycle(c, 60, 'Arm.L', [deg(0, -6), deg(4, -10)])
    cycle(c, 60, 'Arm.R', [deg(0, 6), deg(4, 10)])
    c.finish(True); clips.append(c)

    # 2. Walk
    c = Clip(rig, 'Walk', 24)
    cycle(c, 24, 'Leg.L', [deg(-28), deg(28)])
    cycle(c, 24, 'Leg.R', [deg(28), deg(-28)])
    cycle(c, 24, 'Arm.L', [deg(22, -8), deg(-22, -8)])
    cycle(c, 24, 'Arm.R', [deg(-22, 8), deg(22, 8)])
    cycle(c, 24, 'Hips', [(0, 0, 0), (0, 0, 0.035), (0, 0, 0), (0, 0, 0.035)], 'loc')
    cycle(c, 24, 'Chest', [deg(4, 0, 5), deg(4, 0, -5)])
    c.finish(True); clips.append(c)

    # 3. Run
    c = Clip(rig, 'Run', 16)
    cycle(c, 16, 'Leg.L', [deg(-48), deg(40)])
    cycle(c, 16, 'Leg.R', [deg(40), deg(-48)])
    cycle(c, 16, 'Arm.L', [deg(55, -15), deg(-50, -15)])
    cycle(c, 16, 'Arm.R', [deg(-50, 15), deg(55, 15)])
    cycle(c, 16, 'Hips', [(0, 0, 0.02), (0, 0, 0.07), (0, 0, 0.02), (0, 0, 0.07)], 'loc')
    cycle(c, 16, 'Hips', [deg(14, 0, 4), deg(14, 0, -4)])
    c.finish(True); clips.append(c)

    # 4. Jump (one-shot, holds the last pose)
    c = Clip(rig, 'Jump', 14)
    c.key('Hips', 0, scale=(1.08, 0.86, 1.08))
    c.key('Hips', 4, scale=(0.9, 1.16, 0.9))
    c.key('Hips', 14, scale=(1, 1, 1))
    for f, a in ((0, 0), (4, 150), (14, 135)):
        c.key('Arm.L', f, rot=deg(-10, -a))
        c.key('Arm.R', f, rot=deg(-10, a))
    for f, a in ((0, 0), (5, -25), (14, -18)):
        c.key('Leg.L', f, rot=deg(a))
        c.key('Leg.R', f, rot=deg(a * 0.5))
    c.finish(False); clips.append(c)

    # 5. Fall (loop) - flailing arms, kicking legs.
    c = Clip(rig, 'Fall', 20)
    cycle(c, 20, 'Arm.L', [deg(-25, -140), deg(20, -120), deg(-10, -160)])
    cycle(c, 20, 'Arm.R', [deg(20, 120), deg(-25, 160), deg(-5, 135)])
    cycle(c, 20, 'Leg.L', [deg(-25), deg(20)])
    cycle(c, 20, 'Leg.R', [deg(20), deg(-25)])
    cycle(c, 20, 'Chest', [deg(-6, 6), deg(-6, -6)])
    c.finish(True); clips.append(c)

    # 6. Land (one-shot squash)
    c = Clip(rig, 'Land', 10)
    c.key('Hips', 0, scale=(1, 1, 1))
    c.key('Hips', 2, scale=(1.18, 0.78, 1.18), loc=(0, 0, -0.06))
    c.key('Hips', 10, scale=(1, 1, 1), loc=(0, 0, 0))
    c.key('Arm.L', 2, rot=deg(0, -45))
    c.key('Arm.R', 2, rot=deg(0, 45))
    c.finish(False); clips.append(c)

    # 7. Dive (one-shot, holds) - superman pose.
    c = Clip(rig, 'Dive', 8)
    c.key('Hips', 0, rot=deg(0))
    c.key('Hips', 6, rot=deg(78), loc=(0, 0, -0.1))
    c.key('Hips', 8, rot=deg(80), loc=(0, 0, -0.12))
    for f in (6, 8):
        c.key('Arm.L', f, rot=deg(-160, -15))
        c.key('Arm.R', f, rot=deg(-160, 15))
        c.key('Leg.L', f, rot=deg(25))
        c.key('Leg.R', f, rot=deg(18))
    c.finish(False); clips.append(c)

    # 8. Hit (one-shot recoil)
    c = Clip(rig, 'Hit', 12)
    c.key('Chest', 0, rot=deg(0))
    c.key('Chest', 3, rot=deg(-28, 0, 8))
    c.key('Chest', 12, rot=deg(0))
    c.key('Hips', 3, scale=(1.1, 0.88, 1.1))
    c.key('Hips', 12, scale=(1, 1, 1))
    c.key('Arm.L', 3, rot=deg(-30, -75))
    c.key('Arm.R', 3, rot=deg(-30, 75))
    c.finish(False); clips.append(c)

    # 9. Stumble (loop wobble)
    c = Clip(rig, 'Stumble', 30)
    cycle(c, 30, 'Hips', [deg(10, 14), deg(-6, -14)])
    cycle(c, 30, 'Arm.L', [deg(-40, -80), deg(30, -40)])
    cycle(c, 30, 'Arm.R', [deg(30, 40), deg(-40, 80)])
    cycle(c, 30, 'Leg.L', [deg(-15), deg(15)])
    cycle(c, 30, 'Leg.R', [deg(15), deg(-15)])
    c.finish(True); clips.append(c)

    # 10. Grab (loop) - arms reaching forward.
    c = Clip(rig, 'Grab', 20)
    cycle(c, 20, 'Arm.L', [deg(-88, -4), deg(-80, -10)])
    cycle(c, 20, 'Arm.R', [deg(-88, 4), deg(-80, 10)])
    cycle(c, 20, 'Chest', [deg(10), deg(13)])
    c.finish(True); clips.append(c)

    # 11. Eliminated (one-shot, holds) - flops over backwards.
    c = Clip(rig, 'Eliminated', 30)
    c.key('Hips', 0, rot=deg(0), loc=(0, 0, 0))
    c.key('Hips', 6, rot=deg(10), loc=(0, 0, 0.1))
    c.key('Hips', 20, rot=deg(-88), loc=(0, 0, -0.3))
    c.key('Hips', 24, rot=deg(-80), loc=(0, 0, -0.27))
    c.key('Hips', 30, rot=deg(-86), loc=(0, 0, -0.3))
    c.key('Arm.L', 20, rot=deg(-20, -110))
    c.key('Arm.R', 20, rot=deg(-20, 110))
    c.key('Leg.L', 20, rot=deg(-40))
    c.key('Leg.R', 20, rot=deg(-30))
    c.key('Arm.L', 30, rot=deg(-10, -100))
    c.key('Arm.R', 30, rot=deg(-10, 100))
    c.finish(False); clips.append(c)

    # 12. Celebrate (loop) - hopping with arms waving.
    c = Clip(rig, 'Celebrate', 24)
    cycle(c, 24, 'Hips', [(0, 0, 0), (0, 0, 0.16)], 'loc')
    cycle(c, 24, 'Hips', [(1.06, 0.92, 1.06), (0.95, 1.08, 0.95)], 'scale')
    cycle(c, 24, 'Arm.L', [deg(0, -150), deg(0, -125)])
    cycle(c, 24, 'Arm.R', [deg(0, 125), deg(0, 150)])
    cycle(c, 24, 'Leg.L', [deg(0), deg(-20)])
    cycle(c, 24, 'Leg.R', [deg(0), deg(-20)])
    c.finish(True); clips.append(c)

    # 13. VictoryDance (loop) - side-to-side groove with arm pumps.
    c = Clip(rig, 'VictoryDance', 32)
    cycle(c, 32, 'Hips', [(0.08, 0, 0), (0, 0, 0.05), (-0.08, 0, 0), (0, 0, 0.05)], 'loc')
    cycle(c, 32, 'Hips', [deg(0, 10, 25), deg(0, 0, 0), deg(0, -10, -25), deg(0, 0, 0)])
    cycle(c, 32, 'Arm.L', [deg(-20, -160), deg(-60, -40), deg(-20, -100), deg(-60, -40)])
    cycle(c, 32, 'Arm.R', [deg(-60, 40), deg(-20, 100), deg(-60, 40), deg(-20, 160)])
    cycle(c, 32, 'Leg.L', [deg(-25), deg(0), deg(0), deg(0)])
    cycle(c, 32, 'Leg.R', [deg(0), deg(0), deg(-25), deg(0)])
    cycle(c, 32, 'Chest', [deg(0, -8), deg(0, 0), deg(0, 8), deg(0, 0)])
    c.finish(True); clips.append(c)

    return clips


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def export(fbx_path, blend_path=None):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=False,
        object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',  # Unity normalises FBX axes; the prefab rotates the model 180° to face +Z
        axis_up='Y',
        bake_space_transform=False,
        mesh_smooth_type='FACE',
        colors_type='SRGB',
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
    if blend_path:
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if not argv:
        raise SystemExit('Usage: blender -b --python build_bean.py -- <output.fbx> [<output.blend>]')
    fbx_path = argv[0]
    blend_path = argv[1] if len(argv) > 1 else None

    reset_scene()
    mesh_obj, index_parts = build_mesh()
    rig = build_armature()
    skin(mesh_obj, rig, index_parts)
    clips = build_animations(rig)
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = max(c.length for c in clips)
    export(fbx_path, blend_path)

    tri_count = sum(len(p.vertices) - 2 for p in mesh_obj.data.polygons)
    print(f'[build_bean] verts={len(mesh_obj.data.vertices)} tris~={tri_count} '
          f'bones={len(rig.data.bones)} clips={[c.action.name for c in clips]}')
    print(f'[build_bean] exported {fbx_path}')


if __name__ == '__main__':
    main()
