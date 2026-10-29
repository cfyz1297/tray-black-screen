# tray-black-screen

[English](#english) | [Русский](#русский)

## English

A lightweight Windows 11 tray utility that covers the screen with a solid color. It is useful when you want a dark screen without turning off the monitor.

### Usage

Run `BlackScreen.exe`. Click its tray icon or choose **Вкл/Выкл** from the menu to show or hide the overlay. The cursor remains visible. Move the mouse to reveal the close button in the upper-right corner of the primary screen; it disappears after about 1.2 seconds without movement. Click it to hide the overlay.

The overlay stays above other windows without taking focus or appearing in Alt+Tab. An auto-hidden taskbar does not appear over the full-screen overlay. The interface and tray menu are in Russian.

### Settings

Right-click the tray icon and select **Настройки**:

- **Цвет заливки** — choose the fill color.
- **Заливать все экраны** — cover all monitors; when unchecked, cover only the primary monitor.
- **Автозапуск с Windows** — start the utility when signing in to Windows.

Select **Выйти** from the tray menu to exit. Color and monitor settings are stored in `HKCU\Software\TrayBlackScreen`; autostart is stored in the current user's Windows `Run` key. Only one instance can run at a time.

### Build and self-test

The project is a single C# file using Win32 APIs and the .NET Framework runtime. It uses no WinForms or GDI+. The build script uses the 64-bit .NET Framework C# compiler supplied with Windows; no separate SDK is required.

From the project directory, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1          # BlackScreen.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Console # also builds BlackScreenCon.exe
```

To run the self-test, first exit the running tray utility, then run:

```powershell
.\BlackScreenCon.exe --selftest
```

The test temporarily shows the overlay, moves the cursor and changes settings, then restores them. It checks screen geometry, the close button and settings round-trip. Exit code `0` indicates success. `BlackScreen.exe --autostart on` and `BlackScreen.exe --autostart off` enable and disable autostart for that executable without opening the UI.

While the overlay is hidden, the application waits for Windows messages and runs no overlay timer. The fill color is cached during painting and refreshed when the overlay is shown or settings change.

## Русский

Легковесная утилита для Windows 11, которая работает в системном трее и покрывает экран выбранным цветом. Она пригодится, когда нужно затемнить экран, не выключая монитор.

### Использование

Запустите `BlackScreen.exe`. Нажмите на значок в трее или выберите в меню **Вкл/Выкл**, чтобы показать или скрыть заливку. Курсор остаётся видимым. При движении мыши в правом верхнем углу основного экрана появляется кнопка закрытия; примерно через 1,2 секунды без движения она исчезает. Нажмите на неё, чтобы убрать заливку.

Заливка находится поверх других окон, не перехватывает фокус и не появляется в Alt+Tab. Автоматически скрываемая панель задач не показывается поверх полноэкранной заливки.

### Настройки

Нажмите правой кнопкой на значок в трее и выберите **Настройки**:

- **Цвет заливки** — выбор цвета.
- **Заливать все экраны** — заливка всех мониторов; если флажок снят, используется только основной монитор.
- **Автозапуск с Windows** — запуск утилиты при входе в Windows.

Для завершения работы выберите **Выйти** в меню трея. Цвет и выбор мониторов хранятся в `HKCU\Software\TrayBlackScreen`, автозапуск — в разделе Windows `Run` текущего пользователя. Одновременно может работать только один экземпляр утилиты.

### Сборка и самопроверка

Проект состоит из одного файла C# и использует Win32 API и среду .NET Framework, без WinForms и GDI+. Скрипт сборки использует 64-битный компилятор C# из .NET Framework, поставляемый с Windows; отдельно устанавливать SDK не требуется.

В папке проекта выполните:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1          # BlackScreen.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Console # также создаёт BlackScreenCon.exe
```

Для самопроверки сначала закройте работающую утилиту через трей, затем выполните:

```powershell
.\BlackScreenCon.exe --selftest
```

Тест временно включает заливку, перемещает курсор и изменяет настройки, после чего восстанавливает их. Он проверяет размеры заливки, кнопку закрытия и сохранение настроек. Код выхода `0` означает успех. Команды `BlackScreen.exe --autostart on` и `BlackScreen.exe --autostart off` включают и выключают автозапуск указанного исполняемого файла без открытия интерфейса.

Пока заливка скрыта, приложение ожидает сообщения Windows и не запускает таймер заливки. При отрисовке используется сохранённый цвет; он обновляется при включении заливки или изменении настроек.
