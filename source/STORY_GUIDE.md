# STORY GUIDE — bloque "SIGUIENTE PASO" (pestaña MISIÓN)

Documento de datos para implementar el bloque SIGUIENTE PASO descrito en `HANDOFF.md` §0.
No contiene código. Todos los ids de objeto están verificados contra
`Data/Map Objects/items.atlas` y `ProjectZ.Core/InGame/Things/ItemManager.cs` (árbol activo
`_switchbuild/upstream204/`), y la lógica de progreso contra `Data/scripts.zScript`.

Fecha: 17-sep-2026.

---

## 0. Ids reales (verificados)

| Concepto (HANDOFF) | Id real en el juego | Notas |
|---|---|---|
| sword1 / sword2 | `sword1`, `sword2` | sword2 = 20 caracolas (Seashell Mansion). Opcional. |
| shield | `shield` | Tarin te lo da al empezar; `mirrorShield` = escudo espejo (D7). |
| powder | `powder` | Contador (max 20). `toadstool` = la seta (se quita al dársela a la bruja). |
| tailKey | **`dkey1`** | Llave Tail. No existe `tailKey`. |
| slimeKey | **`dkey2`** | Llave Viscosa (Pradera Ukuku → D3). |
| anglerKey | **`dkey3`** | Llave Abisal (cascada → D4). |
| faceKey | **`dkey4`** | Llave del Rostro (→ D6). |
| birdKey | **`dkey5`** | Llave Rapaz (→ D7). |
| dkey6..dkey8 | **no existen** | D5 (bucear), D7 (dkey5) y D8 (canción de Mamu) no usan llave-objeto. |
| instrument0..7 | `instrument0`..`instrument7` | instrumentN = mazmorra N+1. |
| bracelet / stonelifter | `stonelifter` (L1, D2), `stonelifter2` (L2, D6) | `stonelifter0/1` son solo sprites del atlas. |
| pegasusBoots | `pegasusBoots` | D3. |
| feather | `feather` | D1. |
| flippers | `flippers` | D4. |
| hookshot | `hookshot` | D5. |
| bow | `bow` | Tienda, 980 rupias. Opcional. |
| boomerang | `boomerang` | Goriya de la cueva de Toronbo, requiere `trade13`. Opcional. |
| magicRod | `magicRod` | D8. |
| ocarina | `ocarina` | Santuario de los Sueños (Mabe). |
| canciones | `ocarina_maria` (Balada), `ocarina_manbo` (Mambo), `ocarina_frog` (Canción de Mamu) | Son items normales: `GetItem("ocarina_frog") != null` funciona. |
| shovel | `shovel` | Tienda, 200 rupias. Necesaria para desenterrar `dkey2`. |
| goldLeaf | `goldLeaf` | Contador 0..5. Se quitan las 5 al dárselas a Richard (`remove_item:goldLeaf:5`). |
| acompañantes | `marin`, `rooster`, `ghost` | Items "invisibles": Marin te sigue, gallo volador, fantasma. Se quitan al terminar. |
| trade | `trade0`..`trade13` | `trade0`..`trade11` se quitan al entregarlos; `trade12` se quita en la estatua; **`trade13` (lupa) nunca se quita**. |
| nightmarekey / smallkey / compass / dmap / stonebeak | idem | Objetos de mazmorra; no se usan aquí. |

Las llaves `dkey1..5` **nunca se quitan** del inventario (no hay `remove_item:dkey*` en scripts),
así que "TIENES dkeyN" es una condición estable de por vida.

---

## 1. Tabla de pasos de la misión principal

Reglas de evaluación: se recorre de arriba abajo y **gana la primera fila cuya condición se cumpla**.
`+x` = tienes x (`GetItem("x") != null`), `-x` = no lo tienes. `goldLeaf<5` = `GetItem("goldLeaf")?.Count < 5`.
`tradeN` como condición = ese objeto está en el bolsillo (misma lógica que `reached` en `DrawQuest`).

Textos: máx. 2 líneas de ~40 caracteres, mayúsculas, sin acentos, listos para `Resources.GameFont`.

