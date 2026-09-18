# FLIP GRIP — diseño del second screen vertical (LADXHD Switch)

Alcance acordado con el usuario: **solo NORMAL y FLIP**. DUAL (apaisado partido) queda
descartado por ahora.

---

## 1. Por qué FLIP es el modo que importa

Con un Flip Grip la Switch se sostiene **en vertical**: el panel pasa a ser 720×1280.
Ese es exactamente el formato de una DS, y es para lo que existe el ALEKS second screen:

```
+------------------+  0
|                  |
|      JUEGO       |
|                  |
+------------------+
|       hueco      |
+------------------+
|                  |
|    COMPANION     |
|                  |
+------------------+  1280
```

RightBar (lo que hay hoy) es un apaño para apaisado: el panel se superpone al juego.
FLIP es el modo "de verdad": dos pantallas apiladas, ninguna tapa a la otra.

---

## 2. Autoridad de geometría: los donantes

Dos proyectos hermanos del usuario ya resolvieron esto en hardware. Sus decisiones no se
re-derivan, se copian.

### Z3NXB — `src/aleks_layout.c`, `compute_flip()`

```c
#define ALEKS_FLIP_CANVAS_W 720
#define ALEKS_FLIP_CANVAS_H 1280
out->rotation_degrees = 270;
```

El canvas es **lógico y fijo**: se compone en 720×1280 y el compositor lo **rota una sola
vez** al presentar. Nada aguas arriba sabe que hay una rotación.

**Lección 1 — la corrección de aspecto se aplica UNA vez.** Comentario literal del donante:

> *"Applying the correction twice is what shrank FLIP: a 4:3 game came out 543x724 in the
> canvas instead of 720x540, so the stack no longer filled the portrait screen."*

Dentro del canvas se usa `fit_raw` (sin corrección). La corrección vive solo en `flip_dst`,
el rectángulo donde el canvas aterriza en el panel físico.

**Lección 2 — el destino se calcula sobre la huella ROTADA.** Se ajusta el aspecto
1280×720 (la huella ya rotada) y luego se intercambian ancho y alto:

```c
AleksLayout_Fit(0, 0, out_w, out_h, ALEKS_FLIP_CANVAS_H, ALEKS_FLIP_CANVAS_W, &footprint);
out->flip_dst.w = footprint.h;   /* swapped by the rotation */
out->flip_dst.h = footprint.w;
```

Los dos ejes acaban con escalas distintas (`flip_scale_x` ≠ `flip_scale_y`), y el donante
dice explícitamente que **eso es correcto e inevitable**, porque la superficie no tiene la
forma del panel.

### TMC — `port/port_display_layout.c`, `DISPLAY_FLIP_270`

Mismo canvas 720×1280, misma rotación. Aporta la lección que más pesa aquí:

> *"The game always takes an exact integer scale (2x or 3x) rather than filling a column:
> a 630x420 box would also preserve the 3:2 aspect, but 630/240 is 2.625, and with nearest
> filtering some source columns duplicate twice and others three times, which reads as
> shimmering vertical banding. Integer scaling costs picture size and buys a clean image."*

**Lección 3 — escala entera o bandeo parpadeante.** En TMC el reparto sale exacto:
juego 720×480 (3× de 240×160) + panel 720×800 = 1280 clavados.

Y la lección de usabilidad: el panel por defecto va al 75% para que **el juego lea como la
pantalla principal**, no como una de dos iguales.

---

## 3. Traducción a LADXHD

La diferencia de fondo con los donantes: en zelda3 y TMC el juego es un framebuffer de
tamaño fijo (256×224, 240×160) que se escala. **LADXHD no tiene resolución nativa**: dibuja
por cámara a la resolución de la ventana, y la escala de cámara decide cuánto mundo se ve.

Consecuencia: aquí no se elige "qué múltiplo del framebuffer", sino **qué tamaño tiene la
ranura del juego** y **qué escala de cámara** se usa dentro.

### Reparto propuesto del canvas

```
juego      720 × 720     (cuadrado, el mundo se ve generoso)
hueco      720 ×  20
companion  720 × 540     (4:3, el formato del companion de los donantes)
                 -----
                  1280   exacto
```

720 + 20 + 540 = 1280. Sin sobras que centrar y sin decimales.

### Escala de cámara dentro de la ranura

| Escala | Mundo visible | Comentario |
|---|---|---|
| 3 | 240×240 | demasiado lejos |
| **4** | **180×180** | **por encima de los 160×128 de Game Boy. Recomendada** |
| 5 | 144×144 | por debajo del ancho de GB, algo agobiado |
| 6 | 120×120 | demasiado cerca en vertical |

**Escala 4, entera** (lección 3). Y ojo: **la escala de FLIP tiene que ser propia**, distinta
de la de apaisado (hoy 6), porque la ranura es cuadrada y no 16:9.

