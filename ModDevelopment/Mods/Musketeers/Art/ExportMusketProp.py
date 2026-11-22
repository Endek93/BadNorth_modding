"""Export the authored line infantry musket as a small Unity-ready mesh resource.

Open LineInfantry.blend using Blender and run this script in background mode.
Pass -- --rebuild-prop to replace the prop in the rig before exporting.
"""

import struct
import sys
from pathlib import Path

import bpy


gun = bpy.data.objects["Musket prop"]
muzzle = bpy.data.objects["Visual muzzle"]
rebuild = "--rebuild-prop" in sys.argv
if rebuild:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from MusketProp import MUZZLE_POSITION, build_musket

    for child in list(gun.children):
        if child.type == "MESH":
            bpy.data.objects.remove(child, do_unlink=True)
    build_musket(gun)
    muzzle.location = MUZZLE_POSITION
    bpy.context.view_layer.update()

relative = gun.matrix_world.inverted()
view = bpy.context.evaluated_depsgraph_get()
scale = 0.145


def unity(point):
    return point.x * scale, point.z * scale, point.y * scale


def srgb(value):
    return round(255 * min(1, max(0, value)) ** (1 / 2.2))


palette = []
triangles = []
for obj in gun.children:
    if obj.type != "MESH":
        continue
    evaluated = obj.evaluated_get(view)
    mesh = evaluated.to_mesh()
    try:
        mesh.calc_loop_triangles()
        local = relative @ obj.matrix_world
        for face in mesh.loop_triangles:
            material = obj.data.materials[face.material_index]
            color = tuple(srgb(value) for value in material.diffuse_color[:3])
            if color not in palette:
                palette.append(color)
            index = palette.index(color)

            # Swapping the vertical and forward axes reverses triangle winding.
            for vertex in reversed(face.vertices):
                triangles.append((*unity(local @ mesh.vertices[vertex].co), index))
    finally:
        evaluated.to_mesh_clear()

destination = Path(__file__).resolve().parent.parent / "Assets" / "MusketeerMusket.bytes"
with destination.open("wb") as output:
    output.write(struct.pack("<3fii", *unity(relative @ muzzle.matrix_world.translation), len(palette), len(triangles)))
    for color in palette:
        output.write(bytes(color))
    for vertex in triangles:
        output.write(struct.pack("<3fB", *vertex))

print(f"Exported {len(triangles) // 3} original musket triangles and {len(palette)} palette colors.")
if rebuild:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
