@echo off

setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"



if not exist ".venv312\Scripts\python.exe" (

  echo.

  echo [ERROR] Virtual env not found: "%~dp0.venv312"

  echo Create it from this folder:

  echo   py -3.12 -m venv .venv312

  echo   .venv312\Scripts\pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124

  echo   .venv312\Scripts\pip install -r requirements.txt

  echo.

  pause

  exit /b 1

)



:PIPELINE

echo.

echo ========================================

echo   Specular ML - training pipeline

echo ========================================

echo   Working directory: %CD%

echo.

echo   [1] PyTorch workflow ^(manifest, PyTorch train, export-only^)

echo   [2] ORT workflow ^(loss ONNX export, artifact generation, ORT train^)

echo   [3] Torch-ORT workflow ^(same as [1] + ORTModule; CUDA vs TensorRT EP^)

echo   [0] Exit

echo.

choice /c 1230 /n /m "Select 1, 2, 3, or 0: "

if errorlevel 4 goto END

if errorlevel 3 goto TORCH_ORT_MAIN

if errorlevel 2 goto ORT_MENU

if errorlevel 1 goto PYTORCH_MAIN

goto PIPELINE



:PYTORCH_MAIN

set "RETURN_TO=PYTORCH_MAIN"

echo.

echo ========================================

echo   Specular ML - PyTorch workflow

echo ========================================

echo   [1] Resume training ^(prompts; defaults from checkpoint^)

echo   [2] Refresh LabPBR pack manifest(s)

echo       ^(single dataset or all defaults: multi/pixelart/realism/stylized/ext^)

echo   [3] Train direct artist _s ONNX model ^(diffuse -^> spec RGBA[^+conf]^) — PyTorch

echo   [4] Refresh manifest, then train ^(PyTorch^)

echo   [5] Export ONNX from checkpoint only ^(train_spec --export-only^)

echo   [6] Repeat last training session ^(saved command^)

echo   [0] Back

echo.

choice /c 1234560 /n /m "Select 1-6 or 0: "

if errorlevel 7 goto PIPELINE

if errorlevel 6 goto REPEAT_LAST

if errorlevel 5 goto EXPORT_ONLY

if errorlevel 4 goto BOTH

if errorlevel 3 goto TRAIN_SPEC_MENU

if errorlevel 2 goto MANIFEST

if errorlevel 1 goto RESUME_PYTORCH

goto PYTORCH_MAIN



:RESUME_PYTORCH

set "PY=%~dp0.venv312\Scripts\python.exe"

echo.

echo Resume training ^(defaults from checkpoint; PyTorch^)

echo.

"!PY!" -m ml_specular.resume_training_prompt

echo.

pause

goto !RETURN_TO!



:TORCH_ORT_MAIN

set "RETURN_TO=TORCH_ORT_MAIN"

echo.

echo ========================================

echo   Specular ML - Torch-ORT workflow ^(PyTorch + ORTModule^)

echo ========================================

echo   [1] Resume training ^(prompts; defaults from checkpoint^)

echo   [2] Refresh LabPBR pack manifest(s)

echo       ^(single dataset or all defaults: multi/pixelart/realism/stylized/ext^)

echo   [3] Train direct artist _s ONNX model — Torch-ORT ^(CUDA / TensorRT provider^)

echo   [4] Refresh manifest, then train ^(Torch-ORT^)

echo   [5] Export ONNX from checkpoint only ^(train_spec --export-only^)

echo   [6] Repeat last training session ^(saved command^)

echo   [0] Back

echo.

choice /c 1234560 /n /m "Select 1-6 or 0: "

if errorlevel 7 goto PIPELINE

if errorlevel 6 goto REPEAT_LAST

if errorlevel 5 goto EXPORT_ONLY

if errorlevel 4 goto BOTH_TORCH_ORT

if errorlevel 3 goto TRAIN_SPEC_MENU_TORCH_ORT

if errorlevel 2 goto MANIFEST

if errorlevel 1 goto RESUME_TORCH_ORT

goto TORCH_ORT_MAIN



:RESUME_TORCH_ORT

set "PY=%~dp0.venv312\Scripts\python.exe"

echo.

echo Resume training ^(defaults from checkpoint; Torch-ORT^)

echo.

"!PY!" -m ml_specular.resume_training_prompt --torch-ort

echo.

pause

goto !RETURN_TO!



:ORT_MENU

echo.

echo ========================================

echo   Specular ML - ORT training workflow

echo ========================================

echo   [1] Export train/eval loss ONNX ^(spec_loss graph; our script^)

echo   [2] Export forward ONNX, then ORT artifact generation

echo       ^(optimizer_model.onnx + training/eval graphs from onnxruntime.training^)

echo   [3] Run train_spec ORT backend ^(training / resume^)

echo   [4] Export ONNX from checkpoint only ^(train_spec --export-only^)

echo   [0] Back

echo.

choice /c 12340 /n /m "Select 1-4 or 0: "

if errorlevel 5 goto PIPELINE

if errorlevel 4 goto EXPORT_ONLY

if errorlevel 3 goto TRAIN_SPEC_ORT_MENU

if errorlevel 2 goto ORT_ARTIFACTS_CHAIN

if errorlevel 1 goto ORT_EXPORT_LOSS

goto ORT_MENU



:ORT_EXPORT_LOSS

set "PY=%~dp0.venv312\Scripts\python.exe"

set "ORT_LOSS_DIR=artifacts\ort"

set "ORT_LOSS_CKPT="

set "ORT_LOSS_INCH=4"

set "ORT_LOSS_OUTCH=5"

set "ORT_LOSS_WID=64"

set "ORT_LOSS_TR=128"

echo.

echo Export train_model.onnx and eval_model.onnx ^(full-batch loss = spec_loss contract^).

set /p ORT_LOSS_DIR=output directory [!ORT_LOSS_DIR!]: 

if "!ORT_LOSS_DIR!"=="" set "ORT_LOSS_DIR=artifacts\ort"

set /p ORT_LOSS_CKPT=optional checkpoint .pt ^(empty = random init^): 

set /p ORT_LOSS_INCH=in-channels 3 or 4 [!ORT_LOSS_INCH!]: 

if "!ORT_LOSS_INCH!"=="" set "ORT_LOSS_INCH=4"

set /p ORT_LOSS_OUTCH=out-channels 4 or 5 [!ORT_LOSS_OUTCH!]: 

if "!ORT_LOSS_OUTCH!"=="" set "ORT_LOSS_OUTCH=5"

set /p ORT_LOSS_WID=model width [!ORT_LOSS_WID!]: 

if "!ORT_LOSS_WID!"=="" set "ORT_LOSS_WID=64"

set /p ORT_LOSS_TR=trace train-res [!ORT_LOSS_TR!]: 

if "!ORT_LOSS_TR!"=="" set "ORT_LOSS_TR=128"

echo.

