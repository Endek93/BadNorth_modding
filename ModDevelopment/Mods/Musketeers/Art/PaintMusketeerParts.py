"""Paint the new uniform in the original character shader's 64×126 lookup space."""

from pathlib import Path

from PIL import Image, ImageDraw


SIZE = 4
art = Image.new("RGBA", (64 * SIZE, 126 * SIZE))
draw = ImageDraw.Draw(art)

felt_shadow = "#29343e"
felt = "#45596b"
felt_light = "#718294"
brim_binding = "#b3a281"
head_shadow = "#346881"
head_side = "#426b82"
head_front = "#517a91"
body = "#285069"


def polygon(points, color):
    draw.polygon([(round(x * SIZE), round(y * SIZE)) for x, y in points], fill=color)


def rect(x0, y0, x1, y1, color):
    draw.rectangle((int(x0 * SIZE), int(y0 * SIZE), int(x1 * SIZE) - 1, int(y1 * SIZE) - 1), fill=color)


# The large painted head runs to row 93. The small body and legs are sampled
# from the dark blue band below it, so their equipment is mounted separately.
rect(0, 94, 64, 126, body)
rect(0, 52, 64, 94, head_shadow)

# The lighter face sits beneath the hat and tapers early into the darker
# head, roughly where the stock archer's lighter headpiece tapers out.
polygon([(22, 22), (42, 22), (48, 29), (53, 41), (53, 47), (48, 51),
         (41, 53), (37, 62), (32, 54), (24, 52), (15, 51), (10, 47),
         (10, 41), (14, 32)], head_side)
polygon([(27, 25), (38, 24), (44, 32), (48, 42), (46, 47), (40, 50),
         (37, 60), (34, 52), (26, 50), (21, 46), (22, 41)], head_front)

# Raised side corners and a light binding make the folded brim read at game scale.
polygon([(6, 15), (17, 19), (24, 11), (31, 4), (38, 7), (46, 17),
         (58, 13), (55, 25), (45, 33), (33, 29), (21, 33), (10, 27)], felt_shadow)
polygon([(18, 22), (26, 13), (31, 7), (37, 9), (45, 20), (37, 24), (21, 25)], felt)
polygon([(31, 7), (37, 9), (45, 20), (39, 22), (35, 14)], felt_light)
polygon([(8, 17), (17, 21), (26, 20), (33, 23), (45, 19), (56, 15),
         (54, 19), (45, 23), (33, 27), (26, 24), (18, 25), (10, 21)], brim_binding)

destination = Path(__file__).resolve().parent.parent / "Assets" / "PartTex_Musketeer.png"
art.resize((64, 126), Image.Resampling.LANCZOS).save(destination)
