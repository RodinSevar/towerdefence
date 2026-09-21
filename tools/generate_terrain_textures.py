#!/usr/bin/env python3
"""Generates seamless, tileable, procedural terrain textures for the Northrend-style Wintermaul map.

The original map uses Warcraft III tileset textures, which live in the game's own files. These are original stand-ins,
named after the map's tile ids (see mpq_files/war3map.w3e: Ndrt Glav Nrck Ngrs Nice Nsnw Nsnr, cliffs CNdi CNsn).

Everything is built from periodic noise (wrapping value noise, wrapping cellular noise), so every texture tiles
without seams. Output is deterministic (fixed seeds): edit the recipes and re-run to change the look.

    python tools/generate_terrain_textures.py                 # writes Assets/Textures/Terrain/*.png
    python tools/generate_terrain_textures.py --preview x.png # also writes a contact sheet

Requires numpy and Pillow.
"""
import argparse
import os

import numpy as np
from PIL import Image

SIZE = 512
OUT_DIR = os.path.join("Assets", "Textures", "Terrain")


# ---------------------------------------------------------------------------------------------- noise primitives

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def value_noise(fx, fy, rng, size=SIZE):
    """Wrapping value noise with fx x fy lattice cells (smooth, periodic)."""
    grid = rng.random((fy, fx))
    xs = np.arange(size) * fx / size
    ys = np.arange(size) * fy / size
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    tx = xs - x0
    ty = ys - y0
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    x1 = (x0 + 1) % fx
    y1 = (y0 + 1) % fy
    x0 %= fx
    y0 %= fy
    g00 = grid[y0][:, x0]
    g10 = grid[y0][:, x1]
    g01 = grid[y1][:, x0]
    g11 = grid[y1][:, x1]
    top = g00 * (1 - tx) + g10 * tx
    bot = g01 * (1 - tx) + g11 * tx
    return top * (1 - ty[:, None]) + bot * ty[:, None]


def fbm(base, octaves, rng, gain=0.5, aspect=1.0):
    """Fractal sum of periodic noise, normalized to 0..1. `aspect` stretches the lattice (fx = base*aspect)."""
    total = np.zeros((SIZE, SIZE))
    amp, norm = 1.0, 0.0
    f = base
    for _ in range(octaves):
        total += amp * value_noise(max(1, int(round(f * aspect))), max(1, int(round(f))), rng)
        norm += amp
        amp *= gain
        f *= 2
    total /= norm
    lo, hi = total.min(), total.max()
    return (total - lo) / (hi - lo + 1e-9)


def worley(nx, ny, rng):
    """Wrapping cellular noise. Returns (F1, F2, value of the nearest cell) in cell units."""
    pts = rng.random((ny, nx, 2))
    vals = rng.random((ny, nx))
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]
    xs = xs / SIZE * nx
    ys = ys / SIZE * ny
    ci = np.floor(xs).astype(int)
    cj = np.floor(ys).astype(int)
    f1 = np.full((SIZE, SIZE), 9.0)
    f2 = np.full((SIZE, SIZE), 9.0)
    nearest = np.zeros((SIZE, SIZE))
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            ni = (ci + di) % nx
            nj = (cj + dj) % ny
            px = ci + di + pts[nj, ni, 0]
            py = cj + dj + pts[nj, ni, 1]
            d = np.hypot(xs - px, ys - py)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            nearest = np.where(closer, vals[nj, ni], nearest)
            f1 = np.where(closer, d, f1)
    return f1, f2, nearest


def ramp(v, stops):
    """Maps 0..1 values through colour stops [(pos, (r, g, b)), ...] -> (H, W, 3)."""
    pos = [s[0] for s in stops]
    return np.stack([np.interp(v, pos, [s[1][c] for s in stops]) for c in range(3)], axis=-1)


def mix(a, b, t):
    return a * (1 - t[..., None]) + b * t[..., None]