if not "!ORT_LOSS_CKPT!"=="" if not exist "!ORT_LOSS_CKPT!" (

  echo [Warn] Checkpoint not found, exporting without --ckpt: !ORT_LOSS_CKPT!

  set "ORT_LOSS_CKPT="

)

if "!ORT_LOSS_CKPT!"=="" (

  echo Running: "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR!

  echo.

  pause

  call :SAVE_LAST_SESSION "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR!

  "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR!

) else (

  echo Running: "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR! --ckpt "!ORT_LOSS_CKPT!"

  echo.

  pause

  call :SAVE_LAST_SESSION "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR! --ckpt "!ORT_LOSS_CKPT!"

  "!PY!" -m ml_specular.export_ort_specular_graphs --out-dir "!ORT_LOSS_DIR!" --in-channels !ORT_LOSS_INCH! --out-channels !ORT_LOSS_OUTCH! --width !ORT_LOSS_WID! --train-res !ORT_LOSS_TR! --ckpt "!ORT_LOSS_CKPT!"

)

echo.

pause

goto ORT_MENU



:ORT_ARTIFACTS_CHAIN

set "PY=%~dp0.venv312\Scripts\python.exe"

set "ORT_ADIR=artifacts\ort"

set "ORT_FCKPT=artifacts\SpecLab.pt"

set "ORT_INCH=4"

set "ORT_OUTCH=5"

set "ORT_WID=64"

set "ORT_TR=128"

set "ORT_OPT=adamw"

echo.

echo Step A: export forward_model.onnx ^(logits only^).

echo Step B: onnxruntime.training.artifacts — training_model.onnx, eval_model.onnx, optimizer_model.onnx.

echo Requires: pip install onnxruntime-training

echo.

set /p ORT_ADIR=artifact directory [!ORT_ADIR!]: 

if "!ORT_ADIR!"=="" set "ORT_ADIR=artifacts\ort"

set /p ORT_FCKPT=optional .pt for forward weights [!ORT_FCKPT!]: 

set /p ORT_INCH=in-channels 3 or 4 [!ORT_INCH!]: 

if "!ORT_INCH!"=="" set "ORT_INCH=4"

set /p ORT_OUTCH=out-channels 4 or 5 [!ORT_OUTCH!]: 

if "!ORT_OUTCH!"=="" set "ORT_OUTCH=5"

set /p ORT_WID=model width [!ORT_WID!]: 

if "!ORT_WID!"=="" set "ORT_WID=64"

set /p ORT_TR=trace train-res [!ORT_TR!]: 

if "!ORT_TR!"=="" set "ORT_TR=128"

set /p ORT_OPT=optimizer adamw or sgd [!ORT_OPT!]: 

if "!ORT_OPT!"=="" set "ORT_OPT=adamw"

echo.

set "FOUT=!ORT_ADIR!\forward_model.onnx"

if not exist "!ORT_FCKPT!" (

  echo [Info] No valid checkpoint at !ORT_FCKPT! — forward export uses random weights.

)

echo --- Step A ---

if exist "!ORT_FCKPT!" (

  echo "!PY!" -m ml_specular.export_ort_forward_core --out "!FOUT!" --in-channels !ORT_INCH! --out-channels !ORT_OUTCH! --width !ORT_WID! --train-res !ORT_TR! --ckpt "!ORT_FCKPT!"

  "!PY!" -m ml_specular.export_ort_forward_core --out "!FOUT!" --in-channels !ORT_INCH! --out-channels !ORT_OUTCH! --width !ORT_WID! --train-res !ORT_TR! --ckpt "!ORT_FCKPT!"

) else (

  echo "!PY!" -m ml_specular.export_ort_forward_core --out "!FOUT!" --in-channels !ORT_INCH! --out-channels !ORT_OUTCH! --width !ORT_WID! --train-res !ORT_TR!

  "!PY!" -m ml_specular.export_ort_forward_core --out "!FOUT!" --in-channels !ORT_INCH! --out-channels !ORT_OUTCH! --width !ORT_WID! --train-res !ORT_TR!

)

if errorlevel 1 (

  echo [Step A failed.]

  pause

  goto ORT_MENU

)

echo --- Step B ---

echo "!PY!" -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels !ORT_OUTCH! --base-onnx "!FOUT!" --artifact-directory "!ORT_ADIR!" --optimizer !ORT_OPT!

"!PY!" -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels !ORT_OUTCH! --base-onnx "!FOUT!" --artifact-directory "!ORT_ADIR!" --optimizer !ORT_OPT!

set GEN_ERR=!ERRORLEVEL!

echo.

if !GEN_ERR! neq 0 ( echo [Step B failed with code !GEN_ERR!.] ) else ( echo [Artifacts under %CD%\!ORT_ADIR!] )

pause

goto ORT_MENU



:TRAIN_SPEC_ORT_MENU

call :ASK_DATA_ROOT

set "PY=%~dp0.venv312\Scripts\python.exe"

set "SPEC_DEV=cuda"

set "SPEC_BAT=8"

set "SPEC_EP=40"

set "SPEC_SPATIAL=fixed"

set "SPEC_TR=128"

set "SPEC_MAX_SIDE="

set "SPEC_DOWNSCALE=box"

set "SPEC_ACCUM=1"
set "SPEC_NATIVE_RESTRICT=--native-restrict-to-target-tier"
set "SPEC_BP_ENABLED=--batch-policy-enabled"
set "SPEC_BP_MODE=sqrt"
set "SPEC_BP_BASE_B=8"
set "SPEC_BP_BASE_LR=0.001"
set "SPEC_BP_MAX_LR="
set "SPEC_BP_WARMUP_RATIO=0.05"
set "SPEC_BP_WARMUP_MIN=500"
set "SPEC_BP_WD_MODE=off"
set "SPEC_BP_CLIP=0"
set "SPEC_BP_EMA=--no-ema-enabled"
set "SPEC_BP_EMA_DECAY=0.999"
set "SPEC_BP_ENABLED=--batch-policy-enabled"
set "SPEC_BP_MODE=sqrt"
set "SPEC_BP_BASE_B=8"
set "SPEC_BP_BASE_LR=0.001"
set "SPEC_BP_MAX_LR="
set "SPEC_BP_WARMUP_RATIO=0.05"
set "SPEC_BP_WARMUP_MIN=500"
set "SPEC_BP_WD_MODE=off"
set "SPEC_BP_CLIP=0"
set "SPEC_BP_EMA=--no-ema-enabled"
set "SPEC_BP_EMA_DECAY=0.999"

set "SPEC_WRK=-1"

set "SPEC_INCH=4"

set "SPEC_WID=64"

set "SPEC_OUTCH=5"

set "SPEC_AMP="

set "SPEC_RESUME=--resume-auto"

set "SPEC_RESETOPT="

set "SPEC_ORT_DIR=artifacts\ort"

set "SPEC_ONNX=artifacts\SpecLab.onnx"

