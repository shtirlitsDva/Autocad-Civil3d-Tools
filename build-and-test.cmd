@echo off
setlocal

pushd "%~dp0"

echo === Building NTRExport ===
dotnet build "Acad-C3D-Tools\NTRExport\NTRExport.csproj"
if errorlevel 1 goto :fail

echo === Building Console Tests ===
dotnet build "Acad-C3D-Tools\NTRExport.ConsoleTests\NTRExport.ConsoleTests.csproj"
if errorlevel 1 goto :fail

echo === Running Console Tests ===
dotnet run --project "Acad-C3D-Tools\NTRExport.ConsoleTests\NTRExport.ConsoleTests.csproj"
set result=%ERRORLEVEL%

popd
exit /b %result%

:fail
set result=%ERRORLEVEL%
popd
exit /b %result%


