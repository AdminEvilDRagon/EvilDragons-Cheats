@echo off
rem EvilDragon's Cheats - Find The Needle: puts the game's own libterrain back.
setlocal
cd /d "%~dp0"
set "T=libterrain.windows.release.x86_64.dll"
set "O=libterrain.windows.release.x86_64.original.dll"
if not exist "%O%" goto nothing
if exist "%T%" del /q "%T%" || goto busy
ren "%O%" "%T%" || goto busy
echo  The mod is removed. You can delete EvilDragon.FindTheNeedleCheats.dll, install.bat, uninstall.bat and the EvilDragon folder.
pause
exit /b 0
:nothing
echo  The mod is not installed.
pause
exit /b 0
:busy
echo  Close Find The Needle first, then run uninstall.bat again.
pause
exit /b 1