set "SPEC_CKPT=artifacts\SpecLab.pt"

echo.

echo ORT backend training ^(requires training_model.onnx or train_model.onnx + eval_model.onnx + optimizer_model.onnx^):

echo   device=!SPEC_DEV! batch=!SPEC_BAT! epochs=!SPEC_EP! train-res=!SPEC_TR! workers=!SPEC_WRK!

echo   in-ch=!SPEC_INCH! width=!SPEC_WID! out-ch=!SPEC_OUTCH! ^(4=RGBA, 5=RGBA+confidence^)

echo   ort-artifacts-dir=!SPEC_ORT_DIR!

echo.

set /p SPEC_DEV=device cuda or cpu [!SPEC_DEV!]: 

if "!SPEC_DEV!"=="" set "SPEC_DEV=cuda"

set /p SPEC_BAT=batch size [!SPEC_BAT!]: 

if "!SPEC_BAT!"=="" set "SPEC_BAT=8"

set /p SPEC_EP=epochs [!SPEC_EP!]: 

if "!SPEC_EP!"=="" set "SPEC_EP=40"

set /p SPEC_TR=train-res [!SPEC_TR!]: 

if "!SPEC_TR!"=="" set "SPEC_TR=128"

set /p SPEC_WRK=workers -1=auto, 0=main only [!SPEC_WRK!]: 

if "!SPEC_WRK!"=="" set "SPEC_WRK=-1"

set /p SPEC_INCH=in-channels 3 or 4 [!SPEC_INCH!]: 

if "!SPEC_INCH!"=="" set "SPEC_INCH=4"

set /p SPEC_WID=model width [!SPEC_WID!]: 

if "!SPEC_WID!"=="" set "SPEC_WID=64"

set /p SPEC_OUTCH=out-channels 4 or 5 [!SPEC_OUTCH!]: 

if "!SPEC_OUTCH!"=="" set "SPEC_OUTCH=5"

set /p SPEC_RESUMEYN=Resume automatically if checkpoint exists? Y/n: 

if /i "!SPEC_RESUMEYN!"=="n" (set "SPEC_RESUME=") else (set "SPEC_RESUME=--resume-auto")

set /p SPEC_RESETYN=On resume, reset optimizer state? y/N: 

if /i "!SPEC_RESETYN!"=="y" (set "SPEC_RESETOPT=--reset-optimizer") else (set "SPEC_RESETOPT=")

set /p SPEC_ORT_DIR=ORT artifacts dir [!SPEC_ORT_DIR!]: 

if "!SPEC_ORT_DIR!"=="" set "SPEC_ORT_DIR=artifacts\ort"

set /p SPEC_ONNX=Output .onnx path ^(inference export after train^) [!SPEC_ONNX!]: 

if "!SPEC_ONNX!"=="" set "SPEC_ONNX=artifacts\SpecLab.onnx"

set /p SPEC_CKPT=Checkpoint .pt path [!SPEC_CKPT!]: 

if "!SPEC_CKPT!"=="" set "SPEC_CKPT=artifacts\SpecLab.pt"

set /p SPEC_AMPN=Disable AMP on CUDA? y/N: 

if /i "!SPEC_AMPN!"=="y" (set "SPEC_AMP=--no-amp") else (set "SPEC_AMP=")

echo.

echo Will run:

echo   "!PY!" -m ml_specular.train_spec --trainer-backend ort --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --train-res !SPEC_TR! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

echo.

pause

call :SAVE_LAST_SESSION "!PY!" -m ml_specular.train_spec --trainer-backend ort --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --train-res !SPEC_TR! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

"!PY!" -m ml_specular.train_spec --trainer-backend ort --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --train-res !SPEC_TR! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

set SPECERR=!ERRORLEVEL!

echo.

if !SPECERR! equ 130 (

  echo [Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]

) else if !SPECERR! neq 0 ( echo [ORT training exited with error !SPECERR!.] ) else ( echo [Done. ONNX: %CD%\!SPEC_ONNX!] )

if !SPECERR! equ 0 (

  echo Running ONNX contract check...

  "!PY!" -m ml_specular.verify_spec_onnx "!SPEC_ONNX!"

)

pause

goto ORT_MENU



:REPEAT_LAST

set "LAST_CMD_FILE=%~dp0.last_training_session.bat"

if not exist "!LAST_CMD_FILE!" (

  echo.

  echo [No saved training session yet.]

  pause

  goto !RETURN_TO!

)

echo.

echo Re-running last session from:

echo   !LAST_CMD_FILE!

echo.

echo Command to run:

type "!LAST_CMD_FILE!"

echo.

pause

call "!LAST_CMD_FILE!"

echo.

pause

goto !RETURN_TO!



:EXPORT_ONLY

set "PY=%~dp0.venv312\Scripts\python.exe"

set "EX_CKPT=artifacts\SpecLab.pt"

set "EX_OUT=artifacts\SpecLab.onnx"

echo.

echo Export ONNX without retraining ^(reads in_channels, out_channels, width from .pt^).

set /p EX_CKPT=Checkpoint .pt path [!EX_CKPT!]: 

if "!EX_CKPT!"=="" set "EX_CKPT=artifacts\SpecLab.pt"

set /p EX_OUT=Output .onnx path [!EX_OUT!]: 

if "!EX_OUT!"=="" set "EX_OUT=artifacts\SpecLab.onnx"

echo.

echo Running: "!PY!" -m ml_specular.train_spec --export-only --ckpt "!EX_CKPT!" --out-onnx "!EX_OUT!"

echo.

pause

call :SAVE_LAST_SESSION "!PY!" -m ml_specular.train_spec --export-only --ckpt "!EX_CKPT!" --out-onnx "!EX_OUT!"

"!PY!" -m ml_specular.train_spec --export-only --ckpt "!EX_CKPT!" --out-onnx "!EX_OUT!"

set EX_ERR=!ERRORLEVEL!

echo.

if !EX_ERR! neq 0 ( echo [Export failed with code !EX_ERR!.] ) else ( echo [Done. ONNX: %CD%\!EX_OUT!] )

pause

goto !RETURN_TO!



:MANIFEST

echo.

echo Manifest refresh scope:

echo   [1] Single dataset

echo   [2] All defaults ^(multi_dataset, pixelart_dataset, realism_dataset, stylized_dataset, ext_dataset^)

choice /c 12 /n /m "Select 1 or 2: "

if errorlevel 2 (

  call :ASK_IGNORE_OPTIFINE

  echo.

  echo Running: gen_labpbr.cmd --all-default-datasets !GLP_IGNORE_OPTIFINE!

  echo.

  call gen_labpbr.cmd --all-default-datasets !GLP_IGNORE_OPTIFINE!

) else (

  call :ASK_DATA_ROOT

  call :ASK_IGNORE_OPTIFINE

  echo.

  echo Running: gen_labpbr.cmd --dataset-root "!DATA_ROOT!" !GLP_IGNORE_OPTIFINE!

  echo.

  call gen_labpbr.cmd --dataset-root "!DATA_ROOT!" !GLP_IGNORE_OPTIFINE!

)

