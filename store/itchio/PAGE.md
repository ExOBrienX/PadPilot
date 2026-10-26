# itch.io — página de PadPilot

## Crear la página

1. <https://itch.io/game/new>
2. **Title:** PadPilot · **Project URL:** `exobrienx.itch.io/padpilot`
3. **Short description:** Controller manager for DualSense, DualShock 4 and generic controllers
4. **Classification:** Tools · **Kind of project:** Downloadable
5. **Pricing:** *No payments* → mejor **"$0 or donate"** con donación sugerida **2 USD**
   (itch.io cobra y te paga por PayPal o Stripe; puedes fijar su comisión en 0 % en *Edit → Revenue sharing*).
6. **Uploads:** `PadPilot-1.0.1-win-x64.zip` (del [release en GitHub](https://github.com/ExOBrienX/PadPilot/releases/latest)) → marcar **Windows**.
7. **Cover image:** `store/art/itch-cover-630x500.png` · **Screenshots:** `store/screenshots/en/*.png`
8. **Genre / Tags:** `controller`, `gamepad`, `dualsense`, `ps5`, `ps4`, `utility`, `windows`
9. **Community:** Comments (opcional) · **Visibility:** Public

### Subir versiones nuevas con butler (opcional)

```bash
butler login
butler push publish/PadPilot-1.0.1-win-x64.zip exobrienx/padpilot:windows --userversion 1.0.1
```

## Texto de la página (inglés + español)

### PadPilot — controller manager for Windows

Set up your **DualSense, DualSense Edge, DualShock 4** or **generic controller** via USB or Bluetooth.

- **Light bar** — colors per player or per controller, brightness, low-battery and charging indicators that override games
- **Adaptive triggers** — pistol, shotgun, machine gun, assault rifle, bow and car presets, plus fine tuning
- **Remapping & macros** — Xbox buttons, keys, Windows shortcuts or macro sequences on any button
- **Live tester** — sticks, triggers, buttons, touchpad; stick calibration and dead zones against drift
- **Mic & headphones (USB)** — mic level, Mute button with LED, self-monitoring and headphone test
- **Xbox 360 emulation** — for games that only accept Xbox controllers (needs the free ViGEmBus driver)
- Light/dark theme · English/Spanish · lives in the system tray

**Free.** If it helps you, a small donation keeps it going — here or on [Ko-fi](https://ko-fi.com/exo_brienx). Source code: [GitHub](https://github.com/ExOBrienX/PadPilot) (GPL-3.0).

**Install:** unzip anywhere and run `PadPilot.exe`. If Windows shows *"Windows protected your PC"*, click *More info → Run anyway*.
If **Smart App Control** (Windows 11) blocks it, use the Microsoft Store version, which is signed.

---

### PadPilot — gestor de mandos para Windows

Configura tu **DualSense, DualSense Edge, DualShock 4** o **mando genérico** por USB o Bluetooth: luces, gatillos
adaptativos, remapeo, macros, probador con calibración, micrófono y emulación Xbox 360.

**Gratis.** Si te sirve, una pequeña donación ayuda a seguir mejorándolo, aquí o en [Ko-fi](https://ko-fi.com/exo_brienx). Código: [GitHub](https://github.com/ExOBrienX/PadPilot) (GPL-3.0).

**Instalar:** descomprime donde quieras y abre `PadPilot.exe`. Si Windows muestra *"Windows protegió su PC"*,
pulsa *Más información → Ejecutar de todas formas*.

---

<small>PlayStation, DualSense and DualShock are trademarks of Sony Interactive Entertainment. Xbox is a trademark of
Microsoft. PadPilot is not affiliated with either.</small>
