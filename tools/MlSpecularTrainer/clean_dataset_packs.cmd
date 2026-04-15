@echo off
REM Remove loose PNGs under packs/ that are not diffuse/_n/_s pairings (or _e with diffuse).
REM Default: all dataset folders (multi, pixelart, realism, stylized, ext). Use --dataset-root for one folder only.
REM Same Python env as gen_labpbr.cmd.
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "GLP_THIS=%~f0"

if /I "%~1"=="--all-default-datasets" shift
echo %* | findstr /i /c:"--dataset-root" >nul
if errorlevel 1 goto cdp_all_defaults

REM Single dataset: --dataset-root was passed
set "ARGSTR=%*"
if exist "%~dp0.venv312\Scripts\python.exe" (
  "%~dp0.venv312\Scripts\python.exe" -m ml_specular.clean_dataset_packs %ARGSTR%
  exit /b %ERRORLEVEL%
)
where py >nul 2>nul
if errorlevel 1 (
  echo ERROR: No .venv312 found and "py" launcher missing.
  exit /b 1
)
py -3.12 -m ml_specular.clean_dataset_packs %ARGSTR%
set ERRL=%ERRORLEVEL%
if not %ERRL%==9009 if not %ERRL%==103 exit /b %ERRL%
py -3.11 -m ml_specular.clean_dataset_packs %ARGSTR%
set ERRL=%ERRORLEVEL%
if not %ERRL%==9009 if not %ERRL%==103 exit /b %ERRL%
py -3.10 -m ml_specular.clean_dataset_packs %ARGSTR%
exit /b %ERRORLEVEL%

:cdp_all_defaults
REM Strip --dataset-root pairs from extras so recursive calls are not overridden
set "EXTRA="
:cdp_collect_extra
if "%~1"=="" goto cdp_run
if /I "%~1"=="--dataset-root" (
  shift
  if "%~1"=="" goto cdp_run
  shift
  goto cdp_collect_extra
)
set "EXTRA=!EXTRA! %~1"
shift
goto cdp_collect_extra

:cdp_run
for %%D in (multi_dataset pixelart_dataset realism_dataset stylized_dataset ext_dataset) do (
  echo.
  echo === Clean packs: %%D ===
  call "!GLP_THIS!" --dataset-root %%D !EXTRA!
  if errorlevel 1 exit /b !ERRORLEVEL!
)
exit /b 0
