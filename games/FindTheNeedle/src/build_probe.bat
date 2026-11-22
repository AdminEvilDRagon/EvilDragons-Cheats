@echo off
setlocal
cd /d "%~dp0"
if defined VSCMD_VER goto havevs
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -property installationPath`) do call "%%i\VC\Auxiliary\Build\vcvars64.bat" >nul
:havevs
if not exist bin\probe mkdir bin\probe
cl /nologo /O2 /MT /EHsc /std:c++20 /LD /Fobin\probe\ /Febin\probe\libterrain.windows.release.x86_64.dll probe.cpp user32.lib kernel32.lib || exit /b 1
echo Built probe
