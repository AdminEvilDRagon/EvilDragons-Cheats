@echo off
rem Builds EvilDragon.MoonSweepersCheats.dll (native IL2CPP mod, no BepInEx).
rem Needs Visual Studio (C++ tools) and local.bat (see local.bat.example).
setlocal
set "SRC=%~dp0"
set "SHARED=%~dp0..\..\..\shared\unreal\"
set "DLL=EvilDragon.MoonSweepersCheats.dll"
cd /d "%SRC%"
if not exist "%SRC%local.bat" goto nolocal
call "%SRC%local.bat"
if defined VSCMD_VER goto havevs
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -property installationPath`) do call "%%i\VC\Auxiliary\Build\vcvars64.bat" >nul
:havevs
if not exist bin\obj mkdir bin\obj
set CF=/nologo /O2 /MT /EHsc /std:c++20 /permissive- /W3 /wd4996 /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /I"%SRC%." /I"%SHARED%." /I"%IMGUI%" /I"%IMGUI%\backends" /I"%MINHOOK%\include" /Fobin\obj\ /MP
if exist bin\obj\imgui_impl_dx11.obj goto libsdone
cl %CF% /c "%IMGUI%\imgui.cpp" "%IMGUI%\imgui_draw.cpp" "%IMGUI%\imgui_tables.cpp" "%IMGUI%\imgui_widgets.cpp" "%IMGUI%\backends\imgui_impl_win32.cpp" "%IMGUI%\backends\imgui_impl_dx12.cpp" "%IMGUI%\backends\imgui_impl_dx11.cpp" || exit /b 1
cl %CF% /c "%MINHOOK%\src\buffer.c" "%MINHOOK%\src\hook.c" "%MINHOOK%\src\trampoline.c" "%MINHOOK%\src\hde\hde64.c" || exit /b 1
:libsdone
cl %CF% /c "%SHARED%main.cpp" "%SHARED%render.cpp" "%SHARED%ed_ui.cpp" game.cpp menu.cpp || exit /b 1
rc /nologo /fo bin\obj\cheats.res "%SHARED%cheats.rc" || exit /b 1
link /nologo /DLL /OUT:bin\%DLL% /Brepro /OPT:REF /OPT:ICF bin\obj\*.obj bin\obj\cheats.res d3d11.lib d3d12.lib dxgi.lib user32.lib kernel32.lib || exit /b 1
echo Built bin\%DLL%
if not defined GAMEDIR goto :eof
if not exist "%GAMEDIR%\EvilDragon" mkdir "%GAMEDIR%\EvilDragon"
copy /y bin\%DLL% "%GAMEDIR%\EvilDragon\" >nul && echo Copied to game folder || echo Could not copy - is the game running?
goto :eof
:nolocal
echo local.bat missing - copy local.bat.example
exit /b 1
