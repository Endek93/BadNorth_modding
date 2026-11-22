"""Build the stylized musket silhouette used by the game and the Blender rig."""

import math

import bpy


MUZZLE_POSITION = (0, 1.79, .080)


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
    wood_shadow = material("Shaded walnut bevel", "774d38")
    iron = material("Muted iron", "535d64")
    bands = material("Worn bands", "969188")
    brass = material("Worn brass furniture", "ac8856")

    def mesh(name, vertices, faces, surface):
        data = bpy.data.meshes.new(name)
        data.from_pydata(vertices, [], faces)
        data.materials.append(surface)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.parent = gun
        return data

    def stock_mesh(name, sections, surface):
        vertices = []
        for y, bottom, top, half_width in sections:
            bevel = (top - bottom) * .18
            profile = [
                (-.68, bottom), (.68, bottom), (1, bottom + bevel), (1, top - bevel),
                (.68, top), (-.68, top), (-1, top - bevel), (-1, bottom + bevel),
            ]
            vertices.extend((x * half_width, y, z) for x, z in profile)
        faces = [tuple(range(8))]
        for ring in range(len(sections) - 1):
            start = ring * 8
            for i in range(8):
                a, b = start + i, start + (i + 1) % 8
                faces.append((a, a + 8, b + 8, b))
        faces.append(tuple(range(len(vertices) - 1, len(vertices) - 9, -1)))
        return mesh(name, vertices, faces, surface)

    # An uninterrupted wood shape connects the curved butt to the muzzle.
    sections = [
        (-.75, -.205, .023, .090),
        (-.65, -.215, .036, .101),
        (-.43, -.163, .057, .086),
        (-.24, -.088, .068, .066),
        (-.02, -.096, .077, .081),
        (.31, -.078, .086, .078),
        (.90, -.053, .087, .064),
        (1.36, -.039, .083, .056),
        (1.66, -.028, .079, .050),
    ]
    stock = stock_mesh("Continuous walnut stock", sections, wood)
    stock.materials.append(wood_shadow)
    for ring in range(len(sections) - 1):
        for side in (0, 1, 7):
            stock.polygons[1 + ring * 8 + side].material_index = 1

    stock_mesh("Beveled brass butt plate", [
        (-.79, -.187, .018, .080),
        (-.772, -.213, .034, .095),
        (-.725, -.215, .035, .099),
    ], brass)

    def tube(name, start, end, radius, surface):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=8, radius=radius, depth=end - start,
            location=(0, (start + end) / 2, .080), rotation=(math.pi / 2, 0, 0),
        )
        obj = bpy.context.object
        obj.name = name
        obj.parent = gun
        obj.data.materials.append(surface)

    tube("Slim barrel silhouette", -.11, MUZZLE_POSITION[1], .044, iron)
    for y, radius in ((.44, .084), (.98, .073), (1.57, .062)):
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

    # Keep an opening in the guard so it reads as a shape at the game's camera distance.
    outer = [(-.30, -.070), (-.28, -.147), (-.18, -.208), (-.035, -.213), (.098, -.156), (.152, -.075)]
    inner = [(-.249, -.074), (-.235, -.124), (-.163, -.168), (-.039, -.172), (.066, -.124), (.103, -.076)]
    guard_vertices = [(x, y, z) for x in (-.029, .029) for outline in (outer, inner) for y, z in outline]
    count = len(outer)
    guard_faces = []
    for i in range(count):
        j = (i + 1) % count
        guard_faces.extend([
            (i, i + count, j + count, j),
            (i + 2 * count, j + 2 * count, j + 3 * count, i + 3 * count),
            (i, j, j + 2 * count, i + 2 * count),
            (i + count, i + 3 * count, j + 3 * count, j + count),
        ])
    mesh("Open brass trigger guard", guard_vertices, guard_faces, brass)
    raised_shape("Dark accent inside guard",
                 [(-.10, -.082), (-.109, -.118), (-.088, -.153),
                  (-.062, -.145), (-.073, -.125), (-.048, -.082)],
                 -.014, .014, iron)
