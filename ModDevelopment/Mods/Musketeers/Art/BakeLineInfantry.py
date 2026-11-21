"""Build the original line infantry model and bake its directional game sprites.

Run in Blender with --background --factory-startup --python BakeLineInfantry.py.
Pass -- --preview to render the standing model before baking the full atlas.
"""

import math
import sys
import tempfile
from array import array
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


ART = Path(__file__).resolve().parent
sys.path.insert(0, str(ART))
from MusketProp import build_musket

ASSETS = ART.parent / "Assets"
SIZE = 112
DIRECTIONS = 8
ORTHO_SIZE = 4.85
FRAME_WORLD_SIZE = 0.75

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.resolution_percentage = 100
scene.render.resolution_x = scene.render.resolution_y = SIZE
scene.view_settings.view_transform = "Standard"
scene.display.shading.light = "STUDIO"
scene.display.shading.studiolight_rotate_z = 0.2
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = False
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.cavity_ridge_factor = 0.35
scene.display.shading.cavity_valley_factor = 0.48


def material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*tuple((int(rgb[i:i+2], 16) / 255) ** 2.2 for i in (0, 2, 4)), 1)
    return mat


red = material("Brick red wool", "bb5748")
shadow_red = material("Dark red lining", "84392f")
cream = material("Parchment wool", "eed6ad")
webbing = material("Ivory webbing", "f0dcb9")
ink = material("Charcoal felt and leather", "403638")
boot_black = material("Boot leather", "3a302b")
gold = material("Dull brass", "ae8860")
skin = material("Face and hands", "e0a276")
hair = material("Brown hair", "513627")


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = parent
    return obj


turntable = empty("Directional view")
rig = empty("Line infantry", turntable)
torso = empty("Coat and head", rig)
gun = empty("Musket prop", rig)


def prepare(name, mat, parent):
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.data.materials.append(mat)
    return obj


def block(name, location, half_size, mat, parent, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=2, location=location)
    obj = prepare(name, mat, parent)
    for vertex in obj.data.vertices:
        for axis in range(3):
            vertex.co[axis] *= half_size[axis]
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
    return obj


def pebble(name, location, size, mat, parent):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=location)
    obj = prepare(name, mat, parent)
    for vertex in obj.data.vertices:
        for axis in range(3):
            vertex.co[axis] *= size[axis]
    return obj


def shape(name, vertices, faces, mat, parent, thickness=0):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.parent = parent
    mesh.materials.append(mat)
    if thickness:
        modifier = obj.modifiers.new("Thickness", "SOLIDIFY")
        modifier.thickness = thickness
    return obj


def tube(name, radius, mat, parent):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=1, location=(0, 0, 0))
    return prepare(name, mat, parent)


def segment(obj, start, end):
    a, b = Vector(start), Vector(end)
    direction = b - a
    obj.location = (a + b) * 0.5
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.scale.z = direction.length


block("Coat body", (0, .045, 1.84), (.44, .265, .50), red, torso, .12)
block("Coat tails", (0, .075, 1.19), (.45, .28, .28), red, torso, .09)
shape("Front coat lining", [(-.10, -.216, 1.01), (.12, -.216, 1.01), (.14, -.222, 1.39), (-.14, -.222, 1.39)],
      [(0, 1, 2, 3)], shadow_red, torso, .025)
shape("Cream waistcoat", [(-.19, -.230, 1.00), (.17, -.230, 1.00), (.18, -.249, 1.77),
                          (.12, -.250, 2.17), (-.13, -.250, 2.17), (-.22, -.250, 1.76)],
      [tuple(range(6))], cream, torso, .036)
for side in (-1, 1):
    shape("Cream lapel", [(side * .125, -.289, 2.185), (side * .33, -.23, 2.16),
                          (side * .22, -.293, 1.63), (side * .055, -.301, 1.59)],
          [(0, 1, 2, 3)], cream, torso, .042)
shape("Shoulder belt", [(-.35, -.214, 1.36), (-.25, -.278, 1.32), (.30, -.277, 2.23), (.19, -.241, 2.29)],
      [(0, 1, 2, 3)], webbing, torso, .045)
block("Belt buckle", (-.16, -.315, 1.57), (.075, .021, .055), gold, torso, .015)
block("Cartridge pouch silhouette", (-.44, .08, 1.46), (.15, .152, .18), boot_black, torso, .038)
for z in (1.24, 1.41, 1.80, 1.99):
    pebble("Button", (.113, -.296, z), (.043, .022, .043), gold, torso)