echo.

if errorlevel 1 ( echo [Manifest step reported an error.] ) else ( echo [Manifest refresh done.] )

pause

goto !RETURN_TO!



:BOTH

call :ASK_DATA_ROOT

call :ASK_IGNORE_OPTIFINE

echo.

echo --- Step 1: manifest ---

call gen_labpbr.cmd --dataset-root "!DATA_ROOT!" !GLP_IGNORE_OPTIFINE!

if errorlevel 1 (

  echo [Manifest step failed - skipping training.]

  pause

  goto PYTORCH_MAIN

)

echo.

echo --- Step 2: training ---

goto TRAIN_SPEC_MENU



:BOTH_TORCH_ORT

call :ASK_DATA_ROOT

call :ASK_IGNORE_OPTIFINE

echo.

echo --- Step 1: manifest ---

call gen_labpbr.cmd --dataset-root "!DATA_ROOT!" !GLP_IGNORE_OPTIFINE!

if errorlevel 1 (

  echo [Manifest step failed - skipping training.]

  pause

  goto TORCH_ORT_MAIN

)

echo.

echo --- Step 2: training ---

goto TRAIN_SPEC_MENU_TORCH_ORT



:TRAIN_SPEC_MENU

call :ASK_DATA_ROOT

set "PY=%~dp0.venv312\Scripts\python.exe"

set "SPEC_DEV=cuda"

set "SPEC_BAT=8"

set "SPEC_EP=40"

set "SPEC_SPATIAL=fixed"

set "SPEC_TR=128"

set "SPEC_MAX_SIDE="

set "SPEC_DOWNSCALE=box"

set "SPEC_ACCUM=1"
set "SPEC_NATIVE_RESTRICT=--native-restrict-to-target-tier"

set "SPEC_WRK=-1"

set "SPEC_INCH=4"

set "SPEC_WID=64"

set "SPEC_OUTCH=5"

set "SPEC_AMP="

set "SPEC_TORCH_ORT="
set "SPEC_TORCH_ORT_PROVIDER="
set "SPEC_TRT_FP16="
set "SPEC_TORCH_ORT_DEBUG="
set "SPEC_TORCH_ORT_MEM_OPT=0"
set "SPEC_TORCH_ORT_TRITON="
set "SPEC_TORCH_ORT_ZERO3="

set "SPEC_BACKEND=pytorch"

set "SPEC_RESUME=--resume-auto"

set "SPEC_RESETOPT="

set "SPEC_ORT_DIR=artifacts\ort"

set "SPEC_ONNX=artifacts\SpecLab.onnx"

set "SPEC_CKPT=artifacts\SpecLab.pt"

echo.

echo Direct artist-spec training presets ^(PyTorch backend^):

echo   device=!SPEC_DEV! batch=!SPEC_BAT! epochs=!SPEC_EP! spatial=!SPEC_SPATIAL! train-res=!SPEC_TR! workers=!SPEC_WRK!

echo   in-ch=!SPEC_INCH! width=!SPEC_WID! out-ch=!SPEC_OUTCH! ^(4=RGBA, 5=RGBA+confidence^)

echo   backend=!SPEC_BACKEND! resume=!SPEC_RESUME!

echo.

set /p SPEC_DEV=device cuda or cpu [!SPEC_DEV!]: 

if "!SPEC_DEV!"=="" set "SPEC_DEV=cuda"

set /p SPEC_BAT=batch size [!SPEC_BAT!]: 

if "!SPEC_BAT!"=="" set "SPEC_BAT=8"

set /p SPEC_EP=epochs [!SPEC_EP!]: 

if "!SPEC_EP!"=="" set "SPEC_EP=40"

set /p SPEC_SPATIAL=spatial-mode fixed or native [!SPEC_SPATIAL!]: 

if "!SPEC_SPATIAL!"=="" set "SPEC_SPATIAL=fixed"

if /i not "!SPEC_SPATIAL!"=="fixed" if /i not "!SPEC_SPATIAL!"=="native" (
  echo [Warn] Unsupported spatial-mode "!SPEC_SPATIAL!"; defaulting to fixed.
  set "SPEC_SPATIAL=fixed"
)

if /i "!SPEC_SPATIAL!"=="fixed" (
  set /p SPEC_TR=train-res [!SPEC_TR!]: 
  if "!SPEC_TR!"=="" set "SPEC_TR=128"
) else (
  echo Native mode: train-res prompt skipped.
)

set /p SPEC_MAX_SIDE=max-train-side ^(empty = none^) [!SPEC_MAX_SIDE!]: 

if "!SPEC_MAX_SIDE!"=="" set "SPEC_MAX_SIDE="

set /p SPEC_DOWNSCALE=downscale-for-memory box^|lanczos^|nearest [!SPEC_DOWNSCALE!]: 

if "!SPEC_DOWNSCALE!"=="" set "SPEC_DOWNSCALE=box"

if /i not "!SPEC_DOWNSCALE!"=="box" if /i not "!SPEC_DOWNSCALE!"=="lanczos" if /i not "!SPEC_DOWNSCALE!"=="nearest" (
  echo [Warn] Unsupported downscale option "!SPEC_DOWNSCALE!"; defaulting to box.
  set "SPEC_DOWNSCALE=box"
)

set /p SPEC_ACCUM=grad-accum-steps [!SPEC_ACCUM!]: 

if "!SPEC_ACCUM!"=="" set "SPEC_ACCUM=1"

set /p SPEC_NATIVE_RESTRICT_YN=Native mode: enforce per-sample tag-match filter? Y/n: 
if /i "!SPEC_NATIVE_RESTRICT_YN!"=="n" (set "SPEC_NATIVE_RESTRICT=--no-native-restrict-to-target-tier") else (set "SPEC_NATIVE_RESTRICT=--native-restrict-to-target-tier")

