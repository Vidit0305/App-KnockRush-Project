"""
KnockRush - procedural character builder (Blender 4.5 LTS).

Builds the original KnockRush "Rusher" character entirely from code so it is reproducible:
  * chibi proportions: big round head (skin, painted hair, big eyes, brows, cheeks, smile),
    small outfit torso with sleeves, hands, trousers and sneakers (one mesh)
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
# The head occupies the same space as the old bean's upper body, so hats and face cosmetics
# (built around head top 1.5, eyes 1.14, face depth 0.38) still fit.
HEAD_CENTER = (0.0, 0.0, 1.12)
HEAD_RADII = (0.385, 0.37, 0.37)
EYE_Z = 1.13
EYE_X = 0.135
SHOULDER = (0.235, 0.0, 0.735)
HIP_Z = 0.45
FOOT = (0.11, -0.03, 0.07)
TORSO_CENTER = (0.0, 0.0, 0.59)
TORSO_RADII = (0.245, 0.19, 0.215)

WHITE = (1.0, 1.0, 1.0)
PUPIL = (0.04, 0.04, 0.07)
SKIN = (1.0, 0.80, 0.66)
CHEEK = (1.0, 0.56, 0.56)
HAIR = (0.33, 0.19, 0.11)
BROW = (0.24, 0.13, 0.08)
TROUSERS = (0.18, 0.22, 0.40)
SHOE = (0.97, 0.97, 1.0)
SOLE = (0.90, 0.25, 0.22)
OUTFIT_SHADE = (0.78, 0.78, 0.78)   # tinted, slightly darker (hem, cuffs)

# Vertex alpha is a material channel: 1 = outfit colour, SKIN_CODE = skin tone, HAIR_CODE = hair
# colour (both per character, set by the costume), 0 = fixed colour (eyes, trousers, shoes).
SKIN_CODE = 64 / 255.0
HAIR_CODE = 128 / 255.0


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
    def __init__(self, name, color, alpha, group, color_fn=None):
        self.name = name
        self.color = color
        self.alpha = alpha
        self.group = group
        self.color_fn = color_fn  # optional (position) -> (rgb, alpha) per vertex
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


def head_deform(local):
    # Slightly fuller cheeks and a softer, narrower jaw.
    t = local.z / HEAD_RADII[2]
    jaw = 1.0 - 0.07 * max(0.0, -t) ** 2
    return Vector((local.x * jaw, local.y * jaw, local.z * (0.96 if t < 0 else 1.0)))


def torso_deform(local):
    # Pear-shaped little body: narrower shoulders, rounder belly, flat bottom.
    t = local.z / TORSO_RADII[2]
    width = 0.86 + 0.14 * (1.0 - max(0.0, t))
    out = Vector((local.x * width, local.y * width, local.z))
    if t < -0.6:
        out.z = -TORSO_RADII[2] * (0.6 + (abs(t) - 0.6) * 0.5)
    return out


def head_color(co):
    """Skin face with painted hair: a cap over the top and back with a side-swept fringe."""
    x, y, z = co.x - HEAD_CENTER[0], co.y - HEAD_CENTER[1], co.z - HEAD_CENTER[2]
    front = -y / HEAD_RADII[1]                    # 1 = face centre, -1 = back of the head
    side = abs(x) / HEAD_RADII[0]
    hairline = 0.105 + 0.022 * math.sin(x * 16.0 + 0.6) + 0.05 * side * side   # soft fringe over the brow
    if front < 0.45:
        blend = min(1.0, (0.45 - front) / 0.6)
        hairline = hairline * (1.0 - blend) + (-0.2) * blend   # down to the nape at the back
    if side > 0.78 and front < 0.7:
        hairline = min(hairline, -0.01)                         # sideburns over the ears
    if z > hairline:
        return WHITE, HAIR_CODE
    return WHITE, SKIN_CODE


def build_mesh():
    bm = bmesh.new()
    parts = []

    # Head (skin + painted hair) and ears.
    head = Part('Head', WHITE, SKIN_CODE, 'Chest', color_fn=head_color)
    add_ellipsoid(bm, head, HEAD_CENTER, HEAD_RADII, 56, 36, deform=head_deform)
    parts.append(head)

    for side, sx in (('L', 1.0), ('R', -1.0)):
        ear = Part(f'Ear.{side}', WHITE, SKIN_CODE, 'Chest')
        add_ellipsoid(bm, ear, (sx * 0.37, 0.02, 1.09), (0.05, 0.07, 0.085), 12, 8)
        parts.append(ear)

        # Eyes: big glossy dark ovals with two highlights; a brow above each.
        eye_center = (sx * EYE_X, -0.335, EYE_Z)
        eye = Part(f'Eye.{side}', PUPIL, 0.0, 'Chest')
        add_ellipsoid(bm, eye, eye_center, (0.058, 0.04, 0.088), 20, 12,
                      rotation=Euler((math.radians(-12), 0, -sx * math.radians(10))))
        parts.append(eye)
        hi = Part(f'Highlight.{side}', WHITE, 0.0, 'Chest')
        add_ellipsoid(bm, hi, (sx * (EYE_X - 0.018), -0.371, EYE_Z + 0.035), (0.02, 0.01, 0.026), 8, 6)
        parts.append(hi)
        hi2 = Part(f'Highlight2.{side}', WHITE, 0.0, 'Chest')
        add_ellipsoid(bm, hi2, (sx * (EYE_X + 0.02), -0.37, EYE_Z - 0.035), (0.01, 0.008, 0.012), 6, 4)
        parts.append(hi2)

        brow = Part(f'Brow.{side}', BROW, 0.0, 'Chest')
        add_ellipsoid(bm, brow, (sx * (EYE_X + 0.005), -0.33, EYE_Z + 0.135), (0.05, 0.02, 0.014), 10, 6,
                      rotation=Euler((math.radians(-20), sx * math.radians(-8), 0)))
        parts.append(brow)

        cheek = Part(f'Cheek.{side}', CHEEK, 0.0, 'Chest')
        add_ellipsoid(bm, cheek, (sx * 0.215, -0.285, 1.035), (0.055, 0.03, 0.035), 12, 8,
                      rotation=Euler((0, 0, -sx * math.radians(35))))
        parts.append(cheek)

        # Sleeve (outfit colour) with a darker cuff, then a skin hand.
        shoulder = Vector((sx * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
        arm_dir = Vector((sx * 0.30, 0.0, -0.95)).normalized()
        sleeve = Part(f'Sleeve.{side}', WHITE, 1.0, f'Arm.{side}')
        add_ellipsoid(bm, sleeve, shoulder + arm_dir * 0.11, (0.072, 0.072, 0.13), 16, 10,
                      rotation=Euler((0, sx * math.radians(17), 0)))
        parts.append(sleeve)
        cuff = Part(f'Cuff.{side}', OUTFIT_SHADE, 1.0, f'Arm.{side}')
        add_ellipsoid(bm, cuff, shoulder + arm_dir * 0.215, (0.068, 0.068, 0.03), 14, 6,
                      rotation=Euler((0, sx * math.radians(17), 0)))
        parts.append(cuff)
        hand = Part(f'Hand.{side}', WHITE, SKIN_CODE, f'Arm.{side}')
        add_ellipsoid(bm, hand, shoulder + arm_dir * 0.29, (0.07, 0.065, 0.075), 14, 10)
        parts.append(hand)

        # Trousers and chunky sneakers.
        leg = Part(f'Leg.{side}', TROUSERS, 0.0, f'Leg.{side}')
        add_ellipsoid(bm, leg, (sx * FOOT[0], 0.0, 0.25), (0.085, 0.085, 0.17), 14, 10)
        parts.append(leg)
        shoe = Part(f'Shoe.{side}', SHOE, 0.0, f'Leg.{side}')
        add_ellipsoid(bm, shoe, (sx * FOOT[0], FOOT[1], FOOT[2] + 0.01), (0.095, 0.14, 0.07), 16, 10)
        parts.append(shoe)
        sole = Part(f'Sole.{side}', SOLE, 0.0, f'Leg.{side}')
        add_ellipsoid(bm, sole, (sx * FOOT[0], FOOT[1], 0.025), (0.1, 0.145, 0.028), 16, 6)
        parts.append(sole)

    # Smile: a short curve of small capsules.
    mouth = Part('Mouth', PUPIL, 0.0, 'Chest')
    for i in range(13):
        t = (i - 6) / 6.0
        add_ellipsoid(bm, mouth, (t * 0.052, -0.353 + 0.008 * abs(t), 0.995 + 0.018 * t * t), (0.011, 0.009, 0.009), 8, 6)
    parts.append(mouth)

    # Outfit: little pear-shaped body with a darker hem band, plus a neck.
    neck = Part('Neck', WHITE, SKIN_CODE, 'Chest')
    add_ellipsoid(bm, neck, (0, 0, 0.78), (0.1, 0.09, 0.07), 12, 8)
    parts.append(neck)
    torso = Part('Torso', WHITE, 1.0, None)
    add_ellipsoid(bm, torso, TORSO_CENTER, TORSO_RADII, 32, 20, deform=torso_deform)
    parts.append(torso)
    hem = Part('Hem', OUTFIT_SHADE, 1.0, 'Hips')
    add_ellipsoid(bm, hem, (0, 0, 0.42), (0.225, 0.18, 0.04), 28, 8)
    parts.append(hem)

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
        for i in indices:
            if part.color_fn is not None:
                rgb, alpha = part.color_fn(mesh.vertices[i].co)
            else:
                rgb, alpha = part.color, part.alpha
            color_attr.data[i].color_srgb = (rgb[0], rgb[1], rgb[2], alpha)

    return obj, index_parts


# ---------------------------------------------------------------------------
# Armature + skinning
# ---------------------------------------------------------------------------
BONES = {
    # name: (head, tail, parent, deform)
    'Root': ((0, 0, 0), (0, 0, 0.25), None, False),
    'Hips': ((0, 0, HIP_Z), (0, 0, 0.66), 'Root', True),
    'Chest': ((0, 0, 0.66), (0, 0, 1.42), 'Hips', True),
    'Arm.L': ((SHOULDER[0], 0, SHOULDER[2]), (SHOULDER[0] + 0.09, 0, SHOULDER[2] - 0.29), 'Chest', True),
    'Arm.R': ((-SHOULDER[0], 0, SHOULDER[2]), (-SHOULDER[0] - 0.09, 0, SHOULDER[2] - 0.29), 'Chest', True),
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
        # Torso: blend Hips -> Chest by height.
        for i in indices:
            z = mesh.vertices[i].co.z
            w_chest = smoothstep(0.50, 0.74, z)
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

    # ---- Emotes (Phase 8) -------------------------------------------------------------------
    # 14. Wave - right arm high, waving.
    c = Clip(rig, 'Wave', 24)
    cycle(c, 24, 'Arm.R', [deg(-25, 150), deg(25, 150)])
    cycle(c, 24, 'Arm.L', [deg(0, -8), deg(4, -12)])
    cycle(c, 24, 'Chest', [deg(0, 0, 4), deg(0, 0, -4)])
    c.finish(True); clips.append(c)

    # 15. Bounce - happy hops on the spot.
    c = Clip(rig, 'Bounce', 20)
    cycle(c, 20, 'Hips', [(0, 0, 0), (0, 0, 0.22)], 'loc')
    cycle(c, 20, 'Hips', [(1.1, 0.88, 1.1), (0.94, 1.1, 0.94)], 'scale')
    cycle(c, 20, 'Arm.L', [deg(0, -40), deg(0, -70)])
    cycle(c, 20, 'Arm.R', [deg(0, 40), deg(0, 70)])
    c.finish(True); clips.append(c)

    # 16. Spin - full turn with arms out.
    c = Clip(rig, 'Spin', 30)
    for i, angle in enumerate((0, 120, 240, 360)):
        c.key('Root', i * 10, rot=deg(0, 0, angle))
    for f in (0, 30):
        c.key('Arm.L', f, rot=deg(0, -85))
        c.key('Arm.R', f, rot=deg(0, 85))
    c.finish(True); clips.append(c)

    # 17. Clap - hands meet in front.
    c = Clip(rig, 'Clap', 16)
    cycle(c, 16, 'Arm.L', [deg(-80, -35), deg(-80, -5)])
    cycle(c, 16, 'Arm.R', [deg(-80, 35), deg(-80, 5)])
    cycle(c, 16, 'Hips', [(0, 0, 0), (0, 0, 0.03)], 'loc')
    c.finish(True); clips.append(c)

    # 18. Bow - polite bow and back up.
    c = Clip(rig, 'Bow', 40)
    c.key('Hips', 0, rot=deg(0))
    c.key('Hips', 14, rot=deg(48))
    c.key('Hips', 26, rot=deg(48))
    c.key('Hips', 40, rot=deg(0))
    c.key('Arm.L', 14, rot=deg(-30, -10))
    c.key('Arm.R', 14, rot=deg(20, 60))
    c.key('Arm.L', 26, rot=deg(-30, -10))
    c.key('Arm.R', 26, rot=deg(20, 60))
    c.finish(True); clips.append(c)

    # 19. Flex - double biceps pump.
    c = Clip(rig, 'Flex', 24)
    cycle(c, 24, 'Arm.L', [deg(0, -95), deg(0, -125)])
    cycle(c, 24, 'Arm.R', [deg(0, 95), deg(0, 125)])
    cycle(c, 24, 'Chest', [(1, 1, 1), (1.08, 1.03, 1.08)], 'scale')
    c.finish(True); clips.append(c)

    # 20. Laugh - lean back, belly shake.
    c = Clip(rig, 'Laugh', 16)
    cycle(c, 16, 'Chest', [deg(-16, 0, 3), deg(-12, 0, -3), deg(-16, 0, 3), deg(-12, 0, -3)])
    cycle(c, 16, 'Hips', [(1.04, 0.97, 1.04), (0.98, 1.02, 0.98)], 'scale')
    cycle(c, 16, 'Arm.L', [deg(-35, -12), deg(-40, -8)])
    cycle(c, 16, 'Arm.R', [deg(-35, 12), deg(-40, 8)])
    c.finish(True); clips.append(c)

    # 21. RobotDance - stiff, snappy poses.
    c = Clip(rig, 'RobotDance', 32)
    poses = [(deg(-90, 0), deg(0, 0), deg(0, 0, 25)), (deg(0, 0), deg(-90, 0), deg(0, 0, -25)),
             (deg(0, -90), deg(0, 90), deg(0, 0, 0)), (deg(-90, 0), deg(-90, 0), deg(0, 0, 0))]
    for i, (left, right, hips) in enumerate(poses + [poses[0]]):
        for f in (i * 8, i * 8 + 5):
            if f <= 32:
                c.key('Arm.L', f, rot=left)
                c.key('Arm.R', f, rot=right)
                c.key('Hips', f, rot=hips)
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