pebble("Hair", (0, .045, 2.63), (.39, .335, .36), hair, torso)
block("Head", (0, -.035, 2.60), (.325, .30, .322), skin, torso, .15)
pebble("Nose", (0, -.348, 2.60), (.083, .082, .074), skin, torso)
for side in (-1, 1):
    pebble("Ear", (side * .321, -.01, 2.58), (.073, .075, .091), skin, torso)
    block("Eye", (side * .134, -.324, 2.66), (.042, .015, .049), ink, torso, .009)
    block("Hair at temple", (side * .30, -.145, 2.72), (.055, .14, .143), hair, torso, .031)
pebble("Tied hair", (0, .383, 2.49), (.13, .15, .133), hair, torso)

# Three lifted corners form the hat silhouette. The crown and edging are independent pieces.
corners = [(0, -.55, 3.045), (.42, -.20, 2.965), (.57, .22, 3.12),
           (.29, .36, 2.995), (-.30, .36, 2.995), (-.57, .22, 3.12), (-.42, -.20, 2.965)]
inner = [(x * .56, y * .55, 2.89) for x, y, _ in corners]
shape("Tricorne brim", corners + inner,
      [(i, (i + 1) % len(corners), (i + 1) % len(corners) + len(corners), i + len(corners))
       for i in range(len(corners))], ink, torso, .075)
pebble("Tricorne crown", (0, .025, 2.98), (.335, .315, .203), ink, torso)
for i, point in enumerate(corners):
    edging = tube("Cream hat edging", .021, webbing, torso)
    segment(edging, point, corners[(i + 1) % len(corners)])

build_musket(gun)
muzzle = empty("Visual muzzle", gun)
muzzle.location = (0, 1.60, .080)

arms = []
legs = []
for side in (-1, 1):
    arms.append((tube("Upper coat sleeve", .165, red, rig), tube("Lower coat sleeve", .14, red, rig),
                 tube("Cream cuff", .153, cream, rig), pebble("Glove and hand", (0, 0, 0), (.119, .148, .111), skin, rig)))
    legs.append((tube("Trouser leg", .152, cream, rig), tube("Boot shaft", .132, boot_black, rig),
                 block("Boot", (0, 0, 0), (.157, .245, .105), boot_black, rig, .06)))


def animate(pose, frame=None):
    stride, lift = pose.get("stride", 0), pose.get("lift", 0)
    torsoshift = pose.get("shift", 0)
    torso.location = (0, torsoshift, lift)
    rig.location = (0, 0, .35 if pose.get("fallen") else 0)
    rig.rotation_euler.x = math.radians(-78) if pose.get("fallen") else 0
    dx, dy, dz = pose["grip"]
    gun.location = (dx, dy + torsoshift, dz + lift)
    yaw = math.radians(pose["yaw"])
    pitch = math.radians(pose["pitch"])
    heading = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    rotation = heading.to_track_quat("Y", "Z")
    gun.rotation_mode = "QUATERNION"
    gun.rotation_quaternion = rotation
    for idx, side in enumerate((-1, 1)):
        upper, forearm, cuff, hand = arms[idx]
        shoulder = Vector((side * .365, .025 + torsoshift, 2.12 + lift))
        grip = gun.location + rotation @ Vector((0, .69 if side == -1 else 0, -.084))
        if side == -1 and "free" in pose:
            grip = Vector(pose["free"]) + Vector((0, torsoshift, lift))
        elbow = (shoulder + grip) * .5 + Vector((side * .18, .17, -.24))
        segment(upper, shoulder, elbow)
        segment(forearm, elbow, grip)
        segment(cuff, grip.lerp(elbow, .28), grip.lerp(elbow, .07))
        hand.location = grip
        hand.rotation_mode = "QUATERNION"
        hand.rotation_quaternion = rotation

        pants, shin, foot = legs[idx]
        step = side * stride
        ground = max(0, side * pose.get("foot_lift", 0))
        knee = (side * .22, -.04 + step * .46, .64 + ground * .5)
        ankle = (side * .24, step, .16 + ground)
        segment(pants, (side * .175, .037 + torsoshift, 1.30 + lift), knee)
        segment(shin, knee, ankle)
        foot.location = (ankle[0], ankle[1] - .105, ground + .113)

    if frame is not None:
        objects = (rig, torso, gun, *(piece for limb in arms + legs for piece in limb))
        for obj in objects:
            obj.keyframe_insert(data_path="location", frame=frame)
            obj.keyframe_insert(data_path="rotation_quaternion" if obj.rotation_mode == "QUATERNION" else "rotation_euler", frame=frame)
            obj.keyframe_insert(data_path="scale", frame=frame)
    bpy.context.view_layer.update()