set /p SPEC_BP_CUSTOM_YN=Customize Batch Policy? y/N: 
if /i "!SPEC_BP_CUSTOM_YN!"=="y" (
  set /p SPEC_BP_AUTO_YN=Enable batch scaling safety policy? Y/n: 
  if /i "!SPEC_BP_AUTO_YN!"=="n" (set "SPEC_BP_ENABLED=--no-batch-policy-enabled") else (set "SPEC_BP_ENABLED=--batch-policy-enabled")
  set /p SPEC_BP_MODE=batch policy LR mode off^|sqrt^|linear [!SPEC_BP_MODE!]: 
  if "!SPEC_BP_MODE!"=="" set "SPEC_BP_MODE=sqrt"
  if /i not "!SPEC_BP_MODE!"=="off" if /i not "!SPEC_BP_MODE!"=="sqrt" if /i not "!SPEC_BP_MODE!"=="linear" (
    echo [Warn] Invalid LR mode; defaulting to sqrt.
    set "SPEC_BP_MODE=sqrt"
  )
  set /p SPEC_BP_BASE_B=baseline effective batch [!SPEC_BP_BASE_B!]: 
  if "!SPEC_BP_BASE_B!"=="" set "SPEC_BP_BASE_B=8"
  set /p SPEC_BP_BASE_LR=baseline LR [!SPEC_BP_BASE_LR!]: 
  if "!SPEC_BP_BASE_LR!"=="" set "SPEC_BP_BASE_LR=0.001"
  set /p SPEC_BP_MAX_LR=max scaled LR ^(empty=none^) [!SPEC_BP_MAX_LR!]: 
  set /p SPEC_BP_WARMUP_RATIO=warmup ratio 0..1 [!SPEC_BP_WARMUP_RATIO!]: 
  if "!SPEC_BP_WARMUP_RATIO!"=="" set "SPEC_BP_WARMUP_RATIO=0.05"
  set /p SPEC_BP_WARMUP_MIN=warmup min steps [!SPEC_BP_WARMUP_MIN!]: 
  if "!SPEC_BP_WARMUP_MIN!"=="" set "SPEC_BP_WARMUP_MIN=500"
  set /p SPEC_BP_WD_MODE=weight decay mode off^|mild_batch_scaled [!SPEC_BP_WD_MODE!]: 
  if "!SPEC_BP_WD_MODE!"=="" set "SPEC_BP_WD_MODE=off"
  if /i not "!SPEC_BP_WD_MODE!"=="off" if /i not "!SPEC_BP_WD_MODE!"=="mild_batch_scaled" (
    echo [Warn] Invalid weight decay mode; defaulting to off.
    set "SPEC_BP_WD_MODE=off"
  )
  set /p SPEC_BP_CLIP=grad clip norm ^(<=0 disables^) [!SPEC_BP_CLIP!]: 
  if "!SPEC_BP_CLIP!"=="" set "SPEC_BP_CLIP=0"
  set /p SPEC_BP_EMA_YN=Enable EMA for eval/checkpoints? y/N: 
  if /i "!SPEC_BP_EMA_YN!"=="y" (set "SPEC_BP_EMA=--ema-enabled") else (set "SPEC_BP_EMA=--no-ema-enabled")
  set /p SPEC_BP_EMA_DECAY=EMA decay ^(0..1^) [!SPEC_BP_EMA_DECAY!]: 
  if "!SPEC_BP_EMA_DECAY!"=="" set "SPEC_BP_EMA_DECAY=0.999"
)

set /p SPEC_WRK=workers -1=auto, 0=main only [!SPEC_WRK!]: 

if "!SPEC_WRK!"=="" set "SPEC_WRK=-1"

set /p SPEC_INCH=in-channels 3 or 4 [!SPEC_INCH!]: 

if "!SPEC_INCH!"=="" set "SPEC_INCH=4"

set /p SPEC_WID=model width [!SPEC_WID!]: 

if "!SPEC_WID!"=="" set "SPEC_WID=64"

set /p SPEC_OUTCH=out-channels 4 or 5 [!SPEC_OUTCH!]: 

if "!SPEC_OUTCH!"=="" set "SPEC_OUTCH=5"

set /p SPEC_BACKEND=backend pytorch or ort [!SPEC_BACKEND!]: 

if "!SPEC_BACKEND!"=="" set "SPEC_BACKEND=pytorch"

set /p SPEC_RESUMEYN=Resume automatically if checkpoint exists? Y/n: 

if /i "!SPEC_RESUMEYN!"=="n" (set "SPEC_RESUME=") else (set "SPEC_RESUME=--resume-auto")

set /p SPEC_RESETYN=On resume, reset optimizer state? y/N: 

if /i "!SPEC_RESETYN!"=="y" (set "SPEC_RESETOPT=--reset-optimizer") else (set "SPEC_RESETOPT=")

if /i "!SPEC_BACKEND!"=="ort" (

  set /p SPEC_ORT_DIR=ORT artifacts dir [!SPEC_ORT_DIR!]: 

  if "!SPEC_ORT_DIR!"=="" set "SPEC_ORT_DIR=artifacts\ort"

)

if /i "!SPEC_BACKEND!"=="ort" if /i "!SPEC_SPATIAL!"=="native" (
  echo [Warn] ORT backend only supports spatial-mode=fixed; forcing fixed.
  set "SPEC_SPATIAL=fixed"
)

set /p SPEC_ONNX=Output .onnx path [!SPEC_ONNX!]: 

if "!SPEC_ONNX!"=="" set "SPEC_ONNX=artifacts\SpecLab.onnx"

set /p SPEC_CKPT=Checkpoint .pt path [!SPEC_CKPT!]: 

if "!SPEC_CKPT!"=="" set "SPEC_CKPT=artifacts\SpecLab.pt"

set /p SPEC_AMPN=Disable AMP on CUDA? y/N: 

if /i "!SPEC_AMPN!"=="y" (set "SPEC_AMP=--no-amp") else (set "SPEC_AMP=")

set /p SPEC_TORCH_ORTYN=Use torch-ort ORTModule ^(CUDA only; see requirements-torch-ort.txt^)? y/N: 

