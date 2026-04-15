"""Train and export AutoPBR-compatible specular ML models."""

import sys

# Fail fast on old interpreters (e.g. Windows `python` -> 3.5) before parsing submodules
# that use PEP 585/604 syntax and f-strings.
_MIN_PY = (3, 10)
if sys.version_info < _MIN_PY:
    sys.stderr.write(
        "MlSpecularTrainer requires Python %d.%d+ (this interpreter is %d.%d).\n"
        "On Windows, use the launcher instead of plain `python`:\n"
        "  py -3.12 -m ml_specular.gen_from_labpbr_packs --dataset-root sample_dataset\n"
        "Or run:  gen_labpbr.cmd --dataset-root sample_dataset\n"
        % (_MIN_PY[0], _MIN_PY[1], sys.version_info[0], sys.version_info[1])
    )
    sys.exit(2)

# Linux Docker: libcudnn.so.9 must be visible before any submodule runs ``import torch`` (e.g. resume_training_prompt).
import ml_specular.cuda_torch_bootstrap  # noqa: F401
