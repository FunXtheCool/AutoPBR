"""ONNX-friendly specular training graph: diffuse input + targets -> scalar loss (spec_loss contract)."""

from __future__ import annotations

import torch
import torch.nn as nn

from ml_specular.model import DilatedPbrNet
from ml_specular.spec_loss import spec_loss

# IO names for exported train/eval loss graphs — keep in sync with train_spec_ort + verify_ort_specular_training.
ORT_LOSS_INPUT_NAMES: tuple[str, ...] = ("input", "target_rgba", "valid", "transparent_zero_weight")
ORT_LOSS_OUTPUT_NAMES: tuple[str, ...] = ("loss",)


class SpecularTrainEvalGraph(nn.Module):
    """
    Forward matches PyTorch trainer: same `spec_loss` on core logits.

    Inputs (for ONNX export / ORT feeds):
      - input: [N, in_channels, H, W]
      - target_rgba: [N, 4, H, W]
      - valid: [N, H, W]
      - transparent_zero_weight: [1] float32 (use np.array([w], np.float32) at inference)
    Output:
      - loss: [1] float32 (reduced loss for the batch)
    """

    def __init__(self, core: DilatedPbrNet, *, out_channels: int) -> None:
        super().__init__()
        self.core = core
        _ = out_channels

    def forward(
        self,
        input: torch.Tensor,
        target_rgba: torch.Tensor,
        valid: torch.Tensor,
        transparent_zero_weight: torch.Tensor,
    ) -> torch.Tensor:
        raw = self.core(input)
        elem = spec_loss(
            raw,
            target_rgba,
            valid,
            transparent_zero_weight=transparent_zero_weight,
        )
        return elem.reshape(1)


def build_graph(in_channels: int, out_channels: int, width: int) -> SpecularTrainEvalGraph:
    core = DilatedPbrNet(in_channels=in_channels, out_channels=out_channels, width=width)
    return SpecularTrainEvalGraph(core, out_channels=out_channels)
