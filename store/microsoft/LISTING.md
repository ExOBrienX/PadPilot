# Microsoft Store — ficha de PadPilot

Todo lo que pide Partner Center, listo para copiar. Capturas en `store/screenshots/en/` (1366×768),
arte en `store/art/` y logos del paquete en `store/microsoft/Assets/`.

## Identidad en la Store

| Campo | Valor |
|---|---|
| Package/Identity/Name | `ExOBrienX.PadPilot` |
| Package/Identity/Publisher | `CN=8D1519FD-343A-4078-B795-91BE9465FFC1` |
| PublisherDisplayName | `ExOBrienX` |
| Store ID | `9NC3HH52QQXC` |

Generar el paquete de una versión nueva (subir antes `<Version>` en `Directory.Build.props`):

```powershell
.\store\microsoftuild-msix.ps1 -IdentityName "ExOBrienX.PadPilot" -Publisher "CN=8D1519FD-343A-4078-B795-91BE9465FFC1" -PublisherDisplayName "ExOBrienX" -Version 1.0.1.0
```

## Pasos

1. Crea una cuenta de desarrollador individual en <https://partner.microsoft.com/dashboard> (para personas es gratis).
2. **Apps and games → New product → MSIX or PWA app** y reserva el nombre **PadPilot**.
3. En *Product management → Product identity* copia `Package/Identity/Name`, `Publisher` y `PublisherDisplayName`.
4. Genera el paquete:
   ```powershell
   .\store\microsoft\build-msix.ps1 -IdentityName "<Name>" -Publisher "<Publisher>" -PublisherDisplayName "<PublisherDisplayName>" -Version 1.0.1.0
   ```
5. En la ficha (*Submission*): precio **Free**, categoría **Utilities & tools**, sube el `.msix` en *Packages*,
   rellena *Store listings* (inglés y español, abajo) y *Age ratings* (cuestionario: sin contenido sensible).
6. En **Submission options → Restricted capabilities** justifica `runFullTrust` (texto abajo).

## Datos generales

| Campo | Valor |
|---|---|
| Categoría | Utilities & tools |
| Precio | Free (donaciones en https://ko-fi.com/exo_brienx) |
| Política de privacidad | https://exobrienx.github.io/PadPilot/privacy.html |
| Sitio web | https://exobrienx.github.io/PadPilot/ |
| Soporte | https://github.com/ExOBrienX/PadPilot/issues |
| Copyright | © 2026 ExO |

**Justificación de runFullTrust (inglés):**
> PadPilot is a desktop (Win32) controller manager. It needs full trust to communicate with game controllers
> through HID (USB and Bluetooth), to create virtual Xbox 360 controllers through the ViGEmBus driver, and to
> optionally hide physical controllers through the HidHide driver. It collects no data.

## English listing

**Product name:** PadPilot

**Short description (≤ 100):**
Controller manager for DualSense, DualShock 4 and generic controllers: lights, triggers, remapping.

**Description:**
PadPilot is a lightweight controller manager for Windows. Connect your DualSense, DualSense Edge,
DualShock 4 or generic controller via USB or Bluetooth and set it up your way.

• Light bar: per-player or per-controller colors, brightness, and low-battery and charging indicators that take priority over games.
• Adaptive triggers (DualSense): one-click presets — pistol, shotgun, machine gun, assault rifle, bow, car — plus fine tuning.
• Button remapping: assign Xbox buttons, keyboard keys, Windows shortcuts or macros to any button.
• Macros: sequences of keys and Xbox buttons with custom timing, once or repeated while held.
• Live tester: sticks, triggers, buttons and touchpad, with center calibration and dead zones to fix stick drift.
• Microphone and headphones (USB): mic level, Mute button with LED, self-monitoring and headphone test.
• Xbox 360 emulation for games that only support Xbox controllers, in player order and with rumble.
• Up to 4 players, each controller remembers its settings, light and dark themes, English and Spanish.

Requirements: Xbox 360 emulation needs the free ViGEmBus driver; hiding the physical controller needs the free
HidHide driver (both optional, links inside the app). PadPilot collects no data.

PlayStation, DualSense and DualShock are trademarks of Sony Interactive Entertainment. Xbox is a trademark of
Microsoft. PadPilot is not affiliated with either.

**Features (one per line, ≤ 200 chars each):**
- DualSense, DualSense Edge, DualShock 4 and generic controllers via USB or Bluetooth
- Light bar colors per player or per controller, with brightness control
- Low-battery and charging light indicators that override games
- Adaptive trigger presets: pistol, shotgun, machine gun, bow, car
- Remap buttons to Xbox buttons, keys, Windows shortcuts or macros
- Macro editor with custom timing
- Live input tester with stick calibration and dead zones
- Controller microphone and headphone test over USB
- Xbox 360 controller emulation (requires ViGEmBus)
- Light and dark themes, English and Spanish

**Keywords:** DualSense, DualShock 4, PS5 controller, PS4 controller, gamepad, adaptive triggers, controller remap

## Ficha en español

**Nombre:** PadPilot

**Descripción corta (≤ 100):**
Gestor de mandos DualSense, DualShock 4 y genéricos: luces, gatillos, remapeo.

**Descripción:**
PadPilot es un gestor de mandos ligero para Windows. Conecta tu DualSense, DualSense Edge, DualShock 4
o mando genérico por USB o Bluetooth y configúralo a tu gusto.

• Barra de luz: colores por jugador o por mando, intensidad e indicadores de batería baja y de carga con prioridad sobre los juegos.
• Gatillos adaptativos (DualSense): perfiles de un clic —pistola, escopeta, ametralladora, rifle, arco, coche— y ajuste fino.
• Remapeo: asigna botones Xbox, teclas, atajos de Windows o macros a cualquier botón.
• Macros: secuencias de teclas y botones Xbox con tiempos, una vez o en repetición mientras se mantiene.
• Probador en vivo: sticks, gatillos, botones y touchpad, con calibración de centro y zona muerta contra el drift.
• Micrófono y audífonos (USB): nivel, botón Mute con LED, escucharte y prueba de audífonos.
• Emulación Xbox 360 para juegos que solo aceptan mandos Xbox, en el orden de jugadores y con vibración.
• Hasta 4 jugadores, cada mando recuerda su configuración, tema claro y oscuro, español e inglés.

Requisitos: la emulación Xbox 360 necesita el driver gratuito ViGEmBus; ocultar el mando original necesita el
driver gratuito HidHide (ambos opcionales, con enlaces dentro de la app). PadPilot no recopila datos.

PlayStation, DualSense y DualShock son marcas de Sony Interactive Entertainment. Xbox es una marca de Microsoft.
PadPilot no está afiliado a ninguna de ellas.

**Palabras clave:** DualSense, DualShock 4, mando PS5, mando PS4, gamepad, gatillos adaptativos, remapear mando

## Notas

- En la versión de la Store, "Iniciar con Windows" se activa desde *Configuración de Windows → Aplicaciones → Inicio*
  (la app oculta esa casilla cuando corre empaquetada).
- La Store firma el paquete: los usuarios no verán avisos de SmartScreen ni de Smart App Control.
