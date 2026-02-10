@echo off
setlocal

REM =============================================
REM Build SheetCreationAutomation DLL using MSBuild
REM =============================================

REM Path to MSBuild
set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

REM Path to the SheetCreationAutomation project (relative to this script)
set "SCRIPT_DIR=%~dp0"
set "PROJECT_PATH=%SCRIPT_DIR%Acad-C3D-Tools\SheetCreationAutomation\SheetCreationAutomation.csproj"
set "NUGET_CONFIG=%SCRIPT_DIR%sheetcreationautomation.nuget.config"
set "NUGET_PACKAGES=C:\Users\mgo\.nuget\packages"
set "RESTORE_SOURCES=https://api.nuget.org/v3/index.json;C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\"
set "BUILD_LOG=%TEMP%\sheetcreationautomation-build.log"

REM Configuration and platform
set "CONFIGURATION=Debug"
set "PLATFORM=x64"

REM Build-local NuGet config so broken user-level sources (e.g. X:\...) do not break restore.
> "%NUGET_CONFIG%" echo ^<?xml version="1.0" encoding="utf-8"?^>
>> "%NUGET_CONFIG%" echo ^<configuration^>
>> "%NUGET_CONFIG%" echo   ^<packageSources^>
>> "%NUGET_CONFIG%" echo     ^<clear /^>
>> "%NUGET_CONFIG%" echo     ^<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /^>
>> "%NUGET_CONFIG%" echo     ^<add key="Microsoft Visual Studio Offline Packages" value="C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\" /^>
>> "%NUGET_CONFIG%" echo   ^</packageSources^>
>> "%NUGET_CONFIG%" echo ^</configuration^>

echo.
echo Building SheetCreationAutomation...
echo ==================================

if exist "%BUILD_LOG%" del "%BUILD_LOG%" >nul 2>&1
"%MSBUILD_PATH%" "%PROJECT_PATH%" /t:Build /nologo /v:m /p:Configuration=%CONFIGURATION% /p:Platform=%PLATFORM% /p:RestoreIgnoreFailedSources=true /p:RestoreSources="%RESTORE_SOURCES%" /p:RestoreConfigFile=%NUGET_CONFIG% /p:NuGetConfigFile=%NUGET_CONFIG% > "%BUILD_LOG%" 2>&1
set "EXITCODE=%ERRORLEVEL%"

echo.
if %EXITCODE% NEQ 0 (
    findstr /R /I /C:": error " "%BUILD_LOG%"
    echo Build failed. See errors above.
) else (
    echo Build completed successfully.
)

if exist "%NUGET_CONFIG%" del "%NUGET_CONFIG%" >nul 2>&1
if exist "%BUILD_LOG%" del "%BUILD_LOG%" >nul 2>&1

endlocal
exit /b %EXITCODE%