if /i "!SPEC_TORCH_ORTYN!"=="y" (
  set "SPEC_TORCH_ORT=--torch-ort"
  set /p SPEC_TORCH_ORT_PROVIDER_RAW=tensor backend for torch-ort cuda or tensorrt [cuda]: 
  if "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="" set "SPEC_TORCH_ORT_PROVIDER_RAW=cuda"
  if /i not "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="cuda" if /i not "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="tensorrt" (
    echo [Warn] Unsupported value "!SPEC_TORCH_ORT_PROVIDER_RAW!"; defaulting to cuda.
    set "SPEC_TORCH_ORT_PROVIDER_RAW=cuda"
  )
  set "SPEC_TORCH_ORT_PROVIDER=--torch-ort-provider !SPEC_TORCH_ORT_PROVIDER_RAW!"
  set "SPEC_TRT_FP16="
  if /i "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="tensorrt" (
    set /p SPEC_TRT_FP16_YN=TensorRT FP16 engine ^(ORT_TENSORRT_FP16_ENABLE^)? Y/n: 
    if /i "!SPEC_TRT_FP16_YN!"=="n" (set "SPEC_TRT_FP16=--no-torch-ort-tensorrt-fp16") else (set "SPEC_TRT_FP16=")
  )
  set /p SPEC_TORCH_ORT_DEBUG_YN=Print torch-ort extension diagnostics ^(--torch-ort-debug^)? y/N: 
  if /i "!SPEC_TORCH_ORT_DEBUG_YN!"=="y" (set "SPEC_TORCH_ORT_DEBUG=--torch-ort-debug") else (set "SPEC_TORCH_ORT_DEBUG=")
  set /p SPEC_TORCH_ORT_MEM_OPT=ORTModule Memory Optimizer level ^(--torch-ort-memory-opt-level^) [!SPEC_TORCH_ORT_MEM_OPT!]: 
  if "!SPEC_TORCH_ORT_MEM_OPT!"=="" set "SPEC_TORCH_ORT_MEM_OPT=0"
  echo(!SPEC_TORCH_ORT_MEM_OPT!| findstr /R "^[0-9][0-9]*$" >nul
  if errorlevel 1 (
    echo [Warn] Invalid Memory Optimizer level "!SPEC_TORCH_ORT_MEM_OPT!"; defaulting to 0.
    set "SPEC_TORCH_ORT_MEM_OPT=0"
  )
  set /p SPEC_TORCH_ORT_TRITON_YN=Enable TritonOp ^(--torch-ort-triton-op-enabled^)? y/N: 
  if /i "!SPEC_TORCH_ORT_TRITON_YN!"=="y" (set "SPEC_TORCH_ORT_TRITON=--torch-ort-triton-op-enabled") else (set "SPEC_TORCH_ORT_TRITON=")
  set /p SPEC_TORCH_ORT_ZERO3_YN=Enable ZeRO stage3 support ^(--torch-ort-zero-stage3-support^)? y/N: 
  if /i "!SPEC_TORCH_ORT_ZERO3_YN!"=="y" (set "SPEC_TORCH_ORT_ZERO3=--torch-ort-zero-stage3-support") else (set "SPEC_TORCH_ORT_ZERO3=")
) else (
  set "SPEC_TORCH_ORT="
  set "SPEC_TORCH_ORT_PROVIDER="
  set "SPEC_TRT_FP16="
  set "SPEC_TORCH_ORT_DEBUG="
  set "SPEC_TORCH_ORT_MEM_OPT=0"
  set "SPEC_TORCH_ORT_TRITON="
  set "SPEC_TORCH_ORT_ZERO3="
)

echo.

echo Will run:

set "SPEC_MAX_SIDE_ARG="
if not "!SPEC_MAX_SIDE!"=="" set "SPEC_MAX_SIDE_ARG=--max-train-side !SPEC_MAX_SIDE!"
set "SPEC_BP_MAX_LR_ARG="
if not "!SPEC_BP_MAX_LR!"=="" set "SPEC_BP_MAX_LR_ARG=--batch-policy-max-lr !SPEC_BP_MAX_LR!"
set "SPEC_BATCH_POLICY_ARGS=!SPEC_BP_ENABLED! --batch-policy-lr-mode !SPEC_BP_MODE! --batch-policy-baseline-batch !SPEC_BP_BASE_B! --batch-policy-baseline-lr !SPEC_BP_BASE_LR! !SPEC_BP_MAX_LR_ARG! --warmup-ratio !SPEC_BP_WARMUP_RATIO! --warmup-min-steps !SPEC_BP_WARMUP_MIN! --weight-decay-mode !SPEC_BP_WD_MODE! --grad-clip-norm !SPEC_BP_CLIP! !SPEC_BP_EMA! --ema-decay !SPEC_BP_EMA_DECAY!"

echo   "!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

echo.

pause

call :SAVE_LAST_SESSION "!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

"!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

set SPECERR=!ERRORLEVEL!

echo.

if !SPECERR! equ 130 (

  echo [Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]

) else if !SPECERR! neq 0 ( echo [Direct spec training exited with error !SPECERR!.] ) else ( echo [Done. ONNX: %CD%\!SPEC_ONNX!] )

if !SPECERR! equ 0 (

  echo Running ONNX contract check...

  "!PY!" -m ml_specular.verify_spec_onnx "!SPEC_ONNX!"

)

pause

goto PYTORCH_MAIN



:TRAIN_SPEC_MENU_TORCH_ORT

call :ASK_DATA_ROOT

set "PY=%~dp0.venv312\Scripts\python.exe"

set "SPEC_DEV=cuda"

set "SPEC_BAT=8"

set "SPEC_EP=40"

set "SPEC_SPATIAL=fixed"

set "SPEC_TR=128"

set "SPEC_MAX_SIDE="

set "SPEC_DOWNSCALE=box"

set "SPEC_ACCUM=1"

set "SPEC_WRK=-1"

set "SPEC_INCH=4"

set "SPEC_WID=64"

set "SPEC_OUTCH=5"

set "SPEC_AMP="

set "SPEC_TORCH_ORT=--torch-ort"

set "SPEC_TORCH_ORT_PROVIDER_RAW=cuda"

set "SPEC_TRT_FP16="
set "SPEC_TORCH_ORT_DEBUG="
set "SPEC_TORCH_ORT_MEM_OPT=0"
set "SPEC_TORCH_ORT_TRITON="
set "SPEC_TORCH_ORT_ZERO3="

set "SPEC_BACKEND=pytorch"

set "SPEC_RESUME=--resume-auto"

set "SPEC_RESETOPT="

set "SPEC_ORT_DIR=artifacts\ort"

set "SPEC_ONNX=artifacts\SpecLab.onnx"

set "SPEC_CKPT=artifacts\SpecLab.pt"

echo.

echo Direct artist-spec training ^(Torch-ORT / ORTModule — requires CUDA + torch-ort^):

echo   device=!SPEC_DEV! batch=!SPEC_BAT! epochs=!SPEC_EP! spatial=!SPEC_SPATIAL! train-res=!SPEC_TR! workers=!SPEC_WRK!

echo   in-ch=!SPEC_INCH! width=!SPEC_WID! out-ch=!SPEC_OUTCH! ^(4=RGBA, 5=RGBA+confidence^)

echo   --torch-ort is always on; choose execution provider ^(TensorRT needs compatible ORT/GPU stack^).

echo.

set /p SPEC_DEV=device cuda or cpu [!SPEC_DEV!]: 

if "!SPEC_DEV!"=="" set "SPEC_DEV=cuda"

set /p SPEC_BAT=batch size [!SPEC_BAT!]: 

if "!SPEC_BAT!"=="" set "SPEC_BAT=8"

set /p SPEC_EP=epochs [!SPEC_EP!]: 

if "!SPEC_EP!"=="" set "SPEC_EP=40"

set /p SPEC_SPATIAL=spatial-mode fixed or native [!SPEC_SPATIAL!]: 

if "!SPEC_SPATIAL!"=="" set "SPEC_SPATIAL=fixed"

if /i not "!SPEC_SPATIAL!"=="fixed" if /i not "!SPEC_SPATIAL!"=="native" (
  echo [Warn] Unsupported spatial-mode "!SPEC_SPATIAL!"; defaulting to fixed.
  set "SPEC_SPATIAL=fixed"
)

if /i "!SPEC_SPATIAL!"=="fixed" (
  set /p SPEC_TR=train-res [!SPEC_TR!]: 
  if "!SPEC_TR!"=="" set "SPEC_TR=128"
) else (
  echo Native mode: train-res prompt skipped.
)