| # | Condición | Lugar | ES (línea 1 / línea 2) | EN (línea 1 / línea 2) |
|---|---|---|---|---|
| 1 | `-sword1` | Costa Toronbo | VE A LA PLAYA (COSTA TORONBO) / Y RECUPERA TU ESPADA | GO TO THE BEACH (TORONBO SHORES) / AND GET YOUR SWORD BACK |
| 2 | `+sword1 -toadstool -powder -dkey1` | Bosque Misterioso | BOSQUE MISTERIOSO: COGE LA SETA / Y LLEVASELA A LA BRUJA | MYSTERIOUS FOREST: PICK THE / TOADSTOOL FOR THE WITCH |
| 3 | `+toadstool -powder` | Choza de la bruja (N del bosque) | LLEVA LA SETA A LA BRUJA / (CHOZA AL NORTE DEL BOSQUE) | BRING THE TOADSTOOL TO THE / WITCH'S HUT (NORTH OF FOREST) |
| 4 | `+powder -dkey1` | Bosque Misterioso | ECHA POLVOS AL MAPACHE DEL / BOSQUE Y COGE LA LLAVE TAIL | POWDER THE RACCOON IN THE / FOREST, GET THE TAIL KEY |
| 5 | `+dkey1 -instrument0` | Cueva Tail (S de Mabe) | CUEVA TAIL (SUR DE MABE): / CONSIGUE EL INSTRUMENTO 1 | TAIL CAVE (SOUTH OF MABE): / GET INSTRUMENT 1 |
| 6 | `+instrument0 -stonelifter -instrument1` | Cueva Moblin (Tal Tal) → Pantano Goponga | RESCATA A BOW-WOW (CUEVA MOBLIN) / Y LLEVALO AL PANTANO GOPONGA | RESCUE BOW-WOW (MOBLIN CAVE) / AND TAKE HIM TO GOPONGA SWAMP |
| 7 | `+stonelifter -instrument1` | Gruta del Cántaro (D2) | GRUTA DEL CANTARO: / CONSIGUE EL INSTRUMENTO 2 | BOTTLE GROTTO: / GET INSTRUMENT 2 |
| 8 | `+instrument1 -dkey2 -trade3 -trade4 goldLeaf<5` y `reached<3` | Mabe / Toronbo | HAZ LOS TRUEQUES HASTA LOS / PLATANOS (VER INTERCAMBIO) | DO THE TRADES UP TO THE / BANANAS (SEE TRADE ROW) |
| 9 | `+trade3 -dkey2` | Puente del Castillo Kanalet | DALE LOS PLATANOS A KIKI / (PUENTE DEL CASTILLO KANALET) | GIVE THE BANANAS TO KIKI / (KANALET CASTLE BRIDGE) |
| 10 | `+instrument1 -dkey2 goldLeaf<5` y `reached>=4` | Castillo Kanalet | CASTILLO KANALET: REUNE LAS / 5 HOJAS DORADAS PARA RICHARD | KANALET CASTLE: COLLECT THE / 5 GOLDEN LEAVES FOR RICHARD |
| 11 | `+instrument1 -dkey2 -shovel` | Tienda de Mabe | COMPRA LA PALA EN LA TIENDA / DE MABE (200 RUPIAS) | BUY THE SHOVEL AT THE MABE / SHOP (200 RUPEES) |
| 12 | `+instrument1 -dkey2 +shovel` (`goldLeaf==5` o ya entregadas) | Villa de Richard → Campo de Hoyos | DALE LAS HOJAS A RICHARD Y CAVA / ANTE EL BUHO DEL CAMPO DE HOYOS | GIVE RICHARD THE LEAVES, DIG / AT THE OWL IN POTHOLE FIELD |
| 13 | `+dkey2 -instrument2` | Caverna de la Llave (S de Ukuku) | CAVERNA DE LA LLAVE (SUR DE LA / PRADERA UKUKU): INSTRUMENTO 3 | KEY CAVERN (SOUTH UKUKU / PRAIRIE): GET INSTRUMENT 3 |
| 14 | `+instrument2 -ocarina` | Santuario de los Sueños (Mabe) | MABE: SANTUARIO DE LOS SUENOS / (BRAZALETE Y BOTAS): OCARINA | MABE: DREAM SHRINE (BRACELET / AND BOOTS): GET THE OCARINA |
| 15 | `+instrument2 -dkey3 +marin` | Este de la Aldea Animal | LLEVA A MARIN HASTA LA MORSA / (ESTE DE LA ALDEA ANIMAL) | TAKE MARIN TO THE WALRUS / (EAST OF ANIMAL VILLAGE) |
| 16 | `+instrument2 -dkey3 -marin` | Aldea Animal → Desierto Yarna | ALDEA ANIMAL: MARIN Y LA MORSA, / LUEGO DESIERTO YARNA (LLAVE) | ANIMAL VILLAGE: MARIN AND THE / WALRUS, THEN YARNA DESERT (KEY) |
| 17 | `+dkey3 -instrument3` | Cascada de Tal Tal (N) → Túnel Abisal | TUNEL ABISAL: CERRADURA EN LA / CASCADA DE TAL TAL (NORTE) | ANGLER'S TUNNEL: KEYHOLE AT / THE TAL TAL WATERFALL (NORTH) |
| 18 | `+instrument3 -instrument4` | Bahía de Martha → Fauces del Siluro | BAHIA DE MARTHA: BUCEA HASTA / LAS FAUCES DEL SILURO | MARTHA'S BAY: DIVE DOWN TO / CATFISH'S MAW |
| 19 | `+instrument4 -dkey4` | Ruinas del sur (Templo del Rostro) | RUINAS DEL SUR (TEMPLO DEL / ROSTRO): LLAVE DEL ROSTRO | SOUTHERN FACE SHRINE RUINS: / GET THE FACE KEY |
| 20 | `+dkey4 -instrument5` | Templo del Rostro (D6) | TEMPLO DEL ROSTRO (NORTE DE / LAS RUINAS): INSTRUMENTO 6 | FACE SHRINE (NORTH OF THE / RUINS): GET INSTRUMENT 6 |
| 21 | `+instrument5 -ocarina_frog` | Laberinto de Carteles (Mamu) | LABERINTO DE CARTELES: CANCION / DE MAMU (300 RUPIAS, GANCHO) | SIGNPOST MAZE: MAMU'S SONG / (300 RUPEES, HOOKSHOT) |
| 22 | `+ocarina_frog -rooster -dkey5` | Veleta de Mabe | MABE: LEVANTA LA VELETA (BRAZ. 2) / Y TOCA LA CANCION DE MAMU | MABE: LIFT THE WEATHERVANE (L2) / AND PLAY THE FROG'S SONG |
| 23 | `+rooster -dkey5` | Cueva de Tal Tal (este) | TAL TAL (ESTE): CUEVA DE LA / LLAVE RAPAZ, VUELA CON EL GALLO | TAL TAL (EAST): BIRD KEY CAVE, / FLY WITH THE ROOSTER |
| 24 | `+dkey5 -instrument6` | Torre del Águila (D7) | TORRE DEL AGUILA: CERRADURA / EN LA CORDILLERA TAL TAL | EAGLE'S TOWER: KEYHOLE IN / THE TAL TAL MOUNTAINS |
| 25 | `+instrument6 -instrument7` | Roca de la Tortuga (D8, oeste) | ROCA DE LA TORTUGA (OESTE): / TOCA LA CANCION DE MAMU | TURTLE ROCK (WEST MOUNTAINS): / PLAY THE FROG'S SONG |
| 26 | `+instrument7 -ocarina_maria` | Marin | BUSCA A MARIN Y APRENDE LA / BALADA DEL PEZ DEL VIENTO | FIND MARIN AND LEARN THE / BALLAD OF THE WIND FISH |
| 27 | `+instrument0..7 +ocarina_maria` | Monte Tamaranch (huevo) | MONTE TAMARANCH: TOCA LA BALADA / ANTE EL HUEVO DEL PEZ DEL VIENTO | MT. TAMARANCH: PLAY THE BALLAD / AT THE WIND FISH'S EGG |

