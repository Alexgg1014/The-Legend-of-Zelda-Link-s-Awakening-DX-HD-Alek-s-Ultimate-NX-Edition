# Panel companion — layout A (TMC ALEKS) — especificación (2026-09-17)

Cierra el "qué va dentro del panel" de `FLIPGRIP_FASE2_DESIGN.md`. Decisión del usuario:
**layout A** (barra lateral derecha + pestañas abajo, como TMC ALEKS y Zelda3 ALEKS), con los
slots de mano del juego (A/B/X/Y[/L/R]) en vez de los anillos A/B + prompt R de TMC, **y con
opción de ocultar el HUD del juego**
mientras el panel está visible (la barra lateral ES el HUD).

Maquetas de referencia: `PANEL_LAYOUT_A.png` (render + cotas), `PANEL_LAYOUT_A_SLOTS.png`
(barra lateral con 4 y con 6 botones) y `PANEL_LAYOUT_A_TABS.png` (peor caso de la barra lateral,
pestaña EQUIPO y pestaña MISIÓN).

Todas las cotas son **píxeles del lienzo** (panel 720×540 en FLIP, `AleksLayout.Companion`),
relativas a la esquina superior izquierda del panel. Si el panel cambia de tamaño se escalan con
`u = min(w,h)/540` — igual que el donante hace con `u = min(w,h)/720`.

---

## 1. Regiones

| Región | x | y | w | h | Notas |
|---|---|---|---|---|---|
| Chrome | 0 | 0 | 720 | 540 | Ya hecho: `SprWhite` + `RoundedCornerEffect` r=3, `InventoryBackgroundColorBot` |
| Área principal | 8 | 8 | 552 | 468 | Fondo `SprWhite` negro×0.08 (misma receta que los slots del inventario, `Color.Black * 0.15f`, más suave) |
| Barra lateral | 568 | 8 | 144 | 468 | Sin fondo propio (va sobre el chrome) |
| Barra de pestañas | 8 | 484 | 704 | 48 | 3 botones de 150 + 1 cuadrado de 56, separación 8 |

Desaparece la cabecera de texto actual (`DrawHeader`): el título lo da la pestaña activa.

## 2. Área principal (552×468) — contenido por pestaña

El RT de cada pestaña se ajusta con `AleksLayout.Fit` dentro del área, centrado.

| Pestaña | Fuente | Tamaño resultante |
|---|---|---|
| MAPA | `OverlayManager.MapOverlayRef.RenderTarget` (144×144) + marcador de Link | 460×460 (×3.19) → x=54, y=12 |
| MAZMORRA | `DungeonOverlayRef.RenderTarget` | Fit |
| EQUIPO | `InventoryRenderTarget` (268×208 aprox.) | Fit |
| MISIÓN | Dibujado por nosotros (§5) | — |

Auto-cambio MAPA↔MAZMORRA y pin manual: ya implementado en `SecondScreenState`.

**Marcador de Link:** fuera el cuadrado rojo de `DrawPlayerDot`. El juego tiene su propio marcador
animado: `AnimatorSaveLoad.LoadAnimator("mapPlayer")` (`Data/Animations/mapPlayer.ani`: 2 frames de
5×5 en `minimap.png` (169,113) y (177,113), 200 ms), y `MapOverlay.Draw` lo pinta con
`_animationPlayer.DrawBasic(sb, pos, color, scale)` en `(8 + X*8 + 2, 8 + Y*8 + 2) * scale`.
Hacemos lo mismo en el panel con `scale = dest.Width / 144f` — 16 px en el panel, animado igual
que en el mapa del juego, sin sprites nuevos. `Update()` del animator una vez por frame.
Los iconos del mapa (tienda/?/cueva/búho, `_recIcon` 161,1,30,30) los pinta `MapOverlay.Draw` fuera
del RT y `_mapIcons` es privado: quedan para más adelante (haría falta exponerlo en Core).

**Peor caso verificado en la maqueta (`PANEL_LAYOUT_A_TABS.png`):** 14 corazones + 6 botones + 999
rupias → corazones 16..88, rombo 104..340, rupias 364, llaves 416..452; el límite de la barra
es 476. Cabe sin solaparse.

## 3. Barra lateral (568..712)

Escalas: `@3` = sprite ×3, `@4` = sprite ×4. Todo con `ItemDrawHelper`, que ya recibe `scale`.

