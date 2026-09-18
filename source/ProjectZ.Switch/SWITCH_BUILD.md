# Building for Nintendo Switch (Homebrew)

## Prerequisites

### Required Software
- **.NET SDK 8.0** (8.0.419 or later) - [dotnet.microsoft.com](https://dotnet.microsoft.com/)
- **devkitPro** with devkitA64 - [devkitpro.org](https://devkitpro.org/)
  - `devkitA64` (aarch64-none-elf-gcc)
  - `switch-sdl2`, `switch-mesa`, `switch-libopenal`
  - `libnx`
  - Tools: `elf2nro`, `nacptool`
- **Android workload** (for ProjectZ.Core multi-targeting): `dotnet workload install android`

### Required devkitPro Packages
```
dkp-pacman -S switch-dev switch-sdl2 switch-mesa switch-libopenal switch-glfw
```

### Environment Variable
The build requires `SWITCH_BUILD=1` environment variable so that `ProjectZ.Core` compiles with the `SWITCH` constant:
```batch
set SWITCH_BUILD=1
```

## Build Pipeline

The build has 3 stages:

### Stage 1: NativeAOT Compile (C# to ARM64 native)
```batch
set SWITCH_BUILD=1
dotnet publish ProjectZ.Switch\ProjectZ.Switch.csproj -c Release -r linux-arm64 -p:PublishAot=true
```
This will **fail at the link step** (expected - Windows can't link for Linux ARM64). The important output is:
```
ProjectZ.Switch\obj\Release\net8.0\linux-arm64\native\ProjectZ.Switch.o  (~120MB)
```

### Stage 2: Compile C Shims + Link with devkitPro + Create NRO
```batch
ProjectZ.Switch\_link.bat
```
This batch file:
1. Sets `DEVKITPRO=C:\devkitpro` (adjust if different)
2. Compiles `nativeaot_shims.c`, `crypto_stubs.c`, `dlsym_table.c` with `aarch64-none-elf-gcc`
3. Links everything with devkitPro libraries (libnx, SDL2, OpenAL, Mesa EGL/GLES)
4. Links NativeAOT runtime libraries (`libRuntime.WorkstationGC.a`, `libbootstrapper.o`, etc.)
5. Converts ELF to NRO via `nacptool` + `elf2nro`

Output: `ProjectZ.Switch\bin\SwitchRelease\ProjectZ.Switch.nro`

### Stage 3: Assemble SD Card Package
Copy game assets alongside the NRO:
```batch
mkdir ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd
copy ProjectZ.Switch\bin\SwitchRelease\ProjectZ.Switch.nro ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\
xcopy /E /I ProjectZ.Core\Data ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\Data
xcopy /E /I ProjectZ.Core\Content ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\Content
echo.> ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\portable.txt
```

**Important:** The `Content` folder must contain pre-built XNB files. These come from the original v1.0.0 assets placed in `assets_original/` and migrated via `LADXHD_Migrater.exe`. Alternatively, use Content from an Android build.

## Quick Build (All Stages)

```batch
cd ladxhd_game_source_code
set SWITCH_BUILD=1

:: Stage 1: NativeAOT
dotnet publish ProjectZ.Switch\ProjectZ.Switch.csproj -c Release -r linux-arm64 -p:PublishAot=true

:: Stage 2: Link + NRO  
ProjectZ.Switch\_link.bat

:: Stage 3: Package
mkdir ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd 2>nul
copy /Y ProjectZ.Switch\bin\SwitchRelease\ProjectZ.Switch.nro ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\
xcopy /E /I /Y ProjectZ.Core\Data ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\Data
xcopy /E /I /Y ProjectZ.Core\Content ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\Content
echo.> ProjectZ.Switch\bin\SwitchRelease\switch\zelda-ladxhd\portable.txt
```

## Deploying to Switch

1. Copy the `switch` folder from `ProjectZ.Switch\bin\SwitchRelease\` to the **root of your SD card**
2. The final layout on the SD card should be:
   ```
   SD:/switch/zelda-ladxhd/
       ProjectZ.Switch.nro
       Content/          (207 XNB files)
       Data/             (735+ game files)
       portable.txt
   ```
3. Launch from the Homebrew Menu (Album or title override)

## Rebuild Shortcuts

**If you only changed C shim files** (`nativeaot_shims.c`, `crypto_stubs.c`):
- Skip Stage 1, just run `_link.bat` (much faster, ~30 seconds)

**If you changed C# code** (`Game1.cs`, `InputHandler.cs`, etc.):
- Must redo Stage 1 (NativeAOT ILC, ~3-5 minutes) then `_link.bat`

**If you only changed assets** (Data/ or Content/):
- Just re-copy to the SD card package, no rebuild needed

## Architecture Overview

```
C# Source Code (.cs)
    |
    v  [dotnet publish -p:PublishAot=true]
NativeAOT ILC (ProjectZ.Switch.o - 120MB ARM64 native code)
    |
    +-- nativeaot_shims.o  (glibc->newlib compatibility layer)
    +-- crypto_stubs.o     (OpenSSL/NetSecurity stubs)
    +-- dlsym_table.o      (1730 SDL2/OpenAL/GL symbol lookup table)
    +-- libbootstrapper.o  (NativeAOT runtime bootstrap)
    +-- libRuntime.WorkstationGC.a  (GC)
    +-- libSystem.Native.a (file I/O, threading)
    +-- libSDL2.a          (devkitpro)
    +-- libopenal.a        (devkitpro)
    +-- libEGL.a + libGLESv2.a + libglapi.a  (Mesa)
    +-- libnx.a            (Switch homebrew SDK)
    |
    v  [aarch64-none-elf-gcc + AOT_switch.specs + AOT_switch.ld]
ProjectZ.Switch.elf (ARM64 ELF, ~80MB)
    |
    v  [elf2nro]
ProjectZ.Switch.nro (Switch homebrew, ~33MB)
```

## Key Files

| File | Purpose |
|------|---------|
| `ProjectZ.Switch.csproj` | NativeAOT project config (linux-arm64, InvariantGlobalization) |
| `AOT_switch.ld` | Custom linker script (moves rodata to RW segment for NRO) |
| `AOT_switch.specs` | GCC specs for Switch (PIE, no dynamic linker, no RELRO) |
| `nativeaot_shims.c` | **Core compat layer** (~1400 lines): TLS, mmap, mprotect, stat/dirent ABI conversion, dlopen/dlsym with static symbol table, path translation (sdmc:), GL extension injection, SDL input polling |
| `crypto_stubs.c` | Stubs for OpenSSL/NetSecurity + working pread/pwrite |
| `dlsym_table.c` | Auto-generated: 1730 SDL2/OpenAL/GL function pointers for runtime dlsym |
| `_link.bat` | Compiles shims, links with devkitpro, creates NRO |
| `build-switch.bat` | Full pipeline (Stage 1 + 2 + 3) |
| `rd.xml` | NativeAOT trimming directives (preserve MonoGame reflection) |

## Troubleshooting

### Logs on SD Card
The NRO writes diagnostic logs to `sdmc:/switch/zelda-ladxhd/`:
- `boot.log` - Full boot sequence, SDL init, GL version, shader compilation
- `native_stderr.log` - C# unhandled exceptions / stderr redirect
- Atmosphère crash reports - `01XXXXXXXXX_05c9a9099511f000.log` in the switch root if the process aborts

### Common Issues
- **NativeAOT ILC fails with "Cross-OS not supported"**: The csproj has `DisableUnsupportedError=true` which bypasses this. The clang link step will still fail (expected).
- **Android workload missing**: Run `dotnet workload install android`
- **devkitPro paths**: `_link.bat` assumes `C:\devkitpro`. Edit `DEVKITPRO` variable if different.
- **Content missing**: XNB files must be pre-built. Use Android build Content or run the Migrater tool.
