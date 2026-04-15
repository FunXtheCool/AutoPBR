"""
4th input channel matching AutoPBR SpecularGenerator.BuildLuminanceAndEdge (defaults).

Luminance: (0.3*R + 0.6*G + 0.1*B) / 255 from sRGB bytes.
Sobel 3x3 on luminance with reflect boundary.
12-orientation VC sum, then per-image max normalization to [0, 1].
"""

from __future__ import annotations

import numpy as np
import numpy.typing as npt


def _reflect(i: int, max_v: int) -> int:
    if i < 0:
        return -i - 1
    if i >= max_v:
        return max_v - (i - max_v) - 1
    return i


def luminance_default(rgb_uint8: npt.NDArray[np.uint8]) -> npt.NDArray[np.float32]:
    """(H,W,3) uint8 -> (H,W) float32 luma in [0,1]."""
    r = rgb_uint8[..., 0].astype(np.float32)
    g = rgb_uint8[..., 1].astype(np.float32)
    b = rgb_uint8[..., 2].astype(np.float32)
    return (r * 0.3 + g * 0.6 + b * 0.1) / 255.0


def vc_edge_from_rgb_uint8(rgb_uint8: npt.NDArray[np.uint8]) -> npt.NDArray[np.float32]:
    """
    rgb_uint8: (H, W, 3) or (H, W, 4) — alpha ignored.
    Returns (H, W) float32 in [0, 1].
    """
    if rgb_uint8.ndim != 3 or rgb_uint8.shape[2] < 3:
        raise ValueError("Expected (H,W,3+) uint8 RGB image.")
    lum = luminance_default(rgb_uint8[:, :, :3])
    h, w = lum.shape

    kx = np.array([[-1, 0, 1], [-2, 0, 2], [-1, 0, 1]], dtype=np.float32)
    ky = np.array([[-1, -2, -1], [0, 0, 0], [1, 2, 1]], dtype=np.float32)

    gx = np.zeros_like(lum, dtype=np.float32)
    gy = np.zeros_like(lum, dtype=np.float32)
    for y in range(h):
        for x in range(w):
            sx = sy = 0.0
            for oy in (-1, 0, 1):
                for ox in (-1, 0, 1):
                    rx = _reflect(x + ox, w)
                    ry = _reflect(y + oy, h)
                    v = lum[ry, rx]
                    sx += v * kx[oy + 1, ox + 1]
                    sy += v * ky[oy + 1, ox + 1]
            gx[y, x] = sx
            gy[y, x] = sy

    vc_orientation_count = 12
    angle_step = np.pi / vc_orientation_count
    edge = np.zeros_like(lum, dtype=np.float32)
    for i in range(gx.size):
        gxv = gx.flat[i]
        gyv = gy.flat[i]
        s = 0.0
        for k in range(vc_orientation_count):
            a = k * angle_step
            r = gxv * np.cos(a) + gyv * np.sin(a)
            s += abs(r)
        edge.flat[i] = s

    max_e = float(edge.max()) if edge.size else 0.0
    if max_e > 0.0:
        edge = np.clip(edge / max_e, 0.0, 1.0).astype(np.float32)
    return edge
