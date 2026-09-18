@echo off
cd /d "%~dp0\.."

::-----------------------------------------------
:: Configuration
::-----------------------------------------------

SET "DEVKITPRO=C:\devkitpro"
SET "GCC=%DEVKITPRO%\devkitA64\bin\aarch64-none-elf-gcc.exe"
SET "TOOLS=%DEVKITPRO%\tools\bin"
SET "ARCH_FLAGS=-march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -ftls-model=local-exec"

SET "PROJECT=ProjectZ.Switch"
SET "OUTPUT_DIR=%PROJECT%\bin\SwitchRelease"
SET "NATIVE_DIR=%PROJECT%\obj\Release\net8.0\linux-arm64\native"
SET "NRO_DIR=%OUTPUT_DIR%\switch\zelda-ladxhd"

SET "NATIVEAOT_SDK=%USERPROFILE%\.nuget\packages\runtime.linux-arm64.microsoft.dotnet.ilcompiler\8.0.25\sdk"
SET "NATIVEAOT_FW=%USERPROFILE%\.nuget\packages\runtime.linux-arm64.microsoft.dotnet.ilcompiler\8.0.25\framework"

SET "APP_NAME=Zelda LA DX HD"
SET "APP_AUTHOR=ProjectZ"
SET "APP_VERSION=1.7.3"

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

::-----------------------------------------------
:: Step 1: Build .NET assemblies with NativeAOT
::-----------------------------------------------

