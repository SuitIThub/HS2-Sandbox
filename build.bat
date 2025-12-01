@echo off
echo Building HS2 Sandbox Plugin...
dotnet build HS2-Sandbox.sln -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo DLL location: bin\Release\HS2SandboxPlugin.dll
    echo.
    echo Copy this DLL to: [Your HS2 Install]\BepInEx\plugins\
) else (
    echo.
    echo Build failed! Check the error messages above.
)
pause

