"""
Renders preview images of Bean.blend (rest pose + one frame of selected clips) for quick review.

Usage:
  blender -b Bean.blend --python render_preview.py -- <output_dir> [Clip:frame ...]
"""

import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out_dir = argv[0] if argv else '//previews'
requests = argv[1:] or ['Idle:0', 'Run:4', 'Jump:8', 'Dive:8', 'Fall:5', 'Grab:5', 'Eliminated:30', 'VictoryDance:8']

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'VERTEX'
scene.display.shading.show_shadows = True
scene.render.resolution_x = 480
scene.render.resolution_y = 480
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new('World') if scene.world is None else scene.world

# Tint the body (vertex alpha = tint mask) with a sample player colour for the preview.
mesh = bpy.data.objects['Bean'].data
attr = mesh.color_attributes['Col']
tint = (1.0, 0.45, 0.15)
for item in attr.data:
    r, g, b, a = item.color_srgb
    item.color_srgb = (r * (1 - a) + r * tint[0] * a, g * (1 - a) + g * tint[1] * a, b * (1 - a) + b * tint[2] * a, 1.0)

cam_data = bpy.data.cameras.new('PreviewCam')
cam = bpy.data.objects.new('PreviewCam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
target = Vector((0, 0, 0.8))
cam.location = Vector((1.6, -3.2, 1.6))
direction = target - cam.location
cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
cam_data.lens = 50

rig = bpy.data.objects['BeanRig']
rig.animation_data_create()

os.makedirs(bpy.path.abspath(out_dir), exist_ok=True)
for request in requests:
    name, frame = request.split(':')
    rig.animation_data.action = bpy.data.actions[name]
    scene.frame_set(int(frame))
    scene.render.filepath = os.path.join(bpy.path.abspath(out_dir), f'{name}_{frame}.png')
    bpy.ops.render.render(write_still=True)
    print(f'[preview] {scene.render.filepath}')