echo.
echo ============================================
echo  Step 1: NativeAOT compile (C# to ARM64)
echo ============================================
echo.

dotnet publish %PROJECT%\%PROJECT%.csproj -c Release -r linux-arm64 -p:PublishAot=true --nologo
:: NativeAOT will fail at the clang link step (expected on Windows).
:: The important part is that the ILC step succeeded and produced .o files.

if not exist "%NATIVE_DIR%\%PROJECT%.o" (
    echo [ERROR] NativeAOT ILC did not produce object files!
    pause
    exit /b 1
)

echo ILC succeeded: %NATIVE_DIR%\%PROJECT%.o

::-----------------------------------------------
:: Step 2: Compile shims for glibc/POSIX compat
::-----------------------------------------------

echo.
echo ============================================
echo  Step 2: Compiling platform shims
echo ============================================
echo.

if not exist "%PROJECT%\obj" mkdir "%PROJECT%\obj"

echo   Compiling nativeaot_shims.c...
%GCC% %ARCH_FLAGS% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\nativeaot_shims.o ^
  %PROJECT%\nativeaot_shims.c

if %errorlevel% neq 0 (
    echo [ERROR] nativeaot_shims.c compilation failed!
    pause
    exit /b 1
)

echo   Compiling crypto_stubs.c...
%GCC% %ARCH_FLAGS% -c -O2 ^
  -o %PROJECT%\obj\crypto_stubs.o ^
  %PROJECT%\crypto_stubs.c

if %errorlevel% neq 0 (
    echo [ERROR] crypto_stubs.c compilation failed!
    pause
    exit /b 1
)

::-----------------------------------------------
:: Step 3: Link with devkitpro toolchain
::-----------------------------------------------

echo.
echo ============================================
echo  Step 3: Linking with devkitpro (aarch64)
echo ============================================
echo.

%GCC% ^
  %ARCH_FLAGS% ^
  -specs=%PROJECT%\AOT_switch.specs ^
  -T %PROJECT%\AOT_switch.ld ^
  -Wl,--wrap=write ^
  -Wl,--wrap=main ^
  -Wl,--wrap=malloc ^
  -Wl,--wrap=calloc ^
  -Wl,--wrap=realloc ^
  -Wl,--wrap=pthread_create ^
  -o %OUTPUT_DIR%\%PROJECT%.elf ^
  %NATIVE_DIR%\%PROJECT%.o ^
  %PROJECT%\obj\nativeaot_shims.o ^
  %PROJECT%\obj\crypto_stubs.o ^
  %NATIVEAOT_SDK%\libbootstrapper.o ^
  %NATIVEAOT_SDK%\libRuntime.WorkstationGC.a ^
  %NATIVEAOT_SDK%\libeventpipe-disabled.a ^
  %NATIVEAOT_SDK%\libstdc++compat.a ^
  %NATIVEAOT_FW%\libSystem.Native.a ^
  %NATIVEAOT_FW%\libSystem.IO.Compression.Native.a ^
  -L%DEVKITPRO%\portlibs\switch\lib ^
  -L%DEVKITPRO%\libnx\lib ^
  -lSDL2 -lopenal ^
  -lEGL -lglapi -lGLESv2 -ldrm_nouveau ^
  -lnx -lpthread -lstdc++ -lm ^
  -lz ^
  -Wl,--allow-multiple-definition ^
  -pie -Wl,--gc-sections -Wl,-z,norelro

if %errorlevel% neq 0 (
    echo [ERROR] Linking failed!
    pause
    exit /b 1
)

echo Linked: %OUTPUT_DIR%\%PROJECT%.elf

::-----------------------------------------------
:: Step 4: Create NACP and convert to NRO
::-----------------------------------------------

echo.
echo ============================================
echo  Step 4: Creating NRO package
echo ============================================
echo.

%TOOLS%\nacptool.exe --create "%APP_NAME%" "%APP_AUTHOR%" "%APP_VERSION%" %OUTPUT_DIR%\game.nacp
if %errorlevel% neq 0 (
    echo [ERROR] nacptool failed!
    pause
    exit /b 1
)

SET "ICON_FLAG="
if exist "%PROJECT%\icon.jpg" SET "ICON_FLAG=--icon=%PROJECT%\icon.jpg"

%TOOLS%\elf2nro.exe %OUTPUT_DIR%\%PROJECT%.elf %OUTPUT_DIR%\%PROJECT%.nro --nacp=%OUTPUT_DIR%\game.nacp %ICON_FLAG%
if %errorlevel% neq 0 (
    echo [ERROR] elf2nro failed!
    pause
    exit /b 1
)

echo Created: %OUTPUT_DIR%\%PROJECT%.nro

::-----------------------------------------------
:: Step 5: Assemble SD card package
::-----------------------------------------------

echo.
echo ============================================
echo  Step 5: Assembling SD card package
echo ============================================
echo.

if not exist "%NRO_DIR%" mkdir "%NRO_DIR%"

copy /Y "%OUTPUT_DIR%\%PROJECT%.nro" "%NRO_DIR%\%PROJECT%.nro" >nul

if exist "ProjectZ.Core\Data" (
    xcopy /E /I /Y "ProjectZ.Core\Data" "%NRO_DIR%\Data" >nul
    echo Copied: Data folder
)

SET "CONTENT_SRC="
if exist "ProjectZ.Linux\bin\Release\net8.0\Content" SET "CONTENT_SRC=ProjectZ.Linux\bin\Release\net8.0\Content"
if exist "ProjectZ.Desktop\bin\Release\net8.0\Content" SET "CONTENT_SRC=ProjectZ.Desktop\bin\Release\net8.0\Content"

if defined CONTENT_SRC (
    xcopy /E /I /Y "%CONTENT_SRC%" "%NRO_DIR%\Content" >nul
    echo Copied: Content from %CONTENT_SRC%
) else (
    echo [WARNING] No pre-built Content found!
    echo           Build ProjectZ.Linux or ProjectZ.Desktop ^(GL^) first to generate Content.
)

echo.> "%NRO_DIR%\portable.txt"

::-----------------------------------------------
:: Done
::-----------------------------------------------

echo.
echo ============================================
echo  BUILD COMPLETE!
echo ============================================
echo.
echo NRO: %OUTPUT_DIR%\%PROJECT%.nro
echo SD card package: %NRO_DIR%\
echo.
echo To deploy: copy the "switch" folder from
echo   %OUTPUT_DIR%\
echo to the root of your Switch SD card.
echo.
echo Final layout on SD card:
echo   /switch/zelda-ladxhd/ProjectZ.Switch.nro
echo   /switch/zelda-ladxhd/Data/
echo   /switch/zelda-ladxhd/Content/
echo   /switch/zelda-ladxhd/portable.txt
echo.
pause
