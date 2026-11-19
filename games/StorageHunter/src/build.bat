@echo off
rem Builds EvilDragon.StorageHunterCheats.dll. Needs Visual Studio (C++ tools), Python 3 and local.bat (see local.bat.example).
setlocal
set "ED_SRC=%~dp0."
set "ED_DLL=EvilDragon.StorageHunterCheats.dll"
set "ED_SDK_FILES=BP_CheatManager_functions.cpp BP_PlayerState_functions.cpp StorageHunter_functions.cpp BP_ExperienceSystem_Comp_functions.cpp BP_ItemsSystem_functions.cpp BP_RenownController_Comp_functions.cpp BP_PerksController_Comp_functions.cpp"
call "%~dp0..\..\..\shared\unreal\build_mod.bat"
