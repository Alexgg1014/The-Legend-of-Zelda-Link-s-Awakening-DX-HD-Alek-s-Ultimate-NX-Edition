@echo off
cd /d "%~dp0\.."

echo.
echo ============================================
echo  Stage 1: NativeAOT compile (WITH SWITCH flag)
echo ============================================
echo.

SET SWITCH_BUILD=1
dotnet publish ProjectZ.Switch\ProjectZ.Switch.csproj -c Release -r linux-arm64 -p:PublishAot=true --nologo

if not exist "ProjectZ.Switch\obj\Release\net8.0\linux-arm64\native\ProjectZ.Switch.o" (
    echo.
    echo [ERROR] ILC did not produce ProjectZ.Switch.o  
    pause
    exit /b 1
)

echo.
echo ILC OK: ProjectZ.Switch.o gerado
echo Agora rode: _link.bat
echo.
pause
