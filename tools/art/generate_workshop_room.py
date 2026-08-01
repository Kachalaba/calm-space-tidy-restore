#!/usr/bin/env python3
"""Generate the original cozy-workshop room art for Calm Space.

The room is authored once as a single master painting. Every restored state is
a *local edit of that same master*: the shared background is painted once, each
of the eight beat zones is re-painted only inside its own non-overlapping
rectangle, and the exported states are literal crops of those renders. Camera,
geometry, scale, crop, palette and every unrelated zone are therefore identical
by construction rather than by eye.

Outputs (all original, procedurally painted, no third-party source material):

    Room/WorkshopBaseDirty.png      1080x2400 master, dusty state
    Room/Beat0N<Name>.png           restored patch for one zone
    Room/Beat0N<Name>Before.png     dusty patch for the same zone
    Room/FinalSunlight.png          full-canvas warm finale wash (RGBA)
    workshop-room-manifest.json     zone rectangles consumed by the builder

Usage:
    python3 tools/art/generate_workshop_room.py [--out <assets-dir>]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from workshop_paint import (  # noqa: E402
    BRASS, CLAY, CLAY_DEEP, CLAY_PALE, CREAM, CREAM_DEEP, DUST, DUST_DEEP,
    FLOOR, FLOOR_DEEP, GLASS, HEIGHT, SAGE, SAGE_DEEP, SAGE_PALE, SHADOW, SKY,
    SKY_PALE, SUN, WALL, WALL_COOL, WALL_DEEP, WIDTH, WOOD, WOOD_DEEP,
    WOOD_PALE, WOOD_WARM, blur, cast_shadow, ellipse_mask, fbm, form_shade,
    gouache, hand_cut, line_mask, over, paper_finish, paper_lip, place,
    polygon_mask, rgb, rounded_rect_mask, shift, tint, to_image, top_light,
    vertical_gradient, wood_grain,
)

# ---------------------------------------------------------------------------
# Zone layout. Rectangles are (x0, y0, x1, y1) in the 1080x2400 reference
# composition and are asserted to be pairwise disjoint, which is what makes an
# arbitrary subset of restored patches compose back into a coherent room.
# ---------------------------------------------------------------------------

ZONES = [
    ("Beat01ClearedWalkway", "cozy-workshop.clear-passage", (25, 1600, 430, 2005)),
    ("Beat02PebbleShelf", "cozy-workshop.pebble-shelf", (45, 505, 365, 770)),
    ("Beat03TeaDrawer", "cozy-workshop.tea-drawer", (45, 820, 365, 1185)),
    ("Beat04PaintShelf", "cozy-workshop.paint-shelf", (590, 790, 1045, 1015)),
    ("Beat05ToolTray", "cozy-workshop.fastener-tray", (395, 1170, 825, 1335)),
    ("Beat06WarmWorkbench", "cozy-workshop.warm-workbench", (20, 1345, 785, 1565)),
    ("Beat07RepairedCabinet", "cozy-workshop.cabinet-hinge", (700, 1575, 1055, 1985)),
    ("Beat08ClearWindow", "cozy-workshop.open-window", (590, 190, 1045, 755)),
]

FLOOR_LINE = 1505


def assert_disjoint() -> None:
    for i in range(len(ZONES)):
        for j in range(i + 1, len(ZONES)):
            ax0, ay0, ax1, ay1 = ZONES[i][2]
            bx0, by0, bx1, by1 = ZONES[j][2]
            overlap = not (ax1 <= bx0 or bx1 <= ax0 or ay1 <= by0 or by1 <= ay0)
            if overlap:
                raise SystemExit(
                    f"zones {ZONES[i][0]} and {ZONES[j][0]} overlap")


FEATHER = 26


def rect_mask_of(rect) -> np.ndarray:
    x0, y0, x1, y1 = rect
    mask = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    mask[y0:y1, x0:x1] = 1.0
    return mask


def feathered_rect_of(rect, feather: int = FEATHER) -> np.ndarray:
    """A rectangle mask that fades to zero before it reaches its own edge.

    Zone paint is blended through this mask so that every variant of a zone is
    exactly the shared background at the rectangle boundary. Loose shadow and
    dust spill therefore cannot change a single pixel of a neighbouring zone.
    """
    x0, y0, x1, y1 = rect
    inner = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    inner[y0 + feather:y1 - feather, x0 + feather:x1 - feather] = 1.0
    mask = blur(inner, feather * 0.55)
    mask[:y0, :] = 0.0
    mask[y1:, :] = 0.0
    mask[:, :x0] = 0.0
    mask[:, x1:] = 0.0
    return mask


# ---------------------------------------------------------------------------
# Shared background: walls, floor, skirting, bench frame. Painted once.
# ---------------------------------------------------------------------------


def paint_background() -> np.ndarray:
    canvas = np.zeros((HEIGHT, WIDTH, 3), dtype=np.float32)

    # Plaster wall with a gentle warm falloff toward the lower corners.
    wall = gouache(WALL, 101, mottle=0.085, cell=210)
    wall_shade = gouache(WALL_DEEP, 137, mottle=0.06, cell=160)
    ramp = np.linspace(0.0, 1.0, HEIGHT, dtype=np.float32)[:, None, None]
    canvas[:] = wall * (1.0 - ramp * 0.55) + wall_shade * (ramp * 0.55)

    # Cool bounce along the left wall edge keeps the sage in the room.
    left = np.clip(1.0 - np.linspace(0.0, 1.0, WIDTH, dtype=np.float32) * 3.2,
                   0.0, 1.0)[None, :]
    tint(canvas, np.repeat(left, HEIGHT, axis=0) * 0.55, WALL_COOL, 0.5)

    # Warm light pooling in from the upper-right window opening.
    glow = ellipse_mask(300, -420, 1500, 1100)
    tint(canvas, blur(glow, 130.0) * 0.62, SUN, 0.30)

    # Faint plaster panel seams give the wall a built, aged feeling.
    for seam_x in (250.0, 512.0, 830.0):
        seam = line_mask([(seam_x, 0.0), (seam_x - 6.0, FLOOR_LINE)], 3.0)
        tint(canvas, blur(seam, 2.6) * 0.22, WALL_DEEP, 0.6)

    # Floor: receding warm planks.
    floor_mask = rounded_rect_mask(-40, FLOOR_LINE, WIDTH + 40, HEIGHT + 40)
    floor_paint = wood_grain(FLOOR, 211, horizontal=True, strength=0.09)
    depth = np.clip(
        (np.arange(HEIGHT, dtype=np.float32) - FLOOR_LINE) / 620.0, 0.0, 1.0)
    floor_paint = floor_paint * (0.88 + depth[:, None, None] * 0.18)
    over(canvas, floor_mask, np.clip(floor_paint, 0.0, 1.0))

    # Plank joins fan outward for a soft 2.5D read.
    for index in range(-3, 8):
        top_x = 540.0 + index * 96.0
        bottom_x = 540.0 + index * 198.0
        join = line_mask([(top_x, FLOOR_LINE), (bottom_x, HEIGHT + 30)], 4.0)
        tint(canvas, blur(join, 2.2) * floor_mask * 0.30, FLOOR_DEEP, 0.55)

    # Contact shadow where the wall meets the floor.
    junction = rounded_rect_mask(-40, FLOOR_LINE - 6, WIDTH + 40,
                                 FLOOR_LINE + 96)
    tint(canvas, blur(junction, 34.0) * 0.66, SHADOW, 0.34)

    # Skirting board.
    skirting = hand_cut(
        rounded_rect_mask(-30, FLOOR_LINE - 44, WIDTH + 30, FLOOR_LINE + 8, 6),
        303)
    place(canvas, skirting, wood_grain(WOOD_DEEP, 307, strength=0.07),
          shadow_dy=9, shadow_radius=13.0, shadow_strength=0.26,
          lip_strength=0.28)

    # Workbench frame: legs and stretcher live behind every zone repaint.
    frame_paint = wood_grain(WOOD_DEEP, 331, horizontal=False, strength=0.10)
    for leg_x in (92.0, 606.0):
        leg = hand_cut(
            rounded_rect_mask(leg_x, 1548, leg_x + 74, 1904, 9), 341)
        place(canvas, leg, frame_paint, shadow_dx=9, shadow_dy=14,
              shadow_radius=19.0, shadow_strength=0.30, shade_strength=0.30)
    stretcher = hand_cut(
        rounded_rect_mask(120, 1786, 660, 1834, 8), 349)
    place(canvas, stretcher, frame_paint, shadow_dy=10, shadow_radius=14.0,
          shadow_strength=0.24, shade_strength=0.26)

    # Static peg rail above the bench, hung with the tools of the house.
    rail = hand_cut(rounded_rect_mask(400, 1078, 820, 1104, 8), 353)
    place(canvas, rail, wood_grain(WOOD, 359, strength=0.08),
          shadow_dy=9, shadow_radius=12.0, shadow_strength=0.24)
    for peg_x in (444.0, 560.0, 676.0, 780.0):
        peg = hand_cut(rounded_rect_mask(peg_x, 1100, peg_x + 15, 1136, 6), 367)
        place(canvas, peg, gouache(WOOD_WARM, 371, mottle=0.05, cell=40),
              shadow_dy=7, shadow_radius=8.0, shadow_strength=0.22, lip=False)

    saw_blade = hand_cut(polygon_mask([
        (426, 1112), (498, 1112), (486, 1166), (420, 1160)]), 377)
    place(canvas, saw_blade, gouache(DUST, 379, mottle=0.05, cell=34),
          shadow_dx=5, shadow_dy=9, shadow_radius=11.0, shadow_strength=0.26,
          highlight=True)
    saw_grip = hand_cut(rounded_rect_mask(414, 1104, 452, 1128, 8), 383)
    place(canvas, saw_grip, gouache(CLAY_DEEP, 385, mottle=0.05, cell=20),
          shadow_dy=5, shadow_radius=6.0, shadow_strength=0.22)

    mallet_head = hand_cut(rounded_rect_mask(534, 1108, 598, 1152, 9), 389)
    place(canvas, mallet_head, wood_grain(WOOD_WARM, 391, strength=0.09),
          shadow_dx=5, shadow_dy=9, shadow_radius=11.0, shadow_strength=0.26,
          highlight=True)
    mallet_grip = hand_cut(rounded_rect_mask(556, 1146, 576, 1166, 7), 393)
    place(canvas, mallet_grip, gouache(WOOD_DEEP, 395, mottle=0.05, cell=22),
          shadow_dy=6, shadow_radius=7.0, shadow_strength=0.22)

    twine = hand_cut(ellipse_mask(648, 1108, 712, 1164), 397)
    place(canvas, twine, gouache(CREAM_DEEP, 399, mottle=0.09, cell=26),
          shadow_dx=5, shadow_dy=9, shadow_radius=11.0, shadow_strength=0.26,
          highlight=True)
    twine_eye = hand_cut(ellipse_mask(668, 1126, 692, 1146), 401)
    tint(canvas, twine_eye * 0.85, WOOD_DEEP, 0.40)

    apron_cloth = hand_cut(polygon_mask([
        (766, 1104), (812, 1104), (818, 1160), (760, 1162)]), 403)
    place(canvas, apron_cloth, gouache(SAGE_PALE, 405, mottle=0.10, cell=30),
          shadow_dx=5, shadow_dy=9, shadow_radius=10.0, shadow_strength=0.24,
          highlight=True)
    for stripe in range(3):
        band = rounded_rect_mask(762, 1120 + stripe * 14, 816,
                                 1125 + stripe * 14)
        tint(canvas, band * apron_cloth * 0.8, SAGE_DEEP, 0.35)

    # Hanging plant fills the quiet wall left of the window.
    hook = hand_cut(rounded_rect_mask(196, 214, 216, 252, 5), 407)
    place(canvas, hook, gouache(WOOD_DEEP, 409, mottle=0.04, cell=16),
          shadow_dy=5, shadow_radius=6.0, shadow_strength=0.22, lip=False)
    for cord_x in (172.0, 240.0):
        cord = line_mask([(206, 244), (cord_x, 350)], 4.0)
        tint(canvas, blur(cord, 1.2) * 0.85, WOOD_DEEP, 0.55)
    pot = hand_cut(polygon_mask([
        (158, 344), (254, 344), (240, 416), (172, 416)]), 411)
    place(canvas, pot, gouache(CLAY, 413, mottle=0.08, cell=34),
          shadow_dx=6, shadow_dy=11, shadow_radius=14.0, shadow_strength=0.28,
          highlight=True)
    trail = [((196, 408), (150, 452), 7.0), ((206, 410), (232, 470), 6.0),
             ((216, 406), (272, 440), 5.0)]
    for (sx, sy), (ex, ey), thickness in trail:
        vine = line_mask([(sx, sy), ((sx + ex) / 2 - 14, (sy + ey) / 2),
                          (ex, ey)], thickness)
        place(canvas, hand_cut(vine, 417, jitter=0.03),
              gouache(SAGE_DEEP, 419, mottle=0.06, cell=26),
              shadow=False, shade=False, lip=False)
        for step in range(3):
            t = 0.3 + step * 0.28
            lx = sx + (ex - sx) * t - 12
            ly = sy + (ey - sy) * t - 8
            leaf = hand_cut(ellipse_mask(lx, ly, lx + 26, ly + 17), 421 + step)
            place(canvas, leaf, gouache(SAGE, 423 + step, mottle=0.07,
                                        cell=22),
                  shadow=False, shade=False, lip_strength=0.26)

    # A pieced quilt square: the house remembers the family that built it.
    quilt_frame = hand_cut(rounded_rect_mask(398, 300, 566, 512, 8), 431)
    place(canvas, quilt_frame, wood_grain(WOOD_DEEP, 433, strength=0.07),
          shadow_dx=7, shadow_dy=12, shadow_radius=15.0, shadow_strength=0.28,
          highlight=True)
    quilt_face = hand_cut(rounded_rect_mask(414, 316, 550, 496, 5), 435)
    place(canvas, quilt_face, gouache(CREAM, 437, mottle=0.07, cell=50),
          shadow=False, shade_strength=0.14, lip_strength=0.30)
    patch_colors = [SAGE, CLAY_PALE, CREAM_DEEP, SAGE_PALE, CLAY, SAGE_DEEP,
                    CLAY_PALE, CREAM_DEEP, SAGE]
    for row in range(3):
        for col in range(3):
            x0 = 420.0 + col * 44.0
            y0 = 322.0 + row * 58.0
            patch = hand_cut(
                rounded_rect_mask(x0, y0, x0 + 38, y0 + 52, 4),
                441 + row * 3 + col, jitter=0.04)
            place(canvas, patch,
                  gouache(patch_colors[row * 3 + col], 451 + row * 3 + col,
                          mottle=0.09, cell=26),
                  shadow=False, shade_strength=0.12, lip_strength=0.26)

    # A worn stool tucked under the bench keeps the floor from reading empty.
    stool_seat = hand_cut(ellipse_mask(448, 1652, 592, 1716), 461)
    place(canvas, stool_seat, wood_grain(WOOD, 463, strength=0.09),
          shadow_dx=8, shadow_dy=14, shadow_radius=18.0, shadow_strength=0.30,
          highlight=True)
    for leg_x, lean in ((466.0, -14.0), (556.0, 14.0)):
        stool_leg = hand_cut(polygon_mask([
            (leg_x, 1700), (leg_x + 22, 1700),
            (leg_x + 22 + lean, 1892), (leg_x + lean, 1892)]), 467)
        place(canvas, stool_leg, gouache(WOOD_DEEP, 469, mottle=0.05, cell=30),
              shadow_dx=6, shadow_dy=10, shadow_radius=12.0,
              shadow_strength=0.26, lip=False)

    # Depth: warm light pooling toward the window side, cool falloff opposite.
    corner = np.clip(
        1.0 - np.linspace(0.0, 1.0, WIDTH, dtype=np.float32) * 1.9, 0.0, 1.0)
    depth_ramp = np.clip(
        np.linspace(-0.35, 1.0, HEIGHT, dtype=np.float32), 0.0, 1.0)
    tint(canvas, corner[None, :] * depth_ramp[:, None] * 0.34, SHADOW, 0.40)

    edge = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    edge[:] = 1.0
    edge[70:HEIGHT - 70, 70:WIDTH - 70] = 0.0
    tint(canvas, blur(edge, 110.0) * 0.55, SHADOW, 0.26)

    return canvas


# ---------------------------------------------------------------------------
# Zone painters. Each one draws only inside its own rectangle.
# ---------------------------------------------------------------------------


def zone_clear_passage(canvas: np.ndarray, restored: bool) -> None:
    if restored:
        rug = hand_cut(polygon_mask(
            [(86, 1806), (394, 1782), (416, 1962), (52, 1978)]), 401,
            jitter=0.07)
        place(canvas, rug, gouache(CREAM_DEEP, 405, mottle=0.10, cell=120),
              shadow_dy=8, shadow_radius=16.0, shadow_strength=0.22,
              shade_strength=0.14, lip_strength=0.30)
        for index in range(5):
            y = 1800.0 + index * 36.0
            stripe = line_mask([(64, y + 6), (404, y - 8)], 7.0)
            tint(canvas, blur(stripe, 1.8) * rug * 0.55, SAGE, 0.42)

        stack = [
            (96, 1636, 300, 1748, CLAY, 411),
            (112, 1742, 286, 1826, SAGE, 419),
        ]
        for x0, y0, x1, y1, color, seed in stack:
            box = hand_cut(rounded_rect_mask(x0, y0, x1, y1, 16), seed)
            place(canvas, box, gouache(color, seed + 3, mottle=0.09, cell=90),
                  shadow_dx=8, shadow_dy=13, shadow_radius=17.0,
                  shadow_strength=0.30, highlight=True)
            band = hand_cut(
                rounded_rect_mask(x0 + 22, (y0 + y1) / 2 - 9,
                                  x1 - 22, (y0 + y1) / 2 + 9, 8), seed + 11)
            tint(canvas, band * 0.85, CREAM, 0.55)
        return

    tumble = [
        (60, 1690, 268, 1812, CLAY_DEEP, 421, -0.05),
        (206, 1744, 400, 1876, SAGE_DEEP, 431, 0.06),
        (78, 1846, 250, 1958, DUST_DEEP, 439, 0.03),
    ]
    for x0, y0, x1, y1, color, seed, skew in tumble:
        shear = (y1 - y0) * skew
        box = hand_cut(polygon_mask([
            (x0 + shear, y0), (x1 + shear, y0 - shear * 0.6),
            (x1, y1), (x0, y1 + shear * 0.4)]), seed, jitter=0.075)
        place(canvas, box, gouache(color, seed + 5, mottle=0.10, cell=80),
              shadow_dx=9, shadow_dy=15, shadow_radius=19.0,
              shadow_strength=0.34, shade_strength=0.30)

    cloth = hand_cut(polygon_mask([
        (250, 1900), (404, 1874), (418, 1968), (232, 1974)]), 447, jitter=0.10)
    place(canvas, cloth, gouache(DUST, 451, mottle=0.12, cell=70),
          shadow_dy=9, shadow_radius=14.0, shadow_strength=0.24,
          lip_strength=0.24)
    grime = blur(ellipse_mask(40, 1900, 430, 2000), 24.0)
    tint(canvas, grime * 0.44, DUST_DEEP, 0.34)


def _shelf_board(canvas: np.ndarray, x0, y0, x1, y1, seed) -> np.ndarray:
    board = hand_cut(rounded_rect_mask(x0, y0, x1, y1, 7), seed)
    place(canvas, board, wood_grain(WOOD, seed + 1, strength=0.09),
          shadow_dx=6, shadow_dy=14, shadow_radius=18.0, shadow_strength=0.30,
          highlight=True)
    for bracket_x in (x0 + 34.0, x1 - 60.0):
        bracket = hand_cut(polygon_mask([
            (bracket_x, y1), (bracket_x + 26, y1),
            (bracket_x + 26, y1 + 46)]), seed + 7)
        place(canvas, bracket, gouache(WOOD_DEEP, seed + 9, mottle=0.05,
                                       cell=40),
              shadow_dy=8, shadow_radius=10.0, shadow_strength=0.24, lip=False)
    return board


def zone_pebble_shelf(canvas: np.ndarray, restored: bool) -> None:
    _shelf_board(canvas, 62, 700, 350, 730, 501)

    if restored:
        dish = hand_cut(ellipse_mask(76, 654, 176, 704), 505)
        place(canvas, dish, gouache(CLAY_PALE, 509, mottle=0.07, cell=50),
              shadow_dy=8, shadow_radius=11.0, shadow_strength=0.26,
              highlight=True)
        pairs = [(196, 676, 20), (232, 678, 17), (268, 674, 21),
                 (300, 677, 16), (110, 646, 15), (140, 648, 13)]
        for cx, cy, r in pairs:
            stone = hand_cut(
                ellipse_mask(cx - r, cy - r * 0.82, cx + r, cy + r * 0.82), 511)
            place(canvas, stone,
                  gouache(SAGE_PALE if r % 2 else CREAM, 513 + r,
                          mottle=0.09, cell=30),
                  shadow_dx=4, shadow_dy=6, shadow_radius=7.0,
                  shadow_strength=0.28, highlight=True, lip_strength=0.36)
        sprig = line_mask([(320, 700), (330, 652), (344, 620)], 5.0)
        place(canvas, sprig, gouache(SAGE_DEEP, 521, mottle=0.05, cell=24),
              shadow=False, shade=False, lip=False)
        for leaf_x, leaf_y in ((330, 654), (338, 630), (322, 668)):
            leaf = hand_cut(
                ellipse_mask(leaf_x - 16, leaf_y - 8, leaf_x + 6, leaf_y + 8),
                523)
            place(canvas, leaf, gouache(SAGE, 527, mottle=0.06, cell=22),
                  shadow=False, shade=False, lip_strength=0.26)
        return

    scatter = [(120, 682, 18), (168, 676, 14), (206, 684, 20),
               (286, 680, 15), (336, 706, 17)]
    for cx, cy, r in scatter:
        stone = hand_cut(
            ellipse_mask(cx - r, cy - r * 0.8, cx + r, cy + r * 0.8), 531)
        place(canvas, stone, gouache(DUST_DEEP, 533 + r, mottle=0.08, cell=28),
              shadow_dx=5, shadow_dy=8, shadow_radius=9.0,
              shadow_strength=0.30, shade_strength=0.28, lip_strength=0.20)
    film = blur(rounded_rect_mask(56, 640, 356, 748), 20.0)
    tint(canvas, film * 0.48, DUST, 0.32)


def zone_tea_drawer(canvas: np.ndarray, restored: bool) -> None:
    carcass = hand_cut(rounded_rect_mask(62, 902, 350, 1172, 12), 601)
    place(canvas, carcass, wood_grain(WOOD, 605, horizontal=False,
                                      strength=0.09),
          shadow_dx=9, shadow_dy=15, shadow_radius=20.0, shadow_strength=0.32,
          highlight=True)

    if restored:
        for index in range(3):
            y0 = 916.0 + index * 84.0
            drawer = hand_cut(
                rounded_rect_mask(78, y0, 334, y0 + 72, 8), 611 + index)
            place(canvas, drawer,
                  gouache(WOOD_WARM, 617 + index, mottle=0.06, cell=70),
                  shadow_dy=7, shadow_radius=9.0, shadow_strength=0.22,
                  shade_strength=0.18, highlight=True)
            pull = hand_cut(
                rounded_rect_mask(178, y0 + 30, 234, y0 + 44, 7), 623)
            place(canvas, pull, gouache(BRASS, 629, mottle=0.05, cell=24),
                  shadow_dy=5, shadow_radius=6.0, shadow_strength=0.24,
                  highlight=True, lip_strength=0.5)

        pot = hand_cut(ellipse_mask(96, 830, 196, 900), 631)
        place(canvas, pot, gouache(CLAY, 637, mottle=0.08, cell=44),
              shadow_dy=9, shadow_radius=12.0, shadow_strength=0.28,
              highlight=True)
        spout = hand_cut(polygon_mask([
            (190, 848), (228, 836), (232, 850), (194, 868)]), 641)
        place(canvas, spout, gouache(CLAY_DEEP, 643, mottle=0.05, cell=24),
              shadow=False, lip=False)
        lid = hand_cut(ellipse_mask(126, 812, 166, 836), 647)
        place(canvas, lid, gouache(CLAY_PALE, 653, mottle=0.05, cell=20),
              shadow=False, shade=False, lip_strength=0.4)
        for index, color in enumerate((SAGE, CREAM, SAGE_PALE)):
            x0 = 236.0 + index * 38.0
            tin = hand_cut(rounded_rect_mask(x0, 842, x0 + 32, 900, 6), 659)
            place(canvas, tin, gouache(color, 661 + index, mottle=0.06,
                                       cell=26),
                  shadow_dy=7, shadow_radius=8.0, shadow_strength=0.26,
                  highlight=True)
        return

    for index in range(3):
        y0 = 916.0 + index * 84.0
        if index == 1:
            drawer = hand_cut(polygon_mask([
                (58, y0 + 6), (322, y0 - 4), (330, y0 + 74), (66, y0 + 84)]),
                671, jitter=0.06)
            place(canvas, drawer, gouache(DUST_DEEP, 673, mottle=0.07,
                                          cell=60),
                  shadow_dx=10, shadow_dy=14, shadow_radius=18.0,
                  shadow_strength=0.34, shade_strength=0.30)
            gap = blur(rounded_rect_mask(66, y0 + 78, 330, y0 + 96), 7.0)
            tint(canvas, gap * 0.8, SHADOW, 0.55)
        else:
            drawer = hand_cut(
                rounded_rect_mask(78, y0, 334, y0 + 72, 8), 677 + index)
            place(canvas, drawer,
                  gouache(DUST_DEEP, 683 + index, mottle=0.07, cell=60),
                  shadow_dy=7, shadow_radius=9.0, shadow_strength=0.24,
                  shade_strength=0.26, lip_strength=0.20)

    for index in range(3):
        x0 = 92.0 + index * 62.0
        tin = hand_cut(polygon_mask([
            (x0, 862), (x0 + 34, 852), (x0 + 42, 902), (x0 + 6, 906)]), 691)
        place(canvas, tin, gouache(DUST, 693 + index, mottle=0.08, cell=26),
              shadow_dx=6, shadow_dy=9, shadow_radius=10.0,
              shadow_strength=0.28, lip_strength=0.18)
    film = blur(rounded_rect_mask(56, 830, 356, 1178), 26.0)
    tint(canvas, film * 0.44, DUST, 0.30)


def zone_paint_shelf(canvas: np.ndarray, restored: bool) -> None:
    _shelf_board(canvas, 606, 946, 1030, 976, 701)

    if restored:
        colors = [SAGE_DEEP, SAGE, SAGE_PALE, CREAM, CLAY_PALE, CLAY,
                  CLAY_DEEP]
        for index, color in enumerate(colors):
            x0 = 626.0 + index * 55.0
            jar = hand_cut(rounded_rect_mask(x0, 862, x0 + 44, 946, 9), 705)
            place(canvas, jar, gouache(color, 709 + index, mottle=0.07,
                                       cell=34),
                  shadow_dx=5, shadow_dy=8, shadow_radius=10.0,
                  shadow_strength=0.26, highlight=True)
            cap = hand_cut(rounded_rect_mask(x0 + 4, 852, x0 + 40, 870, 6), 719)
            tint(canvas, cap * 0.9, CREAM, 0.72)
        cup = hand_cut(rounded_rect_mask(1010, 876, 1030, 946, 8), 723)
        place(canvas, cup, gouache(WOOD_PALE, 727, mottle=0.05, cell=26),
              shadow_dy=7, shadow_radius=8.0, shadow_strength=0.24)
        return

    tumble = [(628, 880, 44, 66, DUST_DEEP, -0.08), (686, 866, 46, 80, DUST, 0.0),
              (742, 892, 50, 54, SAGE_DEEP, 0.10), (804, 872, 42, 74, DUST_DEEP, 0.0),
              (858, 898, 54, 48, CLAY_DEEP, -0.14), (924, 878, 44, 68, DUST, 0.05)]
    for x0, y0, w, h, color, skew in tumble:
        shear = h * skew
        jar = hand_cut(polygon_mask([
            (x0 + shear, y0), (x0 + w + shear, y0 - shear * 0.5),
            (x0 + w, y0 + h), (x0, y0 + h)]), 731, jitter=0.07)
        place(canvas, jar, gouache(color, 733 + int(x0), mottle=0.09, cell=30),
              shadow_dx=7, shadow_dy=11, shadow_radius=13.0,
              shadow_strength=0.32, shade_strength=0.28, lip_strength=0.20)
    spill = hand_cut(ellipse_mask(846, 924, 962, 968), 739, jitter=0.11)
    place(canvas, spill, gouache(DUST_DEEP, 743, mottle=0.10, cell=30),
          shadow=False, lip=False, shade_strength=0.16)
    film = blur(rounded_rect_mask(600, 852, 1040, 990), 20.0)
    tint(canvas, film * 0.46, DUST, 0.30)


def zone_tool_tray(canvas: np.ndarray, restored: bool) -> None:
    if restored:
        tray = hand_cut(rounded_rect_mask(422, 1196, 800, 1318, 14), 801)
        place(canvas, tray, wood_grain(WOOD_WARM, 805, strength=0.08),
              shadow_dx=8, shadow_dy=13, shadow_radius=17.0,
              shadow_strength=0.30, highlight=True)
        inner = hand_cut(rounded_rect_mask(438, 1210, 784, 1306, 10), 809)
        tint(canvas, inner * 0.85, WOOD_DEEP, 0.30)
        for index in range(4):
            x0 = 444.0 + index * 86.0
            cell_mask = hand_cut(
                rounded_rect_mask(x0, 1216, x0 + 76, 1300, 8), 811 + index)
            place(canvas, cell_mask,
                  gouache(CREAM_DEEP, 817 + index, mottle=0.05, cell=40),
                  shadow=False, shade_strength=0.16, lip_strength=0.34)
            for row in range(2):
                for col in range(3):
                    cx = x0 + 18.0 + col * 21.0
                    cy = 1240.0 + row * 30.0
                    part = hand_cut(
                        ellipse_mask(cx - 8, cy - 8, cx + 8, cy + 8), 823)
                    place(canvas, part,
                          gouache(BRASS if index % 2 else DUST_DEEP,
                                  827 + row * 3 + col, mottle=0.06, cell=16),
                          shadow_dx=2, shadow_dy=3, shadow_radius=4.0,
                          shadow_strength=0.26, lip_strength=0.40)
        return

    plank = hand_cut(rounded_rect_mask(422, 1250, 800, 1318, 10), 831)
    place(canvas, plank, wood_grain(WOOD_DEEP, 835, strength=0.07),
          shadow_dx=8, shadow_dy=12, shadow_radius=15.0, shadow_strength=0.28,
          shade_strength=0.28, lip_strength=0.22)
    generator = np.random.default_rng(9001)
    for _ in range(26):
        cx = float(generator.uniform(432.0, 792.0))
        cy = float(generator.uniform(1200.0, 1300.0))
        r = float(generator.uniform(5.0, 11.0))
        part = hand_cut(ellipse_mask(cx - r, cy - r, cx + r, cy + r), 839)
        place(canvas, part, gouache(DUST_DEEP, 841, mottle=0.07, cell=14),
              shadow_dx=3, shadow_dy=5, shadow_radius=6.0,
              shadow_strength=0.30, lip=False)
    film = blur(rounded_rect_mask(400, 1180, 820, 1330), 22.0)
    tint(canvas, film * 0.46, DUST, 0.30)


def zone_workbench(canvas: np.ndarray, restored: bool) -> None:
    top = hand_cut(polygon_mask([
        (34, 1372), (762, 1358), (770, 1462), (26, 1476)]), 901, jitter=0.035)
    apron = hand_cut(rounded_rect_mask(38, 1458, 758, 1542, 8), 905)

    if restored:
        place(canvas, top, wood_grain(WOOD_WARM, 911, strength=0.12),
              shadow_dx=9, shadow_dy=16, shadow_radius=22.0,
              shadow_strength=0.32, highlight=True, lip_strength=0.55)
        place(canvas, apron, wood_grain(WOOD, 915, strength=0.09),
              shadow_dy=10, shadow_radius=13.0, shadow_strength=0.26,
              shade_strength=0.26)
        sheen = blur(polygon_mask([
            (70, 1382), (700, 1370), (706, 1404), (64, 1416)]), 26.0)
        tint(canvas, sheen * top * 0.6, SUN, 0.26)
        cloth = hand_cut(polygon_mask([
            (556, 1388), (742, 1382), (748, 1442), (548, 1448)]), 919,
            jitter=0.06)
        place(canvas, cloth, gouache(SAGE_PALE, 923, mottle=0.09, cell=50),
              shadow_dy=7, shadow_radius=10.0, shadow_strength=0.24,
              shade_strength=0.16, lip_strength=0.34)
        return

    place(canvas, top, wood_grain(DUST_DEEP, 931, strength=0.06),
          shadow_dx=9, shadow_dy=16, shadow_radius=22.0, shadow_strength=0.32,
          shade_strength=0.30, lip_strength=0.20)
    place(canvas, apron, wood_grain(WOOD_DEEP, 935, strength=0.06),
          shadow_dy=10, shadow_radius=13.0, shadow_strength=0.26,
          shade_strength=0.28, lip_strength=0.18)
    for cx, cy, rx, ry, seed in ((196, 1412, 74, 26, 941),
                                 (430, 1396, 56, 20, 947),
                                 (612, 1428, 62, 22, 953)):
        stain = hand_cut(
            ellipse_mask(cx - rx, cy - ry, cx + rx, cy + ry), seed, jitter=0.13)
        tint(canvas, blur(stain, 5.0) * top * 0.7, SHADOW, 0.22)
    film = blur(polygon_mask([
        (30, 1360), (766, 1352), (774, 1548), (26, 1554)]), 22.0)
    tint(canvas, film * 0.5, DUST, 0.30)


def zone_cabinet(canvas: np.ndarray, restored: bool) -> None:
    carcass = hand_cut(rounded_rect_mask(718, 1614, 1042, 1962, 12), 1001)
    place(canvas, carcass, wood_grain(WOOD, 1005, horizontal=False,
                                      strength=0.09),
          shadow_dx=10, shadow_dy=16, shadow_radius=22.0,
          shadow_strength=0.34, highlight=True)

    if restored:
        for index in range(2):
            x0 = 732.0 + index * 152.0
            door = hand_cut(
                rounded_rect_mask(x0, 1630, x0 + 144, 1946, 9), 1011 + index)
            place(canvas, door,
                  gouache(SAGE if index == 0 else SAGE_PALE, 1017 + index,
                          mottle=0.07, cell=90),
                  shadow_dy=8, shadow_radius=11.0, shadow_strength=0.24,
                  shade_strength=0.18, highlight=True, lip_strength=0.46)
            panel = hand_cut(
                rounded_rect_mask(x0 + 22, 1660, x0 + 122, 1916, 7),
                1023 + index)
            tint(canvas, panel * 0.7, CREAM, 0.24)
            handle = hand_cut(
                rounded_rect_mask(
                    x0 + (120 if index == 0 else 12), 1770,
                    x0 + (134 if index == 0 else 26), 1822, 6), 1031)
            place(canvas, handle, gouache(BRASS, 1033, mottle=0.05, cell=18),
                  shadow_dy=5, shadow_radius=6.0, shadow_strength=0.26,
                  highlight=True, lip_strength=0.5)
        for hinge_y in (1682.0, 1888.0):
            hinge = hand_cut(
                rounded_rect_mask(724, hinge_y, 748, hinge_y + 42, 5), 1037)
            place(canvas, hinge, gouache(BRASS, 1039, mottle=0.04, cell=16),
                  shadow_dy=4, shadow_radius=5.0, shadow_strength=0.22,
                  lip_strength=0.45)
        pot = hand_cut(polygon_mask([
            (858, 1560), (938, 1560), (926, 1612), (870, 1612)]), 1041)
        place(canvas, pot, gouache(CLAY, 1043, mottle=0.07, cell=30),
              shadow_dy=8, shadow_radius=10.0, shadow_strength=0.26,
              highlight=True)
        for angle, size in ((-1, 46), (0, 58), (1, 44)):
            leaf = hand_cut(ellipse_mask(
                894 + angle * 30 - 16, 1560 - size, 894 + angle * 30 + 16,
                1566), 1047)
            place(canvas, leaf, gouache(SAGE_DEEP if angle else SAGE, 1049,
                                        mottle=0.07, cell=24),
                  shadow=False, shade=False, lip_strength=0.28)
        return

    right = hand_cut(rounded_rect_mask(884, 1630, 1028, 1946, 9), 1051)
    place(canvas, right, gouache(DUST_DEEP, 1053, mottle=0.07, cell=90),
          shadow_dy=8, shadow_radius=11.0, shadow_strength=0.24,
          shade_strength=0.26, lip_strength=0.20)

    cavity = rounded_rect_mask(732, 1630, 876, 1946, 8)
    tint(canvas, cavity, SHADOW, 0.82)
    tint(canvas, blur(cavity, 12.0) * 0.5, SHADOW, 0.4)

    sag = hand_cut(polygon_mask([
        (742, 1652), (866, 1626), (890, 1930), (760, 1952)]), 1057,
        jitter=0.055)
    place(canvas, sag, gouache(DUST_DEEP, 1059, mottle=0.08, cell=80),
          shadow_dx=12, shadow_dy=16, shadow_radius=20.0,
          shadow_strength=0.36, shade_strength=0.30, lip_strength=0.18)
    broken = hand_cut(rounded_rect_mask(726, 1668, 748, 1700, 4), 1063)
    place(canvas, broken, gouache(DUST, 1067, mottle=0.05, cell=14),
          shadow_dy=4, shadow_radius=5.0, shadow_strength=0.24, lip=False)
    film = blur(rounded_rect_mask(710, 1600, 1050, 1974), 24.0)
    tint(canvas, film * 0.42, DUST, 0.26)


def zone_window(canvas: np.ndarray, restored: bool) -> None:
    reveal = hand_cut(rounded_rect_mask(612, 226, 1024, 690, 10), 1101)
    place(canvas, reveal, gouache(WALL_DEEP, 1105, mottle=0.06, cell=80),
          shadow_dx=8, shadow_dy=13, shadow_radius=18.0, shadow_strength=0.28,
          shade_strength=0.20)

    pane_area = rounded_rect_mask(636, 250, 1000, 646, 6)
    if restored:
        sky = vertical_gradient(SKY_PALE, SKY)
        over(canvas, pane_area, sky)
        for index in range(3):
            cloud = blur(ellipse_mask(
                660 + index * 108, 300 + index * 46,
                806 + index * 108, 356 + index * 46), 16.0)
            tint(canvas, cloud * pane_area * 0.7, CREAM, 0.55)
        hill = hand_cut(polygon_mask([
            (636, 560), (760, 486), (880, 546), (1000, 500), (1000, 646),
            (636, 646)]), 1109, jitter=0.05)
        tint(canvas, hill * pane_area, SAGE_DEEP, 0.72)
        glow = blur(pane_area, 40.0)
        tint(canvas, glow * 0.5, SUN, 0.22)
    else:
        over(canvas, pane_area, gouache(GLASS, 1113, mottle=0.10, cell=70))
        grime = fbm(HEIGHT, WIDTH, 40, 4, 1117)
        tint(canvas, pane_area * np.clip(grime * 1.2, 0.0, 1.0) * 0.8,
             DUST_DEEP, 0.62)
        for index in range(4):
            streak = line_mask(
                [(660 + index * 88, 258), (676 + index * 88, 640)], 26.0)
            tint(canvas, blur(streak, 10.0) * pane_area * 0.7, DUST, 0.42)
        web = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
        for index in range(5):
            web = np.maximum(web, line_mask(
                [(1000, 250), (1000 - 40 - index * 34, 250 + 34 + index * 30)],
                3.0))
        for index in range(3):
            web = np.maximum(web, line_mask([
                (1000 - 20 - index * 30, 250 + 26 + index * 26),
                (1000 - 54 - index * 30, 250 + 60 + index * 30)], 3.0))
        tint(canvas, blur(web, 1.4) * pane_area * 0.8, CREAM_DEEP, 0.5)

    # Sash frame: identical geometry in both states, so the crops align.
    frame = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    frame = np.maximum(frame, rounded_rect_mask(628, 242, 1008, 654, 8))
    frame -= rounded_rect_mask(648, 262, 988, 634, 4)
    frame = np.clip(frame, 0.0, 1.0)
    frame = np.maximum(frame, rounded_rect_mask(806, 250, 830, 646))
    frame = np.maximum(frame, rounded_rect_mask(636, 434, 1000, 458))
    frame = hand_cut(frame, 1121, jitter=0.035)
    place(canvas, frame,
          gouache(CREAM if restored else CREAM_DEEP, 1125, mottle=0.06,
                  cell=60),
          shadow_dx=5, shadow_dy=8, shadow_radius=11.0, shadow_strength=0.26,
          shade_strength=0.18, highlight=restored, lip_strength=0.5)

    sill = hand_cut(rounded_rect_mask(602, 646, 1034, 690, 7), 1129)
    place(canvas, sill, wood_grain(WOOD_PALE if restored else DUST_DEEP, 1133,
                                   strength=0.08),
          shadow_dy=11, shadow_radius=14.0, shadow_strength=0.28,
          highlight=restored, lip_strength=0.42 if restored else 0.22)

    if restored:
        pot = hand_cut(polygon_mask([
            (896, 600), (958, 600), (950, 646), (904, 646)]), 1137)
        place(canvas, pot, gouache(CLAY_PALE, 1139, mottle=0.06, cell=26),
              shadow_dy=7, shadow_radius=9.0, shadow_strength=0.26,
              highlight=True)
        for offset, size in ((-22, 40), (0, 52), (20, 38)):
            leaf = hand_cut(ellipse_mask(
                927 + offset - 14, 600 - size, 927 + offset + 14, 606), 1141)
            place(canvas, leaf, gouache(SAGE, 1143 + offset, mottle=0.07,
                                        cell=22),
                  shadow=False, shade=False, lip_strength=0.28)
    else:
        dust = blur(rounded_rect_mask(600, 226, 1036, 692), 26.0)
        tint(canvas, dust * 0.42, DUST, 0.26)


ZONE_PAINTERS = [
    zone_clear_passage,
    zone_pebble_shelf,
    zone_tea_drawer,
    zone_paint_shelf,
    zone_tool_tray,
    zone_workbench,
    zone_cabinet,
    zone_window,
]


def render_zone(background: np.ndarray, index: int, restored: bool) -> np.ndarray:
    """Paint one zone over the shared background and apply the paper finish.

    The finish is a pure function of pixel position plus a short-radius bloom.
    Because every zone variant is finished on top of the *same* background, and
    zone content is inset from its rectangle by more than the bloom radius, the
    finished result outside the rectangle is identical for every variant. That
    is what lets a crop be pasted back onto the master without a seam.
    """
    canvas = background.copy()
    ZONE_PAINTERS[index](canvas, restored)
    blend = feathered_rect_of(ZONES[index][2])[:, :, None]
    canvas = background * (1.0 - blend) + canvas * blend
    return paper_finish(canvas)


def compose_master(
    finished_background: np.ndarray,
    zone_renders,
) -> np.ndarray:
    """Assemble a full room from the finished background plus zone crops."""
    canvas = finished_background.copy()
    for index, (_, _, rect) in enumerate(ZONES):
        x0, y0, x1, y1 = rect
        canvas[y0:y1, x0:x1] = zone_renders[index][y0:y1, x0:x1]
    return canvas


def render_final_sunlight(background: np.ndarray) -> Image.Image:
    """A warm, transparent finale wash: a light shaft plus drifting motes."""
    shaft = polygon_mask([
        (612, 236), (1030, 236), (596, 1560), (-60, 1560)])
    shaft = blur(shaft, 70.0) * 0.40
    falloff = np.clip(
        1.0 - (np.arange(HEIGHT, dtype=np.float32) - 240.0) / 1500.0, 0.0, 1.0)
    shaft = shaft * falloff[:, None]

    warmth = blur(ellipse_mask(320, -260, 1300, 1060), 170.0) * 0.22

    motes = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    generator = np.random.default_rng(31337)
    for _ in range(90):
        cx = float(generator.uniform(0.0, 1040.0))
        cy = float(generator.uniform(260.0, 1520.0))
        r = float(generator.uniform(3.0, 8.0))
        motes = np.maximum(
            motes, ellipse_mask(cx - r, cy - r, cx + r, cy + r))
    motes = blur(motes, 3.0) * shaft * 2.4

    alpha = np.clip(shaft * 0.34 + warmth + motes * 0.9, 0.0, 0.62)
    color = np.zeros((HEIGHT, WIDTH, 3), dtype=np.float32)
    color[:] = rgb(SUN)[None, None, :]
    color = color * (0.86 + blur(motes, 2.0)[:, :, None] * 0.5)

    rgba = np.concatenate(
        [np.clip(color, 0.0, 1.0), alpha[:, :, None]], axis=2)
    return Image.fromarray((rgba * 255.0 + 0.5).astype(np.uint8), "RGBA")


def save(image: Image.Image, path: str) -> str:
    image.save(path, "PNG", optimize=True)
    with open(path, "rb") as handle:
        digest = hashlib.sha256(handle.read()).hexdigest()
    return digest


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--out",
        default=os.path.join("Assets", "CalmSpace", "UI", "Workshop"),
        help="destination folder for the generated art")
    args = parser.parse_args()

    assert_disjoint()

    room_dir = os.path.join(args.out, "Art", "Room")
    os.makedirs(room_dir, exist_ok=True)

    print("painting shared background ...")
    background = paint_background()

    manifest = {
        "referenceWidth": WIDTH,
        "referenceHeight": HEIGHT,
        "chapterId": "cozy-workshop",
        "baseImage": "WorkshopBaseDirty.png",
        "finaleImage": "FinalSunlight.png",
        "zones": [],
    }

    finished_background = paper_finish(background)
    dirty_renders = []
    restored_renders = []
    for index, (name, _, rect) in enumerate(ZONES):
        dirty_full = render_zone(background, index, False)
        restored_full = render_zone(background, index, True)

        outside = rect_mask_of(rect) == 0.0
        for label, render in (("dirty", dirty_full), ("restored",
                                                      restored_full)):
            leak = float(
                np.abs(render - finished_background)[outside].max())
            if leak > 0.02:
                raise SystemExit(
                    f"zone {name} {label} paint reaches its rectangle edge "
                    f"(max {leak:.5f}); inset the artwork further")

        dirty_renders.append(dirty_full)
        restored_renders.append(restored_full)

    dirty_master = compose_master(finished_background, dirty_renders)
    digest = save(to_image(dirty_master),
                  os.path.join(room_dir, "WorkshopBaseDirty.png"))
    print(f"  WorkshopBaseDirty.png {WIDTH}x{HEIGHT} sha256={digest[:16]}")
    manifest["baseSha256"] = digest

    for index, (name, beat_id, rect) in enumerate(ZONES):
        x0, y0, x1, y1 = rect
        restored_full = restored_renders[index]
        dirty_full = dirty_renders[index]

        restored_crop = to_image(restored_full[y0:y1, x0:x1])
        dirty_crop = to_image(dirty_full[y0:y1, x0:x1])
        changed = float(
            np.abs(restored_full - dirty_full)[y0:y1, x0:x1].mean())
        if changed < 0.01:
            raise SystemExit(f"zone {name} barely changes (mean {changed:.5f})")

        # The dusty crop must be byte-identical to that region of the master,
        # so a reveal overlay lands exactly on top of what the player sees.
        master_region = to_image(dirty_master[y0:y1, x0:x1])
        if np.asarray(master_region).tobytes() != \
                np.asarray(dirty_crop).tobytes():
            raise SystemExit(
                f"zone {name} before-crop does not match the master region")

        restored_digest = save(
            restored_crop, os.path.join(room_dir, f"{name}.png"))
        before_digest = save(
            dirty_crop, os.path.join(room_dir, f"{name}Before.png"))
        print(
            f"  {name}.png {x1 - x0}x{y1 - y0} delta={changed:.3f} "
            f"sha256={restored_digest[:16]}")

        manifest["zones"].append({
            "stageIndex": index,
            "beatId": beat_id,
            "name": name,
            "restored": f"{name}.png",
            "before": f"{name}Before.png",
            "x": x0,
            "y": y0,
            "width": x1 - x0,
            "height": y1 - y0,
            "restoredSha256": restored_digest,
            "beforeSha256": before_digest,
        })

    finale_digest = save(
        render_final_sunlight(background),
        os.path.join(room_dir, "FinalSunlight.png"))
    manifest["finaleSha256"] = finale_digest
    print(f"  FinalSunlight.png {WIDTH}x{HEIGHT} sha256={finale_digest[:16]}")

    manifest_path = os.path.join(args.out, "workshop-room-manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2, sort_keys=True)
        handle.write("\n")
    print(f"manifest -> {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
