using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml;

namespace PadPilot.App.Services;

/// <summary>
/// Textos de la interfaz en español e inglés. El idioma se elige al arrancar (ajuste guardado o el de Windows);
/// cambiarlo requiere reiniciar la app, así la interfaz no necesita refrescarse en caliente.
/// </summary>
public static class Loc
{
    public static string Language { get; private set; } = "es";

    /// <param name="setting">"es", "en" o vacío para usar el idioma de Windows.</param>
    public static void Init(string? setting) =>
        Language = setting is "es" or "en" ? setting : SystemLanguage();

    public static string T(string key) =>
        Strings.TryGetValue(key, out var s) ? (Language == "en" ? s.En : s.Es) : key;

    public static string T(string key, params object?[] args) => string.Format(T(key), args);

    /// <summary>Idioma de la interfaz de Windows (la app usa globalización invariante, así que se pregunta al sistema).</summary>
    private static string SystemLanguage()
    {
        try { return (GetUserDefaultUILanguage() & 0x3FF) == 0x0A ? "es" : "en"; }
        catch (Exception) { return "en"; }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    private static readonly Dictionary<string, (string Es, string En)> Strings = new()
    {
        // ---- Navegación ----
        ["nav.controllers"] = ("Mandos", "Controllers"),
        ["nav.settings"] = ("Ajustes", "Settings"),
        ["nav.about"] = ("Acerca de", "About"),

        // ---- Emulación y HidHide ----
        ["emu.title"] = ("Emulación Xbox 360", "Xbox 360 emulation"),
        ["emu.on"] = ("Cada mando aparece en los juegos como un mando Xbox 360, en el orden de los jugadores.",
                      "Each controller appears to games as an Xbox 360 controller, in player order."),
        ["emu.off"] = ("Desactivada. Los juegos reciben los mandos tal como son.",
                       "Off. Games receive your controllers as they are."),
        ["emu.noVigem"] = ("Requiere el controlador ViGEmBus. Consulta Ajustes → Requisitos.",
                           "Requires the ViGEmBus driver. See Settings → Requirements."),
        ["hide.check"] = ("Ocultar los mandos originales a los juegos (HidHide)", "Hide physical controllers from games (HidHide)"),
        ["hide.off"] = ("Los juegos pueden detectar a la vez el mando original y el virtual.",
                        "Games may detect both the physical and the virtual controller."),
        ["hide.notInstalled"] = ("HidHide no está instalado. Consulta Ajustes → Requisitos.",
                                 "HidHide is not installed. See Settings → Requirements."),
        ["hide.needsAdmin"] = ("Para ocultar mandos, PadPilot debe ejecutarse como administrador.",
                               "PadPilot must run as administrator to hide controllers."),
        ["hide.waiting"] = ("Se aplicará al activar la emulación.", "Applies when emulation is turned on."),
        ["hide.on"] = ("Mandos originales ocultos. Si un juego abierto aún los detecta, reinícialo.",
                       "Physical controllers hidden. Restart any open game that still detects them."),
        ["hide.error"] = ("No se pudieron ocultar los mandos: {0}", "Could not hide the controllers: {0}"),
        ["btn.restartAdmin"] = ("Reiniciar como administrador", "Restart as administrator"),
        ["btn.download"] = ("Descargar", "Download"),

        // ---- Lista de mandos ----
        ["empty.title"] = ("No hay mandos conectados", "No controllers connected"),
        ["empty.body"] = ("Conecta un DualSense, DualShock 4 o mando compatible por USB o Bluetooth.",
                          "Connect a DualSense, DualShock 4 or compatible controller via USB or Bluetooth."),
        ["slot.n"] = ("Jugador {0}", "Player {0}"),
        ["slot.short"] = ("J{0}", "P{0}"),
        ["generic.name"] = ("Mando genérico", "Generic controller"),
        ["battery"] = ("Batería", "Battery"),
        ["bat.reading"] = ("Leyendo…", "Reading…"),
        ["bat.error"] = ("Error de batería", "Battery error"),
        ["bat.full"] = ("100 % · Cargada", "100% · Charged"),
        ["bat.charging"] = ("{0} % · Cargando", "{0}% · Charging"),
        ["bat.pct"] = ("{0} %", "{0}%"),
        ["bat.none"] = ("Sin información de batería", "No battery information"),

        // ---- Pestañas ----
        ["tab.light"] = ("Luz", "Light"),
        ["tab.triggers"] = ("Gatillos", "Triggers"),
        ["tab.buttons"] = ("Botones", "Buttons"),
        ["tab.test"] = ("Probar", "Test"),
        ["tab.setup"] = ("Configurar", "Setup"),
        ["tab.audio"] = ("Audio", "Audio"),
        ["tab.macros"] = ("Macros", "Macros"),

        // ---- Luz ----
        ["light.title"] = ("Barra de luz", "Light bar"),
        ["light.slot"] = ("Color del jugador", "Player color"),
        ["light.own"] = ("Color propio", "Custom color"),
        ["light.slotHint"] = ("Se aplica a cualquier mando asignado como Jugador {0}.", "Applies to any controller assigned as Player {0}."),
        ["light.noSlot"] = ("Sin jugador asignado: se usa blanco.", "No player assigned: white is used."),
        ["light.ownHint"] = ("Se aplica solo a este mando.", "Applies to this controller only."),
        ["light.custom"] = ("Personalizar…", "Customize…"),
        ["light.intensity"] = ("Intensidad", "Brightness"),
        ["light.rumble"] = ("Probar vibración", "Test rumble"),
        ["color.blue"] = ("Azul", "Blue"),
        ["color.cyan"] = ("Cian", "Cyan"),
        ["color.green"] = ("Verde", "Green"),
        ["color.yellow"] = ("Amarillo", "Yellow"),
        ["color.orange"] = ("Naranja", "Orange"),
        ["color.red"] = ("Rojo", "Red"),
        ["color.pink"] = ("Rosa", "Pink"),
        ["color.purple"] = ("Morado", "Purple"),
        ["color.white"] = ("Blanco", "White"),

        // ---- Indicadores de batería ----
        ["batlights.title"] = ("Indicadores de batería", "Battery indicators"),
        ["batlights.hint"] = ("Tienen prioridad sobre el color de la barra. Los LEDs de jugador siguen indicando el número de jugador.",
                              "They take priority over the light bar color. Player LEDs keep showing the player number."),
        ["batlights.low"] = ("Batería baja: rojo intermitente", "Low battery: blinking red"),
        ["batlights.lowAt"] = ("Desde {0:0} % o menos", "At {0:0}% or less"),
        ["batlights.lowNote"] = ("El mando informa en pasos de 10 % (5, 15, 25…). Al 5 % parpadea más rápido.",
                                 "Controllers report battery in 10% steps (5, 15, 25…). At 5% it blinks faster."),
        ["batlights.charge"] = ("Carga: verde, luego azul y azul intermitente", "Charging: green, then blue, then blinking blue"),
        ["batlights.blueAt"] = ("Azul desde {0:0} %", "Blue from {0:0}%"),
        ["batlights.chargeNote"] = ("Desde 90 % o con la carga completa, azul intermitente.",
                                    "From 90% or when fully charged, blinking blue."),
        ["batlights.enforce"] = ("Dar prioridad a las luces de PadPilot", "Keep PadPilot's lights on top"),
        ["batlights.enforceNote"] = ("Restablece las luces si un juego u otra aplicación las cambia. No afecta a la vibración ni a los gatillos.",
                                     "Restores the lights if a game or another app changes them. Rumble and triggers are not affected."),

        // ---- Gatillos ----
        ["trig.presets"] = ("Perfiles", "Presets"),
        ["trig.custom"] = ("Personalizado: ajusta cada gatillo abajo.", "Custom: adjust each trigger below."),
        ["trig.fine"] = ("Ajuste fino", "Fine tuning"),
        ["trig.mode.off"] = ("Sin efecto", "Off"),
        ["trig.mode.feedback"] = ("Resistencia", "Resistance"),
        ["trig.mode.weapon"] = ("Gatillo de arma", "Weapon"),
        ["trig.mode.vibration"] = ("Vibración", "Vibration"),
        ["trig.strength"] = ("Fuerza", "Strength"),
        ["trig.start"] = ("Punto de inicio", "Start position"),
        ["trig.freq"] = ("Frecuencia (Hz)", "Frequency (Hz)"),
        ["preset.pistol"] = ("Pistola", "Pistol"),
        ["preset.pistol.desc"] = ("R2 con clic a media carrera; L2 suave para apuntar.", "R2 clicks halfway; light L2 for aiming."),
        ["preset.shotgun"] = ("Escopeta", "Shotgun"),
        ["preset.shotgun.desc"] = ("R2 firme con clic al final del recorrido.", "Stiff R2 with a click near the end."),
        ["preset.smg"] = ("Ametralladora", "Machine gun"),
        ["preset.smg.desc"] = ("R2 vibra rápido al apretar.", "R2 rattles quickly when pressed."),
        ["preset.rifle"] = ("Rifle automático", "Assault rifle"),
        ["preset.rifle.desc"] = ("R2 vibra más fino y desde más abajo.", "Finer R2 vibration starting deeper."),
        ["preset.bow"] = ("Arco", "Bow"),
        ["preset.bow.desc"] = ("Ambos gatillos tensos desde el inicio.", "Both triggers tense from the start."),
        ["preset.car"] = ("Coche", "Car"),
        ["preset.car.desc"] = ("L2 como freno firme; R2 vibra como un motor.", "L2 as a firm brake; R2 rumbles like an engine."),
        ["preset.off"] = ("Sin efecto", "Off"),
        ["preset.off.desc"] = ("Gatillos libres.", "Free triggers."),

        // ---- Botones ----
        ["btn.xboxOff"] = ("La emulación Xbox está desactivada: las asignaciones a botones Xbox no tienen efecto. Las teclas y macros funcionan siempre.",
                           "Xbox emulation is off: Xbox button assignments have no effect. Keys and macros always work."),
        ["btn.xboxOn"] = ("Este mando aparece en los juegos como Xbox 360 · Jugador {0}.", "This controller appears to games as Xbox 360 · Player {0}."),
        ["btn.xboxOnNoPlayer"] = ("Este mando aparece en los juegos como Xbox 360.", "This controller appears to games as Xbox 360."),
        ["btn.hint"] = ("Asigna a cada botón un botón Xbox, una tecla, un atajo de Windows o una macro.",
                        "Assign each button an Xbox button, a key, a Windows shortcut or a macro."),
        ["btn.default"] = ("Predeterminado: {0}", "Default: {0}"),
        ["btn.defaultNone"] = ("Predeterminado: ninguno", "Default: none"),
        ["btn.reset"] = ("Restablecer", "Reset"),
        ["action.default"] = ("Predeterminado", "Default"),
        ["action.none"] = ("Desactivado", "Disabled"),
        ["action.xbox"] = ("Xbox: {0}", "Xbox: {0}"),
        ["action.key"] = ("Tecla {0}", "Key {0}"),
        ["action.macro"] = ("Macro: {0}", "Macro: {0}"),
        ["action.gamebar"] = ("Xbox Game Bar (Win+G)", "Xbox Game Bar (Win+G)"),
        ["action.screenshot"] = ("Captura de pantalla (Win+Alt+ImprPant)", "Screenshot (Win+Alt+PrtScn)"),
        ["action.clip"] = ("Grabar clip (Win+Alt+G)", "Record clip (Win+Alt+G)"),
        ["action.snip"] = ("Recorte de pantalla (Win+Shift+S)", "Snip (Win+Shift+S)"),
        ["action.alttab"] = ("Cambiar de ventana (Alt+Tab)", "Switch window (Alt+Tab)"),
        ["action.desktop"] = ("Mostrar escritorio (Win+D)", "Show desktop (Win+D)"),
        ["action.playpause"] = ("Reproducir / Pausar", "Play / Pause"),
        ["action.next"] = ("Pista siguiente", "Next track"),
        ["action.prev"] = ("Pista anterior", "Previous track"),
        ["action.volup"] = ("Subir volumen", "Volume up"),
        ["action.voldown"] = ("Bajar volumen", "Volume down"),
        ["action.mute"] = ("Silenciar sonido", "Mute sound"),
        ["key.space"] = ("Espacio", "Space"),
        ["xbox.ls"] = ("LS (pulsar stick izquierdo)", "LS (left stick click)"),
        ["xbox.rs"] = ("RS (pulsar stick derecho)", "RS (right stick click)"),
        ["xbox.back"] = ("View / Back", "View / Back"),
        ["xbox.start"] = ("Menu / Start", "Menu / Start"),
        ["xbox.guide"] = ("Guía", "Guide"),
        ["xbox.none"] = ("Ninguno", "None"),
        ["dpad.up"] = ("Cruceta ↑", "D-pad ↑"),
        ["dpad.down"] = ("Cruceta ↓", "D-pad ↓"),
        ["dpad.left"] = ("Cruceta ←", "D-pad ←"),
        ["dpad.right"] = ("Cruceta →", "D-pad →"),
        ["src.cross"] = ("✕  Cruz", "✕  Cross"),
        ["src.circle"] = ("○  Círculo", "○  Circle"),
        ["src.square"] = ("□  Cuadrado", "□  Square"),
        ["src.triangle"] = ("△  Triángulo", "△  Triangle"),
        ["src.touchpad"] = ("Touchpad (pulsar)", "Touchpad (click)"),
        ["src.mute"] = ("Mute (micrófono)", "Mute (microphone)"),
        ["src.ls"] = ("LS (pulsar)", "LS (click)"),
        ["src.rs"] = ("RS (pulsar)", "RS (click)"),

        // ---- Probar ----
        ["test.left"] = ("Stick izquierdo", "Left stick"),
        ["test.right"] = ("Stick derecho", "Right stick"),
        ["test.output"] = ("salida {0,5:0.00} {1,5:0.00}", "out {0,5:0.00} {1,5:0.00}"),
        ["test.legend"] = ("Azul: posición real. Verde: valor enviado al mando virtual. Naranja: zona muerta.",
                           "Blue: actual position. Green: value sent to the virtual controller. Orange: dead zone."),
        ["test.dzLeft"] = ("Zona muerta izquierda: {0:0} %", "Left dead zone: {0:0}%"),
        ["test.dzRight"] = ("Zona muerta derecha: {0:0} %", "Right dead zone: {0:0}%"),
        ["test.calibrate"] = ("Calibrar centro", "Calibrate center"),
        ["test.calHint"] = ("Si un stick se mueve solo, suelta ambos sticks y pulsa Calibrar centro.",
                            "If a stick drifts, release both sticks and press Calibrate center."),
        ["test.measuring"] = ("Midiendo… no toques los sticks.", "Measuring… don't touch the sticks."),
        ["test.done"] = ("Calibrado. Izquierdo: {0}. Derecho: {1}.", "Calibrated. Left: {0}. Right: {1}."),
        ["test.centered"] = ("centrado, zona muerta {0:0} %", "centered, dead zone {0:0}%"),
        ["test.corrected"] = ("centro corregido ({0:0} % de desviación), zona muerta {1:0} %",
                              "center corrected ({0:0}% off), dead zone {1:0}%"),
        ["test.buttons"] = ("Botones", "Buttons"),
        ["test.touchpad"] = ("Touchpad", "Touchpad"),
        ["test.touchIdle"] = ("Toca el touchpad", "Touch the touchpad"),
        ["test.touch1"] = ("1 dedo · {0}, {1}", "1 finger · {0}, {1}"),
        ["test.touch2"] = ("2 dedos · {0}, {1} / {2}, {3}", "2 fingers · {0}, {1} / {2}, {3}"),

        // ---- Configurar (genéricos) ----
        ["setup.title"] = ("Asignación de botones", "Button mapping"),
        ["setup.model"] = ("{0} · {1} botones · ejes {2}", "{0} · {1} buttons · axes {2}"),
        ["setup.hat"] = (" · cruceta", " · D-pad"),
        ["setup.custom"] = ("Asignación personalizada", "Custom mapping"),
        ["setup.standard"] = ("Asignación estándar", "Standard mapping"),
        ["setup.wizard"] = ("Asignar uno por uno", "Map one by one"),
        ["setup.useStandard"] = ("Usar estándar", "Use standard"),
        ["setup.skipHint"] = ("Si tu mando no tiene este control, pulsa Omitir. Lo omitido conserva su asignación.",
                              "If your controller lacks this control, press Skip. Skipped controls keep their mapping."),
        ["setup.skip"] = ("Omitir", "Skip"),
        ["setup.cancel"] = ("Cancelar", "Cancel"),
        ["setup.cancelled"] = ("Asistente cancelado. No se realizaron cambios.", "Wizard cancelled. No changes were made."),
        ["setup.done"] = ("Listo. Pruébalo en la pestaña Probar.", "Done. Try it in the Test tab."),
        ["setup.current"] = ("Asignación actual", "Current mapping"),
        ["setup.step"] = ("Paso {0} de {1}", "Step {0} of {1}"),
        ["setup.note"] = ("Este mando no admite salida por Bluetooth: no hay vibración ni control de LEDs.",
                          "This controller has no output over Bluetooth: no rumble or LED control."),
        ["setup.button"] = ("Botón {0}", "Button {0}"),
        ["setup.axis"] = ("Eje {0}", "Axis {0}"),
        ["setup.inverted"] = (" (invertido)", " (inverted)"),
        ["setup.hatName"] = ("Cruceta (hat)", "D-pad (hat)"),
        ["setup.dpad"] = ("Cruceta", "D-pad"),
        ["setup.lx"] = ("Stick izq. ↔", "Left stick ↔"),
        ["setup.ly"] = ("Stick izq. ↕", "Left stick ↕"),
        ["setup.rx"] = ("Stick der. ↔", "Right stick ↔"),
        ["setup.ry"] = ("Stick der. ↕", "Right stick ↕"),
        ["setup.p.a"] = ("Pulsa A", "Press A"),
        ["setup.p.b"] = ("Pulsa B", "Press B"),
        ["setup.p.x"] = ("Pulsa X", "Press X"),
        ["setup.p.y"] = ("Pulsa Y", "Press Y"),
        ["setup.p.lb"] = ("Pulsa LB (botón superior izquierdo)", "Press LB (left bumper)"),
        ["setup.p.rb"] = ("Pulsa RB (botón superior derecho)", "Press RB (right bumper)"),
        ["setup.p.lt"] = ("Aprieta LT a fondo y suéltalo", "Fully press LT and release it"),
        ["setup.p.rt"] = ("Aprieta RT a fondo y suéltalo", "Fully press RT and release it"),
        ["setup.p.back"] = ("Pulsa Back / Select", "Press Back / Select"),
        ["setup.p.start"] = ("Pulsa Start / Menu", "Press Start / Menu"),
        ["setup.p.home"] = ("Pulsa el botón central (Home)", "Press the center button (Home)"),
        ["setup.p.ls"] = ("Pulsa el stick izquierdo hacia dentro", "Click the left stick"),
        ["setup.p.rs"] = ("Pulsa el stick derecho hacia dentro", "Click the right stick"),
        ["setup.p.up"] = ("Pulsa la cruceta hacia arriba", "Press D-pad up"),
        ["setup.p.down"] = ("Pulsa la cruceta hacia abajo", "Press D-pad down"),
        ["setup.p.left"] = ("Pulsa la cruceta hacia la izquierda", "Press D-pad left"),
        ["setup.p.right"] = ("Pulsa la cruceta hacia la derecha", "Press D-pad right"),
        ["setup.p.lx"] = ("Mueve el stick izquierdo a la derecha y suéltalo", "Move the left stick right and release it"),
        ["setup.p.ly"] = ("Mueve el stick izquierdo hacia abajo y suéltalo", "Move the left stick down and release it"),
        ["setup.p.rx"] = ("Mueve el stick derecho a la derecha y suéltalo", "Move the right stick right and release it"),
        ["setup.p.ry"] = ("Mueve el stick derecho hacia abajo y suéltalo", "Move the right stick down and release it"),

        // ---- Audio ----
        ["audio.btOnly"] = ("El micrófono y la salida de audio del mando solo están disponibles por USB.",
                            "The controller's microphone and audio output are only available over USB."),
        ["audio.notFound"] = ("Windows aún no muestra el audio de este mando. Espera unos segundos y pulsa Buscar de nuevo.",
                              "Windows doesn't list this controller's audio yet. Wait a few seconds and press Refresh."),
        ["audio.mic"] = ("Micrófono", "Microphone"),
        ["audio.muted"] = ("Silenciado", "Muted"),
        ["audio.level"] = ("Nivel", "Level"),
        ["audio.volume"] = ("Volumen {0:0} %", "Volume {0:0}%"),
        ["audio.muteButton"] = ("El botón Mute del mando silencia el micrófono", "The controller's Mute button toggles the microphone"),
        ["audio.monitor"] = ("Escucharme", "Monitoring"),
        ["audio.toController"] = ("Por los audífonos del mando", "Through the controller's headphones"),
        ["audio.toPc"] = ("Por la salida de audio del PC", "Through the PC's audio output"),
        ["audio.live"] = ("Escuchar en directo", "Listen live"),
        ["audio.record"] = ("Grabar 4 s y reproducir", "Record 4 s and play back"),
        ["audio.recording"] = ("Grabando…", "Recording…"),
        ["audio.feedback"] = ("Con altavoces puede producirse acoplamiento. Usa audífonos.", "Speakers may cause feedback. Use headphones."),
        ["audio.headphones"] = ("Audífonos (conector del mando)", "Headphones (controller jack)"),
        ["audio.test"] = ("Probar sonido (izquierda → derecha)", "Test sound (left → right)"),
        ["audio.defaultHint"] = ("Para usarlos en juegos, selecciónalos como salida en el juego o en la configuración de sonido de Windows.",
                                 "To use them in games, select them as output in the game or in Windows sound settings."),
        ["audio.refresh"] = ("Buscar de nuevo", "Refresh"),

        // ---- Macros ----
        ["macros.hint"] = ("Una macro reproduce una secuencia de teclas y botones Xbox. Asígnala a un botón en la pestaña Botones.",
                           "A macro plays a sequence of keys and Xbox buttons. Assign it to a button in the Buttons tab."),
        ["macros.none"] = ("Sin macros", "No macros"),
        ["macros.new"] = ("Nueva", "New"),
        ["macros.delete"] = ("Eliminar", "Delete"),
        ["macros.name"] = ("Nombre de la macro", "Macro name"),
        ["macros.repeat"] = ("Repetir mientras se mantenga pulsado", "Repeat while held"),
        ["macros.action"] = ("Acción", "Action"),
        ["macros.hold"] = ("Mantener (ms)", "Hold (ms)"),
        ["macros.delay"] = ("Pausa (ms)", "Delay (ms)"),
        ["macros.addStep"] = ("Añadir paso", "Add step"),
        ["macros.note"] = ("Los pasos con botones Xbox requieren la emulación Xbox activada.", "Xbox button steps require Xbox emulation to be on."),
        ["macros.defaultName"] = ("Macro {0}", "Macro {0}"),

        // ---- Ajustes ----
        ["settings.general"] = ("General", "General"),
        ["settings.language"] = ("Idioma", "Language"),
        ["settings.langAuto"] = ("Automático (Windows)", "Automatic (Windows)"),
        ["settings.restartNote"] = ("El cambio de idioma se aplica al reiniciar PadPilot.", "Language changes apply after restarting PadPilot."),
        ["settings.restartNow"] = ("Reiniciar ahora", "Restart now"),
        ["settings.theme"] = ("Tema", "Theme"),
        ["theme.system"] = ("Sistema", "System"),
        ["theme.light"] = ("Claro", "Light"),
        ["theme.dark"] = ("Oscuro", "Dark"),
        ["settings.layout"] = ("Diseño", "Layout"),
        ["layout.vertical"] = ("Vertical (compacto)", "Vertical (compact)"),
        ["layout.horizontal"] = ("Horizontal (panel lateral)", "Horizontal (sidebar)"),
        ["settings.autostart"] = ("Iniciar con Windows", "Start with Windows"),
        ["settings.autostartNote"] = ("Se inicia minimizado en el área de notificación.", "Starts minimized to the notification area."),
        ["req.title"] = ("Requisitos", "Requirements"),
        ["req.intro"] = ("Los mandos DualSense, DualShock 4 y genéricos funcionan sin instalar nada. Estos componentes gratuitos activan funciones opcionales:",
                         "DualSense, DualShock 4 and generic controllers work out of the box. These free components enable optional features:"),
        ["req.vigemDesc"] = ("Necesario para la emulación de mando Xbox 360.", "Required for Xbox 360 controller emulation."),
        ["req.hidhideDesc"] = ("Opcional. Oculta los mandos originales a los juegos durante la emulación. Requiere reiniciar el PC tras instalarlo.",
                               "Optional. Hides physical controllers from games during emulation. Requires a PC restart after installing."),
        ["req.installed"] = ("Instalado", "Installed"),
        ["req.missing"] = ("No instalado", "Not installed"),
        ["req.check"] = ("Comprobar de nuevo", "Check again"),

        // ---- Acerca de ----
        ["about.desc"] = ("Gestor de mandos para Windows: DualSense, DualShock 4 y mandos genéricos.",
                          "Controller manager for Windows: DualSense, DualShock 4 and generic controllers."),
        ["about.version"] = ("Versión {0}", "Version {0}"),
        ["about.author"] = ("Desarrollado por {0}", "Developed by {0}"),
        ["about.support"] = ("Apoya el proyecto", "Support the project"),
        ["about.supportText"] = ("PadPilot es gratuito. Si te resulta útil, puedes apoyar su desarrollo con una donación.",
                                 "PadPilot is free. If you find it useful, you can support its development with a donation."),
        ["about.donate"] = ("Donar en Ko-fi", "Donate on Ko-fi"),
        ["about.help"] = ("Ayuda y contacto", "Help & contact"),
        ["about.report"] = ("Informar de un problema", "Report an issue"),
        ["about.website"] = ("Sitio web", "Website"),
        ["about.linksSoon"] = ("Los enlaces estarán disponibles próximamente.", "Links will be available soon."),
        ["about.licenses"] = ("Componentes de terceros", "Third-party components"),
        ["about.trademarks"] = ("PlayStation, DualSense y DualShock son marcas de Sony Interactive Entertainment. Xbox es una marca de Microsoft. PadPilot no está afiliado a ninguna de ellas.",
                                "PlayStation, DualSense and DualShock are trademarks of Sony Interactive Entertainment. Xbox is a trademark of Microsoft. PadPilot is not affiliated with either."),

        // ---- Bandeja ----
        ["tray.open"] = ("Abrir PadPilot", "Open PadPilot"),
        ["tray.exit"] = ("Salir", "Exit"),
        ["tray.none"] = ("PadPilot · sin mandos", "PadPilot · no controllers"),
    };
}

/// <summary>Uso en XAML: <c>Text="{l:T nav.settings}"</c>.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
