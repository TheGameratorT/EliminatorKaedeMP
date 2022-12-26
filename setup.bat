@echo off
set root=%~dp0
set argc=0
for %%x in (%*) do set /a argc+=1
if not %argc% gtr 0 goto noarg
set mngdir=%~dp1Eliminator ver 1.2_Data\Managed
if not exist "%mngdir%\" goto nogamedir
cd "%mngdir%"
set libdir=%root%\EliminatorKaedeMP\Libraries
if not exist "%libdir%" mkdir "%libdir%"
call :doUnlock Assembly-CSharp.dll
if %errorlevel% neq 0 goto fail
call :doCopy Assembly-CSharp-firstpass.dll
if %errorlevel% neq 0 goto fail
call :doCopy UnityEngine.UI.dll
if %errorlevel% neq 0 goto fail
cd %root%
echo Project ready.
pause
exit /b 0
:doUnlock
echo Unlocking "%1" -^> "Libraries\%1"
call "%root%\EliminatorKaedeMP\Tools\dllunlocker.exe" "%mngdir%\%1" "%libdir%\%1" > NUL
if %errorlevel% neq 0 echo Unlock failed. && exit /b 1
exit /b 0
:doCopy
echo Copying "%1" -^> "Libraries\%1"
copy "%mngdir%\%1" "%libdir%\%1" > NUL
if %errorlevel% neq 0 echo Copy failed. && exit /b 1
exit /b 0
:noarg
echo Please drag the Eliminator Kaede executable on top of this script.
pause
exit /b 0
:nogamedir
echo Could not find "%mngdir%".
echo Are you sure you have the correct game version?
goto fail
:fail
cd %root%
echo Project not ready.
pause
exit /b 1
