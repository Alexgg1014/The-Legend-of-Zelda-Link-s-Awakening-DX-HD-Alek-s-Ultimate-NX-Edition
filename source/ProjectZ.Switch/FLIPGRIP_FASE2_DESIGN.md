# Flip Grip — fase 2: qué va dentro del panel

Continuación de `FLIPGRIP_DESIGN.md`, que resolvió **dónde** va el panel. Esto resuelve
**qué** contiene.

Directiva del usuario: que quede como los paneles de **TMC ALEKS**, **Zelda3 ALEKS** y
**Dusklight**, y **sin complicarlo**. Así que esto no inventa una UI: copia la que esos
proyectos ya tienen, traducida a lo que LADXHD ya sabe dibujar.

---

## 1. La disposición, tal cual la de TMC

Comentario literal de `port_second_screen.c`:

> - **SIDEBAR right**: hearts on top, the game's contextual R prompt under them, the A/B
>   equip rings in the middle, the rupee/keys chip at the bottom.
> - **TAB BAR bottom**: [QUEST][MAP][ITEMS] plus a square settings button, all in the pause
>   menu's own button plate.
> - **ITEMS / QUEST / SETTINGS tabs** replace the map area with menu-style panels.

Traducido a nuestro panel de **720×540** en coordenadas del lienzo:

```
+-------------------------------------------------+  0
|                                     |  corazones |
|                                     |            |
|          AREA PRINCIPAL             |   A   B    |  barra
|      (mapa / mazmorra / equipo)     |  anillos   |  lateral
|                                     |            |
|                                     | rupias  🔑 |
+-------------------------------------------------+
|  [ QUEST ] [ MAPA ] [ EQUIPO ]           [ ⚙ ]   |  barra de pestañas
+-------------------------------------------------+  540
```

Reparto propuesto: barra lateral **160 px**, barra de pestañas **56 px**, área principal
**560×484**. Todo derivado de la unidad `u = min(w,h)/720` como en el donante, para que
sobreviva a cambios de tamaño del panel.

---

## 2. De dónde sale cada píxel

La doctrina ALEKS es "cero píxeles horneados": todo se reconstruye con el arte que el
juego ya carga. En TMC eso obligaba a decodificar tablas de la ROM. **En LADXHD es mucho
más fácil**, porque los recursos ya son objetos cargados:

| Elemento del panel | Fuente en LADXHD | Estado |
|---|---|---|
| Chrome de ventana | `SprWhite` + `RoundedCornerEffect` + `InventoryBackgroundColorBot` | ✅ hecho |
| Mapa | RT de `MapOverlay` | ✅ hecho |
| Mazmorra | RT de `DungeonOverlay` (ya trae llaves, brújula, mapa, pico) | ✅ hecho |
| Equipo | RT de `InventoryOverlay` | pendiente, 1 accesor |
| Corazones / rupias / llaves | `ItemDrawHelper` — el mismo dibujado del HUD | pendiente |
| Iconos de objetos | `Resources.SprItem` | pendiente |
| Texto | `Resources.GameFont` / `GameHeaderFont` | ✅ en uso |
| Colores de túnica | `OverlayManager.InventoryTunicColors` | pendiente |

**Ninguno pide arte nuevo.** Es la misma promesa del donante, cumplida con menos trabajo.

---

## 3. Pestañas

### MAPA ✅ y MAZMORRA ✅
Hechas, con auto-cambio al entrar y salir de mazmorra y "pin" manual, igual que el donante.

### EQUIPO
`InventoryOverlay` tiene su propio render target y se dibuja exactamente igual que los dos
que ya usamos. **Es la pestaña más barata que queda**: un accesor de una línea en Core y
reutilizar el camino existente. Debería ser la siguiente.

### QUEST / COLECCIÓN
Aquí es donde este juego se diferencia. LADXHD guarda, y no enseña en ningún sitio cómodo:

- **Los 8 instrumentos** (`instrument0..7`)
- **Las 20 conchas secretas**
- **Las 5 hojas doradas**
- **Piezas de corazón**
- **Las 12 fotos** (exclusivas de DX)
- **La secuencia de intercambio de 14 pasos**

La secuencia de intercambio es *la* función que justifica el panel en este juego: es una
cadena que la gente consulta en guías constantemente y que el juego no muestra jamás.

Dibujada con `SprItem` sobre las celdas del chrome, es el equivalente directo de la
"quest-status screen" del donante.

### AJUSTES
Los combos actuales (`L+R+…`) son andamiaje. La pestaña de ajustes los sustituye por filas
normales: panel on/off, límite de fps, escala, hueco, contador. El donante persiste los
suyos en su config; nosotros ya tenemos `switchsettings.txt` y su `Save()`.

**Nota**: mientras no haya toque, esta pestaña es inalcanzable — exactamente el mismo caso
que documenta `port_second_screen_config_switch.c` del donante ("the Settings tab that
would surface them is unreachable"). Así que va **después** del toque, no antes.

---

## 4. Barra lateral

Es lo que convierte el panel de "un mapa" en un HUD secundario, y en Link's Awakening tiene
un valor extra: **permite quitar el HUD de la pantalla del juego**. Con la consola en
vertical la ranura de juego es 4:3 y el HUD ocupa sitio; moverlo abajo lo libera.

- **Corazones** arriba — `ItemDrawHelper`, mismo dibujado que el HUD
- **Anillos A/B** en medio, con el objeto equipado
- **Rupias y llaves** abajo

Los anillos A/B del donante son además tappables: tocas un anillo y luego un objeto para
asignarlo. Eso es fase 3.

---

## 5. Toque

Lo que separa "una segunda pantalla que mira" de "una segunda pantalla".

**A favor**: con Flip Grip la consola va en modo portátil y la pantalla táctil queda
accesible. Y la des-rotación ya está portada, con la matriz de composición construida como
su inversa exacta — así que el toque caerá donde se ve sin reajustar nada.

**A verificar antes de prometer nada**: que los eventos de toque de SDL lleguen a través de
nuestro shim. No está comprobado. Es lo primero que hay que mirar cuando se aborde.

---

## 6. Orden propuesto

Pensado para que cada paso valga por sí solo, sin big bang:

1. **Pestaña EQUIPO** — un accesor, camino ya existente. Media hora, y duplica la utilidad.
2. **Barra lateral con corazones y rupias** — `ItemDrawHelper`, sin lógica nueva.
3. **Barra de pestañas dibujada** — sustituye la cabecera de texto actual por los botones
   del donante. Prepara el toque sin necesitarlo (se sigue navegando con la cruceta).
4. **Toque** — primero verificar que SDL lo entrega; luego mapear con `MapTouch`.
5. **Pestaña QUEST** con la secuencia de intercambio.
6. **Pestaña AJUSTES**, ya con toque.

Los pasos 1-3 no necesitan nada nuevo de Core más allá de un accesor, y no tocan el
pipeline gráfico. El 4 es el único con riesgo de investigación.

---

## 7. Lo que este diseño NO hace, a propósito

- **Nada de DUAL horizontal.** Descartado por el usuario y por Dusklight.
- **Nada de sistema de temas.** El donante necesitaba uno porque decodificaba de la ROM;
  aquí los recursos ya están cargados.
- **Nada de arte nuevo.** Si algo no se puede dibujar con lo que el juego ya tiene, no entra.
- **Nada de tocar la lógica del juego.** El teletransporte desde el mapa (que sería posible,
  `TeleportMap` ya existe) queda fuera hasta que todo lo demás esté sólido.
