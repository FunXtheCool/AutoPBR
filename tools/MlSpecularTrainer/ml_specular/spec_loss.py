"""Specular training loss (PyTorch) shared by PyTorch trainer and ORT ONNX export."""

from __future__ import annotations

import torch


def _to_transparent_weight(raw_out: torch.Tensor, transparent_zero_weight: float | torch.Tensor) -> torch.Tensor:
    if isinstance(transparent_zero_weight, torch.Tensor):
        return transparent_zero_weight.float().reshape(()).clamp(min=0.0)
    return raw_out.new_tensor(float(max(transparent_zero_weight, 0.0)))


def _masked_l1(pred: torch.Tensor, target: torch.Tensor, valid: torch.Tensor) -> torch.Tensor:
    # pred/target: [N,4,H,W], valid: [N,H,W]
    mask = valid.unsqueeze(1)
    denom = torch.clamp(mask.sum() * pred.shape[1], min=1.0)
    return torch.sum(torch.abs(pred - target) * mask) / denom


def _channel_loss(pred: torch.Tensor, target: torch.Tensor, valid: torch.Tensor, ch: int) -> torch.Tensor:
    mask = valid
    denom = torch.clamp(mask.sum(), min=1.0)
    return torch.sum(torch.abs(pred[:, ch] - target[:, ch]) * mask) / denom


def spec_loss(
    raw_out: torch.Tensor,
    target_rgba: torch.Tensor,
    valid: torch.Tensor,
    *,
    transparent_zero_weight: float | torch.Tensor,
) -> torch.Tensor:
    """
    Same contract as legacy _spec_loss in train_spec:
    - raw_out: core logits [N, 4, H, W]
    - target_rgba: [N, 4, H, W] in [0, 1]
    - valid: [N, H, W] mask (1 = supervised / valid diffuse)
    - transparent_zero_weight: float or 0-dim / [1] tensor; clamps to >= 0
    """
    tw = _to_transparent_weight(raw_out, transparent_zero_weight)
    invalid = 1.0 - valid
    pred = torch.sigmoid(raw_out[:, :4])
    base = _masked_l1(pred, target_rgba, valid)
    g_l1 = _channel_loss(pred, target_rgba, valid, 1)
    r_l1 = _channel_loss(pred, target_rgba, valid, 0)
    loss = base + 0.4 * g_l1 + 0.2 * r_l1
    bg_denom = torch.clamp(invalid.sum() * pred.shape[1], min=1.0)
    bg_l1 = torch.sum(torch.abs(pred) * invalid.unsqueeze(1)) / bg_denom
    loss = loss + tw * bg_l1
    return loss
