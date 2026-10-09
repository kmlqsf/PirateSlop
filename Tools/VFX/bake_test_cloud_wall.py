import json
import sys
from pathlib import Path

import numpy as np


TILE = 64.0
DEPTH = 20.0
NX, NY, NZ = 256, 256, 80
DX, DY, DZ = TILE / NX, TILE / NY, DEPTH / NZ
CLOUD_MIN, CLOUD_MAX = 13.0, 16.0


def smooth_union(a, b, width):
    blend = np.clip(0.5 + 0.5 * (b - a) / width, 0.0, 1.0)
    return a * (1.0 - blend) + b * blend + width * blend * (1.0 - blend)


def cloud_centers(rng):
    # A cloud roll has air around its entire circular cross section. Three
    # overlapping, differently phased rolls form the wall in the shader.
    x = rng.uniform(0, 8.0)
    centers = []
    while x < TILE + 8:
        half_size = rng.uniform(CLOUD_MIN, CLOUD_MAX) * 0.5
        for side in range(4):
            angle = side * np.pi * .5 + rng.uniform(-.45, .45)
            radius = rng.uniform(1.5, 2.0)
            center = np.array([(x + rng.uniform(-1, 1)) % TILE, TILE * .5 + np.cos(angle) * radius,
                               DEPTH * .5 + np.sin(angle) * radius])
            centers.append((center, half_size))
        x += rng.uniform(4.5, 6.5)
    return centers


def periodic_noise(rng, cells):
    values = rng.uniform(-1.0, 1.0, (cells, cells))
    x = (np.arange(NX) + 0.5) * cells / NX
    y = (np.arange(NY) + 0.5) * cells / NY
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - ix, y - iy
    fx, fy = fx ** 3 * (fx * (fx * 6 - 15) + 10), fy ** 3 * (fy * (fy * 6 - 15) + 10)
    low = values[iy[:, None] % cells, ix[None, :] % cells] * (1 - fx)[None, :] + values[iy[:, None] % cells, (ix[None, :] + 1) % cells] * fx[None, :]
    high = values[(iy[:, None] + 1) % cells, ix[None, :] % cells] * (1 - fx)[None, :] + values[(iy[:, None] + 1) % cells, (ix[None, :] + 1) % cells] * fx[None, :]
    return (low * (1 - fy)[:, None] + high * fy[:, None])[None, :, :]


def shifted_density(density, axis, offset):
    result = np.roll(density, -offset, axis=axis)
    if axis == 0:
        if offset > 0:
            result[-offset:] = 0
        elif offset < 0:
            result[:-offset] = 0
    return result


def volume_noise(rng, spacing):
    cells = int(TILE / spacing)
    layers = int(np.ceil(DEPTH / spacing)) + 2
    grid = rng.uniform(-1, 1, (layers, cells, cells)).astype(np.float32)
    x = (np.arange(NX) + 0.5) * cells / NX
    y = (np.arange(NY) + 0.5) * cells / NY
    z = (np.arange(NZ) + 0.5) * DZ / spacing
    ix, iy, iz = [np.floor(p).astype(int) for p in (x, y, z)]
    fx, fy, fz = [p - np.floor(p) for p in (x, y, z)]
    fx, fy, fz = [f * f * (3 - 2 * f) for f in (fx, fy, fz)]
    result = np.zeros((NZ, NY, NX), np.float32)
    for dz in (0, 1):
        for dy in (0, 1):
            for dx in (0, 1):
                weight = (fz if dz else 1 - fz)[:, None, None] * (fy if dy else 1 - fy)[None, :, None] * (fx if dx else 1 - fx)[None, None, :]
                result += grid[(iz + dz)[:, None, None], ((iy + dy) % cells)[None, :, None], ((ix + dx) % cells)[None, None, :]] * weight
    return result


