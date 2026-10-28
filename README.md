# tray-black-screen

Tray app for Windows 11: click the tray icon (or «Вкл/Выкл» in the menu) → fill color over the screen,
same again → back. The overlay carries a close button (×) in the top-right corner of the primary
screen, Win11-style (red on hover). It only appears while the mouse is being moved and fades out
after ~1.2 s of stillness, so nothing is permanently drawn on the panel. Cursor stays usable, and an
auto-hidden taskbar still slides up on hover: the overlay never takes the foreground and is not
topmost, so Windows lets the taskbar reveal itself above it.

Right-click the tray icon:

- **Вкл/Выкл** — toggle the overlay.
- **Настройки** — fill color picker, «Заливать все экраны» (off = primary monitor only),
  «Автозапуск с Windows». Saved to `HKCU\Software\TrayBlackScreen` (color, screens) and the
  `Run` key (autostart, for this exe path).
- **Выйти** — quit.

Build (needs nothing installed, uses the C# compiler bundled with Windows):

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

`BlackScreen.exe --autostart on|off` sets or clears autostart without the UI.
`BlackScreen.exe --selftest` shows the overlay for ~3 s and checks the primary/all-screen geometry,
taskbar reveal, z-order, the close button (hidden until the mouse moves, fades out when idle, hides
the overlay on a real click, ignores clicks elsewhere), and config round-trip
(exit code 0 = pass, needs a console build to see output).
