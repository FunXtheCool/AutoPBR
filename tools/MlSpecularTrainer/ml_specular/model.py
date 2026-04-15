"""
Dilated FCN: any H×W (no pooling/skip mismatch). AutoPBR runs at native texture resolution.

For a heavier U-Net-style model, train at fixed multiples of 32 or add pad/crop in the app;
this architecture matches the plan's requirement for dynamic spatial ONNX export.
"""

from __future__ import annotations

import torch
import torch.nn as nn


class DilatedPbrNet(nn.Module):
    """3 or 4 input channels, configurable output channels (NCHW)."""

    def __init__(self, in_channels: int = 4, out_channels: int = 3, width: int = 48) -> None:
        super().__init__()
        if in_channels not in (3, 4):
            raise ValueError("in_channels must be 3 or 4")
        if out_channels <= 0:
            raise ValueError("out_channels must be > 0")
        w = width
        self.net = nn.Sequential(
            nn.Conv2d(in_channels, w, 3, padding=1, bias=False),
            nn.BatchNorm2d(w),
            nn.ReLU(inplace=True),
            nn.Conv2d(w, w, 3, padding=1, bias=False),
            nn.BatchNorm2d(w),
            nn.ReLU(inplace=True),
            nn.Conv2d(w, w, 3, padding=2, dilation=2, bias=False),
            nn.BatchNorm2d(w),
            nn.ReLU(inplace=True),
            nn.Conv2d(w, w, 3, padding=4, dilation=4, bias=False),
            nn.BatchNorm2d(w),
            nn.ReLU(inplace=True),
            nn.Conv2d(w, w, 3, padding=8, dilation=8, bias=False),
            nn.BatchNorm2d(w),
            nn.ReLU(inplace=True),
            nn.Conv2d(w, out_channels, 1),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)