| Elemento | Posición (x,y) | Tamaño | Llamada | Fuente de datos |
|---|---|---|---|---|
| Corazones | 580, 16 | **5 por fila** @3, paso 24 → 120 de ancho; 14 corazones (máximo del juego) = 3 filas = 72 alto | bucle propio con `Resources.GetSprite("ui heart")` (mismo cálculo de tipo 0..4 que `ItemDrawHelper.DrawHearts`; el helper hace filas de 7 = 168 px y no cabe en 144) | `GameManager.CurrentHealth`, `MaxHearts` |
| Slots de mano | rombo, origen (580, 16 + altoCorazones + 16) | celda 56×56, paso 4 → 120×176 (4 botones) / 120×236 (6) | por slot: `SprWhite` negro×0.15 + `ItemDrawHelper.DrawItemWithInfo(sb, Equipment[i], offset, rect, 3, Color.White)` + etiqueta `GameFont` en (x+4, y+14) | `GameManager.Equipment[0..HandItemSlots-1]`, etiquetas `ControlHandler.ControllerLabels[ControlHandler.ControllerIndex, i]` |
| Rupias | 582, (fin del rombo + 24) | 112×24 | `ItemDrawHelper.DrawRubee(sb, pos, 4, Color.White)` | interno del helper (`_rubyCount`, animado) |
| Llaves | 610, (rupias + 52) | 84×36 | `ItemDrawHelper.DrawSmallKeys(sb, pos, 4, Color.White)` | interno del helper |

**Disposición de los slots: el rombo del propio juego.** LADXHD v2 tiene `Values.HandItemSlots` =
**4 o 6** (opción "seis botones" en `ControlSettingsPage`), y `InventoryOverlay.UpdateButtonLayout`
los coloca como los botones físicos del mando. Copiamos esas posiciones tal cual, con celda W=56
y separación p=4 (el juego usa W=30, p=8/2 a escala 1). Ver `PANEL_LAYOUT_A_SLOTS.png`.

| Índice `Equipment[i]` | Posición relativa al origen | Botón (Nintendo) |
|---|---|---|
| 0 | (W/2+4, 2W+2p) — abajo | B |
| 1 | (W+8, W+p) — derecha | A |
| 2 | (0, W+p) — izquierda | Y |
| 3 | (W/2+4, 0) — arriba | X |
| 4 | (0, −W−p) — arriba-izq (solo 6) | L |
| 5 | (W+8, −W−p) — arriba-der (solo 6) | R |

Con 6 botones el rombo entero baja W+p para dejar sitio a la fila L/R, y rupias/llaves bajan 60 px.
Leer `Values.HandItemSlots` cada frame: si el usuario cambia la opción, el panel se reordena solo.
Las etiquetas salen de `ControllerLabels[ControllerIndex, i]`, así que en un mando Xbox/PS se ven
las suyas — igual que en el HUD del juego.

Ventaja en FLIP: el rombo del Joy-Con derecho queda físicamente al lado de este rombo dibujado.
Y para el toque de fase 3 (asignar objeto a un slot), tocar la celda con la forma del botón es
más claro que una lista.

Alternativa de una sola llamada: `ItemSlotOverlay.Draw(sb, true, position, scale, 1f)` es
`public static` y dibuja todos los slots de mano con etiquetas, pero usa las posiciones de su
propio `UpdatePositions` (las del HUD, con celdas 30×20 del atlas `inventory item selection`), así
que las dibujamos una a una para tener celdas cuadradas de 56.

## 4. Barra de pestañas (y=484, h=48)

| Botón | x | w | Texto |
|---|---|---|---|
| MISIÓN | 8 | 150 | `Resources.GameHeaderFont` |
| MAPA / MAZMORRA | 166 | 150 | cambia el rótulo con el contexto |
| EQUIPO | 324 | 150 | |
| ⚙ AJUSTES | 656 | 56 | `Resources.GetSprite("gearIcon")` — existe en `Content/Menu/gearIcon.xnb` |

Estados: inactivo = `SprWhite` negro×0.12, texto `InventoryTunicColors`-neutro oscuro; activo =
`SprWhite` blanco×0.95 + borde de 3 px verde (`ItemDrawHelper.CloakColors[0]`, el verde de la túnica)
+ texto azul (`CloakColors[1]`). Esquinas: `RoundedCornerEffect` r=6.
Estos son exactamente los colores que ya carga el juego: cero constantes nuevas.

