@echo off
setlocal

REM =============================================
REM Build NSTBL DLL using MSBuild
REM =============================================

REM Path to MSBuild
set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

REM Path to the NSTBL project
set "PROJECT_PATH=X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\NSTBL\NSTBL.csproj"

REM Configuration
set "CONFIGURATION=Debug"

echo.
echo Building NSTBL...
echo ==============================

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
