@echo off
SET "DEVKITPRO=C:\devkitpro"
SET "GCC=%DEVKITPRO%\devkitA64\bin\aarch64-none-elf-gcc.exe"
SET "NATIVEAOT_SDK=%USERPROFILE%\.nuget\packages\runtime.linux-arm64.microsoft.dotnet.ilcompiler\8.0.25\sdk"
SET "NATIVEAOT_FW=%USERPROFILE%\.nuget\packages\runtime.linux-arm64.microsoft.dotnet.ilcompiler\8.0.25\framework"
SET "ARCH=-march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -ftls-model=local-exec"
SET "PROJECT=ProjectZ.Switch"

cd /d "%~dp0\.."

echo.
echo ============================================
echo  Step 2: Compiling nativeaot_shims.c
echo ============================================
%GCC% %ARCH% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\nativeaot_shims.o ^
  %PROJECT%\nativeaot_shims.c

if %errorlevel% neq 0 ( echo [ERROR] Shim compilation failed! & pause & exit /b 1 )
echo Shims compiled OK

echo.
echo ============================================
echo  Step 3: Linking with devkitpro (aarch64)
echo ============================================
echo Linking...
%GCC% %ARCH% ^
  -specs=%PROJECT%\AOT_switch.specs ^
  -T %PROJECT%\AOT_switch.ld ^
  -Wl,--wrap=write -Wl,--wrap=main -Wl,--wrap=malloc -Wl,--wrap=calloc -Wl,--wrap=realloc ^
  -Wl,--wrap=pthread_create -Wl,--wrap=RaiseFailFastException -Wl,--wrap=abort ^
  -Wl,--wrap=glGetString -Wl,--wrap=SDL_GL_GetProcAddress ^
  -Wl,--wrap=opendir -Wl,--wrap=SDL_PollEvent ^
  -Wl,--wrap=PalHijack ^
  -o %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.elf ^
  %PROJECT%\obj\nativeaot_shims.o ^
  %PROJECT%\obj\crypto_stubs.o ^
  %PROJECT%\obj\dlsym_table.o ^
  %PROJECT%\obj\Release\net8.0\linux-arm64\native\ProjectZ.Switch.o ^
  %NATIVEAOT_SDK%\libbootstrapper.o ^
  %NATIVEAOT_SDK%\libRuntime.WorkstationGC.a ^
  %NATIVEAOT_SDK%\libeventpipe-disabled.a ^
  %NATIVEAOT_SDK%\libstdc++compat.a ^
  %NATIVEAOT_FW%\libSystem.Native.a ^
  %NATIVEAOT_FW%\libSystem.IO.Compression.Native.a ^
  -L%DEVKITPRO%\portlibs\switch\lib ^
  -L%DEVKITPRO%\libnx\lib ^
  -lSDL2 -lopenal -lEGL -lglapi -lGLESv2 -ldrm_nouveau ^
  -lnx -lpthread -lstdc++ -lm -lz ^
  -Wl,--allow-multiple-definition ^
  -pie -Wl,--gc-sections -Wl,-z,norelro -Wl,--eh-frame-hdr

if %errorlevel% neq 0 ( echo LINK FAILED! & exit /b 1 )
echo LINK OK!

echo Converting to NRO...
%DEVKITPRO%\tools\bin\nacptool.exe --create "Zelda LA DX HD" "ProjectZ" "1.7.3" %PROJECT%\bin\SwitchRelease\game.nacp
SET "ICON_FLAG="
if exist %PROJECT%\Icon.jpg SET "ICON_FLAG=--icon=%PROJECT%\Icon.jpg"
%DEVKITPRO%\tools\bin\elf2nro.exe %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.elf %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.nro --nacp=%PROJECT%\bin\SwitchRelease\game.nacp %ICON_FLAG%
if %errorlevel% neq 0 ( echo ELF2NRO FAILED! & exit /b 1 )
echo NRO OK!

copy /Y %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.nro %PROJECT%\bin\SwitchRelease\switch\zelda-ladxhd\ProjectZ.Switch.nro >nul 2>&1
echo Package updated!
