# PadPilot

A controller manager for Windows. It works with the DualSense, DualSense Edge, DualShock 4 and most generic
USB or Bluetooth gamepads. It sits in the system tray, and each controller remembers its own settings.

[Website](https://exobrienx.github.io/PadPilot/) · [Download](https://github.com/ExOBrienX/PadPilot/releases/latest) · [Support on Ko-fi](https://ko-fi.com/exo_brienx)

![PadPilot](store/screenshots/en/01-light.png)

## What it does

- **Light bar.** Pick a color per player or per controller and adjust the brightness. When the battery runs low the
  light blinks red, and while charging it goes from green to blue. These indicators take priority, so a game can't
  override them.
- **Adaptive triggers.** Presets for the DualSense (pistol, shotgun, machine gun, assault rifle, bow, car), plus
  manual tuning for each trigger.
- **Button remapping.** Assign any button to an Xbox button, a keyboard key, a Windows shortcut or a macro.
- **Macros.** Sequences of keys and Xbox buttons with your own timing. They can run once or repeat while you hold the button.
- **Live tester.** Sticks, triggers, buttons and the touchpad in real time. It includes stick center calibration and
  dead zones, which helps a lot with drift.
- **Microphone and headphones.** Over USB you can check the mic level, use the Mute button (its LED follows the mic
  state), listen to yourself and test the headphone jack.
- **Xbox 360 emulation.** For games that only recognize Xbox controllers. Players keep their order and rumble is
  passed back to the real controller.
- **Generic controllers.** They're detected automatically. If a button ends up in the wrong place, there's a
  step-by-step mapping wizard.
- English and Spanish, light and dark theme, vertical or horizontal layout, and an option to start with Windows.

## Requirements

- Windows 10 or 11, 64-bit. .NET is bundled, so there's nothing else to install.
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases/latest), only if you want Xbox 360 emulation.
- [HidHide](https://github.com/nefarius/HidHide/releases/latest), optional. It hides the physical controller from
  games while emulation is on, so they don't see two controllers.

## Installing

Download the zip from [Releases](https://github.com/ExOBrienX/PadPilot/releases/latest), extract it anywhere and run
`PadPilot\PadPilot.exe`.

The app isn't digitally signed yet. Windows may show "Windows protected your PC"; click *More info* and then
*Run anyway*. If Smart App Control is turned on, Windows may block it with no way to continue. A signed version
is planned.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet run --project src/PadPilot.App
```

There's also a small console tool for testing the controller protocol without the UI:

```bash
dotnet run --project tools/PadPilot.Probe
```

To make a portable build:

```bash
dotnet publish src/PadPilot.App -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -p:DebugType=none -o publish/folder
```

It's published as a folder rather than a single file on purpose: Smart App Control is much more likely to block a
large unsigned single-file executable. Pushing a `vX.Y.Z` tag makes GitHub Actions build and publish the release.

### Project layout

```
src/PadPilot.Core/     Controller protocol, devices, profiles and lights (no UI)
src/PadPilot.App/      Avalonia UI, Xbox emulation, HidHide, audio, macros, translations
tools/PadPilot.Probe/  Console diagnostics
docs/                  Website (GitHub Pages)
store/                 Microsoft Store and itch.io assets and listings
installer/             Inno Setup script
```

### A few protocol notes

- Over Bluetooth, the DualSense and DualShock 4 start with a reduced input report. Reading feature report `0x05`
  switches them to the full one.
- Bluetooth output reports end with a CRC32 seeded with `0xA2`. If it's wrong, the controller silently ignores the report.
- Battery level is reported in 10% steps (5, 15, 25 and so on).
- To keep the lights in place, PadPilot resends only the light state once per second, so it never touches a game's
  rumble or trigger effects.
- If the same controller is connected over USB and Bluetooth at the same time, USB is preferred because it has audio.

## Reporting problems

Open an [issue](https://github.com/ExOBrienX/PadPilot/issues). Mention your controller, whether it's connected over USB
or Bluetooth, and your Windows version.

## Supporting the project

PadPilot is free and will stay free. If it's useful to you, you can buy me a coffee on
[Ko-fi](https://ko-fi.com/exo_brienx). It helps cover things like the code signing certificate.

## License

GPL-3.0. You're free to use, study and modify it. If you distribute it, it has to be with the source code and under
the same license. Third-party components are listed in `THIRD-PARTY-NOTICES.txt`.

PlayStation, DualSense and DualShock are trademarks of Sony Interactive Entertainment. Xbox is a trademark of
Microsoft. PadPilot isn't affiliated with either of them.
