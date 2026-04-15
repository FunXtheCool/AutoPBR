"""Custom onnxblock loss matching :func:`spec_loss` for ORT ``generate_artifacts``.

Requires ``onnxruntime-training``. Forward ONNX must expose logits as a graph output
(default name ``logits`` from ``export_ort_forward_core``).

Training graph inputs added for loss (if missing): ``target_rgba``, ``valid``,
``transparent_zero_weight`` — same semantics as ``SpecularManifestDataset`` /
``train_spec`` PyTorch path.
"""

from __future__ import annotations

import copy

import numpy as np
import onnx
from onnx import TensorProto
from onnxruntime.training.onnxblock import _graph_utils as ob_graph_utils
from onnxruntime.training.onnxblock.blocks import Block, Sigmoid


def _has_graph_input(model: onnx.ModelProto, name: str) -> bool:
    return any(i.name == name for i in model.graph.input)


def _append_initializer(model: onnx.ModelProto, name: str, arr: np.ndarray) -> None:
    model.graph.initializer.append(onnx.numpy_helper.from_array(arr, name))


def _append_node(
    model: onnx.ModelProto, op_type: str, inputs: list[str], outputs: list[str], **kwargs
) -> None:
    model.graph.node.append(
        onnx.helper.make_node(
            op_type,
            inputs,
            outputs,
            ob_graph_utils.generate_graph_name(op_type),
            **kwargs,
        )
    )


def _ensure_target_rgba(model: onnx.ModelProto, logits_output: str, name: str) -> None:
    if _has_graph_input(model, name):
        return
    ref = ob_graph_utils.get_output_from_output_name(model, logits_output)
    vi = copy.deepcopy(ref)
    vi.name = name
    dims = vi.type.tensor_type.shape.dim
    if len(dims) >= 4:
        if dims[1].HasField("dim_value"):
            dims[1].dim_value = 4
        elif dims[1].HasField("dim_param"):
            dims[1].ClearField("dim_param")
            dims[1].dim_value = 4
    model.graph.input.append(vi)


def _ensure_valid_nhw(model: onnx.ModelProto, logits_output: str, name: str) -> None:
    if _has_graph_input(model, name):
        return
    ref = ob_graph_utils.get_output_from_output_name(model, logits_output)
    vi = copy.deepcopy(ref)
    vi.name = name
    dims = vi.type.tensor_type.shape.dim
    if len(dims) >= 4:
        new_dims = [dims[0], dims[2], dims[3]]
        del vi.type.tensor_type.shape.dim[:]
        for d in new_dims:
            vi.type.tensor_type.shape.dim.add().CopyFrom(d)
    model.graph.input.append(vi)


def _ensure_tw_scalar1(model: onnx.ModelProto, name: str) -> None:
    if _has_graph_input(model, name):
        return
    vi = onnx.helper.make_tensor_value_info(name, TensorProto.FLOAT, [1])
    model.graph.input.append(vi)


def _const_scalar(model: onnx.ModelProto, val: float) -> str:
    n = ob_graph_utils.generate_graph_name("const_f")
    _append_initializer(model, n, np.asarray([val], dtype=np.float32))
    return n


def _mul_const(model: onnx.ModelProto, x: str, val: float) -> str:
    c = _const_scalar(model, val)
    out = ob_graph_utils.generate_graph_name("mul_c")
    _append_node(model, "Mul", [x, c], [out])
    return out


def _reduce_sum_all(model: onnx.ModelProto, x: str) -> str:
    out = ob_graph_utils.generate_graph_name("rsum")
    _append_node(model, "ReduceSum", [x], [out], keepdims=False, noop_with_empty_axes=False)
    return out


def _reduce_mean_axis1_keepdims(model: onnx.ModelProto, x: str) -> str:
    """ReduceMean over channel axis (opset 13+: axes as input)."""
    axes = ob_graph_utils.generate_graph_name("rm_ax")
    _append_initializer(model, axes, np.asarray([1], dtype=np.int64))
    out = ob_graph_utils.generate_graph_name("rmean")
    _append_node(model, "ReduceMean", [x, axes], [out], keepdims=1, noop_with_empty_axes=False)
    return out


def _safe_div(model: onnx.ModelProto, num: str, den: str) -> str:
    one = _const_scalar(model, 1.0)
    den_clip = ob_graph_utils.generate_graph_name("den_clip")
    _append_node(model, "Max", [den, one], [den_clip])
    out = ob_graph_utils.generate_graph_name("div_out")
    _append_node(model, "Div", [num, den_clip], [out])
    return out


def _unsqueeze1(model: onnx.ModelProto, x: str) -> str:
    axes_name = ob_graph_utils.generate_graph_name("axes1")
    _append_initializer(model, axes_name, np.asarray([1], dtype=np.int64))
    out = ob_graph_utils.generate_graph_name("unsq")
    _append_node(model, "Unsqueeze", [x, axes_name], [out])
    return out


def _squeeze1(model: onnx.ModelProto, x: str) -> str:
    axes_name = ob_graph_utils.generate_graph_name("axes_sq")
    _append_initializer(model, axes_name, np.asarray([1], dtype=np.int64))
    out = ob_graph_utils.generate_graph_name("sq")
    _append_node(model, "Squeeze", [x, axes_name], [out])
    return out


def _slice_axis1_single(model: onnx.ModelProto, x: str, ch: int) -> str:
    starts = ob_graph_utils.generate_graph_name("sl_s")
    ends = ob_graph_utils.generate_graph_name("sl_e")
    axes = ob_graph_utils.generate_graph_name("sl_ax")
    _append_initializer(model, starts, np.asarray([ch], dtype=np.int64))
    _append_initializer(model, ends, np.asarray([ch + 1], dtype=np.int64))
    _append_initializer(model, axes, np.asarray([1], dtype=np.int64))
    out = ob_graph_utils.generate_graph_name("slice_ch")
    _append_node(model, "Slice", [x, starts, ends, axes], [out])
    return out


