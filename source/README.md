# Source — Switch layer

- `ProjectZ.Switch/` — the whole port: platform services, NativeAOT shims (`nativeaot_shims.c`), Flip Grip compositor, companion panel, touch, settings, story guide. Drop it next to `ProjectZ.Core/` in a checkout of [ladxhd_updated](https://gitlab.com/bighead.0/ladxhd_updated) **tag v2.0.8**.
- `core-patches-v2.0.8.diff` — the small patches this port needs in `ProjectZ.Core` (render size override, UI scale round-up, `IPlatformVideoOptions` hook, render-target accessors, discovery-fog optimization, `ModFile` dynamic→object for NativeAOT). Apply with `git apply`.
- `STORY_GUIDE.md` — data for the "next step" guide (item ids verified against the game's atlas and scripts).
- Build steps: `ProjectZ.Switch/SWITCH_BUILD.md` (.NET 9 SDK, ILCompiler for linux-arm64, devkitPro/devkitA64, switch-sdl2/mesa/openal). Stage 1 = `dotnet publish -p:PublishAot=true` (its final Linux link is expected to fail), Stage 2 = `_link_adapted.bat`.
