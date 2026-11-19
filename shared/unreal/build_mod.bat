@echo off
rem Shared build for the Unreal Engine mods. Called by games\<Game>\src\build.bat, which sets:
rem   ED_SRC        folder of the game's sources (cheats.h, ed_game.h, game.cpp, menu.cpp)
rem   ED_DLL        output dll name, e.g. EvilDragon.StorageHunterCheats.dll
rem   ED_SDK_FILES  the SDK *_functions.cpp files the game needs (besides Basic/CoreUObject/Engine)
rem and local.bat (SDK, IMGUI, MINHOOK, GAMEDIR).
setlocal
set "SHARED=%~dp0"
cd /d "%ED_SRC%"
if not exist "%ED_SRC%\local.bat" (echo local.bat missing - copy local.bat.example & exit /b 1)
call "%ED_SRC%\local.bat"
if not defined VSCMD_VER (
  for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath`) do call "%%i\VC\Auxiliary\Build\vcvars64.bat" >nul
)
if not exist bin\obj mkdir bin\obj

rem repair the Dumper-7 function lookups (safe to run every time)
python "%SHARED%fix_sdk.py" "%SDK%" || exit /b 1

set CF=/nologo /O2 /MT /EHsc /std:c++20 /bigobj /permissive- /w /DNDEBUG /DWIN32_LEAN_AND_MEAN /D_CRT_SECURE_NO_WARNINGS /I"%ED_SRC%" /I"%SHARED%." /I"%SDK%" /I"%IMGUI%" /I"%IMGUI%\backends" /I"%MINHOOK%\include" /Fobin\obj\ /MP

rem the SDK base only needs to be compiled once (slow)
if not exist bin\obj\Engine_functions.obj (
  echo Compiling SDK...
  cl %CF% /c "%SDK%\SDK\Basic.cpp" "%SDK%\SDK\CoreUObject_functions.cpp" "%SDK%\SDK\Engine_functions.cpp" || exit /b 1
)
set FILES=
for %%f in (%ED_SDK_FILES%) do call set FILES=%%FILES%% "%SDK%\SDK\%%f"
cl %CF% /c %FILES% || exit /b 1
if not exist bin\obj\imgui_impl_dx11.obj (
  cl %CF% /c "%IMGUI%\imgui.cpp" "%IMGUI%\imgui_draw.cpp" "%IMGUI%\imgui_tables.cpp" "%IMGUI%\imgui_widgets.cpp" "%IMGUI%\backends\imgui_impl_win32.cpp" "%IMGUI%\backends\imgui_impl_dx12.cpp" "%IMGUI%\backends\imgui_impl_dx11.cpp" || exit /b 1
  cl %CF% /c "%MINHOOK%\src\buffer.c" "%MINHOOK%\src\hook.c" "%MINHOOK%\src\trampoline.c" "%MINHOOK%\src\hde\hde64.c" || exit /b 1
)
cl %CF% /c "%SHARED%main.cpp" "%SHARED%render.cpp" "%SHARED%ed_ui.cpp" game.cpp menu.cpp || exit /b 1
rc /nologo /fo bin\obj\cheats.res "%SHARED%cheats.rc" || exit /b 1
link /nologo /DLL /OUT:bin\%ED_DLL% /Brepro /OPT:REF /OPT:ICF bin\obj\*.obj bin\obj\cheats.res d3d11.lib d3d12.lib dxgi.lib user32.lib kernel32.lib || exit /b 1
echo Built bin\%ED_DLL%
if defined GAMEDIR (
  if not exist "%GAMEDIR%\EvilDragon" mkdir "%GAMEDIR%\EvilDragon"
  copy /y bin\%ED_DLL% "%GAMEDIR%\EvilDragon\" >nul && echo Copied to game folder || echo Could not copy - is the game running?
)
