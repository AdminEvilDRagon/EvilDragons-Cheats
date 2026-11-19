@echo off
rem Builds EvilDragon.DiggingAHoleCheats.dll. Needs Visual Studio (C++ tools), Python 3 and local.bat (see local.bat.example).
setlocal
set "ED_SRC=%~dp0."
set "ED_DLL=EvilDragon.DiggingAHoleCheats.dll"
set "ED_SDK_FILES=CubicDestruction_FirstPersonCharacter_functions.cpp BP_OreSpawner_functions.cpp"
call "%~dp0..\..\..\shared\unreal\build_mod.bat"
