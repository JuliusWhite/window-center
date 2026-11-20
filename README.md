# WindowsCenter

A small tray app that slides the active window to the center of the monitor it's on.

It is based on [TcNo's CenterWindow.ahk](https://github.com/TCNOco/TcNo-Random-Scripts/blob/master/AHK/Tool%20-%20CenterWindow.ahk), but it doesn't need AutoHotkey.

## Usage

Double-click `WindowsCenter.exe`. A tray icon appears. Then focus a window and press one of these:

| Hotkey      | Action                                      |
|-------------|---------------------------------------------|
| Win+C       | Center horizontally. The height stays the same. |
| Alt+C       | Center vertically. The horizontal position stays the same. |
| Win+Alt+C   | Center horizontally and vertically.         |

The app tries to register each hotkey normally. If Windows refuses one, the app catches the key with a low-level keyboard hook instead. Windows 11 usually refuses Win+C because it reserves it for Copilot. The tray menu shows which method each hotkey uses.

The app takes Alt+C even in programs that use it themselves, for example as a menu shortcut.

### Smart guides and snapping

While you drag a window:
- **Horizontal center:** a faint vertical line appears when the window's center comes within about 5 px of the monitor's horizontal center.
- **Vertical center:** a faint horizontal line appears when the window comes that close to the monitor's vertical center.
- **Snapping:** if you let go while a line is showing, the window slides exactly onto that center line. If both lines are showing, it snaps to the full center.

Guides never appear while you resize a window or when the window is maximized. The window snaps when you let go of the mouse, not during the drag. Windows sets a dragged window's position from the cursor, so another app can't hold it in place mid-drag without making it flicker.

### Tray menu

Right-click the tray icon to:
- **Smart guides & snapping**: turns the guides on or off. This setting is remembered.
- **Start with Windows**: turns auto-start at login on or off. It is stored in the current user's Run registry key.
- **Exit**: closes the app.

## Building

Run `build.bat`. It uses the C# compiler that comes with Windows (.NET Framework 4.x), so you don't need to install anything.

## Differences from the AHK script

- It shows alignment guides and snaps windows to the center while you drag them.
- There are separate hotkeys for horizontal, vertical, and full centering.
- It uses the monitor the window is on, not always the primary monitor, and it handles monitors that aren't at position 0,0.
- It centers the visible frame, so Windows 10/11's invisible resize borders don't push the window slightly off-center.
- It ignores maximized and minimized windows.
- The window slides into place over about 220 ms with an ease-out curve instead of jumping. To change the speed, edit `DurationMs` in the source.
