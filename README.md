# tray-black-screen

Tray app for Windows 11: click the tray icon (or «Вкл/Выкл» in the menu) → fill color over the screen,
same again → back. The overlay carries a close button (×) in the top-right corner of the primary
screen, Win11-style (red on hover). It only appears while the mouse is being moved and fades out
after ~1.2 s of stillness, so nothing is permanently drawn on the panel.

## Implementation

Single C# file built with the csc.exe that ships with Windows — no SDK needed. The app is pure
Win32 on top of the .NET Framework runtime: no WinForms, no GDI+, no visual styles. Idle cost is
one sleeping thread, zero timers while the overlay is hidden, ~17 MB working set (~60% less than
the WinForms version the tool used before).

How the auto-hidden taskbar keeps working: the overlay never takes the foreground and is not
topmost. On top of that, while the cursor grazes the bottom edge of the primary screen the overlay
drops the 16 px strip above the edge from its window region, so the shell can slide the taskbar up
over that strip; the strip is only exposed transiently while the cursor hugs the edge, and the
region is restored as soon as the cursor moves away (nothing burns into the panel). Current
Windows stopped revealing the band under a fullscreen window even when that window is non-topmost
and inactive, which is why the strip cut-out exists.

## Tray menu

- **Вкл/Выкл** — toggle the overlay.
- **Настройки** — fill color picker, «Заливать все экраны» (off = primary monitor only),
  «Автозапуск с Windows». Saved to `HKCU\Software\TrayBlackScreen` (color, screens) and the
  `Run` key (autostart, for this exe path).
- **Выйти** — quit.

## Build

Needs nothing installed, uses the C# compiler bundled with Windows:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1          # BlackScreen.exe (winexe)
powershell -ExecutionPolicy Bypass -File build.ps1 -Console # BlackScreenCon.exe (console)
```

`BlackScreen.exe --autostart on|off` sets or clears autostart without the UI.
`BlackScreen.exe --selftest` shows the overlay for ~3 s and checks the primary/all-screen geometry,
taskbar edge-reveal (with the auto-hidden band sliding back in and staying up on hover), the close
button (hidden until the mouse moves, fades out when idle, hides the overlay on a real click,
ignores clicks elsewhere), and config round-trip (exit code 0 = pass; build with `-Console` to see
the output). Only one instance can run at a time, and the selftest refuses to run next to it.
