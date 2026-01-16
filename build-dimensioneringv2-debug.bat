@echo off
setlocal

REM =============================================
REM Build DimensioneringV2 DLL using MSBuild
REM =============================================

REM Path to MSBuild
set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

REM Path to the DimensioneringV2 project
set "PROJECT_PATH=X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DimensioneringV2\DimensioneringV2.csproj"

REM Configuration
set "CONFIGURATION=Debug"

echo.
echo Building DimensioneringV2...
echo ============================

"%MSBUILD_PATH%" "%PROJECT_PATH%" /t:Build /p:Configuration=%CONFIGURATION% /clp:ErrorsOnly;Summary
set "EXITCODE=%ERRORLEVEL%"

echo.
if %EXITCODE% NEQ 0 (
    echo Build failed. See errors above.
) else (
    echo Build completed successfully.
)

endlocal
exit /b %EXITCODE%