Objetos que salen de cada mazmorra (no hacen falta como condición, solo para saber que la
mazmorra da lo que abre el siguiente tramo): D1 `feather` · D2 `stonelifter` · D3 `pegasusBoots` ·
D4 `flippers` · D5 `hookshot` · D6 `stonelifter2` · D7 `mirrorShield` · D8 `magicRod`.

### Filas opcionales (si sobra sitio; no son misión principal)

| Condición | ES | EN |
|---|---|---|
| `+flippers -ocarina_manbo` | ESTANQUE DE MANBO (NORTE DEL / TUNEL ABISAL): MAMBO DE MANBO | MANBO'S POND (NORTH OF / ANGLER'S TUNNEL): MANBO'S MAMBO |
| `+trade13 -boomerang` | CUEVA DE TORONBO: EL GORIYA / OCULTO CAMBIA EL BUMERAN | TORONBO CAVE: THE HIDDEN / GORIYA TRADES THE BOOMERANG |
| `shell>=20 -sword2` | MANSION DE LAS CARACOLAS: / ESPADA NIVEL 2 | SEASHELL MANSION: / LEVEL 2 SWORD |
| `+instrument7 +trade13` | BIBLIOTECA DE MABE: LEE EL / LIBRO CON LA LUPA (RUTA HUEVO) | MABE LIBRARY: READ THE BOOK / WITH THE LENS (EGG ROUTE) |

