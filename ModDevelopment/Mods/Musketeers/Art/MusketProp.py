"""Build the stylized musket silhouette used by the game and the Blender rig."""

import math

import bpy


def material(name, color):
    existing = bpy.data.materials.get(name)
    if existing:
        return existing
    result = bpy.data.materials.new(name)
    channels = tuple((int(color[i:i + 2], 16) / 255) ** 2.2 for i in (0, 2, 4))
    result.diffuse_color = (*channels, 1.0)
    return result


def build_musket(gun):
    wood = material("Warm walnut stock", "97664a")
    iron = material("Muted iron", "535d64")
    bands = material("Worn bands", "969188")

    def mesh(name, vertices, faces, surface):
        data = bpy.data.meshes.new(name)
        data.from_pydata(vertices, [], faces)
        data.materials.append(surface)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.parent = gun

    # An uninterrupted wood shape connects the curved butt to the muzzle.
    sections = [
        (-.73, -.203, -.040, .072),
        (-.65, -.214, .022, .101),
        (-.43, -.150, .052, .086),
        (-.24, -.088, .068, .066),
        (-.02, -.096, .077, .081),
        (.31, -.078, .086, .078),
        (.81, -.053, .087, .064),
        (1.21, -.039, .083, .056),
        (1.48, -.028, .079, .050),
    ]
    stock_vertices = []
    for y, bottom, top, half_width in sections:
        bevel = (top - bottom) * .18
        profile = [
            (-.68, bottom), (.68, bottom), (1, bottom + bevel), (1, top - bevel),
            (.68, top), (-.68, top), (-1, top - bevel), (-1, bottom + bevel),
        ]
        stock_vertices.extend((x * half_width, y, z) for x, z in profile)
    stock_faces = [tuple(range(8))]
    for ring in range(len(sections) - 1):
        start = ring * 8
        for i in range(8):
            a, b = start + i, start + (i + 1) % 8
            stock_faces.append((a, a + 8, b + 8, b))
    stock_faces.append(tuple(range(len(stock_vertices) - 1, len(stock_vertices) - 9, -1)))
    mesh("Continuous walnut stock", stock_vertices, stock_faces, wood)

    def tube(name, start, end, radius, surface):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=8, radius=radius, depth=end - start,
            location=(0, (start + end) / 2, .080), rotation=(math.pi / 2, 0, 0),
        )
        obj = bpy.context.object
        obj.name = name
        obj.parent = gun
        obj.data.materials.append(surface)

    tube("Slim barrel silhouette", -.11, 1.60, .044, iron)
    for y, radius in ((.43, .084), (.91, .073), (1.40, .062)):
        tube("Stock band", y - .028, y + .028, radius, bands)

    def raised_shape(name, outline, inner_x, outer_x, surface):
        count = len(outline)
        vertices = [(x, y, z) for x in (inner_x, outer_x) for y, z in outline]
        faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, 2 * count))]
        for i in range(count):
            j = (i + 1) % count
            faces.append((i, j, j + count, i + count))
        mesh(name, vertices, faces, surface)

    raised_shape("Lock plate silhouette",
                 [(-.22, -.043), (.10, -.034), (.18, .022), (.09, .052), (-.19, .040), (-.24, .010)],
                 .055, .093, iron)
    raised_shape("Raised flintlock silhouette",
                 [(-.14, .031), (-.057, .043), (-.065, .106), (.015, .150),
                  (.023, .188), (-.080, .171), (-.127, .113)],
                 .022, .091, iron)
