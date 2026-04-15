@echo off
REM Run LabPBR pack generator with Python 3.10+.
REM Prefers project venv .venv312 (where pip install was run); else py -3.12 / 3.11 / 3.10.
setlocal EnableDelayedExpansion
cd /d "%~dp0"
REM Must be set before any SHIFT: SHIFT moves %1 into %0, so %~f0 would later become an arg (e.g. --all-default-datasets).
set "GLP_THIS=%~f0"

if /I "%~1"=="--all-default-datasets" goto glp_all_defaults

set "ARGSTR=%*"
if "%ARGSTR%"=="" set "ARGSTR=--dataset-root multi_dataset"
echo %ARGSTR% | findstr /i /c:"--dataset-root" >nul
if errorlevel 1 set "ARGSTR=--dataset-root multi_dataset %ARGSTR%"

if exist "%~dp0.venv312\Scripts\python.exe" (
  "%~dp0.venv312\Scripts\python.exe" -m ml_specular.gen_from_labpbr_packs %ARGSTR%
  exit /b %ERRORLEVEL%
)

where py >nul 2>nul
if errorlevel 1 (
  echo ERROR: No .venv312 found and "py" launcher missing.
  echo Create venv and install deps:
  echo   py -3.12 -m venv .venv312
  echo   .venv312\Scripts\pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu
  echo   .venv312\Scripts\pip install -r requirements.txt
  exit /b 1
)

py -3.12 -m ml_specular.gen_from_labpbr_packs %ARGSTR%
set ERRL=%ERRORLEVEL%
if not %ERRL%==9009 if not %ERRL%==103 exit /b %ERRL%

py -3.11 -m ml_specular.gen_from_labpbr_packs %ARGSTR%
set ERRL=%ERRORLEVEL%
if not %ERRL%==9009 if not %ERRL%==103 exit /b %ERRL%

py -3.10 -m ml_specular.gen_from_labpbr_packs %ARGSTR%
exit /b %ERRORLEVEL%

:glp_all_defaults
REM Each dataset folder gets its own manifest.jsonl + splits. Strip any trailing
REM --dataset-root from extra flags: otherwise Python argparse uses the *last*
REM --dataset-root and every pass would scan/write the same folder only.
shift
set "EXTRA="
:glp_collect_extra
if "%~1"=="" goto glp_run_defaults
if /I "%~1"=="--dataset-root" (
  shift
  if "%~1"=="" goto glp_run_defaults
  shift
  goto glp_collect_extra
)
set "EXTRA=!EXTRA! %~1"
shift
goto glp_collect_extra

:glp_run_defaults
for %%D in (multi_dataset pixelart_dataset realism_dataset stylized_dataset ext_dataset) do (
  echo.
  echo === Manifest: %%D ===
  call "!GLP_THIS!" --dataset-root %%D !EXTRA!
  if errorlevel 1 exit /b !ERRORLEVEL!
)
exit /b 0
