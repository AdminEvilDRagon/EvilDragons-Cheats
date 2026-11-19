@echo off
rem Builds the loader (dxgi.dll). Needs Visual Studio with the C++ tools.
setlocal
cd /d "%~dp0"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq tokens=*" %%i in (`call "%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VS=%%i"
call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul
if not exist bin mkdir bin
ml64 /nologo /c /Fobin\stubs.obj stubs.asm || exit /b 1
cl /nologo /O2 /MT /EHsc /c /Fobin\loader.obj loader.cpp || exit /b 1
link /nologo /DLL /DEF:dxgi.def /OUT:bin\dxgi.dll /Brepro bin\loader.obj bin\stubs.obj kernel32.lib user32.lib || exit /b 1
echo OK