---

## 4. Dónde se engancha: el punto de presentación

`Game1.Draw` termina así:

```csharp
Graphics.GraphicsDevice.SetRenderTarget(null);
Graphics.GraphicsDevice.Viewport = new Viewport(0, 0, MainRenderTarget.Width, MainRenderTarget.Height);
Graphics.GraphicsDevice.Clear(Color.Black);
SpriteBatch.Begin(..., doColorCorrect);
SpriteBatch.Draw(_finalRenderTarget, new Rectangle(0, 0, MainRenderTarget.Width, MainRenderTarget.Height), Color.White);
SpriteBatch.End();
```

**El frame entero ya compuesto vive en `_finalRenderTarget`.** Ese es el punto de enganche
natural, y es exactamente el modelo del donante: "el compositor rota una vez".

El plan, todo desde `SwitchGame.Draw`, sin tocar cómo dibuja el juego:

1. El juego renderiza normal en `_finalRenderTarget`, ya dimensionado a la ranura (720×720).
2. Tras `base.Draw()`, limpiar el backbuffer.
3. Dibujar `_finalRenderTarget` **rotado 270°** en su sitio del canvas.
4. Dibujar el companion, también rotado, debajo.

En MonoGame la rotación es un parámetro de `SpriteBatch.Draw`, así que no hace falta ningún
render target extra para el canvas: se puede componer directamente sobre el backbuffer
aplicando la misma transformación a las dos piezas.

---

## 5. El único enganche que falta en Core

Para que el juego renderice a 720×720 en vez de a 1280×720 hay que **desacoplar el tamaño
de render del tamaño del panel**. Hoy no se puede: `Game1` recalcula cada frame

```csharp
else if (WindowWidth != Window.ClientBounds.Width || WindowHeight != Window.ClientBounds.Height)
    OnResize();
```

así que cualquier valor que le pongamos lo revierte al frame siguiente.

Hace falta **un override**: un `Point?` estático que, cuando tiene valor, mande sobre
`ClientBounds` en los dos sitios que calculan el tamaño (`Update` y `ForceRecalculateScaling`).
Serían ~6 líneas en Core.

**Y ese mismo enganche da gratis la escala de resolución** que ya identificamos como la
optimización pendiente más gorda para el modo normal. Dos funciones, un solo cambio.

---

## 6. FLIP es además una optimización

Esto no lo esperaba y es el hallazgo más interesante del diseño.

| Modo | Píxeles de mundo por frame |
|---|---|
| Apaisado 1280×720 | 921.600 |
| FLIP, ranura 720×720 | 518.400 |

**−44% de relleno.** Y como los render targets del blur se dimensionan con
`width / blurScale`, también encogen.

Medido en hardware: el juego está limitado por GPU (subir la CPU de 1020 a 1500 MHz solo
da ~2 fps; subir la GPU a 460 MHz lo lleva cerca de 60). O sea que **FLIP debería correr
sensiblemente mejor que el modo apaisado**, no peor, pese a dibujar además el companion.

La intuición del usuario ("con flip iría mejor porque la resolución es menor") es correcta,
y por este mecanismo exacto.

---

## 7. Toque (cuando llegue)

El donante ya resuelve la des-rotación, y hay que copiarla tal cual porque cada eje lleva
su propia escala:

```c
lx = logical_w / 2 - (phys_y - out_h / 2) / flip_scale_x;
ly = logical_h / 2 + (phys_x - out_w / 2) / flip_scale_y;
```

---

## 8. Orden de trabajo

1. `AleksLayout`: añadir `Mode.Flip` con `ComputeFlip`, portando `compute_flip` con sus tres
   lecciones. Es geometría pura y se puede validar sin hardware.
2. El override de tamaño de render en Core (~6 líneas) + `SwitchSettings.RenderScale`.
3. Accesor `Game1.FinalRenderTarget` (séptimo accesor de solo lectura).
4. Composición rotada en `SwitchGame.Draw`.
5. Escala de cámara propia de FLIP (`flipgamescale`, por defecto 4).
6. Ajustes: `flipcompanionpct` (por defecto 75, como TMC) y `flipgap`.
7. Afinar en hardware. Los donantes dicen claramente que estos valores se tocan en consola.

---

## 9. Riesgos conocidos

- **La UI del juego no sabe de la rotación.** El HUD, los menús y los overlays se dibujarán
  dentro de la ranura del juego, que es lo correcto — pero a 720×720 el HUD puede quedar
  apretado. Puede pedir tocar `UiScale`.
- **El contador de FPS y el HUD del benchmark** se dibujan hoy sobre el backbuffer sin
  rotar: en FLIP saldrían de lado. Hay que meterlos en la composición rotada.
- **El primer frame tras cambiar de modo** recrea render targets; con la lección del
  benchmark, hay que descartar ese frame en cualquier medición.