def _slice_axis1_range(model: onnx.ModelProto, x: str, ch_start: int, ch_end: int) -> str:
    starts = ob_graph_utils.generate_graph_name("sl_rs")
    ends = ob_graph_utils.generate_graph_name("sl_re")
    axes = ob_graph_utils.generate_graph_name("sl_rax")
    _append_initializer(model, starts, np.asarray([ch_start], dtype=np.int64))
    _append_initializer(model, ends, np.asarray([ch_end], dtype=np.int64))
    _append_initializer(model, axes, np.asarray([1], dtype=np.int64))
    out = ob_graph_utils.generate_graph_name("slice_rng")
    _append_node(model, "Slice", [x, starts, ends, axes], [out])
    return out


def _masked_l1_n4(model: onnx.ModelProto, pred: str, target: str, valid: str) -> str:
    mask = _unsqueeze1(model, valid)
    diff = ob_graph_utils.generate_graph_name("sub")
    _append_node(model, "Sub", [pred, target], [diff])
    ad = ob_graph_utils.generate_graph_name("abs")
    _append_node(model, "Abs", [diff], [ad])
    weighted = ob_graph_utils.generate_graph_name("mw")
    _append_node(model, "Mul", [ad, mask], [weighted])
    num = _reduce_sum_all(model, weighted)
    msum = _reduce_sum_all(model, mask)
    four = _const_scalar(model, 4.0)
    den = ob_graph_utils.generate_graph_name("den_ml")
    _append_node(model, "Mul", [msum, four], [den])
    return _safe_div(model, num, den)


def _channel_l1(model: onnx.ModelProto, pred: str, target: str, valid: str, ch: int) -> str:
    pc = _slice_axis1_single(model, pred, ch)
    tc = _slice_axis1_single(model, target, ch)
    diff = ob_graph_utils.generate_graph_name("ch_sub")
    _append_node(model, "Sub", [pc, tc], [diff])
    ad = ob_graph_utils.generate_graph_name("ch_abs")
    _append_node(model, "Abs", [diff], [ad])
    sq = _squeeze1(model, ad)
    w = ob_graph_utils.generate_graph_name("ch_w")
    _append_node(model, "Mul", [sq, valid], [w])
    num = _reduce_sum_all(model, w)
    vsum = _reduce_sum_all(model, valid)
    return _safe_div(model, num, vsum)


class SpecularLabPbrLoss(Block):
    """onnxblock loss graph aligned with ``ml_specular.spec_loss.spec_loss``."""

    def __init__(self) -> None:
        super().__init__()
        self._sigmoid = Sigmoid()

    def build(
        self,
        logits_name: str,
        target_rgba_name: str,
        valid_name: str,
        transparent_zero_weight_name: str,
    ) -> str:
        m = self.base
        _ensure_target_rgba(m, logits_name, target_rgba_name)
        _ensure_valid_nhw(m, logits_name, valid_name)
        _ensure_tw_scalar1(m, transparent_zero_weight_name)

        tw_c = ob_graph_utils.generate_graph_name("tw_clip")
        zero = _const_scalar(m, 0.0)
        _append_node(m, "Max", [transparent_zero_weight_name, zero], [tw_c])

        pred = self._sigmoid.build(logits_name)
        base = _masked_l1_n4(m, pred, target_rgba_name, valid_name)
        g_l1 = _channel_l1(m, pred, target_rgba_name, valid_name, 1)
        r_l1 = _channel_l1(m, pred, target_rgba_name, valid_name, 0)

        g_term = _mul_const(m, g_l1, 0.4)
        r_term = _mul_const(m, r_l1, 0.2)
        lr = ob_graph_utils.generate_graph_name("base_gr")
        _append_node(m, "Add", [base, g_term], [lr])
        lr2 = ob_graph_utils.generate_graph_name("loss_mid")
        _append_node(m, "Add", [lr, r_term], [lr2])

        one_v = _const_scalar(m, 1.0)
        inv = ob_graph_utils.generate_graph_name("inv")
        _append_node(m, "Sub", [one_v, valid_name], [inv])
        mask_i = _unsqueeze1(m, inv)
        pred_abs = ob_graph_utils.generate_graph_name("pabs2")
        _append_node(m, "Abs", [pred], [pred_abs])
        bg_m = ob_graph_utils.generate_graph_name("bgm")
        _append_node(m, "Mul", [pred_abs, mask_i], [bg_m])
        bg_num = _reduce_sum_all(m, bg_m)
        msum = _reduce_sum_all(m, mask_i)
        four = _const_scalar(m, 4.0)
        bg_den = ob_graph_utils.generate_graph_name("bgden")
        _append_node(m, "Mul", [msum, four], [bg_den])
        bg_l1 = _safe_div(m, bg_num, bg_den)
        tw_bg = ob_graph_utils.generate_graph_name("tw_bg")
        _append_node(m, "Mul", [tw_c, bg_l1], [tw_bg])
        loss_out = ob_graph_utils.generate_graph_name("spec_loss")
        _append_node(m, "Add", [lr2, tw_bg], [loss_out])
        return loss_out


def spec_loss_feed_input_names(logits_output_name: str) -> list[str]:
    """Loss input names for ``generate_artifacts(..., loss_input_names=...)``."""
    return [logits_output_name, "target_rgba", "valid", "transparent_zero_weight"]