set /p SPEC_MAX_SIDE=max-train-side ^(empty = none^) [!SPEC_MAX_SIDE!]: 

if "!SPEC_MAX_SIDE!"=="" set "SPEC_MAX_SIDE="

set /p SPEC_DOWNSCALE=downscale-for-memory box^|lanczos^|nearest [!SPEC_DOWNSCALE!]: 

if "!SPEC_DOWNSCALE!"=="" set "SPEC_DOWNSCALE=box"

if /i not "!SPEC_DOWNSCALE!"=="box" if /i not "!SPEC_DOWNSCALE!"=="lanczos" if /i not "!SPEC_DOWNSCALE!"=="nearest" (
  echo [Warn] Unsupported downscale option "!SPEC_DOWNSCALE!"; defaulting to box.
  set "SPEC_DOWNSCALE=box"
)

set /p SPEC_ACCUM=grad-accum-steps [!SPEC_ACCUM!]: 

if "!SPEC_ACCUM!"=="" set "SPEC_ACCUM=1"

set /p SPEC_BP_CUSTOM_YN=Customize Batch Policy? y/N: 
if /i "!SPEC_BP_CUSTOM_YN!"=="y" (
  set /p SPEC_BP_AUTO_YN=Enable batch scaling safety policy? Y/n: 
  if /i "!SPEC_BP_AUTO_YN!"=="n" (set "SPEC_BP_ENABLED=--no-batch-policy-enabled") else (set "SPEC_BP_ENABLED=--batch-policy-enabled")
  set /p SPEC_BP_MODE=batch policy LR mode off^|sqrt^|linear [!SPEC_BP_MODE!]: 
  if "!SPEC_BP_MODE!"=="" set "SPEC_BP_MODE=sqrt"
  if /i not "!SPEC_BP_MODE!"=="off" if /i not "!SPEC_BP_MODE!"=="sqrt" if /i not "!SPEC_BP_MODE!"=="linear" (
    echo [Warn] Invalid LR mode; defaulting to sqrt.
    set "SPEC_BP_MODE=sqrt"
  )
  set /p SPEC_BP_BASE_B=baseline effective batch [!SPEC_BP_BASE_B!]: 
  if "!SPEC_BP_BASE_B!"=="" set "SPEC_BP_BASE_B=8"
  set /p SPEC_BP_BASE_LR=baseline LR [!SPEC_BP_BASE_LR!]: 
  if "!SPEC_BP_BASE_LR!"=="" set "SPEC_BP_BASE_LR=0.001"
  set /p SPEC_BP_MAX_LR=max scaled LR ^(empty=none^) [!SPEC_BP_MAX_LR!]: 
  set /p SPEC_BP_WARMUP_RATIO=warmup ratio 0..1 [!SPEC_BP_WARMUP_RATIO!]: 
  if "!SPEC_BP_WARMUP_RATIO!"=="" set "SPEC_BP_WARMUP_RATIO=0.05"
  set /p SPEC_BP_WARMUP_MIN=warmup min steps [!SPEC_BP_WARMUP_MIN!]: 
  if "!SPEC_BP_WARMUP_MIN!"=="" set "SPEC_BP_WARMUP_MIN=500"
  set /p SPEC_BP_WD_MODE=weight decay mode off^|mild_batch_scaled [!SPEC_BP_WD_MODE!]: 
  if "!SPEC_BP_WD_MODE!"=="" set "SPEC_BP_WD_MODE=off"
  if /i not "!SPEC_BP_WD_MODE!"=="off" if /i not "!SPEC_BP_WD_MODE!"=="mild_batch_scaled" (
    echo [Warn] Invalid weight decay mode; defaulting to off.
    set "SPEC_BP_WD_MODE=off"
  )
  set /p SPEC_BP_CLIP=grad clip norm ^(<=0 disables^) [!SPEC_BP_CLIP!]: 
  if "!SPEC_BP_CLIP!"=="" set "SPEC_BP_CLIP=0"
  set /p SPEC_BP_EMA_YN=Enable EMA for eval/checkpoints? y/N: 
  if /i "!SPEC_BP_EMA_YN!"=="y" (set "SPEC_BP_EMA=--ema-enabled") else (set "SPEC_BP_EMA=--no-ema-enabled")
  set /p SPEC_BP_EMA_DECAY=EMA decay ^(0..1^) [!SPEC_BP_EMA_DECAY!]: 
  if "!SPEC_BP_EMA_DECAY!"=="" set "SPEC_BP_EMA_DECAY=0.999"
)

set /p SPEC_WRK=workers -1=auto, 0=main only [!SPEC_WRK!]: 

if "!SPEC_WRK!"=="" set "SPEC_WRK=-1"

set /p SPEC_INCH=in-channels 3 or 4 [!SPEC_INCH!]: 

if "!SPEC_INCH!"=="" set "SPEC_INCH=4"

set /p SPEC_WID=model width [!SPEC_WID!]: 

if "!SPEC_WID!"=="" set "SPEC_WID=64"

set /p SPEC_OUTCH=out-channels 4 or 5 [!SPEC_OUTCH!]: 

if "!SPEC_OUTCH!"=="" set "SPEC_OUTCH=5"

set /p SPEC_TORCH_ORT_PROVIDER_RAW=torch-ort execution provider cuda or tensorrt [!SPEC_TORCH_ORT_PROVIDER_RAW!]: 

if "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="" set "SPEC_TORCH_ORT_PROVIDER_RAW=cuda"

if /i not "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="cuda" if /i not "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="tensorrt" (

  echo [Warn] Unsupported value "!SPEC_TORCH_ORT_PROVIDER_RAW!"; defaulting to cuda.

  set "SPEC_TORCH_ORT_PROVIDER_RAW=cuda"

)

set "SPEC_TORCH_ORT_PROVIDER=--torch-ort-provider !SPEC_TORCH_ORT_PROVIDER_RAW!"

if /i "!SPEC_TORCH_ORT_PROVIDER_RAW!"=="tensorrt" (
  set /p SPEC_TRT_FP16_YN=TensorRT FP16 engine ^(ORT_TENSORRT_FP16_ENABLE^)? Y/n: 
  if /i "!SPEC_TRT_FP16_YN!"=="n" (set "SPEC_TRT_FP16=--no-torch-ort-tensorrt-fp16") else (set "SPEC_TRT_FP16=")
) else (
  set "SPEC_TRT_FP16="
)

set /p SPEC_TORCH_ORT_DEBUG_YN=Print torch-ort extension diagnostics ^(--torch-ort-debug^)? y/N: 

if /i "!SPEC_TORCH_ORT_DEBUG_YN!"=="y" (set "SPEC_TORCH_ORT_DEBUG=--torch-ort-debug") else (set "SPEC_TORCH_ORT_DEBUG=")

