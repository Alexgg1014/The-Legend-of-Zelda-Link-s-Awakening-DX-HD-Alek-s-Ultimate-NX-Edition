#if SWITCH
using System;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// "Siguiente paso" de la misión principal, deducido SOLO de los objetos que Link lleva
    /// (GameManager.GetItem). Tabla y textos: _switchbuild/STORY_GUIDE.md (ids verificados contra
    /// items.atlas, ItemManager y scripts.zScript). Se recorre de arriba abajo y gana la primera
    /// fila que cumple; cada fila asume que las anteriores han fallado.
    ///
    /// Textos en ES/EN (el resto de idiomas cae a EN), 2 líneas de ~40 caracteres, mayúsculas
    /// sin acentos para Resources.GameFont.
    /// </summary>
    internal static class SwitchStoryGuide
    {
        // ------------------------------------------------------------------ misión principal

        private static readonly string[] StepsEs =
        {
            /* 1 */ "VE A LA PLAYA (COSTA TORONBO)\nY RECUPERA TU ESPADA",
            /* 2 */ "BOSQUE MISTERIOSO: COGE LA SETA\nY LLEVASELA A LA BRUJA",
            /* 3 */ "LLEVA LA SETA A LA BRUJA\n(CHOZA AL NORTE DEL BOSQUE)",
            /* 4 */ "ECHA POLVOS AL MAPACHE DEL\nBOSQUE Y COGE LA LLAVE TAIL",
            /* 5 */ "CUEVA TAIL (SUR DE MABE):\nCONSIGUE EL INSTRUMENTO 1",
            /* 6 */ "RESCATA A BOW-WOW (CUEVA MOBLIN)\nY LLEVALO AL PANTANO GOPONGA",
            /* 7 */ "GRUTA DEL CANTARO:\nCONSIGUE EL INSTRUMENTO 2",
            /* 8 */ "HAZ LOS TRUEQUES HASTA LOS\nPLATANOS (VER INTERCAMBIO)",
            /* 9 */ "DALE LOS PLATANOS A KIKI\n(PUENTE DEL CASTILLO KANALET)",
            /* 10 */ "CASTILLO KANALET: REUNE LAS\n5 HOJAS DORADAS PARA RICHARD",
            /* 11 */ "COMPRA LA PALA EN LA TIENDA\nDE MABE (200 RUPIAS)",
            /* 12 */ "DALE LAS HOJAS A RICHARD Y CAVA\nANTE EL BUHO DEL CAMPO DE HOYOS",
            /* 13 */ "CAVERNA DE LA LLAVE (SUR DE LA\nPRADERA UKUKU): INSTRUMENTO 3",
            /* 14 */ "MABE: SANTUARIO DE LOS SUENOS\n(BRAZALETE Y BOTAS): OCARINA",
            /* 15 */ "LLEVA A MARIN HASTA LA MORSA\n(ESTE DE LA ALDEA ANIMAL)",
            /* 16 */ "ALDEA ANIMAL: MARIN Y LA MORSA,\nLUEGO DESIERTO YARNA (LLAVE)",
            /* 17 */ "TUNEL ABISAL: CERRADURA EN LA\nCASCADA DE TAL TAL (NORTE)",
            /* 18 */ "BAHIA DE MARTHA: BUCEA HASTA\nLAS FAUCES DEL SILURO",
            /* 19 */ "RUINAS DEL SUR (TEMPLO DEL\nROSTRO): LLAVE DEL ROSTRO",
            /* 20 */ "TEMPLO DEL ROSTRO (NORTE DE\nLAS RUINAS): INSTRUMENTO 6",
            /* 21 */ "LABERINTO DE CARTELES: CANCION\nDE MAMU (300 RUPIAS, GANCHO)",
            /* 22 */ "MABE: LEVANTA LA VELETA (BRAZ. 2)\nY TOCA LA CANCION DE MAMU",
            /* 23 */ "TAL TAL (ESTE): CUEVA DE LA\nLLAVE RAPAZ, VUELA CON EL GALLO",
            /* 24 */ "TORRE DEL AGUILA: CERRADURA\nEN LA CORDILLERA TAL TAL",
            /* 25 */ "ROCA DE LA TORTUGA (OESTE):\nTOCA LA CANCION DE MAMU",
            /* 26 */ "BUSCA A MARIN Y APRENDE LA\nBALADA DEL PEZ DEL VIENTO",
            /* 27 */ "MONTE TAMARANCH: TOCA LA BALADA\nANTE EL HUEVO DEL PEZ DEL VIENTO",
        };

        private static readonly string[] StepsEn =
        {
            /* 1 */ "GO TO THE BEACH (TORONBO SHORES)\nAND GET YOUR SWORD BACK",
            /* 2 */ "MYSTERIOUS FOREST: PICK THE\nTOADSTOOL FOR THE WITCH",
            /* 3 */ "BRING THE TOADSTOOL TO THE\nWITCH'S HUT (NORTH OF FOREST)",
            /* 4 */ "POWDER THE RACCOON IN THE\nFOREST, GET THE TAIL KEY",
            /* 5 */ "TAIL CAVE (SOUTH OF MABE):\nGET INSTRUMENT 1",
            /* 6 */ "RESCUE BOW-WOW (MOBLIN CAVE)\nAND TAKE HIM TO GOPONGA SWAMP",
            /* 7 */ "BOTTLE GROTTO:\nGET INSTRUMENT 2",
            /* 8 */ "DO THE TRADES UP TO THE\nBANANAS (SEE TRADE ROW)",
            /* 9 */ "GIVE THE BANANAS TO KIKI\n(KANALET CASTLE BRIDGE)",
            /* 10 */ "KANALET CASTLE: COLLECT THE\n5 GOLDEN LEAVES FOR RICHARD",
            /* 11 */ "BUY THE SHOVEL AT THE MABE\nSHOP (200 RUPEES)",
            /* 12 */ "GIVE RICHARD THE LEAVES, DIG\nAT THE OWL IN POTHOLE FIELD",
            /* 13 */ "KEY CAVERN (SOUTH UKUKU\nPRAIRIE): GET INSTRUMENT 3",
            /* 14 */ "MABE: DREAM SHRINE (BRACELET\nAND BOOTS): GET THE OCARINA",
            /* 15 */ "TAKE MARIN TO THE WALRUS\n(EAST OF ANIMAL VILLAGE)",
            /* 16 */ "ANIMAL VILLAGE: MARIN AND THE\nWALRUS, THEN YARNA DESERT (KEY)",
            /* 17 */ "ANGLER'S TUNNEL: KEYHOLE AT\nTHE TAL TAL WATERFALL (NORTH)",
            /* 18 */ "MARTHA'S BAY: DIVE DOWN TO\nCATFISH'S MAW",
            /* 19 */ "SOUTHERN FACE SHRINE RUINS:\nGET THE FACE KEY",
            /* 20 */ "FACE SHRINE (NORTH OF THE\nRUINS): GET INSTRUMENT 6",
            /* 21 */ "SIGNPOST MAZE: MAMU'S SONG\n(300 RUPEES, HOOKSHOT)",
            /* 22 */ "MABE: LIFT THE WEATHERVANE (L2)\nAND PLAY THE FROG'S SONG",
            /* 23 */ "TAL TAL (EAST): BIRD KEY CAVE,\nFLY WITH THE ROOSTER",
            /* 24 */ "EAGLE'S TOWER: KEYHOLE IN\nTHE TAL TAL MOUNTAINS",
            /* 25 */ "TURTLE ROCK (WEST MOUNTAINS):\nPLAY THE FROG'S SONG",
            /* 26 */ "FIND MARIN AND LEARN THE\nBALLAD OF THE WIND FISH",
            /* 27 */ "MT. TAMARANCH: PLAY THE BALLAD\nAT THE WIND FISH'S EGG",
        };

        // ------------------------------------------------------------------ intercambio

        private static readonly string[] TradeEs =
        {
            "DALE EL YOSHI A MAMASHA\n(CASA GRANDE DE MABE)",
            "DALE EL LAZO A CIAO CIAO\n(CASETA DEL PERRO, MABE)",
            "DALE LA COMIDA A SALE\n(CASA DE PLATANOS, TORONBO)",
            "DALE LOS PLATANOS A KIKI\n(PUENTE DEL CASTILLO KANALET)",
            "DALE EL PALO A TARIN\n(ARBOL DEL PANAL, PRADERA UKUKU)",
            "DALE EL PANAL AL OSO COCINERO\n(ALDEA ANIMAL)",
            "DALE LA PINA A PAPAHL\n(PERDIDO EN TAL TAL)",
            "DALE EL HIBISCO A CHRISTINE\n(LA CABRA, ALDEA ANIMAL)",
            "LLEVA LA CARTA AL DR. WRIGHT\n(CASA AL OESTE DEL BOSQUE)",
            "DALE LA ESCOBA A LA ABUELA\nULRIRA (BARRE EN ALDEA ANIMAL)",
            "DALE EL ANZUELO AL PESCADOR\n(BARCA BAJO EL PUENTE, BAHIA)",
            "DEVUELVE EL COLLAR A MARTHA\n(SIRENA, BAHIA DE MARTHA)",
            "PON LA ESCAMA EN LA ESTATUA DE\nLA SIRENA (SUR DE LA BAHIA)",
            "LUPA: LIBRO DE LA BIBLIOTECA Y\nGORIYA OCULTO (BUMERAN)",
        };
        private const string TradeNoneEs = "CONSIGUE EL MUNECO DE YOSHI\nEN EL TRENDY GAME DE MABE";

        private static readonly string[] TradeEn =
        {
            "GIVE THE YOSHI DOLL TO MAMASHA\n(BIG HOUSE, MABE VILLAGE)",
            "GIVE THE RIBBON TO CIAO CIAO\n(DOGHOUSE, MABE VILLAGE)",
            "GIVE THE DOG FOOD TO SALE\n(HOUSE O' BANANAS, TORONBO)",
            "GIVE THE BANANAS TO KIKI\n(KANALET CASTLE BRIDGE)",
            "GIVE THE STICK TO TARIN\n(BEEHIVE TREE, UKUKU PRAIRIE)",
            "GIVE THE HONEYCOMB TO THE\nCHEF BEAR (ANIMAL VILLAGE)",
            "GIVE THE PINEAPPLE TO PAPAHL\n(LOST IN TAL TAL HEIGHTS)",
            "GIVE THE HIBISCUS TO CHRISTINE\n(THE GOAT, ANIMAL VILLAGE)",
            "BRING THE LETTER TO MR. WRITE\n(HOUSE WEST OF THE FOREST)",
            "GIVE THE BROOM TO GRANDMA\nULRIRA (SWEEPING, ANIMAL VILLAGE)",
            "GIVE THE HOOK TO THE FISHERMAN\n(BOAT UNDER THE BAY BRIDGE)",
            "RETURN THE NECKLACE TO MARTHA\n(MERMAID, MARTHA'S BAY)",
            "PLACE THE SCALE ON THE MERMAID\nSTATUE (SOUTH OF THE BAY)",
            "LENS: LIBRARY BOOK AND THE\nHIDDEN GORIYA (BOOMERANG)",
        };
        private const string TradeNoneEn = "GET THE YOSHI DOLL AT THE\nTRENDY GAME (MABE VILLAGE)";

        private static bool Es => Game1.LanguageManager?.CurrentLanguageCode == "esp";

        public static string NextStepLabel => Es ? "SIGUIENTE PASO" : "NEXT STEP";
        public static string TradeLabel => Es ? "INTERCAMBIO" : "TRADE";

        /// <summary>Texto del intercambio. reached = primer i con trade{i} en el bolsillo; 14 = ninguno.</summary>
        public static string TradeText(int reached)
        {
            if (reached < 0 || reached >= 14)
                return Es ? TradeNoneEs : TradeNoneEn;
            return (Es ? TradeEs : TradeEn)[reached];
        }

        /// <summary>Texto del siguiente paso de la misión principal, o null si no hay partida.</summary>
        public static string NextStep(GameManager gm, int reached)
        {
            var step = StepIndex(gm, reached);
            if (step < 0)
                return null;
            var t = Es ? StepsEs : StepsEn;
            return step < t.Length ? t[step] : null;
        }

        /// <summary>
        /// Índice 0..26 de la fila de STORY_GUIDE.md §1 (fila N = índice N-1). Ver §3 para las
        /// decisiones de orden (D7/D8 en cualquier orden, hojas doradas, Marin).
        /// </summary>
        public static int StepIndex(GameManager gm, int reached)
        {
            if (gm == null)
                return -1;
            bool Has(string id) => gm.GetItem(id) != null;
            var leaf = gm.GetItem("goldLeaf");
            var leaves = leaf?.Count ?? 0;
            var tradesStarted = reached >= 0 && reached < 14;

            if (!Has("sword1")) return 0;
            if (!Has("toadstool") && !Has("powder") && !Has("dkey1")) return 1;
            if (Has("toadstool") && !Has("powder")) return 2;
            if (Has("powder") && !Has("dkey1")) return 3;
            if (Has("dkey1") && !Has("instrument0")) return 4;
            if (Has("instrument0") && !Has("stonelifter") && !Has("instrument1")) return 5;
            if (Has("stonelifter") && !Has("instrument1")) return 6;

            if (Has("instrument1") && !Has("dkey2"))
            {
                if (Has("trade3")) return 8;                                                    // plátanos en el bolsillo → Kiki
                if (!Has("trade4") && leaves < 5 && (!tradesStarted || reached < 3)) return 7;   // trueques hasta los plátanos
                // Hojas: al entregarlas el juego las quita (Count 0 o null), indistinguible de "aún no
                // tengo ninguna" (§3.3). Count 1..4 = recogiendo; 5 = listas para Richard; 0/null tras
                // el puente = recogiendo (o ya entregadas: la fila 12 lo cubre en cuanto haya pala).
                var delivered = leaf != null && leaf.Count == 0;
                if (!delivered && leaves < 5 && tradesStarted && reached >= 4) return 9;         // Kanalet: hojas
                if (!Has("shovel")) return 10;                                                  // pala
                return 11;                                                                      // Richard + cavar
            }

            if (Has("dkey2") && !Has("instrument2")) return 12;
            if (Has("instrument2") && !Has("ocarina")) return 13;
            if (Has("instrument2") && !Has("dkey3")) return Has("marin") ? 14 : 15;
            if (Has("dkey3") && !Has("instrument3")) return 16;
            if (Has("instrument3") && !Has("instrument4")) return 17;
            if (Has("instrument4") && !Has("dkey4")) return 18;
            if (Has("dkey4") && !Has("instrument5")) return 19;
            if (Has("instrument5") && !Has("ocarina_frog")) return 20;
            // D7 y D8 en cualquier orden (§3.8): la Torre del Águila se rige por dkey5/instrument6.
            if (!Has("instrument6"))
            {
                if (!Has("dkey5")) return Has("rooster") ? 22 : 21;
                return 23;
            }
            if (!Has("instrument7")) return 24;
            if (!Has("ocarina_maria")) return 25;
            return 26;
        }
    }
}
#endif
