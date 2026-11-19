@echo off
rem Builds EvilDragon.StorageHunterCheats.dll. Needs Visual Studio (C++ tools) and local.bat (see local.bat.example).
setlocal
cd /d "%~dp0"
if not exist local.bat (echo local.bat missing - copy local.bat.example & exit /b 1)
call "%~dp0local.bat"
if not defined VSCMD_VER (
  for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath`) do call "%%i\VC\Auxiliary\Build\vcvars64.bat" >nul
)
if not exist bin\obj mkdir bin\obj

set CF=/nologo /O2 /MT /EHsc /std:c++20 /bigobj /permissive- /w /DNDEBUG /DWIN32_LEAN_AND_MEAN /D_CRT_SECURE_NO_WARNINGS /I"%SDK%" /I"%IMGUI%" /I"%IMGUI%\backends" /I"%MINHOOK%\include" /Fobin\obj\ /MP

rem repair the Dumper-7 function lookups (safe to run every time)
python "%~dp0..\tools\fix_sdk.py" "%SDK%" || exit /b 1

rem the SDK only needs to be compiled once (slow)
if not exist bin\obj\Engine_functions.obj (
  echo Compiling SDK...
  cl %CF% /c "%SDK%\SDK\Basic.cpp" "%SDK%\SDK\CoreUObject_functions.cpp" "%SDK%\SDK\Engine_functions.cpp" || exit /b 1
)
cl %CF% /c "%SDK%\SDK\BP_CheatManager_functions.cpp" "%SDK%\SDK\BP_PlayerState_functions.cpp" "%SDK%\SDK\StorageHunter_functions.cpp" "%SDK%\SDK\BP_ExperienceSystem_Comp_functions.cpp" "%SDK%\SDK\BP_ItemsSystem_functions.cpp" "%SDK%\SDK\BP_RenownController_Comp_functions.cpp" "%SDK%\SDK\BP_PerksController_Comp_functions.cpp" || exit /b 1
cl %CF% /c "%IMGUI%\imgui.cpp" "%IMGUI%\imgui_draw.cpp" "%IMGUI%\imgui_tables.cpp" "%IMGUI%\imgui_widgets.cpp" "%IMGUI%\backends\imgui_impl_win32.cpp" "%IMGUI%\backends\imgui_impl_dx12.cpp" || exit /b 1
cl %CF% /c "%MINHOOK%\src\buffer.c" "%MINHOOK%\src\hook.c" "%MINHOOK%\src\trampoline.c" "%MINHOOK%\src\hde\hde64.c" || exit /b 1
cl %CF% /c main.cpp game.cpp menu.cpp render.cpp || exit /b 1
rc /nologo /fo bin\obj\cheats.res cheats.rc || exit /b 1
link /nologo /DLL /OUT:bin\EvilDragon.StorageHunterCheats.dll /Brepro /OPT:REF /OPT:ICF bin\obj\*.obj bin\obj\cheats.res d3d12.lib dxgi.lib user32.lib kernel32.lib || exit /b 1
echo Built bin\EvilDragon.StorageHunterCheats.dll
if defined GAMEDIR (
  if not exist "%GAMEDIR%\EvilDragon" mkdir "%GAMEDIR%\EvilDragon"
  copy /y bin\EvilDragon.StorageHunterCheats.dll "%GAMEDIR%\EvilDragon\" >nul && echo Copied to game folder || echo Could not copy - is the game running?
)
