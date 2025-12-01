Write-Host "Building HS2 Sandbox Plugin..." -ForegroundColor Cyan
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "DLL location: bin\Release\HS2SandboxPlugin.dll" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Copy this DLL to: [Your HS2 Install]\BepInEx\plugins\" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "Build failed! Check the error messages above." -ForegroundColor Red
}

Read-Host "Press Enter to continue"

