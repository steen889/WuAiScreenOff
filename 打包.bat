@echo off
cd /d "%~dp0"
set "VER=1.3"
echo Packing WuAiScreenOff-v%VER%.zip ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path (Get-ChildItem -Path '*.exe','*.txt' -File) -DestinationPath 'WuAiScreenOff-v%VER%.zip' -Force"
echo.
if exist "WuAiScreenOff-v%VER%.zip" ( echo [OK] Done: WuAiScreenOff-v%VER%.zip  -- upload this to GitHub Release ) else ( echo [X] Failed, see error above. )
echo.
echo Tip: for a new version, edit VER=1.3 above, then run again.
pause