def normal_from_height(h, strength=3.0):
    """Tangent-space normal map (OpenGL convention: green up) from a wrapping height field."""
    dx = np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)
    drow = np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)
    n = np.stack([-dx * strength, drow * strength, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


# ---------------------------------------------------------------------------------------------- recipes
# Each returns (albedo HxWx3 in 0..1, height HxW in 0..1 or None).

def snow(rng):
    drift = fbm(3, 5, rng)
    fine = fbm(24, 3, rng)
    col = ramp(drift, [(0.0, (0.70, 0.81, 0.93)), (0.5, (0.88, 0.93, 0.98)), (1.0, (0.99, 1.0, 1.0))])
    col = col * (0.95 + 0.08 * fine[..., None])
    sparkle = rng.random((SIZE, SIZE)) > 0.9975
    col = col + sparkle[..., None] * 0.18
    return col, drift * 0.7 + fine * 0.15


def ice(rng):
    f1, f2, cell = worley(6, 6, rng)
    crack = 1 - smoothstep(0.0, 0.06, f2 - f1)
    g1, g2, _ = worley(15, 15, rng)
    fine_crack = 1 - smoothstep(0.0, 0.035, g2 - g1)
    body = fbm(4, 4, rng)
    col = ramp(body, [(0.0, (0.40, 0.66, 0.83)), (0.5, (0.58, 0.81, 0.93)), (1.0, (0.82, 0.94, 0.99))])
    col = col * (0.92 + 0.12 * cell[..., None])          # each plate a slightly different tone
    col = col * (1 - 0.30 * crack[..., None])            # deep cracks are darker
    col = col + fine_crack[..., None] * 0.22             # fine white scratches
    return col, body * 0.2 - crack * 0.6 - fine_crack * 0.15


def rock(rng):
    f1, f2, cell = worley(7, 7, rng)
    edge = 1 - smoothstep(0.0, 0.10, f2 - f1)
    dome = 1 - np.clip(f1 / 0.7, 0, 1)
    strata = fbm(3, 5, rng)
    col = ramp(strata, [(0.0, (0.28, 0.33, 0.40)), (0.6, (0.45, 0.50, 0.56)), (1.0, (0.62, 0.66, 0.71))])
    grain = fbm(26, 4, rng)                                 # fine surface grain so the cells are not smooth domes
    col = col * (0.75 + 0.35 * dome[..., None]) * (0.85 + 0.25 * cell[..., None])
    col = col * (0.82 + 0.3 * grain[..., None])
    col = col * (1 - 0.55 * edge[..., None])
    return col, dome * 0.6 - edge * 0.7 + strata * 0.2


def dirt(rng):
    base = fbm(4, 5, rng)
    p1, _, pcell = worley(30, 30, rng)
    radius = 0.14 + 0.26 * pcell                          # pebbles of varied size
    pebble = smoothstep(radius, radius - 0.12, p1)
    col = ramp(base, [(0.0, (0.25, 0.20, 0.17)), (0.5, (0.37, 0.30, 0.24)), (1.0, (0.50, 0.42, 0.34))])
    col = mix(col, ramp(pcell, [(0.0, (0.35, 0.33, 0.32)), (1.0, (0.62, 0.60, 0.58))]), pebble * 0.65)
    frost = smoothstep(0.70, 0.86, fbm(9, 3, rng))
    col = mix(col, np.array([0.86, 0.92, 0.96]), frost * 0.45)
    return col, base * 0.4 + pebble * 0.5


def gravel(rng):
    """The map's 'Glav' tile: unknown in the original, treated as packed frozen gravel."""
    f1, f2, cell = worley(22, 22, rng)
    edge = 1 - smoothstep(0.0, 0.14, f2 - f1)
    dome = 1 - np.clip(f1 / 0.6, 0, 1)
    tone = ramp(cell, [(0.0, (0.27, 0.30, 0.35)), (0.5, (0.42, 0.44, 0.47)), (1.0, (0.62, 0.62, 0.62))])
    col = tone * (0.7 + 0.4 * dome[..., None]) * (1 - 0.6 * edge[..., None])
    dust = smoothstep(0.62, 0.85, fbm(8, 4, rng))
    col = mix(col, np.array([0.80, 0.88, 0.94]), dust * 0.4)
    return col, dome * 0.7 - edge * 0.6


def grass(rng):
    n1 = fbm(4, 4, rng)
    blades = fbm(8, 3, rng, aspect=9.0)                 # long thin streaks running along y (vertical blades)
    tufts = fbm(10, 3, rng)
    col = ramp(n1, [(0.0, (0.26, 0.32, 0.20)), (0.5, (0.40, 0.46, 0.28)), (1.0, (0.57, 0.60, 0.42))])
    col = col * (0.65 + 0.7 * blades[..., None]) * (0.85 + 0.3 * tufts[..., None])
    frost = smoothstep(0.60, 0.82, fbm(6, 3, rng))
    col = mix(col, np.array([0.80, 0.90, 0.92]), frost * 0.5)
    return col, blades * 0.5 + tufts * 0.2


def snowrock(rng):
    r_col, r_h = rock(rng)
    s_col, s_h = snow(rng)
    mask = smoothstep(0.42, 0.62, fbm(4, 5, rng))
    return mix(r_col, s_col, mask), r_h * (1 - mask) + s_h * mask


def cliff(rng, palette, strata_count=9, cracks=True):
    warp = fbm(3, 4, rng)
    y = np.linspace(0, 1, SIZE, endpoint=False)[:, None]
    band = 0.5 + 0.5 * np.sin(2 * np.pi * (strata_count * y + 1.4 * warp))
    grain = fbm(18, 4, rng, aspect=2.0)
    v = np.clip(band * 0.55 + grain * 0.35 + warp * 0.1, 0, 1)
    col = ramp(v, palette)
    h = band * 0.5 + grain * 0.3
    if cracks:
        f1, f2, _ = worley(4, 14, rng)                   # tall thin blocks
        crack = 1 - smoothstep(0.0, 0.07, f2 - f1)
        col = col * (1 - 0.5 * crack[..., None])
        h = h - crack * 0.5
    return col, h


def cliff_dirt(rng):
    return cliff(rng, [(0.0, (0.17, 0.14, 0.13)), (0.5, (0.34, 0.28, 0.24)), (1.0, (0.54, 0.47, 0.40))])


def cliff_snow(rng):
    return cliff(rng, [(0.0, (0.30, 0.35, 0.43)), (0.5, (0.68, 0.76, 0.85)), (1.0, (0.97, 0.99, 1.0))], strata_count=7)


def water(rng):
    a = fbm(4, 5, rng)
    b = fbm(8, 4, rng)
    ridge = (1 - np.abs(2 * a - 1)) * (1 - np.abs(2 * b - 1))
    caustic = smoothstep(0.55, 0.95, ridge)
    col = ramp(a, [(0.0, (0.03, 0.13, 0.29)), (1.0, (0.08, 0.32, 0.52))])
    col = col + caustic[..., None] * np.array([0.20, 0.42, 0.48])
    return col, caustic * 0.25 + a * 0.15


# texture name -> (recipe, seed, normal map strength or None)
TEXTURES = {
    "Ndrt": (dirt, 11, 2.0),
    "Glav": (gravel, 12, 3.0),
    "Nrck": (rock, 13, 3.5),
    "Ngrs": (grass, 14, None),
    "Nice": (ice, 15, 2.5),
    "Nsnw": (snow, 16, 1.5),
    "Nsnr": (snowrock, 17, 3.0),
    "CNdi": (cliff_dirt, 18, 4.0),
    "CNsn": (cliff_snow, 19, 4.0),
    "water": (water, 20, 2.0),
}


def to_image(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def seam_error(arr):
    """Mean colour jump across the wrap edge relative to the jump between ordinary neighbouring pixels (about 1 = seamless)."""
    edge = (np.abs(arr[:, 0] - arr[:, -1]).mean() + np.abs(arr[0] - arr[-1]).mean()) / 2
    inner = (np.abs(arr[:, 1:] - arr[:, :-1]).mean() + np.abs(arr[1:] - arr[:-1]).mean()) / 2
    return edge / (inner + 1e-9)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT_DIR)
    ap.add_argument("--preview", help="optional contact sheet PNG (each texture, plus a 2x2 tiling of the first)")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    albedos = {}
    for name, (recipe, seed, normal_strength) in TEXTURES.items():
        rng = np.random.default_rng(seed)
        col, height = recipe(rng)
        albedos[name] = col
        to_image(col).save(os.path.join(args.out, f"terrain_{name}.png"))
        note = ""
        if normal_strength is not None and height is not None:
            h = (height - height.min()) / (height.max() - height.min() + 1e-9)
            to_image(normal_from_height(h, normal_strength)).save(os.path.join(args.out, f"terrain_{name}_n.png"))
            note = " (+ normal map)"
        print(f"terrain_{name}.png  seam ratio {seam_error(col):.2f}{note}")

    if args.preview:
        names = list(TEXTURES)
        cell = 256
        cols = 5
        rows = (len(names) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * cell, (rows + 1) * cell), (20, 20, 20))
        for i, n in enumerate(names):
            sheet.paste(to_image(albedos[n]).resize((cell, cell), Image.LANCZOS), ((i % cols) * cell, (i // cols) * cell))
        # tiling test: 2x2 repeat of two textures shows any seams
        for k, n in enumerate(("Nice", "CNdi")):
            tile = to_image(albedos[n]).resize((cell // 2, cell // 2), Image.LANCZOS)
            for ty in range(2):
                for tx in range(2):
                    sheet.paste(tile, (k * cell + tx * (cell // 2), rows * cell + ty * (cell // 2)))
        sheet.save(args.preview)
        print("preview:", args.preview)


if __name__ == "__main__":
    main()