set /p SPEC_TORCH_ORT_MEM_OPT=ORTModule Memory Optimizer level ^(--torch-ort-memory-opt-level^) [!SPEC_TORCH_ORT_MEM_OPT!]: 

if "!SPEC_TORCH_ORT_MEM_OPT!"=="" set "SPEC_TORCH_ORT_MEM_OPT=0"

echo(!SPEC_TORCH_ORT_MEM_OPT!| findstr /R "^[0-9][0-9]*$" >nul
if errorlevel 1 (
  echo [Warn] Invalid Memory Optimizer level "!SPEC_TORCH_ORT_MEM_OPT!"; defaulting to 0.
  set "SPEC_TORCH_ORT_MEM_OPT=0"
)

set /p SPEC_TORCH_ORT_TRITON_YN=Enable TritonOp ^(--torch-ort-triton-op-enabled^)? y/N: 
if /i "!SPEC_TORCH_ORT_TRITON_YN!"=="y" (set "SPEC_TORCH_ORT_TRITON=--torch-ort-triton-op-enabled") else (set "SPEC_TORCH_ORT_TRITON=")

set /p SPEC_TORCH_ORT_ZERO3_YN=Enable ZeRO stage3 support ^(--torch-ort-zero-stage3-support^)? y/N: 
if /i "!SPEC_TORCH_ORT_ZERO3_YN!"=="y" (set "SPEC_TORCH_ORT_ZERO3=--torch-ort-zero-stage3-support") else (set "SPEC_TORCH_ORT_ZERO3=")

set /p SPEC_RESUMEYN=Resume automatically if checkpoint exists? Y/n: 

if /i "!SPEC_RESUMEYN!"=="n" (set "SPEC_RESUME=") else (set "SPEC_RESUME=--resume-auto")

set /p SPEC_RESETYN=On resume, reset optimizer state? y/N: 

if /i "!SPEC_RESETYN!"=="y" (set "SPEC_RESETOPT=--reset-optimizer") else (set "SPEC_RESETOPT=")

set /p SPEC_ONNX=Output .onnx path [!SPEC_ONNX!]: 

if "!SPEC_ONNX!"=="" set "SPEC_ONNX=artifacts\SpecLab.onnx"

set /p SPEC_CKPT=Checkpoint .pt path [!SPEC_CKPT!]: 

if "!SPEC_CKPT!"=="" set "SPEC_CKPT=artifacts\SpecLab.pt"

set /p SPEC_AMPN=Disable AMP on CUDA? y/N: 

if /i "!SPEC_AMPN!"=="y" (set "SPEC_AMP=--no-amp") else (set "SPEC_AMP=")

echo.

echo Will run:

set "SPEC_MAX_SIDE_ARG="
if not "!SPEC_MAX_SIDE!"=="" set "SPEC_MAX_SIDE_ARG=--max-train-side !SPEC_MAX_SIDE!"
set "SPEC_BP_MAX_LR_ARG="
if not "!SPEC_BP_MAX_LR!"=="" set "SPEC_BP_MAX_LR_ARG=--batch-policy-max-lr !SPEC_BP_MAX_LR!"
set "SPEC_BATCH_POLICY_ARGS=!SPEC_BP_ENABLED! --batch-policy-lr-mode !SPEC_BP_MODE! --batch-policy-baseline-batch !SPEC_BP_BASE_B! --batch-policy-baseline-lr !SPEC_BP_BASE_LR! !SPEC_BP_MAX_LR_ARG! --warmup-ratio !SPEC_BP_WARMUP_RATIO! --warmup-min-steps !SPEC_BP_WARMUP_MIN! --weight-decay-mode !SPEC_BP_WD_MODE! --grad-clip-norm !SPEC_BP_CLIP! !SPEC_BP_EMA! --ema-decay !SPEC_BP_EMA_DECAY!"

echo   "!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

echo.

pause

call :SAVE_LAST_SESSION "!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

"!PY!" -m ml_specular.train_spec --trainer-backend !SPEC_BACKEND! --data-root "!DATA_ROOT!" --device !SPEC_DEV! --batch !SPEC_BAT! --epochs !SPEC_EP! --spatial-mode !SPEC_SPATIAL! --train-res !SPEC_TR! !SPEC_MAX_SIDE_ARG! --downscale-for-memory !SPEC_DOWNSCALE! --grad-accum-steps !SPEC_ACCUM! !SPEC_BATCH_POLICY_ARGS! !SPEC_NATIVE_RESTRICT! --workers !SPEC_WRK! --in-channels !SPEC_INCH! --width !SPEC_WID! --out-channels !SPEC_OUTCH! !SPEC_RESUME! !SPEC_RESETOPT! !SPEC_AMP! !SPEC_TORCH_ORT! !SPEC_TORCH_ORT_PROVIDER! !SPEC_TRT_FP16! !SPEC_TORCH_ORT_DEBUG! --torch-ort-memory-opt-level !SPEC_TORCH_ORT_MEM_OPT! !SPEC_TORCH_ORT_TRITON! !SPEC_TORCH_ORT_ZERO3! --ort-artifacts-dir "!SPEC_ORT_DIR!" --out-onnx "!SPEC_ONNX!" --ckpt "!SPEC_CKPT!"

set SPECERR=!ERRORLEVEL!

echo.

if !SPECERR! equ 130 (

  echo [Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]

) else if !SPECERR! neq 0 ( echo [Direct spec training exited with error !SPECERR!.] ) else ( echo [Done. ONNX: %CD%\!SPEC_ONNX!] )

if !SPECERR! equ 0 (

  echo Running ONNX contract check...

  "!PY!" -m ml_specular.verify_spec_onnx "!SPEC_ONNX!"

)

pause

goto TORCH_ORT_MAIN



:ASK_DATA_ROOT

set "DATA_ROOT=multi_dataset"

set /p DATA_ROOT=Dataset folder [multi_dataset]: 

if "!DATA_ROOT!"=="" set "DATA_ROOT=multi_dataset"

exit /b 0



:ASK_IGNORE_OPTIFINE

set "GLP_IGNORE_OPTIFINE="

set "GLP_IGN_IN="

set /p GLP_IGN_IN=Ignore OptiFine folders (ctm, plants) in manifest? [y/N]: 

if /I "!GLP_IGN_IN!"=="y" set "GLP_IGNORE_OPTIFINE=--ignore-optifine"

if /I "!GLP_IGN_IN!"=="yes" set "GLP_IGNORE_OPTIFINE=--ignore-optifine"

exit /b 0



:END

echo Goodbye.

endlocal

exit /b 0



:SAVE_LAST_SESSION

set "LAST_CMD_FILE=%~dp0.last_training_session.bat"

(

  echo @echo off

  echo cd /d "%%~dp0"

  echo %*

) > "!LAST_CMD_FILE!"

echo [Saved last session -^> !LAST_CMD_FILE!]

exit /b 0