def add_cloud(sdf, rng, center, half_size):
    cx, cy, cz = center
    ix = np.arange(int(np.floor((cx - half_size) / DX)), int(np.ceil((cx + half_size) / DX)))
    iy = np.arange(int(np.floor((cy - half_size) / DY)), int(np.ceil((cy + half_size) / DY)))
    iz = np.arange(max(0, int(np.floor((cz - half_size) / DZ))), min(NZ, int(np.ceil((cz + half_size) / DZ))))
    x, y, z = np.broadcast_arrays(
        ((ix + 0.5) * DX - cx)[None, None, :] / half_size,
        ((iy + 0.5) * DY - cy)[None, :, None] / half_size,
        ((iz + 0.5) * DZ - cz)[:, None, None] / half_size)
    angle = rng.uniform(-np.pi, np.pi)
    x, y = np.cos(angle) * x - np.sin(angle) * y, np.sin(angle) * x + np.cos(angle) * y
    angle = rng.uniform(-np.pi, np.pi)
    x, z = np.cos(angle) * x - np.sin(angle) * z, np.sin(angle) * x + np.cos(angle) * z
    shape = 0.4 - np.sqrt(x * x + (y / 0.84) ** 2 + (z / 0.76) ** 2)
    for lobe_index in range(rng.integers(6, 9)):
        if lobe_index < 2:
            offset = np.array([0.6 if lobe_index == 0 else -0.6, 0.0, 0.0])
            radius = 0.4
        else:
            direction = rng.normal(size=3)
            direction /= np.linalg.norm(direction)
            offset = direction * rng.uniform(0.28, 0.52)
            radius = rng.uniform(0.28, 0.4)
        axes = rng.uniform(0.85, 1.05, 3)
        if lobe_index < 2:
            axes[0] = 1.0
        lobe = radius - np.sqrt(((x - offset[0]) / axes[0]) ** 2 + ((y - offset[1]) / axes[1]) ** 2 + ((z - offset[2]) / axes[2]) ** 2)
        shape = smooth_union(shape, lobe, 0.045)
        for detail_index in range(rng.integers(4, 7)):
            direction = rng.normal(size=3)
            direction /= np.linalg.norm(direction)
            detail_radius = rng.uniform(0.13, 0.22)
            detail_center = offset + direction * radius * rng.uniform(0.9, 1.12)
            extent = np.linalg.norm(detail_center)
            if extent + detail_radius > 0.98:
                detail_center *= (0.98 - detail_radius) / extent
            detail = detail_radius - np.sqrt((x - detail_center[0]) ** 2 + (y - detail_center[1]) ** 2 + (z - detail_center[2]) ** 2)
            shape = smooth_union(shape, detail, 0.055)
    shape *= half_size
    indices = np.ix_(iz, iy % NY, ix % NX)
    sdf[indices] = smooth_union(sdf[indices], shape, 0.45)


def bake_lighting(density):
    lighting = []
    for radial in (-0.62, 0.62):
        optical_depth = np.zeros_like(density)
        for distance in np.arange(0.375, 14.0, 0.75):
            sample = shifted_density(density, 0, round(radial * distance / DZ))
            sample = shifted_density(sample, 1, round(0.75 * distance / DY))
            sample = shifted_density(sample, 2, round(-0.23 * distance / DX))
            optical_depth += sample * 0.75
        lighting.append(np.exp(-optical_depth * 0.75))
    ambient = np.zeros_like(density)
    for axis, step in ((0, DZ), (1, DY), (2, DX)):
        for sign in (-1, 1):
            optical_depth = np.zeros_like(density)
            for distance in np.arange(0.25, 6.0, 0.5):
                optical_depth += shifted_density(density, axis, sign * round(distance / step)) * 0.5
            ambient += np.exp(-optical_depth * 0.8) / 6
    return np.stack((density, *lighting, ambient), axis=-1)


