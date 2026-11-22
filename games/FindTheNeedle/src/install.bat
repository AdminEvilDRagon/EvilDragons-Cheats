@echo off
rem EvilDragon's Cheats - Find The Needle installer (unofficial mod).
rem Keeps the game's real libterrain as libterrain.windows.release.x86_64.original.dll and puts the mod in its place.
setlocal
cd /d "%~dp0"
if not exist FindTheNeedle.exe goto wrongdir
if not exist EvilDragon.FindTheNeedleCheats.dll goto nomod
set "T=libterrain.windows.release.x86_64.dll"
set "O=libterrain.windows.release.x86_64.original.dll"
if not exist "%T%" goto copy
findstr /m /c:"EvilDragonCheatsProxy" "%T%" >nul 2>&1
if not errorlevel 1 goto copy
rem the current libterrain is the game's own one (fresh install or after a game update)
if exist "%O%" del /q "%O%"
ren "%T%" "%O%" || goto busy
:copy
if not exist "%O%" goto noorig
copy /y EvilDragon.FindTheNeedleCheats.dll "%T%" >nul || goto busy
echo.
echo  Done! Start Find The Needle, load a save and press F1.
echo.
pause
exit /b 0
:wrongdir
echo  Put the files from the zip next to FindTheNeedle.exe and run install.bat again.
pause
exit /b 1
:nomod
echo  EvilDragon.FindTheNeedleCheats.dll is missing - extract the whole zip.
pause
exit /b 1
:noorig
echo  The game's libterrain.windows.release.x86_64.dll is missing. Verify the game files in Steam, then run install.bat again.
pause
exit /b 1
:busy
echo  Close Find The Needle first, then run install.bat again.
pause
exit /b 1
