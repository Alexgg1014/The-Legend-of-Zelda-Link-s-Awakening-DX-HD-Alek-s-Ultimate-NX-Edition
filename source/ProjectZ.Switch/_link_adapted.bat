@echo off
REM ============================================================================
REM  _link_adapted.bat - Stage 2 link, adapted from the repo's _link.bat
REM
REM  Differences from the original _link.bat:
REM   1. ILCompiler version 8.0.25 -> 9.0.19 (restored by .NET SDK 9.0.317, which
REM      the v2.0.6 rebase requires: upstream moved every project to net9.0).
REM   2. Also compiles crypto_stubs.c and dlsym_table.c. The original _link.bat
REM      links obj\crypto_stubs.o and obj\dlsym_table.o but never builds them,
REM      so it only worked on a tree where they had been compiled previously.
REM  Everything else (arch flags, specs, linker script, --wrap set, library
REM  list, elf2nro invocation) is byte-for-byte the original.
REM ============================================================================

SET "DEVKITPRO=C:\devkitpro"
SET "GCC=%DEVKITPRO%\devkitA64\bin\aarch64-none-elf-gcc.exe"
SET "ILC=%USERPROFILE%\.nuget\packages\runtime.linux-arm64.microsoft.dotnet.ilcompiler\9.0.19"
SET "NATIVEAOT_SDK=%ILC%\sdk"
SET "NATIVEAOT_FW=%ILC%\framework"
SET "ARCH=-march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -ftls-model=local-exec"
SET "PROJECT=ProjectZ.Switch"

cd /d "%~dp0\.."

if not exist "%PROJECT%\obj" mkdir "%PROJECT%\obj"
if not exist "%PROJECT%\bin\SwitchRelease" mkdir "%PROJECT%\bin\SwitchRelease"

echo.
echo ============================================
echo  Step 2a: Compiling nativeaot_shims.c
echo ============================================
%GCC% %ARCH% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\nativeaot_shims.o ^
  %PROJECT%\nativeaot_shims.c
if %errorlevel% neq 0 ( echo [ERROR] nativeaot_shims.c failed! & exit /b 1 )

echo.
echo ============================================
echo  Step 2b: Compiling crypto_stubs.c
echo ============================================
%GCC% %ARCH% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\crypto_stubs.o ^
  %PROJECT%\crypto_stubs.c
if %errorlevel% neq 0 ( echo [ERROR] crypto_stubs.c failed! & exit /b 1 )

echo.
echo ============================================
echo  Step 2c: Compiling dlsym_table.c
echo ============================================
%GCC% %ARCH% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\dlsym_table.o ^
  %PROJECT%\dlsym_table.c
if %errorlevel% neq 0 ( echo [ERROR] dlsym_table.c failed! & exit /b 1 )

echo.
echo ============================================
echo  Step 2d: Compiling libc_stubs.c
echo ============================================
%GCC% %ARCH% -c -O2 ^
  -isystem %DEVKITPRO%\libnx\include ^
  -I%DEVKITPRO%\portlibs\switch\include ^
  -D__SWITCH__ ^
  -o %PROJECT%\obj\libc_stubs.o ^
  %PROJECT%\libc_stubs.c
if %errorlevel% neq 0 ( echo [ERROR] libc_stubs.c failed! & exit /b 1 )

echo Shims compiled OK

echo.
echo ============================================
echo  Step 3: Linking with devkitpro (aarch64)
echo ============================================
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
  %PROJECT%\obj\libc_stubs.o ^
  %PROJECT%\obj\Release\net9.0\linux-arm64\native\ProjectZ.Switch.o ^
  %NATIVEAOT_SDK%\libbootstrapper.o ^
  %NATIVEAOT_SDK%\libRuntime.WorkstationGC.a ^
  %NATIVEAOT_SDK%\libeventpipe-disabled.a ^
  %NATIVEAOT_SDK%\libstandalonegc-disabled.a ^
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

echo.
echo Converting to NRO...
%DEVKITPRO%\tools\bin\nacptool.exe --create "Alek's Ultimate Zelda LA DX HD" "Alexgg1014" "1.0.0" %PROJECT%\bin\SwitchRelease\game.nacp
SET "ICON_FLAG="
if exist %PROJECT%\Icon.jpg SET "ICON_FLAG=--icon=%PROJECT%\Icon.jpg"
%DEVKITPRO%\tools\bin\elf2nro.exe %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.elf %PROJECT%\bin\SwitchRelease\ProjectZ.Switch.nro --nacp=%PROJECT%\bin\SwitchRelease\game.nacp %ICON_FLAG%
if %errorlevel% neq 0 ( echo ELF2NRO FAILED! & exit /b 1 )
echo NRO OK!
