#if SWITCH
using System;
using System.Collections.Generic;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// "Siguiente paso" de la misión principal, deducido SOLO de los objetos que Link lleva
    /// (GameManager.GetItem). Tabla y textos: _switchbuild/STORY_GUIDE.md (ids verificados contra
    /// items.atlas, ItemManager y scripts.zScript). Se recorre de arriba abajo y gana la primera
    /// fila que cumple; cada fila asume que las anteriores han fallado.
    ///
    /// Textos en eng/esp/por/fre/deu/ita (el resto de idiomas cae a EN), 2 líneas de ~40
    /// caracteres, mayúsculas sin acentos para Resources.GameFont.
    ///
    /// Los topónimos, mazmorras, llaves y personajes de cada idioma NO están inventados: salen
    /// de los propios ficheros del juego (Data/Languages/dialog_&lt;idioma&gt;.lng, claves map_*,
    /// dungeon_* y dkey*). Por eso en francés es "Plage Coco" y no "Plage Toronbo", en alemán
    /// "Wurmpalast" y no "Schwanzhöhle", etc. Al añadir una fila nueva, buscar el nombre ahí.
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

        private static readonly string[] StepsPt =
        {
            /* 1 */ "VA A PRAIA (COSTA TORONBO)\nE RECUPERE SUA ESPADA",
            /* 2 */ "BOSQUES MISTERIOSOS: PEGUE O\nCOGUMELO PARA A BRUXA",
            /* 3 */ "LEVE O COGUMELO A CABANA DA\nBRUXA (NORTE DO BOSQUE)",
            /* 4 */ "JOGUE PO MAGICO NO GUAXINIM DO\nBOSQUE E PEGUE A CHAVE DA CAUDA",
            /* 5 */ "CAVERNA DA CAUDA (SUL DE MABE):\nPEGUE O INSTRUMENTO 1",
            /* 6 */ "RESGATE BOWWOW (CAVERNA MOBLIN)\nE LEVE-O AO PANTANO GOPONGA",
            /* 7 */ "CAVERNA DA GARRAFA:\nPEGUE O INSTRUMENTO 2",
            /* 8 */ "FACA AS TROCAS ATE AS BANANAS\n(VEJA A LINHA DE TROCAS)",
            /* 9 */ "DE AS BANANAS AO KIKI\n(PONTE DO CASTELO DE KANALET)",
            /* 10 */ "CASTELO DE KANALET: JUNTE AS\n5 FOLHAS DOURADAS PARA RICHARD",
            /* 11 */ "COMPRE A PA NA LOJA DE MABE\n(200 RUBIS)",
            /* 12 */ "DE AS FOLHAS AO RICHARD E CAVE\nNA CORUJA DO JARDIM DOS BURACOS",
            /* 13 */ "CAVERNA DA CHAVE (SUL DA\nPLANICIE UKUKU): INSTRUMENTO 3",
            /* 14 */ "MABE: SANTUARIO DOS SONHOS\n(BRACELETE E BOTAS): OCARINA",
            /* 15 */ "LEVE MARIN ATE A MORSA\n(LESTE DA ALDEIA ANIMAL)",
            /* 16 */ "ALDEIA ANIMAL: MARIN E A MORSA,\nDEPOIS DESERTO DE YARNA (CHAVE)",
            /* 17 */ "TUNEL DO PESCADOR: FECHADURA NA\nCACHOEIRA TAL TAL (NORTE)",
            /* 18 */ "BAIA DE MARTHA: MERGULHE ATE A\nMANDIBULA DO PEIXE GATO",
            /* 19 */ "RUINAS DO SANTUARIO AO SUL:\nPEGUE A CHAVE DA SUPERFICIE",
            /* 20 */ "SANTUARIO DA SUPERFICIE (NORTE\nDAS RUINAS): INSTRUMENTO 6",
            /* 21 */ "LABIRINTO DA ORIENTACAO: CANCAO\nDE MAMU (300 RUBIS, GANCHO)",
            /* 22 */ "MABE: LEVANTE O CATAVENTO (BR. 2)\nE TOQUE A CANCAO DE MAMU",
            /* 23 */ "TAL TAL (LESTE): CAVERNA DA\nCHAVE DO PASSARO, VOE COM O GALO",
            /* 24 */ "TORRE DA AGUIA: FECHADURA NA\nCORDILHEIRA TAL TAL",
            /* 25 */ "PEDRA DA TARTARUGA (OESTE):\nTOQUE A CANCAO DE MAMU",
            /* 26 */ "ACHE MARIN E APRENDA A BALADA\nDO PEIXE DOS VENTOS",
            /* 27 */ "MONTE TAMARANCH: TOQUE A BALADA\nNO OVO DO PEIXE DOS VENTOS",
        };

        private static readonly string[] StepsFr =
        {
            /* 1 */ "VA A LA PLAGE COCO ET\nRECUPERE TON EPEE",
            /* 2 */ "FORET ENCHANTEE : PRENDS LE\nCHAMPIGNON POUR LA SORCIERE",
            /* 3 */ "APPORTE LE CHAMPIGNON A LA HUTTE\nDE LA SORCIERE (NORD DE LA FORET)",
            /* 4 */ "POUDRE LE RATON LAVEUR DE LA\nFORET ET PRENDS LA CLE FLAGELLO",
            /* 5 */ "CAVE FLAGELLO (SUD DU VILLAGE) :\nPRENDS L'INSTRUMENT 1",
            /* 6 */ "LIBERE TOUTOU (GROTTE MOBLIN) ET\nMENE-LE AU MARAIS DES ANEMONES",
            /* 7 */ "GROTTE DU GENIE :\nPRENDS L'INSTRUMENT 2",
            /* 8 */ "FAIS LES ECHANGES JUSQU'AUX\nBANANES (VOIR LIGNE ECHANGES)",
            /* 9 */ "DONNE LES BANANES A KIKI\n(PONT DU CHATEAU CANULET)",
            /* 10 */ "CHATEAU CANULET : RASSEMBLE LES\n5 FEUILLES D'OR POUR RICHARD",
            /* 11 */ "ACHETE LA PELLE AU BAZAR DU\nCENTRE (200 RUBIS)",
            /* 12 */ "DONNE LES FEUILLES A RICHARD ET\nCREUSE AU HIBOU, CHAMP DES PIEGES",
            /* 13 */ "CAVE AUX CLES (SUD DE LA\nPRAIRIE) : INSTRUMENT 3",
            /* 14 */ "GITE DES REVES (MOUETTES) :\nBRACELET, BOTTES, PUIS OCARINA",
            /* 15 */ "MENE MARINE JUSQU'AU MORSE\n(EST DU VILLAGE DES ANIMAUX)",
            /* 16 */ "VILLAGE DES ANIMAUX : MARINE ET\nLE MORSE, PUIS DUNES DE YARNA",
            /* 17 */ "ABIME DU POISSON : SERRURE A LA\nCASCADE TARTARE (NORD)",
            /* 18 */ "BAIE DE MARTHA : PLONGE JUSQU'AU\nPOISSON-CHAT",
            /* 19 */ "RUINES DU SUD (TEMPLE DU\nMASQUE) : PRENDS LA CLE MASQUE",
            /* 20 */ "TEMPLE DU MASQUE (NORD DES\nRUINES) : INSTRUMENT 6",
            /* 21 */ "DEDALE DES PANNEAUX : CHANSON DE\nMAMU (300 RUBIS, GRAPPIN)",
            /* 22 */ "MOUETTES : SOULEVE LA GIROUETTE\n(BRACELET 2) ET JOUE MAMU",
            /* 23 */ "TARTARE (EST) : CAVE DE LA CLE\nVAUTOUR, VOLE AVEC LE COQ",
            /* 24 */ "TOUR DU VAUTOUR : SERRURE DANS\nLA CORDILLERE TARTARE",
            /* 25 */ "ROC DE LA TORTUE (OUEST) :\nJOUE LA CHANSON DE MAMU",
            /* 26 */ "RETROUVE MARINE ET APPRENDS LA\nBALLADE DU POISSON-REVE",
            /* 27 */ "MT TAMARANCH : JOUE LA BALLADE\nDEVANT L'OEUF SACRE",
        };

        private static readonly string[] StepsDe =
        {
            /* 1 */ "GEH ZUM TORONGASTRAND UND\nHOL DEIN SCHWERT ZURUECK",
            /* 2 */ "ZAUBERWALD: NIMM DEN PILZ\nFUER DIE HEXE",
            /* 3 */ "BRING DEN PILZ ZUM HEXENHAEUS-\nCHEN (NOERDLICH DES WALDES)",
            /* 4 */ "BESTREUE DEN WASCHBAEREN IM WALD,\nHOL DEN WURMSCHLUESSEL",
            /* 5 */ "WURMPALAST (SUEDLICH VON\nMOEVENDORF): INSTRUMENT 1",
            /* 6 */ "BEFREIE STRUPPI (MOBLIN-HOEHLE)\nUND BRING IHN ZUM SCHLEIMSUMPF",
            /* 7 */ "DJINN-GROTTE:\nHOL INSTRUMENT 2",
            /* 8 */ "TAUSCHE BIS ZU DEN BANANEN\n(SIEHE TAUSCHKETTE)",
            /* 9 */ "GIB KIKI DIE BANANEN\n(BRUECKE VON SCHLOSS KANALET)",
            /* 10 */ "SCHLOSS KANALET: SAMMLE DIE\n5 GOLDBLAETTER FUER RICHARD",
            /* 11 */ "KAUF DIE SCHAUFEL IM TANTE EMMA\nLADEN (200 RUBINE)",
            /* 12 */ "GIB RICHARD DIE BLAETTER, GRAB\nBEI DER EULE IM SCHICKSALSACKER",
            /* 13 */ "TEUFELSVILLA (SUEDLICH DER\nURUNGA-STEPPE): INSTRUMENT 3",
            /* 14 */ "MOEVENDORF: TRAUMSCHREIN\n(ARMBAND, STIEFEL): OKARINA",
            /* 15 */ "BRING MARIN ZUM WALROSS\n(OESTLICH VOM ZOODORF)",
            /* 16 */ "ZOODORF: MARIN UND DAS WALROSS,\nDANN DURSTWUESTE (SCHLUESSEL)",
            /* 17 */ "WUNDERTUNNEL: SCHLOSS AM\nTALTAL-WASSERFALL (NORDEN)",
            /* 18 */ "ZWERGENBUCHT: TAUCHE HINAB\nZUM FISCHMAUL",
            /* 19 */ "RUINEN IM SUEDEN (MASKENTEMPEL):\nHOL DEN MASKENSCHLUESSEL",
            /* 20 */ "MASKENTEMPEL (NOERDLICH DER\nRUINEN): INSTRUMENT 6",
            /* 21 */ "SCHILDERWALD: MAMUS LIED\n(300 RUBINE, ENTERHAKEN)",
            /* 22 */ "MOEVENDORF: HEB DIE WETTERFAHNE\n(ARMBAND 2), SPIEL MAMUS LIED",
            /* 23 */ "TALTAL (OSTEN): HOEHLE MIT DEM\nADLERSCHLUESSEL, FLIEG MIT DEM HAHN",
            /* 24 */ "ADLERFESTUNG: SCHLOSS IM\nTALTAL-GEBIRGE",
            /* 25 */ "REPTILFELSEN (WESTEN):\nSPIEL MAMUS LIED",
            /* 26 */ "FINDE MARIN UND LERNE DIE\nBALLADE VOM WINDFISCH",
            /* 27 */ "TAMARANCHBERG: SPIEL DIE BALLADE\nVOR DEM WINDFISCH-EI",
        };

        private static readonly string[] StepsIt =
        {
            /* 1 */ "VAI ALLE RIVE TORONBO E\nRIPRENDI LA TUA SPADA",
            /* 2 */ "BOSCO MISTERIOSO: PRENDI IL\nFUNGO PER LA STREGA",
            /* 3 */ "PORTA IL FUNGO ALLA CAPANNA\nDELLA STREGA (NORD DEL BOSCO)",
            /* 4 */ "GETTA LA POLVERE SUL PROCIONE\nDEL BOSCO, PRENDI LA CHIAVE TAIL",
            /* 5 */ "CAVA TAIL (SUD DI MABE):\nPRENDI LO STRUMENTO 1",
            /* 6 */ "LIBERA BOWWOW (CAVA MOBLIN) E\nPORTALO ALLA PALUDE GOPONGA",
            /* 7 */ "GROTTA BOTTLE:\nPRENDI LO STRUMENTO 2",
            /* 8 */ "FAI GLI SCAMBI FINO ALLE BANANE\n(VEDI LA RIGA SCAMBI)",
            /* 9 */ "DAI LE BANANE A KIKI\n(PONTE DEL CASTELLO KANALET)",
            /* 10 */ "CASTELLO KANALET: RACCOGLI LE\n5 FOGLIE D'ORO PER RICHARD",
            /* 11 */ "COMPRA LA PALA AL NEGOZIO DI\nMABE (200 RUPIE)",
            /* 12 */ "DAI LE FOGLIE A RICHARD E SCAVA\nAL GUFO NEL CAMPO BUCHI",
            /* 13 */ "CAVERNA KEY (SUD DELLE PRATERIE\nUKUKU): PRENDI LO STRUMENTO 3",
            /* 14 */ "MABE: TEMPIO DREAM (BRACCIALE\nE STIVALI): PRENDI L'OCARINA",
            /* 15 */ "PORTA MARIN DAL TRICHECO\n(EST DEL VILLAGGIO ANIMAL)",
            /* 16 */ "VILLAGGIO ANIMAL: MARIN E IL\nTRICHECO, POI DESERTO YARNA",
            /* 17 */ "TUNNEL ANGLER: SERRATURA ALLA\nCASCATA TAL TAL (NORD)",
            /* 18 */ "BAIA DI MARTHA: TUFFATI FINO AL\nPESCEGATTO MAW",
            /* 19 */ "ROVINE A SUD (TEMPIO FACE):\nPRENDI LA CHIAVE FACE",
            /* 20 */ "TEMPIO FACE (NORD DELLE ROVINE):\nPRENDI LO STRUMENTO 6",
            /* 21 */ "PUNTO CARTELLI: CANZONE DI MAMU\n(300 RUPIE, RAMPINO)",
            /* 22 */ "MABE: SOLLEVA LA BANDERUOLA\n(BRACC. 2) E SUONA MAMU",
            /* 23 */ "TAL TAL (EST): CAVA DELLA CHIAVE\nBIRD, VOLA CON IL GALLO",
            /* 24 */ "TORRE EAGLE: SERRATURA NELLA\nCATENA MONTUOSA TAL TAL",
            /* 25 */ "ROCCIA TURTLE (OVEST):\nSUONA LA CANZONE DI MAMU",
            /* 26 */ "TROVA MARIN E IMPARA LA\nBALLATA DEL PESCE WIND",
            /* 27 */ "MT. TAMARANCH: SUONA LA BALLATA\nDAVANTI ALL'UOVO PESCE WIND",
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

        private static readonly string[] TradePt =
        {
            "DE O BONECO DO YOSHI A MAMASHA\n(CASA GRANDE DE MABE)",
            "DE A FITA PARA CIAOCIAO\n(CASA DO CAO, MABE)",
            "DE A RACAO PARA SALE\n(VENDINHA DO BANANAS, TORONBO)",
            "DE AS BANANAS AO KIKI\n(PONTE DO CASTELO DE KANALET)",
            "DE O GRAVETO AO TARIN\n(COLMEIA, PLANICIE UKUKU)",
            "DE O FAVO DE MEL AO URSO CHEFE\n(ALDEIA ANIMAL)",
            "DE O ABACAXI AO PAPAHL\n(PERDIDO NO PICO TAL TAL)",
            "DE O HIBISCO A CHRISTINE\n(A CABRA, ALDEIA ANIMAL)",
            "LEVE A CARTA AO SR. WRITE\n(CASA A OESTE DO BOSQUE)",
            "DE A VASSOURA A VOVO ULRIRA\n(VARRENDO NA ALDEIA ANIMAL)",
            "DE O ANZOL AO PESCADOR\n(BARCO SOB A PONTE DA BAIA)",
            "DEVOLVA O COLAR A MARTHA\n(SEREIA, BAIA DE MARTHA)",
            "PONHA A ESCAMA NA ESTATUA DA\nSEREIA (SUL DA BAIA)",
            "LUPA: LIVRO DA BIBLIOTECA E O\nGORIYA ESCONDIDO (BUMERANGUE)",
        };
        private const string TradeNonePt = "PEGUE O BONECO DO YOSHI NO\nJOGO DA GARRA (ALDEIA MABE)";

        private static readonly string[] TradeFr =
        {
            "DONNE LA POUPEE YOSHI A MAMASHA\n(GRANDE MAISON, MOUETTES)",
            "DONNE LE RUBAN A MME MIAOU\nMIAOU (NICHE DU CHIEN)",
            "DONNE LA PATEE A ALLIGOBANANE\n(PLAGE COCO)",
            "DONNE LES BANANES A KIKI\n(PONT DU CHATEAU CANULET)",
            "DONNE LE BATON A TARKIN\n(ARBRE A MIEL, LA PRAIRIE)",
            "DONNE LE MIEL A L'OURS CUISINIER\n(VILLAGE DES ANIMAUX)",
            "DONNE L'ANANAS A PAPAHL\n(PERDU SUR LE PLATEAU TARTARE)",
            "DONNE L'HIBISCUS A CHRISTINE\n(LA CHEVRE, VILLAGE DES ANIMAUX)",
            "PORTE LA LETTRE A MR WRIGHT\n(MAISON A L'OUEST DE LA FORET)",
            "DONNE LE BALAI A MAMIE ULRIRA\n(ELLE BALAIE, VILLAGE ANIMAUX)",
            "DONNE L'HAMECON AU PECHEUR\n(BARQUE SOUS LE PONT DE LA BAIE)",
            "RENDS LE COLLIER A MARTHA\n(SIRENE, BAIE DE MARTHA)",
            "POSE L'ECAILLE SUR LA STATUE DE\nLA SIRENE (SUD DE LA BAIE)",
            "LOUPE : LIVRE DE LA BIBLIOTHEQUE\nET GORIYA CACHE (BOOMERANG)",
        };
        private const string TradeNoneFr = "PRENDS LA POUPEE YOSHI AU JEU DE\nHASARD (VILLAGE DES MOUETTES)";

        private static readonly string[] TradeDe =
        {
            "GIB MAMASHA DIE YOSHI-PUPPE\n(GROSSES HAUS, MOEVENDORF)",
            "GIB MADAME MIOUMIOU DIE\nSCHLEIFE (HUNDEHUETTE, MOEVENDORF)",
            "GIB SALE DAS HUNDEFUTTER\n(BANANENHAENDLER, TORONGASTRAND)",
            "GIB KIKI DIE BANANEN\n(BRUECKE VON SCHLOSS KANALET)",
            "GIB TARIN DEN STOCK\n(HONIGBAUM, URUNGA-STEPPE)",
            "GIB DIE HONIGWABE DEM KOCH-BAEREN\n(ZOODORF)",
            "GIB PAPAHL DIE ANANAS\n(VERIRRT IN DEN TALTAL-HOEHEN)",
            "GIB CHRISTINE DEN HIBISKUS\n(DIE ZIEGE, ZOODORF)",
            "BRING DEN BRIEF ZU DR. WRIGHT\n(HAUS WESTLICH DES WALDES)",
            "GIB OMA ULRIRA DEN BESEN\n(SIE KEHRT IM ZOODORF)",
            "GIB DEM ANGLER DEN ANGELHAKEN\n(BOOT UNTER DER BRUECKE, BUCHT)",
            "GIB DER NIXE DIE KETTE ZURUECK\n(ZWERGENBUCHT)",
            "LEG DIE SCHUPPE AUF DIE NIXEN-\nSTATUE (SUEDLICH DER BUCHT)",
            "LUPE: BUCH DER BUECHEREI UND\nVERSTECKTER GORIYA (BUMERANG)",
        };
        private const string TradeNoneDe = "HOL DIE YOSHI-PUPPE IN DER\nSPIELHALLE (MOEVENDORF)";

        private static readonly string[] TradeIt =
        {
            "DAI LA BAMBOLA YOSHI A MAMASHA\n(CASA GRANDE, VILLAGGIO MABE)",
            "DAI IL NASTRO A CIAOCIAO\n(CUCCIA DEL CANE, MABE)",
            "DAI IL CIBO PER CANI A SALE\n(CASA DELLE BANANE, TORONBO)",
            "DAI LE BANANE A KIKI\n(PONTE DEL CASTELLO KANALET)",
            "DAI IL BASTONE A TARIN\n(ALBERO DEL MIELE, PRATERIE UKUKU)",
            "DAI IL FAVO ALL'ORSO CUOCO\n(VILLAGGIO ANIMAL)",
            "DAI L'ANANAS A PAPAHL\n(PERSO SULLE ALTURE TAL TAL)",
            "DAI L'IBISCO A CHRISTINE\n(LA CAPRA, VILLAGGIO ANIMAL)",
            "PORTA LA LETTERA AL SIG. WRITE\n(CASA A OVEST DEL BOSCO)",
            "DAI LA SCOPA A NONNA ULRIRA\n(SPAZZA NEL VILLAGGIO ANIMAL)",
            "DAI L'AMO AL PESCATORE\n(BARCA SOTTO IL PONTE DELLA BAIA)",
            "RESTITUISCI LA COLLANA A MARTHA\n(SIRENA, BAIA DI MARTHA)",
            "METTI LA SCAGLIA SULLA STATUA\nDELLA SIRENA (SUD DELLA BAIA)",
            "LENTE: LIBRO DELLA BIBLIOTECA E\nGORIYA NASCOSTO (BOOMERANG)",
        };
        private const string TradeNoneIt = "PRENDI LA BAMBOLA YOSHI AL\nTRENDY GAME (VILLAGGIO MABE)";

        // ------------------------------------------------------------------ selección de idioma
        // Declarado DESPUÉS de las tablas a propósito: los inicializadores de campos estáticos
        // corren en orden textual, y si esto fuera arriba las referencias llegarían a null.

        private static readonly Dictionary<string, string[]> StepsByLang = new Dictionary<string, string[]>
        {
            ["eng"] = StepsEn, ["esp"] = StepsEs, ["por"] = StepsPt,
            ["fre"] = StepsFr, ["deu"] = StepsDe, ["ita"] = StepsIt,
        };

        private static readonly Dictionary<string, string[]> TradeByLang = new Dictionary<string, string[]>
        {
            ["eng"] = TradeEn, ["esp"] = TradeEs, ["por"] = TradePt,
            ["fre"] = TradeFr, ["deu"] = TradeDe, ["ita"] = TradeIt,
        };

        private static readonly Dictionary<string, string> TradeNoneByLang = new Dictionary<string, string>
        {
            ["eng"] = TradeNoneEn, ["esp"] = TradeNoneEs, ["por"] = TradeNonePt,
            ["fre"] = TradeNoneFr, ["deu"] = TradeNoneDe, ["ita"] = TradeNoneIt,
        };

        private static readonly Dictionary<string, string> StepLabelByLang = new Dictionary<string, string>
        {
            ["eng"] = "NEXT STEP", ["esp"] = "SIGUIENTE PASO", ["por"] = "PROXIMO PASSO",
            ["fre"] = "PROCHAINE ETAPE", ["deu"] = "NAECHSTER SCHRITT", ["ita"] = "PROSSIMO PASSO",
        };

        private static readonly Dictionary<string, string> TradeLabelByLang = new Dictionary<string, string>
        {
            ["eng"] = "TRADE", ["esp"] = "INTERCAMBIO", ["por"] = "TROCAS",
            ["fre"] = "ECHANGES", ["deu"] = "TAUSCHKETTE", ["ita"] = "SCAMBI",
        };

        private static string Lang => Game1.LanguageManager?.CurrentLanguageCode ?? "eng";

        private static T Pick<T>(Dictionary<string, T> table) =>
            table.TryGetValue(Lang, out var v) ? v : table["eng"];

        public static string NextStepLabel => Pick(StepLabelByLang);
        public static string TradeLabel => Pick(TradeLabelByLang);

        /// <summary>Texto del intercambio. reached = primer i con trade{i} en el bolsillo; 14 = ninguno.</summary>
        public static string TradeText(int reached)
        {
            if (reached < 0 || reached >= 14)
                return Pick(TradeNoneByLang);
            var t = Pick(TradeByLang);
            return reached < t.Length ? t[reached] : Pick(TradeNoneByLang);
        }

        /// <summary>Texto del siguiente paso de la misión principal, o null si no hay partida.</summary>
        public static string NextStep(GameManager gm, int reached)
        {
            var step = StepIndex(gm, reached);
            if (step < 0)
                return null;
            var t = Pick(StepsByLang);
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