---

## 2. Cadena de intercambio (`trade0`..`trade13`)

`reached` en `DrawQuest` = primer `i` con `GetItem("trade"+i) != null`. El objeto `trade{reached}`
está en el bolsillo y la fila de abajo dice **a quién se lo das**. `trade3` es el único tramo que
bloquea la misión principal (puente de Kanalet); todo lo demás es opcional hasta la lupa.

| En bolsillo | Objeto | Se entrega a / dónde (ES) | Deliver to / where (EN) | Recibes |
|---|---|---|---|---|
| `trade0` | Muñeco de Yoshi (Trendy Game, Mabe, 10 rupias) | DALE EL YOSHI A MAMASHA / (CASA GRANDE DE MABE) | GIVE THE YOSHI DOLL TO MAMASHA / (BIG HOUSE, MABE VILLAGE) | `trade1` lazo |
| `trade1` | Lazo | DALE EL LAZO A CIAO CIAO / (CASETA DEL PERRO, MABE) | GIVE THE RIBBON TO CIAO CIAO / (DOGHOUSE, MABE VILLAGE) | `trade2` comida de perro |
| `trade2` | Comida para perros | DALE LA COMIDA A SALE / (CASA DE PLATANOS, TORONBO) | GIVE THE DOG FOOD TO SALE / (HOUSE O' BANANAS, TORONBO) | `trade3` plátanos |
| `trade3` | Plátanos | DALE LOS PLATANOS A KIKI / (PUENTE DEL CASTILLO KANALET) | GIVE THE BANANAS TO KIKI / (KANALET CASTLE BRIDGE) | `trade4` palo (queda en el suelo tras el puente) |
| `trade4` | Palo | DALE EL PALO A TARIN / (ARBOL DEL PANAL, PRADERA UKUKU) | GIVE THE STICK TO TARIN / (BEEHIVE TREE, UKUKU PRAIRIE) | `trade5` panal |
| `trade5` | Panal | DALE EL PANAL AL OSO COCINERO / (ALDEA ANIMAL) | GIVE THE HONEYCOMB TO THE / CHEF BEAR (ANIMAL VILLAGE) | `trade6` piña |
| `trade6` | Piña | DALE LA PINA A PAPAHL / (PERDIDO EN TAL TAL) | GIVE THE PINEAPPLE TO PAPAHL / (LOST IN TAL TAL HEIGHTS) | `trade7` hibisco |
| `trade7` | Hibisco | DALE EL HIBISCO A CHRISTINE / (LA CABRA, ALDEA ANIMAL) | GIVE THE HIBISCUS TO CHRISTINE / (THE GOAT, ANIMAL VILLAGE) | `trade8` carta |
| `trade8` | Carta | LLEVA LA CARTA AL DR. WRIGHT / (CASA AL OESTE DEL BOSQUE) | BRING THE LETTER TO MR. WRITE / (HOUSE WEST OF THE FOREST) | `trade9` escoba |
| `trade9` | Escoba | DALE LA ESCOBA A LA ABUELA / ULRIRA (BARRE EN ALDEA ANIMAL) | GIVE THE BROOM TO GRANDMA / ULRIRA (SWEEPING, ANIMAL VILLAGE) | `trade10` anzuelo |
| `trade10` | Anzuelo | DALE EL ANZUELO AL PESCADOR / (BARCA BAJO EL PUENTE, BAHIA) | GIVE THE HOOK TO THE FISHERMAN / (BOAT UNDER THE BAY BRIDGE) | `trade11` collar |
| `trade11` | Collar | DEVUELVE EL COLLAR A MARTHA / (SIRENA, BAHIA DE MARTHA) | RETURN THE NECKLACE TO MARTHA / (MERMAID, MARTHA'S BAY) | `trade12` escama |
| `trade12` | Escama | PON LA ESCAMA EN LA ESTATUA DE / LA SIRENA (SUR DE LA BAHIA) | PLACE THE SCALE ON THE MERMAID / STATUE (SOUTH OF THE BAY) | `trade13` lupa |
| `trade13` | Lupa (final) | LUPA: LIBRO DE LA BIBLIOTECA Y / GORIYA OCULTO (BUMERAN) | LENS: LIBRARY BOOK AND THE / HIDDEN GORIYA (BOOMERANG) | fin de la cadena |

Notas de esta versión HD (scripts.zScript):
- La abuela Ulrira se muda a la Aldea Animal en `instrument4` (`npc_grandmother_moved:1`), es
  decir, tras D5. Antes de eso no se puede entregar `trade9`.
- `trade4` no se "recibe" de nadie: aparece en el suelo cuando Kiki construye el puente
  (`trade4 You found a stick a monkey left behind`).
- `trade13` no se elimina nunca. Con la lupa en el bolsillo `reached == 13` para siempre.

---

## 3. Notas de ambigüedad

1. **Orden de evaluación.** La tabla es una lista de `if/else if` de arriba abajo: cada fila
   asume que las anteriores han fallado. Así "`+powder -dkey1`" (fila 4) no necesita comprobar
   `sword1`, porque la fila 1 ya habría ganado si faltara la espada.

2. **Bow-Wow (fila 6).** No hay ningún objeto que marque "Bow-Wow rescatado"; es la variable
   `bowWow` (1 = secuestrado tras `instrument0`, 3 = devuelto tras `instrument1`). Como solo
   miramos objetos, el texto cubre los dos subpasos (rescatar + llevarlo al pantano). Si en el
   futuro se quiere afinar, `GameManager.SaveManager.GetString("bowWow")` distingue 1/2/3.

3. **Hojas doradas y Richard (filas 10-12).** Al dárselas a Richard se hace
   `remove_item:goldLeaf:5`, así que el contador vuelve a 0 y por objetos no se distingue "aún no
   las tengo" de "ya se las di". Por eso la fila 12 (`+shovel`) va después de la fila 10
   (`goldLeaf<5` y ya pasado el puente) y su texto dice las dos cosas ("dale las hojas y cava").
   Si `GetItem("goldLeaf")` sigue devolviendo un objeto con `Count == 0` tras la entrega
   (pendiente de comprobar en `ItemManager.RemoveItem`), se podría usar `obj != null && Count == 0`
   como "entregadas". La condición `reached>=4` (palo o posterior en el bolsillo) sirve como
   "el puente de Kanalet ya existe".

4. **Pala (fila 11).** Se puede comprar en cualquier momento; si el jugador ya la tiene, la fila
   11 nunca se muestra y se pasa directamente a la 12. Si no la tiene, se le pide antes de mandarle
   al campo de hoyos. `shopLevel` no importa: la pala está siempre a la venta.

5. **Marin y la morsa (filas 15-16).** El item `marin` existe solo mientras te sigue
   (`remove_item:marin:1` al llegar a la morsa y en `marin_return`). Antes de hablar con ella y
   después de que la morsa se aparte el estado por objetos es el mismo (`-marin -dkey3`), así que
   el texto de la fila 16 nombra los dos destinos (Marin/morsa y desierto). Con `+marin` (fila 15)
   sí sabemos que hay que llevarla al este.
   Corrección respecto al HANDOFF: la Llave Abisal (`dkey3`) está en el **Desierto de Yarna**
   (Lanmola), no "en Ángler"; y la Llave del Rostro (`dkey4`) está en las **ruinas del sur**
   (Armos Knight), tras D5. El propio búho del juego lo confirma (`owl_3_0`, `owl_6_0`).

6. **Ocarina y canciones.** La ocarina (fila 14) se pone tras D3 porque necesita brazalete +
   botas, pero el juego no la exige hasta Mamu (fila 21). Si el jugador salta la fila 14, la
   fila 21 pedirá la canción de Mamu sin tener ocarina; Mamu no habla sin ella. Por eso la fila 14
   se coloca antes de todo lo de D4-D6: con `-ocarina` gana siempre esa fila. Lo mismo con la
   Balada (`ocarina_maria`): se puede aprender en cualquier momento tras tener ocarina
   (`maria:1_1` se activa en `instrument1`), la fila 26 es solo la red de seguridad antes del huevo.

7. **Gallo (filas 22-23).** `rooster` se añade al revivirlo y se quita en `instrument6`. Como
   `dkey5` es permanente, después de D7 ya no se llega a esas filas aunque `rooster` haya
   desaparecido. Requiere `stonelifter2` (D6) para levantar la veleta; no hace falta comprobarlo
   porque `instrument5` implica haber terminado D6.

8. **D7 y D8 se pueden hacer en cualquier orden.** El script tiene `d7_d8_check` /
   `d8_d7_check` precisamente para eso. La tabla propone el orden canónico (Torre del Águila
   primero, porque `mirrorShield` protege de la fuente de fuego en el camino a la Roca de la
   Tortuga). Si el jugador hace D8 antes: `+instrument7 -instrument6` no casa con la fila 25
   (`+instrument6 -instrument7`) y cae en la fila 26/27 según tenga la Balada… **hay que añadir la
   fila espejo** `+instrument7 -instrument6` → mismo texto que la fila 24 (o 22/23 si `-dkey5`).
   Implementación sencilla: en las filas 22-24 usar `-instrument6` en lugar de asumir orden, y
   en la 25 usar `+instrument6 -instrument7`; la 27 exige los 8.

9. **Polvos (`powder`) a 0.** `powder` es un contador; si el jugador gasta los 20 antes de usar
   al mapache, `GetItem("powder")` puede seguir devolviendo un objeto con `Count == 0`. Para la
   fila 4 basta `!= null` (el texto sigue siendo válido: "echa polvos al mapache"; si no tiene,
   la bruja/tienda le dan más). No hace falta caso especial.

10. **`reached == 14` en `DrawQuest`.** Con el código actual, si no hay ningún `tradeN` en el
    inventario (partida recién empezada) `reached` vale 14 y todas las celdas se pintan como
    "entregadas". Como `trade13` no se elimina nunca, el estado real "todo entregado" es
    `reached == 13`. Sugerencia para el bloque nuevo: tratar `reached == 14` como
    "no empezada" (texto: CONSIGUE EL MUNECO DE YOSHI / EN EL TRENDY GAME DE MABE) y
    `reached == 13` como "completa".

11. **Fantasma (`ghost`).** Tras D4 el fantasma te sigue hasta que lo llevas a su casa y luego a
    su tumba; no bloquea nada de la misión principal. No se incluye en la tabla; si se quiere,
    fila opcional `+ghost` → LLEVA AL FANTASMA A SU CASA / (SUR DE LA BAHIA) Y A SU TUMBA.

12. **Escudo.** `shield` se recibe de Tarin en la cinemática inicial antes de tener control, por
    lo que "`-shield`" nunca es un estado visible; no hay fila para él.

---

## 4. Fuentes consultadas

Orden de la misión y localizaciones de llaves/objetos, contrastados con `scripts.zScript` y los
diálogos del búho (`dialog_esp.lng` / `dialog_eng.lng`) del propio juego:

- Zelda Dungeon — Angler's Tunnel, Face Shrine, Turtle Rock, Key Cavern walkthroughs
  (https://www.zeldadungeon.net/links-awakening-walkthrough/)
- Zelda Wiki (Fandom) — Face Key, Bird Key, Turtle Rock, Slime Key, Frog's Song of Soul, Dream Shrine
  (https://zelda.fandom.com/wiki/Face_Key, .../Bird_Key, .../Turtle_Rock, .../Slime_Key, .../Frog%27s_Song_of_Soul, .../Dream_Shrine)
- Nintendo Life — Angler Key / Yarna Desert, Bird Key / Rooster, Slime Key / Golden Leaves
  (https://www.nintendolife.com/guides/zelda-links-awakening-angler-key-location-animal-village-yarna-desert-and-mountain-waterfall)
- Zelda Central — Trading Sequence (Game Boy & DX)
  (https://zeldacentral.com/games/links-awakening/trading-sequence/)
- Zelda Dungeon Wiki — Link's Awakening Trading Sequence
  (https://www.zeldadungeon.net/wiki/Link's_Awakening_Trading_Sequence)
- Zelda Universe — Chapter 7: Animal Village and Yarna Desert
  (https://zeldauniverse.net/guides/links-awakening/walkthrough/chapter-7-animal-village-and-yarna-desert/)