def bake():
    rng = np.random.default_rng(20261009)
    sdf = np.full((NZ, NY, NX), -4.0, dtype=np.float32)
    cloud_count = 0
    for center, half_size in cloud_centers(rng):
        add_cloud(sdf, rng, center, half_size)
        cloud_count += 1
    sdf -= 0.12 + volume_noise(rng, 6.0) * 0.9 + volume_noise(rng, 2.0) * 0.5 + volume_noise(rng, 0.75) * 0.12
    y = ((np.arange(NY) + .5) * DY - TILE * .5)[None, :, None]
    z = ((np.arange(NZ) + .5) * DZ - DEPTH * .5)[:, None, None]
    cross_radius = np.sqrt(y * y + z * z)
    # Fill only insufficiently opaque interior columns with compound clouds.
    # A smooth cylinder would hide their relief and reveal straight pipe rows.
    interior = np.abs((np.arange(NY) + .5) * DY - TILE * .5) < 4.8
    for iteration in range(320):
        density = np.clip(sdf, 0.0, 1.0)
        column = (density * density * (3 - 2 * density)).sum(axis=0) * DZ
        column[~interior] = np.inf
        iy, ix = np.unravel_index(np.argmin(column), column.shape)
        if column[iy, ix] > 6.5:
            break
        half_size = rng.uniform(CLOUD_MIN, CLOUD_MAX) * .5
        center = ((ix + rng.uniform(-1, 1)) * DX,
                  TILE * .5 + np.clip((iy + .5) * DY - TILE * .5, -2, 2),
                  DEPTH * .5 + rng.uniform(-1.5, 1.5))
        add_cloud(sdf, rng, center, half_size)
        cloud_count += 1
    sdf = np.minimum(sdf, 9.8 - cross_radius)
    density = np.clip(sdf / 1.0, 0.0, 1.0)
    density = density * density * (3.0 - 2.0 * density)
    normal_sdf = sdf.copy()
    for axis in (0, 1, 2):
        normal_sdf = (normal_sdf * 2 + np.roll(normal_sdf, 1, axis) + np.roll(normal_sdf, -1, axis)) * 0.25
    gradient = [(np.roll(normal_sdf, -1, 2) - np.roll(normal_sdf, 1, 2)) / (2 * DX),
                (np.roll(normal_sdf, -1, 1) - np.roll(normal_sdf, 1, 1)) / (2 * DY),
                np.gradient(normal_sdf, DZ, axis=0)]
    length = np.sqrt(sum(g * g for g in gradient)) + 0.0001
    normals = np.stack([-g / length * 0.5 + 0.5 for g in gradient] + [np.clip(sdf / 8 + 0.5, 0, 1)], axis=-1)
    rgba = bake_lighting(density)
    raw = np.round(np.clip(rgba, 0.0, 1.0) * 255.0).astype(np.uint8)
    transmission = np.exp(-raw[..., 0].sum(axis=0, dtype=np.float64) / 255.0 * DZ * 1.805 * 0.85)
    core_columns = np.abs((np.arange(NY) + .5) * DY - TILE * .5) < 4.5
    core_transmission = float(transmission[core_columns].max())
    if core_transmission > 0.002:
        raise RuntimeError('The baked cloud roll contains an insufficiently opaque interior.')
    root = Path(__file__).resolve().parents[2]
    output = root / 'Library' / 'StormTestCloudWallBake.rgba'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(raw.tobytes())
    normal_output = output.with_name('StormTestCloudWallNormals.rgba')
    normal_output.write_bytes(np.round(np.clip(normals, 0, 1) * 255).astype(np.uint8).tobytes())
    print(json.dumps(dict(path=str(output), size=[NX, NY, NZ], world_size=[TILE, TILE, DEPTH],
                          clouds=cloud_count, cloud_diameter=[CLOUD_MIN, CLOUD_MAX], roller_radius=9.8,
                          maximum_core_transmission=core_transmission, bytes=raw.nbytes)))


if __name__ == '__main__':
    if '--relight' in sys.argv:
        output = Path(__file__).resolve().parents[2] / 'Library/StormTestCloudWallBake.rgba'
        raw = np.fromfile(output, np.uint8).reshape(NZ, NY, NX, 4)
        rgba = bake_lighting(raw[..., 0].astype(np.float32) / 255)
        output.write_bytes(np.round(np.clip(rgba, 0, 1) * 255).astype(np.uint8).tobytes())
        print('Lighting rebaked.')
    else:
        bake()
