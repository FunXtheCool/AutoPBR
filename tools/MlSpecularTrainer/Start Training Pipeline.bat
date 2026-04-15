@echo off
setlocal EnableExtensions

cd /d "%~dp0"

where docker >nul 2>nul
if errorlevel 1 (
  echo.
  echo [ERROR] Docker is not available on PATH.
  echo Install/start Docker Desktop, then retry.
  echo.
  pause
  exit /b 1
)

echo.
echo ========================================
echo   Specular ML - Container shell
echo   (ORT training + torch-ort enabled)
echo ========================================
echo   Folder: %CD%
echo.
echo This will start the Linux container workflow and pre-enable:
echo   - onnxruntime-training install
echo   - torch-ort install ^(+ configure best-effort^)
echo   - DeepSpeed install ^(required for ORTModule ZeRO stage3 support^)
echo.
echo When you type exit in the Linux shell, the container stops and this window
echo shows an exit code. That is normal — nothing runs in the background.
echo.
echo At the Linux prompt, run the same guided menu as Training Menu.bat:
echo   bash training_menu.sh
echo.
echo Docker shared memory: /dev/shm is set to 2GB ^(see docker\docker-compose.yml: shm_size^)
echo for PyTorch DataLoader workers ^(avoids bus error / insufficient shm^).
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_ml_container.ps1" -InstallOrtTraining -InstallTorchOrt -InstallDeepSpeed
set "ERR=%ERRORLEVEL%"

echo.
if not "%ERR%"=="0" (
  echo [Container session exited with code %ERR%]
) else (
  echo [Container session ended.]
)
echo.
pause
exit /b %ERR%
