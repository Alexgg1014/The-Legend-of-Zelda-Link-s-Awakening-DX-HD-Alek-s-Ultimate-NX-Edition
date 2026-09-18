# ALEKS Second Screen — diseño (LADXHD)

Directiva del usuario: el companion debe verse como PARTE del juego. Todo el arte
sale de los assets del propio LADXHD — cero arte nuevo, cero UI genérica.

## Fuentes de arte nativas (verificadas en ProjectZ.Core)

| Elemento del panel | Fuente en el juego |
|---|---|
| Sprites de items (por idioma + Redux) | `Resources.SprItem` (hoja completa) |
| Minimapa del overworld | `Resources.SprMiniMap` (por idioma) |
| Iconos sueltos (cursores, botones...) | `Resources.GetSprite(id)` (atlas `ui.atlas`) |
| Tipografías | `Resources.GameFont` / `Resources.GameHeaderFont` |
| Corazones / rupias / contadores | `ItemDrawHelper` (mismo dibujado que el HUD) |
| Panel de mapa completo | `MapOverlay` RT (ya con estilo del juego) |
| Panel de inventario | `InventoryOverlay` RT + fondos `InventoryBackgroundColorTop/Bot` |
| Automap de mazmorra | `DungeonOverlay` RT |
| Colores de túnica / fondos moddeables | `OverlayManager` (respeta LAHDMods del usuario) |

## Estructura (blueprint = worktree TMC ALEKS, `repro_f001/port/`)

Módulos a imitar 1:1 en C#:
- `port_second_screen_state.c`  -> SecondScreenState.cs (tab actual, selección, pin)
- `port_second_screen_render.c` -> SecondScreenRender.cs (composición del 320x240)
- `port_second_screen_dungeonmap.c` -> tab mazmorra (usa DungeonOverlay RT)
- `port_second_screen_quest.c`  -> tab quest/equipo (SprItem + ItemDrawHelper)
- `port_second_screen_config_switch.c` -> valores DUAL afinados en hardware
- Geometría: ya portada en `AleksLayout.cs` (fase 1)

## Tabs (fase 2)

1. MAPA — MapOverlay RT (fase 1 ya lo muestra)
2. MAZMORRA — DungeonOverlay RT cuando Link está en dungeon (auto-switch, como TMC)
3. EQUIPO — grid de `GameManager.Equipment` dibujado con SprItem + marcos del atlas,
   fondo con InventoryBackgroundColorTop/Bot (respeta mods de color del usuario)
4. AJUSTES del companion — estilo InterfacePage del juego

Look del marco: tarjeta con `RoundedCornerEffect` (el shader que ya usa el juego
para el mapa/inventario), NO rectángulos planos. El placeholder gris de la fase 1
se sustituye por esto.

## Gating

Nada de esto se despliega hasta cerrar: (1) el cuelgue de Draw (evidencia
pendiente: repro del usuario sobre el NRO diag v4), (2) el arranque en consola
real (probable modo applet vs reserva de 1.8GB — datos pendientes).
