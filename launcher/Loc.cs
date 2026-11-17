namespace Launcher;

// The launcher speaks the language chosen for the game. The French text written in the code is the key;
// each row below is { French, English, German, Spanish, Italian }. A text without a row is shown as written.
public static class Loc
{
    static readonly string[] Keys = { "french", "english", "german", "spanish", "italian" };

    // "english" while a bug report is built, so that it reads the same whatever the player's language.
    public static string? Force;

    public static string T(string fr, string language)
    {
        language = Force ?? language;
        int col = Array.IndexOf(Keys, language);
        if (col <= 0 || !Table.TryGetValue(fr, out var row)) return fr;
        return row[col - 1];
    }

    // Language of the player's Windows, used the first time the launcher runs.
    public static string FromSystem()
    {
        return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "fr" => "french", "de" => "german", "es" => "spanish", "it" => "italian", _ => "english",
        };
    }

    static readonly Dictionary<string, string[]> Table = Build(new[]
    {
        // prompts and screen titles
        new[] { "SÉLECTIONNER", "SELECT", "AUSWÄHLEN", "SELECCIONAR", "SELEZIONA" },
        new[] { "RETOUR", "BACK", "ZURÜCK", "VOLVER", "INDIETRO" },
        new[] { "MENU PRINCIPAL", "MAIN MENU", "HAUPTMENÜ", "MENÚ PRINCIPAL", "MENU PRINCIPALE" },
        new[] { "PARAMÈTRES", "SETTINGS", "EINSTELLUNGEN", "AJUSTES", "IMPOSTAZIONI" },
        new[] { "AFFICHAGE", "DISPLAY", "ANZEIGE", "PANTALLA", "SCHERMO" },
        new[] { "GRAPHISMES", "GRAPHICS", "GRAFIK", "GRÁFICOS", "GRAFICA" },
        new[] { "JEU", "GAME", "SPIEL", "JUEGO", "GIOCO" },
        new[] { "FICHIERS DU JEU", "GAME FILES", "SPIELDATEIEN", "ARCHIVOS DEL JUEGO", "FILE DI GIOCO" },
        new[] { "CONTENU TÉLÉCHARGEABLE", "DOWNLOADABLE CONTENT", "DOWNLOAD-INHALTE", "CONTENIDO DESCARGABLE", "CONTENUTI SCARICABILI" },
        new[] { "DIAGNOSTIC", "DIAGNOSTICS", "DIAGNOSE", "DIAGNÓSTICO", "DIAGNOSTICA" },
        new[] { "Revenir à l'écran précédent.", "Go back to the previous screen.", "Zurück zum vorherigen Bildschirm.", "Volver a la pantalla anterior.", "Torna alla schermata precedente." },

        // main menu
        new[] { "JOUER", "PLAY", "SPIELEN", "JUGAR", "GIOCA" },
        new[] { "ARRÊTER LE JEU", "STOP THE GAME", "SPIEL BEENDEN", "DETENER EL JUEGO", "CHIUDI IL GIOCO" },
        new[] { "QUITTER", "QUIT", "BEENDEN", "SALIR", "ESCI" },
        new[] { "Lance WWE 2K17 avec tes paramètres.", "Launch WWE 2K17 with your settings.", "Startet WWE 2K17 mit deinen Einstellungen.", "Inicia WWE 2K17 con tus ajustes.", "Avvia WWE 2K17 con le tue impostazioni." },
        new[] { "Affichage, graphismes, langue, son et manette.", "Display, graphics, language, sound and controller.", "Anzeige, Grafik, Sprache, Ton und Controller.", "Pantalla, gráficos, idioma, sonido y mando.", "Schermo, grafica, lingua, audio e controller." },
        new[] { "Installe le jeu depuis ton ISO, ajoute le Title Update 1.04, ouvre les dossiers.", "Install the game from your ISO, add Title Update 1.04, open the folders.", "Installiere das Spiel von deiner ISO, füge das Title Update 1.04 hinzu, öffne die Ordner.", "Instala el juego desde tu ISO, añade el Title Update 1.04 y abre las carpetas.", "Installa il gioco dalla tua ISO, aggiungi il Title Update 1.04, apri le cartelle." },
        new[] { "Ajoute ou retire les contenus téléchargeables (DLC).", "Add or remove downloadable content (DLC).", "DLC-Inhalte hinzufügen oder entfernen.", "Añade o quita contenido descargable (DLC).", "Aggiungi o rimuovi i contenuti scaricabili (DLC)." },
        new[] { "Vérifie que tout est en place et copie un rapport à joindre en cas de problème.", "Checks that everything is in place and copies a report to attach if something goes wrong.", "Prüft, ob alles bereit ist, und kopiert einen Bericht, den du bei Problemen mitschicken kannst.", "Comprueba que todo está en su sitio y copia un informe para adjuntar si hay problemas.", "Verifica che sia tutto a posto e copia un rapporto da allegare in caso di problemi." },
        new[] { "Ferme le launcher.", "Closes the launcher.", "Schließt den Launcher.", "Cierra el launcher.", "Chiude il launcher." },

        // settings
        new[] { "Fenêtré ou plein écran, résolution de la fenêtre, V-Sync, carte graphique.", "Windowed or fullscreen, window size, V-Sync, graphics card.", "Fenster oder Vollbild, Fenstergröße, V-Sync, Grafikkarte.", "Ventana o pantalla completa, tamaño de la ventana, V-Sync, tarjeta gráfica.", "Finestra o schermo intero, dimensione della finestra, V-Sync, scheda video." },
        new[] { "Moteur graphique, finesse du rendu, anticrénelage, filtrage, ombres.", "Graphics engine, render quality, anti-aliasing, filtering, shadows.", "Grafik-Engine, Renderqualität, Kantenglättung, Filterung, Schatten.", "Motor gráfico, calidad de render, suavizado de bordes, filtrado, sombras.", "Motore grafico, qualità del rendering, anti-aliasing, filtri, ombre." },
        new[] { "Langue, 60 images/s, vibration, son, options avancées.", "Language, 60 frames/s, vibration, sound, advanced options.", "Sprache, 60 Bilder/s, Vibration, Ton, erweiterte Optionen.", "Idioma, 60 imágenes/s, vibración, sonido, opciones avanzadas.", "Lingua, 60 fotogrammi/s, vibrazione, audio, opzioni avanzate." },

        // display
        new[] { "Fenêtré ou plein écran.", "Windowed or fullscreen.", "Fenster oder Vollbild.", "Ventana o pantalla completa.", "Finestra o schermo intero." },
        new[] { "PLEIN ÉCRAN", "FULLSCREEN", "VOLLBILD", "PANTALLA COMPLETA", "SCHERMO INTERO" },
        new[] { "FENÊTRÉ", "WINDOWED", "FENSTER", "VENTANA", "FINESTRA" },
        new[] { "RÉSOLUTION", "RESOLUTION", "AUFLÖSUNG", "RESOLUCIÓN", "RISOLUZIONE" },
        new[] { "Taille de la fenêtre du jeu (720p, 900p, 1080p, 1440p, 4K).", "Size of the game window (720p, 900p, 1080p, 1440p, 4K).", "Größe des Spielfensters (720p, 900p, 1080p, 1440p, 4K).", "Tamaño de la ventana del juego (720p, 900p, 1080p, 1440p, 4K).", "Dimensione della finestra di gioco (720p, 900p, 1080p, 1440p, 4K)." },
        new[] { "Synchronise l'image avec l'écran pour éviter les déchirures. Peut ajouter un peu de latence.", "Syncs the image with the screen to avoid tearing. May add a little latency.", "Synchronisiert das Bild mit dem Bildschirm, um Tearing zu vermeiden. Kann etwas Verzögerung hinzufügen.", "Sincroniza la imagen con la pantalla para evitar cortes. Puede añadir algo de latencia.", "Sincronizza l'immagine con lo schermo per evitare lo strappo. Può aggiungere un po' di latenza." },
        new[] { "OUI", "YES", "JA", "SÍ", "SÌ" },
        new[] { "NON", "NO", "NEIN", "NO", "NO" },
        new[] { "CARTE GRAPHIQUE", "GRAPHICS CARD", "GRAFIKKARTE", "TARJETA GRÁFICA", "SCHEDA VIDEO" },
        new[] { "AUTO choisit la plus puissante. Change-la si ton PC a deux cartes (portable) et que le jeu prend la mauvaise.", "AUTO picks the most powerful one. Change it if your PC has two cards (laptop) and the game picks the wrong one.", "AUTO wählt die leistungsstärkste. Ändere sie, wenn dein PC zwei Karten hat (Laptop) und das Spiel die falsche nimmt.", "AUTO elige la más potente. Cámbiala si tu PC tiene dos tarjetas (portátil) y el juego elige la equivocada.", "AUTO sceglie la più potente. Cambiala se il tuo PC ha due schede (portatile) e il gioco usa quella sbagliata." },

        // graphics
        new[] { "MOTEUR GRAPHIQUE", "GRAPHICS ENGINE", "GRAFIK-ENGINE", "MOTOR GRÁFICO", "MOTORE GRAFICO" },
        new[] { "Aucun moteur compilé : lance tools\\build.ps1.", "No engine built: run tools\\build.ps1.", "Keine Engine kompiliert: starte tools\\build.ps1.", "Ningún motor compilado: ejecuta tools\\build.ps1.", "Nessun motore compilato: esegui tools\\build.ps1." },
        new[] { "AUCUN", "NONE", "KEINE", "NINGUNO", "NESSUNO" },
        new[] { "NATIF · VULKAN", "NATIVE · VULKAN", "NATIV · VULKAN", "NATIVO · VULKAN", "NATIVO · VULKAN" },
        new[] { "NATIF · DIRECT3D 12", "NATIVE · DIRECT3D 12", "NATIV · DIRECT3D 12", "NATIVO · DIRECT3D 12", "NATIVO · DIRECT3D 12" },
        new[] { "NATIF · DIRECT3D 11", "NATIVE · DIRECT3D 11", "NATIV · DIRECT3D 11", "NATIVO · DIRECT3D 11", "NATIVO · DIRECT3D 11" },
        new[] { "ÉMULATION XENOS", "XENOS EMULATION", "XENOS-EMULATION", "EMULACIÓN XENOS", "EMULAZIONE XENOS" },
        new[] { "Rendu natif Vulkan : le plus rapide (recommandé). Les vidéos plein écran sont un peu saccadées.", "Native Vulkan rendering: the fastest (recommended). Full-screen videos are slightly choppy.", "Natives Vulkan-Rendering: am schnellsten (empfohlen). Vollbild-Videos ruckeln leicht.", "Renderizado nativo Vulkan: el más rápido (recomendado). Los vídeos a pantalla completa van algo a tirones.", "Rendering nativo Vulkan: il più veloce (consigliato). I video a schermo intero sono leggermente a scatti." },
        new[] { "Rendu natif Direct3D 12 : essaie-le si Vulkan pose problème avec ta carte.", "Native Direct3D 12 rendering: try it if Vulkan gives you trouble with your card.", "Natives Direct3D-12-Rendering: probiere es, wenn Vulkan mit deiner Karte Probleme macht.", "Renderizado nativo Direct3D 12: pruébalo si Vulkan da problemas con tu tarjeta.", "Rendering nativo Direct3D 12: provalo se Vulkan dà problemi con la tua scheda." },
        new[] { "Rendu natif Direct3D 11 : pour les vieilles cartes graphiques.", "Native Direct3D 11 rendering: for older graphics cards.", "Natives Direct3D-11-Rendering: für ältere Grafikkarten.", "Renderizado nativo Direct3D 11: para tarjetas gráficas antiguas.", "Rendering nativo Direct3D 11: per le schede video più vecchie." },
        new[] { "Émulation de la puce de la Xbox 360 : le plus fidèle, joue les vidéos correctement, mais plus lent.", "Emulation of the Xbox 360 chip: the most faithful, plays videos correctly, but slower.", "Emulation des Xbox-360-Chips: am originalgetreuesten, spielt Videos korrekt ab, aber langsamer.", "Emulación del chip de la Xbox 360: el más fiel, reproduce bien los vídeos, pero más lento.", "Emulazione del chip della Xbox 360: il più fedele, riproduce correttamente i video, ma più lento." },
        new[] { "RENDU INTERNE", "INTERNAL RENDERING", "INTERNES RENDERING", "RENDER INTERNO", "RENDERING INTERNO" },
        new[] { "Finesse du rendu. AUTO adapte à ton écran et à ta carte. Plus haut = plus net mais plus lourd (540p/720p = cartes faibles).", "Render quality. AUTO adapts to your screen and card. Higher = sharper but heavier (540p/720p = weak cards).", "Renderqualität. AUTO passt sich an Bildschirm und Karte an. Höher = schärfer, aber aufwendiger (540p/720p = schwache Karten).", "Calidad de render. AUTO se adapta a tu pantalla y tarjeta. Más alto = más nítido pero más pesado (540p/720p = tarjetas débiles).", "Qualità del rendering. AUTO si adatta a schermo e scheda. Più alto = più nitido ma più pesante (540p/720p = schede deboli)." },
        new[] { "ANTICRÉNELAGE", "ANTI-ALIASING", "KANTENGLÄTTUNG", "ANTIALIASING", "ANTIALIASING" },
        new[] { "Adoucit les bords crénelés (FXAA).", "Smooths jagged edges (FXAA).", "Glättet gezackte Kanten (FXAA).", "Suaviza los bordes dentados (FXAA).", "Ammorbidisce i bordi seghettati (FXAA)." },
        new[] { "FORT", "STRONG", "STARK", "FUERTE", "FORTE" },
        new[] { "MISE À L'ÉCHELLE", "UPSCALING", "HOCHSKALIERUNG", "ESCALADO", "UPSCALING" },
        new[] { "Quand le rendu interne est plus petit que la fenêtre, l'agrandit avec un filtre plus net que le flou habituel.", "When the internal rendering is smaller than the window, enlarges it with a sharper filter than the usual blur.", "Ist das interne Rendering kleiner als das Fenster, wird es mit einem schärferen Filter als dem üblichen Weichzeichner vergrößert.", "Cuando el render interno es más pequeño que la ventana, lo amplía con un filtro más nítido que el desenfoque habitual.", "Quando il rendering interno è più piccolo della finestra, lo ingrandisce con un filtro più nitido del solito effetto sfocato." },
        new[] { "RÉSOLUTION INTERNE", "INTERNAL RESOLUTION", "INTERNE AUFLÖSUNG", "RESOLUCIÓN INTERNA", "RISOLUZIONE INTERNA" },
        new[] { "Multiplie la finesse du rendu (1× = comme la Xbox 360). Plus haut = plus net mais plus lourd.", "Multiplies render quality (1× = like the Xbox 360). Higher = sharper but heavier.", "Vervielfacht die Renderqualität (1× = wie auf der Xbox 360). Höher = schärfer, aber aufwendiger.", "Multiplica la calidad del render (1× = como en Xbox 360). Más alto = más nítido pero más pesado.", "Moltiplica la qualità del rendering (1× = come su Xbox 360). Più alto = più nitido ma più pesante." },
        new[] { "FILTRAGE ANISOTROPE", "ANISOTROPIC FILTERING", "ANISOTROPE FILTERUNG", "FILTRADO ANISOTRÓPICO", "FILTRO ANISOTROPICO" },
        new[] { "Textures plus nettes vues de biais.", "Sharper textures seen at an angle.", "Schärfere Texturen bei schrägem Blickwinkel.", "Texturas más nítidas vistas en ángulo.", "Texture più nitide viste di sbieco." },
        new[] { "PAR DÉFAUT", "DEFAULT", "STANDARD", "PREDETERMINADO", "PREDEFINITO" },
        new[] { "DÉSACTIVÉ", "OFF", "AUS", "DESACTIVADO", "DISATTIVATO" },
        new[] { "OMBRES DU RING", "RING SHADOWS", "RING-SCHATTEN", "SOMBRAS DEL RING", "OMBRE DEL RING" },
        new[] { "Ombres sur le tapis. Désactive-les sur une carte graphique faible pour gagner des images.", "Shadows on the mat. Turn them off on a weak graphics card to gain frames.", "Schatten auf der Matte. Auf einer schwachen Grafikkarte ausschalten, um Bilder zu gewinnen.", "Sombras sobre la lona. Desactívalas en una tarjeta débil para ganar imágenes.", "Ombre sul tappeto. Disattivale su una scheda debole per guadagnare fotogrammi." },

        // game options
        new[] { "LANGUE DU JEU", "GAME LANGUAGE", "SPRACHE DES SPIELS", "IDIOMA DEL JUEGO", "LINGUA DEL GIOCO" },
        new[] { "Langue des textes du jeu. Le launcher change de langue avec elle.", "Language of the game text. The launcher switches language with it.", "Sprache der Spieltexte. Der Launcher wechselt mit ihr die Sprache.", "Idioma de los textos del juego. El launcher cambia de idioma con él.", "Lingua dei testi del gioco. Il launcher cambia lingua di conseguenza." },
        new[] { "60 IMAGES/S", "60 FRAMES/S", "60 BILDER/S", "60 IMÁGENES/S", "60 FOTOGRAMMI/S" },
        new[] { "Entrées, victoires, rediffusions et cinématiques à 60 images/s au lieu de 30 comme sur Xbox 360.", "Entrances, victories, replays and cutscenes at 60 frames/s instead of 30 as on Xbox 360.", "Einmärsche, Siege, Wiederholungen und Zwischensequenzen mit 60 statt 30 Bildern/s wie auf der Xbox 360.", "Entradas, victorias, repeticiones y cinemáticas a 60 imágenes/s en lugar de 30 como en Xbox 360.", "Entrate, vittorie, replay e filmati a 60 fotogrammi/s invece di 30 come su Xbox 360." },
        new[] { "VIBRATION", "VIBRATION", "VIBRATION", "VIBRACIÓN", "VIBRAZIONE" },
        new[] { "Vibration de la manette.", "Controller vibration.", "Controller-Vibration.", "Vibración del mando.", "Vibrazione del controller." },
        new[] { "COUPER LE SON", "MUTE SOUND", "TON AUSSCHALTEN", "SILENCIAR SONIDO", "DISATTIVA AUDIO" },
        new[] { "Lance le jeu sans son.", "Starts the game without sound.", "Startet das Spiel ohne Ton.", "Inicia el juego sin sonido.", "Avvia il gioco senza audio." },
        new[] { "OPTIONS AVANCÉES", "ADVANCED OPTIONS", "ERWEITERTE OPTIONEN", "OPCIONES AVANZADAS", "OPZIONI AVANZATE" },
        new[] { "Arguments en plus pour le jeu (facultatif). Entrée pour écrire, Échap pour finir.", "Extra arguments for the game (optional). Enter to type, Esc to finish.", "Zusätzliche Argumente für das Spiel (optional). Eingabe zum Schreiben, Esc zum Beenden.", "Argumentos adicionales para el juego (opcional). Intro para escribir, Esc para terminar.", "Argomenti aggiuntivi per il gioco (facoltativo). Invio per scrivere, Esc per finire." },

        // diagnostic
        new[] { "MOTEURS GRAPHIQUES", "GRAPHICS ENGINES", "GRAFIK-ENGINES", "MOTORES GRÁFICOS", "MOTORI GRAFICI" },
        new[] { "Moteurs compilés : {0}.", "Built engines: {0}.", "Kompilierte Engines: {0}.", "Motores compilados: {0}.", "Motori compilati: {0}." },
        new[] { "DONNÉES DU JEU", "GAME DATA", "SPIELDATEN", "DATOS DEL JUEGO", "DATI DEL GIOCO" },
        new[] { "PRÉSENTES", "FOUND", "VORHANDEN", "PRESENTES", "PRESENTI" },
        new[] { "ABSENTES", "MISSING", "FEHLEN", "AUSENTES", "ASSENTI" },
        new[] { "Dossier game\\ avec default.xex.", "Folder game\\ with default.xex.", "Ordner game\\ mit default.xex.", "Carpeta game\\ con default.xex.", "Cartella game\\ con default.xex." },
        new[] { "MAUVAISE VERSION", "WRONG VERSION", "FALSCHE VERSION", "VERSIÓN INCORRECTA", "VERSIONE ERRATA" },
        new[] { "ABSENT", "MISSING", "FEHLT", "AUSENTE", "ASSENTE" },
        new[] { "Le moteur est compilé pour la version 1.04 du Title Update.", "The engine is built for version 1.04 of the Title Update.", "Die Engine ist für Version 1.04 des Title Updates kompiliert.", "El motor está compilado para la versión 1.04 del Title Update.", "Il motore è compilato per la versione 1.04 del Title Update." },
        new[] { "DLC INSTALLÉS", "INSTALLED DLC", "INSTALLIERTE DLC", "DLC INSTALADOS", "DLC INSTALLATI" },
        new[] { "Les contenus téléchargeables installés dans userdata.", "Downloadable content installed in userdata.", "In userdata installierte DLC-Inhalte.", "Contenido descargable instalado en userdata.", "Contenuti scaricabili installati in userdata." },
        new[] { "{0} GO RAM · {1} GO LIBRES", "{0} GB RAM · {1} GB FREE", "{0} GB RAM · {1} GB FREI", "{0} GB RAM · {1} GB LIBRES", "{0} GB RAM · {1} GB LIBERI" },
        new[] { "Mémoire vive (8 Go minimum conseillés) et place libre sur le disque du jeu (les caches de shaders grossissent).", "Memory (8 GB minimum recommended) and free space on the game disk (shader caches grow).", "Arbeitsspeicher (mindestens 8 GB empfohlen) und freier Platz auf dem Spiel-Laufwerk (Shader-Caches wachsen).", "Memoria (se aconsejan 8 GB como mínimo) y espacio libre en el disco del juego (las cachés de shaders crecen).", "Memoria (consigliati almeno 8 GB) e spazio libero sul disco del gioco (le cache degli shader crescono)." },
        new[] { "Cartes vues : {0}", "Cards seen: {0}", "Gesehene Karten: {0}", "Tarjetas detectadas: {0}", "Schede rilevate: {0}" },
        new[] { "Carte détectée par Windows.", "Card detected by Windows.", "Von Windows erkannte Karte.", "Tarjeta detectada por Windows.", "Scheda rilevata da Windows." },
        new[] { "COPIER LE RAPPORT", "COPY THE REPORT", "BERICHT KOPIEREN", "COPIAR EL INFORME", "COPIA IL RAPPORTO" },
        new[] { "Copie un rapport complet (versions, fichiers, dernières lignes du journal) à coller dans un message.", "Copies a full report (versions, files, last log lines) to paste into a message.", "Kopiert einen vollständigen Bericht (Versionen, Dateien, letzte Logzeilen) zum Einfügen in eine Nachricht.", "Copia un informe completo (versiones, archivos, últimas líneas del registro) para pegar en un mensaje.", "Copia un rapporto completo (versioni, file, ultime righe del log) da incollare in un messaggio." },
        new[] { "OUVRIR LE DERNIER JOURNAL", "OPEN THE LAST LOG", "LETZTES LOG ÖFFNEN", "ABRIR EL ÚLTIMO REGISTRO", "APRI L'ULTIMO LOG" },
        new[] { "Ouvre le dernier fichier log dans le Bloc-notes.", "Opens the last log file in Notepad.", "Öffnet die letzte Logdatei im Editor.", "Abre el último archivo de registro en el Bloc de notas.", "Apre l'ultimo file di log nel Blocco note." },

        // game files
        new[] { "INSTALLER DEPUIS UN ISO", "INSTALL FROM AN ISO", "VON EINER ISO INSTALLIEREN", "INSTALAR DESDE UNA ISO", "INSTALLA DA UN'ISO" },
        new[] { "Choisis ton image disque WWE 2K17 (.iso) : le launcher extrait le jeu dans game\\ (environ 9 Go).", "Choose your WWE 2K17 disc image (.iso): the launcher extracts the game into game\\ (about 9 GB).", "Wähle dein WWE-2K17-Disc-Image (.iso): Der Launcher entpackt das Spiel nach game\\ (ca. 9 GB).", "Elige la imagen de disco de WWE 2K17 (.iso): el launcher extrae el juego en game\\ (unos 9 GB).", "Scegli l'immagine disco di WWE 2K17 (.iso): il launcher estrae il gioco in game\\ (circa 9 GB)." },
        new[] { "INSTALLÉ", "INSTALLED", "INSTALLIERT", "INSTALADO", "INSTALLATO" },
        new[] { "À FAIRE", "TO DO", "OFFEN", "PENDIENTE", "DA FARE" },
        new[] { "INSTALLER DEPUIS UN DOSSIER", "INSTALL FROM A FOLDER", "VON EINEM ORDNER INSTALLIEREN", "INSTALAR DESDE UNA CARPETA", "INSTALLA DA UNA CARTELLA" },
        new[] { "Choisis le dossier du disque déjà extrait (celui qui contient default.xex) : il est copié dans game\\.", "Choose the folder of the already extracted disc (the one containing default.xex): it is copied into game\\.", "Wähle den Ordner der bereits entpackten Disc (der default.xex enthält): Er wird nach game\\ kopiert.", "Elige la carpeta del disco ya extraído (la que contiene default.xex): se copia en game\\.", "Scegli la cartella del disco già estratto (quella che contiene default.xex): viene copiata in game\\." },
        new[] { "INSTALLER LE TITLE UPDATE 1.04", "INSTALL TITLE UPDATE 1.04", "TITLE UPDATE 1.04 INSTALLIEREN", "INSTALAR EL TITLE UPDATE 1.04", "INSTALLA IL TITLE UPDATE 1.04" },
        new[] { "Choisis le fichier tu00000001_00000000 (version 1.04, environ 109 Mo) : le launcher l'extrait et l'applique au jeu.", "Choose the file tu00000001_00000000 (version 1.04, about 109 MB): the launcher extracts it and applies it to the game.", "Wähle die Datei tu00000001_00000000 (Version 1.04, ca. 109 MB): Der Launcher entpackt sie und wendet sie auf das Spiel an.", "Elige el archivo tu00000001_00000000 (versión 1.04, unos 109 MB): el launcher lo extrae y lo aplica al juego.", "Scegli il file tu00000001_00000000 (versione 1.04, circa 109 MB): il launcher lo estrae e lo applica al gioco." },
        new[] { "1.04 INSTALLÉ", "1.04 INSTALLED", "1.04 INSTALLIERT", "1.04 INSTALADO", "1.04 INSTALLATO" },
        new[] { "OUVRIR LES SAUVEGARDES", "OPEN SAVES", "SPEICHERSTÄNDE ÖFFNEN", "ABRIR PARTIDAS GUARDADAS", "APRI I SALVATAGGI" },
        new[] { "Ouvre le dossier des sauvegardes et données (userdata).", "Opens the saves and data folder (userdata).", "Öffnet den Ordner mit Speicherständen und Daten (userdata).", "Abre la carpeta de partidas y datos (userdata).", "Apre la cartella di salvataggi e dati (userdata)." },
        new[] { "OUVRIR LES JOURNAUX", "OPEN LOGS", "LOGS ÖFFNEN", "ABRIR REGISTROS", "APRI I LOG" },
        new[] { "Ouvre le dossier des logs : utile si le jeu plante.", "Opens the logs folder: useful if the game crashes.", "Öffnet den Log-Ordner: nützlich, wenn das Spiel abstürzt.", "Abre la carpeta de registros: útil si el juego se cierra.", "Apre la cartella dei log: utile se il gioco si blocca." },
        new[] { "OUVRIR LE DOSSIER DU JEU", "OPEN THE GAME FOLDER", "SPIELORDNER ÖFFNEN", "ABRIR LA CARPETA DEL JUEGO", "APRI LA CARTELLA DEL GIOCO" },
        new[] { "Ouvre le dossier du projet dans l'explorateur.", "Opens the project folder in the file explorer.", "Öffnet den Projektordner im Explorer.", "Abre la carpeta del proyecto en el explorador.", "Apre la cartella del progetto in Esplora risorse." },

        // DLC
        new[] { "AJOUTER DES FICHIERS DLC", "ADD DLC FILES", "DLC-DATEIEN HINZUFÜGEN", "AÑADIR ARCHIVOS DLC", "AGGIUNGI FILE DLC" },
        new[] { "Choisis un ou plusieurs paquets DLC Xbox 360 (les fichiers sans extension, noms en chiffres et lettres).", "Choose one or more Xbox 360 DLC packages (files without extension, names made of digits and letters).", "Wähle ein oder mehrere Xbox-360-DLC-Pakete (Dateien ohne Endung, Namen aus Ziffern und Buchstaben).", "Elige uno o varios paquetes DLC de Xbox 360 (archivos sin extensión, con nombres de cifras y letras).", "Scegli uno o più pacchetti DLC Xbox 360 (file senza estensione, nomi di cifre e lettere)." },
        new[] { "AJOUTER UN DOSSIER DE DLC", "ADD A DLC FOLDER", "DLC-ORDNER HINZUFÜGEN", "AÑADIR UNA CARPETA DE DLC", "AGGIUNGI UNA CARTELLA DI DLC" },
        new[] { "Choisis un dossier : tous les paquets DLC trouvés dedans (sous-dossiers compris) sont ajoutés.", "Choose a folder: all DLC packages found inside (subfolders included) are added.", "Wähle einen Ordner: Alle darin gefundenen DLC-Pakete (inkl. Unterordner) werden hinzugefügt.", "Elige una carpeta: se añaden todos los paquetes DLC que haya dentro (subcarpetas incluidas).", "Scegli una cartella: vengono aggiunti tutti i pacchetti DLC trovati al suo interno (sottocartelle comprese)." },
        new[] { "{0} Mo installés. Entrée pour retirer ce DLC.", "{0} MB installed. Press Enter to remove this DLC.", "{0} MB installiert. Eingabe zum Entfernen dieses DLC.", "{0} MB instalados. Intro para quitar este DLC.", "{0} MB installati. Invio per rimuovere questo DLC." },
        new[] { "Aucun DLC installé pour l'instant.", "No DLC installed yet.", "Noch kein DLC installiert.", "Aún no hay ningún DLC instalado.", "Nessun DLC installato per ora." },

        // checks and status
        new[] { "MOTEUR", "ENGINE", "ENGINE", "MOTOR", "MOTORE" },
        new[] { "Le moteur n'est pas compilé : lance tools\\build.ps1.", "The engine is not built: run tools\\build.ps1.", "Die Engine ist nicht kompiliert: starte tools\\build.ps1.", "El motor no está compilado: ejecuta tools\\build.ps1.", "Il motore non è compilato: esegui tools\\build.ps1." },
        new[] { "Le jeu n'est pas installé : va dans FICHIERS DU JEU.", "The game is not installed: go to GAME FILES.", "Das Spiel ist nicht installiert: gehe zu SPIELDATEIEN.", "El juego no está instalado: ve a ARCHIVOS DEL JUEGO.", "Il gioco non è installato: vai in FILE DI GIOCO." },
        new[] { "Il manque le Title Update 1.04 : va dans FICHIERS DU JEU.", "The Title Update 1.04 is missing: go to GAME FILES.", "Das Title Update 1.04 fehlt: gehe zu SPIELDATEIEN.", "Falta el Title Update 1.04: ve a ARCHIVOS DEL JUEGO.", "Manca il Title Update 1.04: vai in FILE DI GIOCO." },
        new[] { "Ce n'est pas le Title Update 1.04 : le moteur est fait pour la 1.04, réinstalle-la dans FICHIERS DU JEU.", "This is not Title Update 1.04: the engine is built for 1.04, reinstall it in GAME FILES.", "Das ist nicht das Title Update 1.04: Die Engine ist für 1.04 gebaut, installiere es erneut unter SPIELDATEIEN.", "Este no es el Title Update 1.04: el motor está hecho para la 1.04, reinstálala en ARCHIVOS DEL JUEGO.", "Non è il Title Update 1.04: il motore è fatto per la 1.04, reinstallalo in FILE DI GIOCO." },
        new[] { "Rapport copié : colle-le dans ton message.", "Report copied: paste it into your message.", "Bericht kopiert: füge ihn in deine Nachricht ein.", "Informe copiado: pégalo en tu mensaje.", "Rapporto copiato: incollalo nel tuo messaggio." },
        new[] { "Impossible de copier : {0}", "Could not copy: {0}", "Kopieren nicht möglich: {0}", "No se pudo copiar: {0}", "Impossibile copiare: {0}" },
        new[] { "Aucun journal pour l'instant : lance le jeu une fois.", "No log yet: launch the game once.", "Noch kein Log: starte das Spiel einmal.", "Aún no hay registro: inicia el juego una vez.", "Ancora nessun log: avvia il gioco una volta." },

        // jobs and dialogs
        new[] { "{0} (Échap pour annuler)", "{0} (Esc to cancel)", "{0} (Esc zum Abbrechen)", "{0} (Esc para cancelar)", "{0} (Esc per annullare)" },
        new[] { "{0} : {1}%  (Échap pour annuler)", "{0}: {1}%  (Esc to cancel)", "{0}: {1}%  (Esc zum Abbrechen)", "{0}: {1}%  (Esc para cancelar)", "{0}: {1}%  (Esc per annullare)" },
        new[] { "Extraction du disque", "Extracting the disc", "Disc wird entpackt", "Extrayendo el disco", "Estrazione del disco" },
        new[] { "Copie du jeu", "Copying the game", "Spiel wird kopiert", "Copiando el juego", "Copia del gioco" },
        new[] { "Installation du Title Update", "Installing the Title Update", "Title Update wird installiert", "Instalando el Title Update", "Installazione del Title Update" },
        new[] { "Installation de {0} DLC", "Installing {0} DLC", "{0} DLC werden installiert", "Instalando {0} DLC", "Installazione di {0} DLC" },
        new[] { "Terminé.", "Done.", "Fertig.", "Terminado.", "Fatto." },
        new[] { "Annulé.", "Cancelled.", "Abgebrochen.", "Cancelado.", "Annullato." },
        new[] { "Erreur : {0}", "Error: {0}", "Fehler: {0}", "Error: {0}", "Errore: {0}" },
        new[] { "Jeu installé dans game\\. Ajoute maintenant le Title Update 1.04.", "Game installed in game\\. Now add Title Update 1.04.", "Spiel in game\\ installiert. Füge jetzt das Title Update 1.04 hinzu.", "Juego instalado en game\\. Ahora añade el Title Update 1.04.", "Gioco installato in game\\. Ora aggiungi il Title Update 1.04." },
        new[] { "Jeu copié dans game\\.", "Game copied into game\\.", "Spiel nach game\\ kopiert.", "Juego copiado en game\\.", "Gioco copiato in game\\." },
        new[] { "Choisis l'image disque de WWE 2K17", "Choose the WWE 2K17 disc image", "Wähle das WWE-2K17-Disc-Image", "Elige la imagen de disco de WWE 2K17", "Scegli l'immagine disco di WWE 2K17" },
        new[] { "Image disque Xbox 360", "Xbox 360 disc image", "Xbox-360-Disc-Image", "Imagen de disco de Xbox 360", "Immagine disco Xbox 360" },
        new[] { "Tous les fichiers", "All files", "Alle Dateien", "Todos los archivos", "Tutti i file" },
        new[] { "Choisis le dossier du disque extrait (celui qui contient default.xex)", "Choose the extracted disc folder (the one containing default.xex)", "Wähle den Ordner der entpackten Disc (der default.xex enthält)", "Elige la carpeta del disco extraído (la que contiene default.xex)", "Scegli la cartella del disco estratto (quella che contiene default.xex)" },
        new[] { "Le dossier game\\ contient déjà un jeu. Les fichiers identiques seront remplacés. Continuer ?", "The game\\ folder already contains a game. Identical files will be replaced. Continue?", "Der Ordner game\\ enthält bereits ein Spiel. Gleiche Dateien werden ersetzt. Fortfahren?", "La carpeta game\\ ya contiene un juego. Los archivos idénticos se reemplazarán. ¿Continuar?", "La cartella game\\ contiene già un gioco. I file identici saranno sostituiti. Continuare?" },
        new[] { "Choisis le Title Update 1.04 (tu00000001_00000000)", "Choose Title Update 1.04 (tu00000001_00000000)", "Wähle das Title Update 1.04 (tu00000001_00000000)", "Elige el Title Update 1.04 (tu00000001_00000000)", "Scegli il Title Update 1.04 (tu00000001_00000000)" },
        new[] { "Title Update installé ({0}).", "Title Update installed ({0}).", "Title Update installiert ({0}).", "Title Update instalado ({0}).", "Title Update installato ({0})." },
        new[] { "Choisis les paquets DLC de WWE 2K17", "Choose the WWE 2K17 DLC packages", "Wähle die DLC-Pakete für WWE 2K17", "Elige los paquetes DLC de WWE 2K17", "Scegli i pacchetti DLC di WWE 2K17" },
        new[] { "Choisis le dossier qui contient les DLC", "Choose the folder containing the DLC", "Wähle den Ordner mit den DLC", "Elige la carpeta que contiene los DLC", "Scegli la cartella che contiene i DLC" },
        new[] { "Aucun paquet DLC trouvé dans ce dossier.", "No DLC package found in this folder.", "Kein DLC-Paket in diesem Ordner gefunden.", "No se encontró ningún paquete DLC en esta carpeta.", "Nessun pacchetto DLC trovato in questa cartella." },
        new[] { "{0} DLC installé(s).", "{0} DLC installed.", "{0} DLC installiert.", "{0} DLC instalado(s).", "{0} DLC installato/i." },
        new[] { " Problème : {0}", " Problem: {0}", " Problem: {0}", " Problema: {0}", " Problema: {0}" },
        new[] { "Retirer « {0} » ?", "Remove “{0}”?", "„{0}“ entfernen?", "¿Quitar «{0}»?", "Rimuovere «{0}»?" },
        new[] { "DLC retiré.", "DLC removed.", "DLC entfernt.", "DLC quitado.", "DLC rimosso." },
        new[] { "Impossible de retirer ce DLC : {0}", "Could not remove this DLC: {0}", "Dieses DLC konnte nicht entfernt werden: {0}", "No se pudo quitar este DLC: {0}", "Impossibile rimuovere questo DLC: {0}" },

        // launching
        new[] { "Essai : ligne de commande écrite, le jeu n'est pas lancé.", "Test: command line written, the game was not started.", "Test: Befehlszeile geschrieben, das Spiel wurde nicht gestartet.", "Prueba: línea de comandos escrita, el juego no se ha iniciado.", "Prova: riga di comando scritta, il gioco non è stato avviato." },
        new[] { "Impossible de lancer le jeu : {0}", "Could not start the game: {0}", "Das Spiel konnte nicht gestartet werden: {0}", "No se pudo iniciar el juego: {0}", "Impossibile avviare il gioco: {0}" },
        new[] { "Jeu fermé normalement ({0}).", "Game closed normally ({0}).", "Spiel normal beendet ({0}).", "Juego cerrado con normalidad ({0}).", "Gioco chiuso normalmente ({0})." },
        new[] { "Le jeu s'est arrêté (code {0}) après {1}. Va dans DIAGNOSTIC pour copier un rapport ou ouvrir le journal.", "The game stopped (code {0}) after {1}. Go to DIAGNOSTICS to copy a report or open the log.", "Das Spiel wurde beendet (Code {0}) nach {1}. Gehe zu DIAGNOSE, um einen Bericht zu kopieren oder das Log zu öffnen.", "El juego se detuvo (código {0}) tras {1}. Ve a DIAGNÓSTICO para copiar un informe o abrir el registro.", "Il gioco si è fermato (codice {0}) dopo {1}. Vai in DIAGNOSTICA per copiare un rapporto o aprire il log." },
        new[] { "Jeu lancé (PID {0}). Bon match !", "Game started (PID {0}). Have a great match!", "Spiel gestartet (PID {0}). Viel Spaß beim Match!", "Juego iniciado (PID {0}). ¡Buen combate!", "Gioco avviato (PID {0}). Buon match!" },

        // Steam Deck / small PC profile
        new[] { "Cela ressemble à un Steam Deck.", "This looks like a Steam Deck.", "Das sieht nach einem Steam Deck aus.", "Esto parece una Steam Deck.", "Sembra uno Steam Deck." },
        new[] { "Cela ressemble à un petit PC (carte graphique intégrée ou peu de mémoire).", "This looks like a small PC (integrated graphics or little memory).", "Das sieht nach einem kleinen PC aus (integrierte Grafik oder wenig Speicher).", "Esto parece un PC modesto (gráfica integrada o poca memoria).", "Sembra un PC modesto (grafica integrata o poca memoria)." },
        new[] { "Réglages proposés :", "Proposed settings:", "Vorgeschlagene Einstellungen:", "Ajustes propuestos:", "Impostazioni proposte:" },
        new[] { "Rendu natif Vulkan", "Native Vulkan rendering", "Natives Vulkan-Rendering", "Renderizado nativo Vulkan", "Rendering nativo Vulkan" },
        new[] { "Plein écran", "Fullscreen", "Vollbild", "Pantalla completa", "Schermo intero" },
        new[] { "Rendu interne {0}p", "Internal rendering {0}p", "Internes Rendering {0}p", "Render interno {0}p", "Rendering interno {0}p" },
        new[] { "Tu pourras annuler dans PARAMÈTRES > ANNULER LE PRÉRÉGLAGE.", "You can undo this in SETTINGS > UNDO THE PRESET.", "Du kannst das unter EINSTELLUNGEN > VOREINSTELLUNG RÜCKGÄNGIG MACHEN zurücknehmen.", "Podrás deshacerlo en AJUSTES > DESHACER EL PRERREGULADO.", "Potrai annullare in IMPOSTAZIONI > ANNULLA IL PRESET." },
        new[] { "Non merci, ne plus demander", "No thanks, don't ask again", "Nein danke, nicht mehr fragen", "No, gracias, no volver a preguntar", "No grazie, non chiedere più" },
        new[] { "Pas maintenant", "Not now", "Nicht jetzt", "Ahora no", "Non ora" },
        new[] { "Appliquer le profil", "Apply the profile", "Profil anwenden", "Aplicar el perfil", "Applica il profilo" },
        new[] { "Préréglage appliqué. Tu peux l'annuler dans PARAMÈTRES.", "Preset applied. You can undo it in SETTINGS.", "Voreinstellung angewendet. Du kannst sie unter EINSTELLUNGEN rückgängig machen.", "Prerregulado aplicado. Puedes deshacerlo en AJUSTES.", "Preset applicato. Puoi annullarlo in IMPOSTAZIONI." },
        new[] { "ANNULER LE PRÉRÉGLAGE", "UNDO THE PRESET", "VOREINSTELLUNG RÜCKGÄNGIG MACHEN", "DESHACER EL PRERREGULADO", "ANNULLA IL PRESET" },
        new[] { "Remet les réglages d'avant le préréglage Steam Deck / petit PC.", "Restores the settings from before the Steam Deck / small PC preset.", "Stellt die Einstellungen vor der Steam-Deck-/Kleinrechner-Voreinstellung wieder her.", "Restaura los ajustes de antes del prerregulado Steam Deck / PC modesto.", "Ripristina le impostazioni precedenti al preset Steam Deck / PC modesto." },
        new[] { "Réglages d'avant restaurés.", "Previous settings restored.", "Frühere Einstellungen wiederhergestellt.", "Ajustes anteriores restaurados.", "Impostazioni precedenti ripristinate." },

        // GameSetup messages
        new[] { "Ce fichier n'est pas une image de disque Xbox 360.", "This file is not an Xbox 360 disc image.", "Diese Datei ist kein Xbox-360-Disc-Image.", "Este archivo no es una imagen de disco de Xbox 360.", "Questo file non è un'immagine disco Xbox 360." },
        new[] { "Cette image ne contient pas default.xex : ce n'est pas le disque du jeu.", "This image does not contain default.xex: it is not the game disc.", "Dieses Image enthält keine default.xex: Es ist nicht die Spiel-Disc.", "Esta imagen no contiene default.xex: no es el disco del juego.", "Questa immagine non contiene default.xex: non è il disco del gioco." },
        new[] { "Ce dossier ne contient pas default.xex.", "This folder does not contain default.xex.", "Dieser Ordner enthält keine default.xex.", "Esta carpeta no contiene default.xex.", "Questa cartella non contiene default.xex." },
        new[] { "C'est déjà le dossier du jeu.", "This is already the game folder.", "Das ist bereits der Spielordner.", "Esta ya es la carpeta del juego.", "Questa è già la cartella del gioco." },
        new[] { "Cette mise à jour n'est pas pour WWE 2K17.", "This update is not for WWE 2K17.", "Dieses Update ist nicht für WWE 2K17.", "Esta actualización no es para WWE 2K17.", "Questo aggiornamento non è per WWE 2K17." },
        new[] { "Ce fichier n'est pas un Title Update.", "This file is not a Title Update.", "Diese Datei ist kein Title Update.", "Este archivo no es un Title Update.", "Questo file non è un Title Update." },
    });

    static Dictionary<string, string[]> Build(string[][] rows)
    {
        var d = new Dictionary<string, string[]>(rows.Length);
        foreach (var r in rows) d[r[0]] = r[1..];
        return d;
    }
}