Navegación: cruceta izq/der (ya existe `Cycle(±1)` con el combo) y toque (§7).

## 5. Pestaña MISIÓN (dentro del área principal 552×468)

Todo desde `Resources.SprItem` vía `Resources.GetSprite(id)` y `GameManager.GetItem(id)`.

| Fila | y | Contenido | Datos |
|---|---|---|---|
| Instrumentos | 24 | 8 celdas de 56×56, paso 66 desde x=24, `instrument0..7` @3 (48×48); no conseguidos al 28 % alpha | `GetItem("instrument"+i) != null` |
| Colección | 104 | `shell` N/20 · `goldLeaf` N/5 · `heartMeter` N/4 · fotos N/12 | `GetItem("shell").Count`, `GetItem("goldLeaf").Count`, `GetItem("heartMeter").Count`, `SaveManager.GetString("photo_"+(i+1))` no vacío |
| Intercambio | 186 | rótulo + 14 celdas 64×72 en 2 filas de 7 (paso 76 / 84), numeradas 1..14, `trade0..13` @3; el poseído actual con marco de selección (`inventory item selection` del atlas `ui`, o esquinas naranjas como TMC) | `GetItem("trade"+i) != null` (solo uno a la vez: el siguiente a entregar) |
| Espacio libre | 372..468 | ~96 px sin uso: reservado para el texto del paso actual del intercambio ("Dale el plátano a Kiki") con `GameFont`, o para crecer las celdas | |
| Números | — | `ItemDrawHelper.DrawNumber(sb, x, y, n, len, 3, Color.Black)` (dígitos `ui letter`) | |

## 6. HUD del juego oculto

`Game1.GameManager.InGameOverlay.HideHud(bool)` **ya existe y es público** (`OverlayManager.cs:873`);
anima la salida del HUD con el mismo fade que usan los menús. Llamar con `true` cuando el panel
esté visible y la opción `switchsettings.txt: hidehud=1` esté activa; con `false` al salir de FLIP.
Sin tocar Core.

## 7. Toque (con `SwitchTouch` + `AleksLayout.MapTouch`, ya en marcha)

Zonas tappables, en px de panel (`MapTouch` devuelve `localX/localY` en 320×240 → multiplicar
por 2.25):

| Zona | Rectángulo | Acción |
|---|---|---|
| MISIÓN / MAPA / EQUIPO | 8..158 / 166..316 / 324..474 × 484..532 | `SecondScreenState.Set(tab)` |
| ⚙ | 656..712 × 484..532 | pestaña AJUSTES |
| Slot de mano i | celda 56×56 en la posición del rombo (§3) | fase 3 (asignar objeto) |
| Mapa | área principal | fase 3 (teletransporte, `TeleportMap`) |

Sustituye el reparto provisional en tercios de `PollTouch()`.

## 8. Accesores necesarios en Core

**Ninguno.** Verificado en v2.0.6:

- `OverlayManager`: `InGameHud`, `InventoryRenderTarget`, `MapOverlayRef`, `DungeonOverlayRef`,
  `InventoryBackgroundColorTop/Bot`, `InventoryTunicColors`, `UserCustomAlpha`, `HideHud(bool)`.
- `GameManager`: `Equipment[]`, `CurrentHealth`, `MaxHearts`, `GetItem(id)`, `SaveManager`,
  `PlayerMapPosition`.
- `ItemDrawHelper`: `DrawHearts/DrawRubee/DrawSmallKeys/DrawNumber/DrawItem/DrawItemWithInfo`
  (todos `public static` con parámetro `scale`), `CloakColors`.
- `Resources`: `SprItem`, `SprWhite`, `GameFont`, `GameHeaderFont`, `RoundedCornerEffect`,
  `GetSprite(id)`.

## 9. Orden de implementación (revisado)

1. Barra lateral (§3) + quitar `DrawHeader`. Sin lógica nueva.
2. Barra de pestañas dibujada (§4) con cruceta.
3. Toque sobre botones y anillos (§7) — cerrar primero la verificación de `MapTouch` en consola.
4. `hidehud` (§6) + fila en AJUSTES.
5. Pestaña MISIÓN (§5).
6. Pestaña AJUSTES (sustituye los combos L+R+…).