ready = dict(grip=(.29, -.29, 1.60), yaw=37, pitch=12)
aim = dict(grip=(.32, -.32, 1.93), yaw=8, pitch=1, shift=-.035)
poses = [
    ready,
    dict(ready, stride=.325, lift=.026, foot_lift=.08),
    dict(ready, stride=.055, lift=.067, foot_lift=.12),
    dict(ready, stride=-.325, lift=.026, foot_lift=-.08),
    dict(ready, stride=-.055, lift=.067, foot_lift=-.12),
    aim,
    dict(aim, grip=(.32, -.26, 1.98), pitch=10, shift=-.012),
    dict(grip=(.28, -.30, 1.63), yaw=27, pitch=-14),
    dict(grip=(.28, -.27, 1.47), yaw=43, pitch=-18, free=(-.03, -.38, 1.44)),
    dict(grip=(.28, -.26, 1.50), yaw=44, pitch=-16, free=(-.13, -.36, 1.67)),
    dict(grip=(.28, -.28, 1.62), yaw=36, pitch=-5),
    dict(ready, grip=(.30, -.30, 1.77), yaw=27, pitch=8),
    dict(ready, pitch=-15, fallen=True),
]

bpy.ops.object.camera_add(location=(0, -7.4, 5.35))
camera = bpy.context.object
camera.name = "Battle view sprite camera"
camera.rotation_euler = (Vector((0, 0, 1.44)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = ORTHO_SIZE
scene.camera = camera
bpy.context.view_layer.update()

if "--preview" in sys.argv:
    animate(ready)
    turntable.rotation_euler.z = math.pi / 4
    scene.render.resolution_x = scene.render.resolution_y = 600
    scene.render.filepath = str(ART / "LineInfantry-preview.png")
    bpy.ops.render.render(write_still=True)
    sys.exit()

foot = world_to_camera_view(scene, camera, Vector((0, 0, 0)))
if not (0.0 < foot.x < 1.0 and 0.0 < foot.y < 1.0):
    raise ValueError("The soldier's feet must project inside each sprite frame.")
atlas_width = SIZE * DIRECTIONS
atlas_height = SIZE * len(poses)
pixels = array("f", [0]) * (atlas_width * atlas_height * 4)
muzzles = []
with tempfile.TemporaryDirectory(dir=ART, prefix="musketeer_bake_") as folder:
    frame_path = Path(folder) / "frame.png"
    frame_pixels = array("f", [0]) * (SIZE * SIZE * 4)
    for row, pose in enumerate(poses):
        animate(pose)
        for direction in range(DIRECTIONS):
            turntable.rotation_euler.z = direction * math.tau / DIRECTIONS
            bpy.context.view_layer.update()
            if row == 6:
                point = world_to_camera_view(scene, camera, muzzle.matrix_world.translation)
                muzzles.append(((point.x - foot.x) * FRAME_WORLD_SIZE, (point.y - foot.y) * FRAME_WORLD_SIZE,
                                (point.z - foot.z) * FRAME_WORLD_SIZE / ORTHO_SIZE))
            scene.render.filepath = str(frame_path)
            bpy.ops.render.render(write_still=True)
            image = bpy.data.images.load(str(frame_path), check_existing=False)
            image.pixels.foreach_get(frame_pixels)
            for line in range(SIZE):
                start = ((row * SIZE + line) * atlas_width + direction * SIZE) * 4
                pixels[start:start + SIZE * 4] = frame_pixels[line * SIZE * 4:(line + 1) * SIZE * 4]
            bpy.data.images.remove(image)

atlas = bpy.data.images.new("Line infantry directional poses", width=atlas_width, height=atlas_height, alpha=True)
atlas.pixels.foreach_set(pixels)
atlas.filepath_raw = str(ASSETS / "LineInfantry.png")
atlas.file_format = "PNG"
atlas.save()
with (ASSETS / "LineInfantryMuzzles.txt").open("w", encoding="ascii") as metadata:
    metadata.write(f"{foot.x:.6f} {foot.y:.6f} {FRAME_WORLD_SIZE:.6f}\n")
    for point in muzzles:
        metadata.write(" ".join(f"{value:.6f}" for value in point) + "\n")

turntable.rotation_euler.z = math.pi / 4
timeline = [(1, 0), (9, 1), (15, 2), (21, 3), (27, 4), (33, 0), (42, 5), (46, 6),
            (49, 7), (58, 8), (67, 9), (77, 10), (86, 11), (91, 5)]
for frame, pose in timeline:
    animate(poses[pose], frame)
scene.frame_end = 91
scene.frame_set(1)
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.filepath = "//LineInfantry.png"
bpy.data.images.remove(atlas)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "LineInfantry.blend"))
