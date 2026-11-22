@echo off
rem Builds EvilDragon.ChoppingTreesCheats.dll. Needs Visual Studio (C++ tools), Python 3 and local.bat (see local.bat.example).
setlocal
set "ED_SRC=%~dp0."
set "ED_DLL=EvilDragon.ChoppingTreesCheats.dll"
set "ED_SDK_FILES=TreesMustGo_functions.cpp TreesChopper_functions.cpp"
call "%~dp0..\..\..\shared\unreal\build_mod.bat"
