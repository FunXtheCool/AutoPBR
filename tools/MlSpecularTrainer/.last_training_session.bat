@echo off
cd /d "%~dp0"
"Z:\Cursor Projects\AutoPBR\tools\MlSpecularTrainer\.venv312\Scripts\python.exe" -m ml_specular.export_ort_specular_graphs --out-dir "artifacts\ort" --in-channels 4 --out-channels 4 --width 1440 --train-res 32 --ckpt "artifacts\specular_predictor.pt"
