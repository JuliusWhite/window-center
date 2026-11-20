// WindowsCenter - centers the active window on its monitor, with a slide animation,
// plus smart alignment guides with center snapping while you drag windows.
// Inspired by TcNo's CenterWindow.ahk.
//
// Hotkeys:
//   Win+C      center horizontally
//   Alt+C      center vertically
//   Win+Alt+C  center horizontally and vertically
//
// Smart guides: while dragging any window, a guide line appears when the window's center comes
// within a few pixels of the monitor's horizontal or vertical center. Release the mouse while a
// guide is showing and the window glides exactly onto that center line.
//
// Each hotkey is registered with RegisterHotKey when Windows allows it; any that are refused
// (e.g. Win+C, reserved for Copilot on Windows 11) are caught by a low-level keyboard hook.
// Runs in the system tray. Right-click the tray icon for options.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("WindowsCenter")]
[assembly: System.Reflection.AssemblyProduct("WindowsCenter")]
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]

namespace WindowsCenter
{
    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const int SW_HIDE = 0;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_C = 0x43;
        public const int VK_MASK = 0xE8; // unassigned key; stops Win/Alt release from opening Start or a menu bar
        public const uint LLKHF_INJECTED = 0x10;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A, EVENT_SYSTEM_MOVESIZEEND = 0x000B, EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
        public const int OBJID_WINDOW = 0;

        public const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hHook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint evMin, uint evMax, IntPtr hmod, WinEventProc fn, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT r, int size);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        // Per-monitor DPI awareness gives true pixel coordinates on mixed-DPI setups.
        public static void MakeDpiAware()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } // PER_MONITOR_AWARE_V2
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }

        public static double ScaleFor(IntPtr hwnd)
        {
            try { uint dpi = GetDpiForWindow(hwnd); if (dpi > 0) return dpi / 96.0; }
            catch (EntryPointNotFoundException) { }
            return 1.0;
        }
    }

    [Flags]
    enum Axis { None = 0, Horizontal = 1, Vertical = 2, Both = Horizontal | Vertical }

    // Geometry of a window relative to the work area of the monitor it's on.
    struct WindowGeometry
    {
        public Native.RECT Win;   // real window rect (includes invisible resize borders)
        public Native.RECT Vis;   // visible frame
        public Native.RECT Work;  // monitor work area (excludes taskbar)

        public static bool TryGet(IntPtr hwnd, out WindowGeometry g)
        {
            g = new WindowGeometry();
            if (!Native.GetWindowRect(hwnd, out g.Win)) return false;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out g.Vis, Marshal.SizeOf(typeof(Native.RECT))) != 0)
                g.Vis = g.Win;
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO)) };
            if (!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref mi)) return false;
            g.Work = mi.rcWork;
            return true;
        }

        public int WorkCenterX { get { return (Work.Left + Work.Right) / 2; } }
        public int WorkCenterY { get { return (Work.Top + Work.Bottom) / 2; } }
        public int VisCenterX { get { return (Vis.Left + Vis.Right) / 2; } }
        public int VisCenterY { get { return (Vis.Top + Vis.Bottom) / 2; } }

        // Window position (GetWindowRect coordinates) that puts the visible frame at the center.
        public int CenteredX { get { return Work.Left + ((Work.Right - Work.Left) - (Vis.Right - Vis.Left)) / 2 - (Vis.Left - Win.Left); } }
        public int CenteredY { get { return Work.Top + ((Work.Bottom - Work.Top) - (Vis.Bottom - Vis.Top)) / 2 - (Vis.Top - Win.Top); } }
    }

    class Hotkey
    {
        public int Id;
        public uint Mods;    // MOD_WIN / MOD_ALT combination (Ctrl and Shift must be up)
        public Axis Axis;
        public string Name;
        public string Mode;  // "registered", "keyboard hook" or "unavailable"
    }

    // Listens for the hotkeys. Uses RegisterHotKey when possible, otherwise a low-level keyboard hook.
    class HotkeyListener : NativeWindow, IDisposable
    {
        public event Action<Axis> Pressed;
        public readonly List<Hotkey> Hotkeys = new List<Hotkey>
        {
            new Hotkey { Id = 1, Mods = Native.MOD_WIN | Native.MOD_ALT, Axis = Axis.Both,       Name = "Win+Alt+C" },
            new Hotkey { Id = 2, Mods = Native.MOD_WIN,                  Axis = Axis.Horizontal, Name = "Win+C" },
            new Hotkey { Id = 3, Mods = Native.MOD_ALT,                  Axis = Axis.Vertical,   Name = "Alt+C" },
        };

        readonly List<Hotkey> hooked = new List<Hotkey>();
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc hookProc; // kept in a field so the GC doesn't collect it
        bool swallowingC;

        public HotkeyListener() { CreateHandle(new CreateParams()); }

        public void Start()
        {
            foreach (var hk in Hotkeys)
            {
                if (Native.RegisterHotKey(Handle, hk.Id, hk.Mods | Native.MOD_NOREPEAT, (uint)Native.VK_C))
                    hk.Mode = "registered";
                else
                    hooked.Add(hk);
            }

            if (hooked.Count == 0) return;

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, hookProc, Native.GetModuleHandle(null), 0);
            foreach (var hk in hooked) hk.Mode = hook != IntPtr.Zero ? "keyboard hook" : "unavailable";
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = (int)wParam;
                bool injected = (kb.flags & Native.LLKHF_INJECTED) != 0;

                if (!injected && kb.vkCode == Native.VK_C)
                {
                    bool keyDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                    bool keyUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

                    if (keyDown)
                    {
                        Hotkey match = MatchHooked();
                        if (match != null)
                        {
                            // A dummy key press stops Windows treating the later Win/Alt release as
                            // "open Start" / "activate the menu bar".
                            Native.keybd_event((byte)Native.VK_MASK, 0, 0, UIntPtr.Zero);
                            Native.keybd_event((byte)Native.VK_MASK, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
                            if (!swallowingC) Native.PostMessage(Handle, Native.WM_HOTKEY, (IntPtr)match.Id, IntPtr.Zero);
                            swallowingC = true; // also swallows auto-repeat
                            return (IntPtr)1;
                        }
                    }
                    else if (keyUp && swallowingC)
                    {
                        swallowingC = false;
                        return (IntPtr)1;
                    }
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        Hotkey MatchHooked()
        {
            if (Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_SHIFT)) return null;
            uint mods = 0;
            if (Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN)) mods |= Native.MOD_WIN;
            if (Native.IsDown(Native.VK_MENU)) mods |= Native.MOD_ALT;
            foreach (var hk in hooked) if (hk.Mods == mods) return hk;
            return null;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null)
            {
                int id = (int)m.WParam;
                foreach (var hk in Hotkeys) if (hk.Id == id) { Pressed(hk.Axis); break; }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            foreach (var hk in Hotkeys) Native.UnregisterHotKey(Handle, hk.Id);
            DestroyHandle();
        }
    }

    // Slides a window to the center of its monitor's work area along the requested axes.
    class Centerer : IDisposable
    {
        const int DurationMs = 220;

        readonly Timer timer = new Timer { Interval = 10 };
        readonly Stopwatch clock = new Stopwatch();
        IntPtr animHwnd;
        int fromX, fromY, toX, toY;

        public Centerer() { timer.Tick += (s, e) => Step(); }

        public void CenterActive(Axis axis)
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == Native.GetShellWindow()) return;
            Center(hwnd, axis);
        }

        public void Center(IntPtr hwnd, Axis axis)
        {
            if (Native.IsZoomed(hwnd) || Native.IsIconic(hwnd)) return; // maximized/minimized: nothing to do

            WindowGeometry g;
            if (!WindowGeometry.TryGet(hwnd, out g)) return;

            int targetX = (axis & Axis.Horizontal) != 0 ? g.CenteredX : g.Win.Left;
            int targetY = (axis & Axis.Vertical) != 0 ? g.CenteredY : g.Win.Top;
            if (targetX == g.Win.Left && targetY == g.Win.Top) return;

            // Start (or restart) the slide from where the window is right now.
            animHwnd = hwnd;
            fromX = g.Win.Left; fromY = g.Win.Top;
            toX = targetX;      toY = targetY;
            clock.Restart();
            timer.Start();
            Step();
        }

        void Step()
        {
            if (!Native.IsWindow(animHwnd)) { timer.Stop(); return; }

            double t = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / DurationMs);
            double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            int x = (int)Math.Round(fromX + (toX - fromX) * eased);
            int y = (int)Math.Round(fromY + (toY - fromY) * eased);

            Native.SetWindowPos(animHwnd, IntPtr.Zero, x, y, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);

            if (t >= 1.0) timer.Stop();
        }

        public void Dispose() { timer.Dispose(); }
    }

    // A thin, always-on-top, click-through colored bar used as a guide line.
    class GuideLine : Form
    {
        bool shown;

        public GuideLine()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(255, 120, 150, 185); // muted slate blue
            Opacity = 0.5;
            TopMost = true;
            var h = Handle; // create the window now so it can be positioned natively
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void Place(int x, int y, int w, int h)
        {
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
            shown = true;
        }

        public void HideLine()
        {
            if (!shown) return;
            Native.ShowWindow(Handle, Native.SW_HIDE);
            shown = false;
        }
    }

    // Watches window drags system-wide. Shows a guide when the dragged window's center is near the
    // monitor's center, and snaps the window onto that center when the drag ends.
    class SnapGuides : IDisposable
    {
        const int SnapDistance = 15; // pixels at 100% scaling

        readonly Centerer centerer;
        readonly GuideLine vLine = new GuideLine(); // marks the horizontal center (vertical line)
        readonly GuideLine hLine = new GuideLine(); // marks the vertical center (horizontal line)
        readonly Native.WinEventProc moveSizeProc, locationProc; // kept in fields so the GC doesn't collect them
        IntPtr moveSizeHook = IntPtr.Zero, locationHook = IntPtr.Zero;

        IntPtr dragHwnd = IntPtr.Zero;
        int startW, startH;
        Axis near = Axis.None;

        public bool Enabled { get; set; }

        public SnapGuides(Centerer centerer)
        {
            this.centerer = centerer;
            moveSizeProc = OnMoveSize;
            locationProc = OnLocationChange;
        }

        public void Start()
        {
            moveSizeHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_MOVESIZESTART, Native.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero, moveSizeProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
        }

        void OnMoveSize(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != Native.OBJID_WINDOW || idChild != 0) return;

            if (evt == Native.EVENT_SYSTEM_MOVESIZESTART)
            {
                EndDrag();
                if (!Enabled || Native.IsZoomed(hwnd)) return;

                Native.RECT r;
                if (!Native.GetWindowRect(hwnd, out r)) return;
                dragHwnd = hwnd;
                startW = r.Right - r.Left;
                startH = r.Bottom - r.Top;

                // Only listen to location changes from the dragged window's thread while dragging.
                uint pid;
                uint tid = Native.GetWindowThreadProcessId(hwnd, out pid);
                locationHook = Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE,
                    IntPtr.Zero, locationProc, pid, tid, Native.WINEVENT_OUTOFCONTEXT);
                Update();
            }
            else if (evt == Native.EVENT_SYSTEM_MOVESIZEEND && hwnd == dragHwnd)
            {
                Update();
                Axis snap = near;
                EndDrag();
                if (snap != Axis.None) centerer.Center(hwnd, snap);
            }
        }

        void OnLocationChange(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == dragHwnd && idObject == Native.OBJID_WINDOW && idChild == 0) Update();
        }

        void Update()
        {
            near = Axis.None;
            WindowGeometry g;
            if (dragHwnd == IntPtr.Zero || !WindowGeometry.TryGet(dragHwnd, out g)) { HideAll(); return; }

            // A size change means this is a resize, not a move: no guides.
            if (g.Win.Right - g.Win.Left != startW || g.Win.Bottom - g.Win.Top != startH) { HideAll(); return; }

            double scale = Native.ScaleFor(dragHwnd);
            int threshold = (int)Math.Round(SnapDistance * scale);
            int thick = Math.Max(2, (int)Math.Round(2 * scale));

            if (Math.Abs(g.VisCenterX - g.WorkCenterX) <= threshold)
            {
                near |= Axis.Horizontal;
                vLine.Place(g.WorkCenterX - thick / 2, g.Work.Top, thick, g.Work.Bottom - g.Work.Top);
            }
            else vLine.HideLine();

            if (Math.Abs(g.VisCenterY - g.WorkCenterY) <= threshold)
            {
                near |= Axis.Vertical;
                hLine.Place(g.Work.Left, g.WorkCenterY - thick / 2, g.Work.Right - g.Work.Left, thick);
            }
            else hLine.HideLine();
        }

        void HideAll()
        {
            vLine.HideLine();
            hLine.HideLine();
        }

        void EndDrag()
        {
            if (locationHook != IntPtr.Zero) { Native.UnhookWinEvent(locationHook); locationHook = IntPtr.Zero; }
            dragHwnd = IntPtr.Zero;
            near = Axis.None;
            HideAll();
        }

        public void Dispose()
        {
            EndDrag();
            if (moveSizeHook != IntPtr.Zero) Native.UnhookWinEvent(moveSizeHook);
            vLine.Dispose();
            hLine.Dispose();
        }
    }

    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string SettingsKey = @"Software\WindowsCenter";
        const string AppName = "WindowsCenter";

        readonly NotifyIcon tray;
        readonly HotkeyListener hotkeys;
        readonly Centerer centerer;
        readonly SnapGuides guides;
        readonly ToolStripMenuItem startupItem, guidesItem;

        public TrayApp()
        {
            centerer = new Centerer();
            hotkeys = new HotkeyListener();
            hotkeys.Pressed += centerer.CenterActive;
            hotkeys.Start();

            guides = new SnapGuides(centerer) { Enabled = LoadGuidesEnabled() };
            guides.Start();

            var menu = new ContextMenuStrip();
            foreach (var hk in hotkeys.Hotkeys)
            {
                string what = hk.Axis == Axis.Both ? "center both" : hk.Axis == Axis.Horizontal ? "center horizontally" : "center vertically";
                menu.Items.Add(new ToolStripMenuItem(hk.Name + ": " + what + " (" + hk.Mode + ")") { Enabled = false });
            }
            menu.Items.Add(new ToolStripSeparator());
            guidesItem = new ToolStripMenuItem("Smart guides && snapping", null, (s, e) => ToggleGuides()) { Checked = guides.Enabled };
            menu.Items.Add(guidesItem);
            startupItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleStartup()) { Checked = IsStartupEnabled() };
            menu.Items.Add(startupItem);
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            Icon icon;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { icon = SystemIcons.Application; }

            tray = new NotifyIcon
            {
                Icon = icon,
                Text = "WindowsCenter",
                ContextMenuStrip = menu,
                Visible = true
            };

            tray.BalloonTipTitle = "WindowsCenter";
            tray.BalloonTipText = "Win+C: horizontal  ·  Alt+C: vertical  ·  Win+Alt+C: both\nDrag a window near the center to snap it.";
            tray.ShowBalloonTip(3000);
        }

        static bool LoadGuidesEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object v = key == null ? null : key.GetValue("SmartGuides");
                return v == null || Convert.ToInt32(v) != 0; // on by default
            }
        }

        void ToggleGuides()
        {
            guides.Enabled = !guides.Enabled;
            guidesItem.Checked = guides.Enabled;
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue("SmartGuides", guides.Enabled ? 1 : 0, RegistryValueKind.DWord);
        }

        static bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(AppName) != null;
        }

        void ToggleStartup()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (IsStartupEnabled()) key.DeleteValue(AppName, false);
                else key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
            }
            startupItem.Checked = IsStartupEnabled();
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Dispose();
            hotkeys.Dispose();
            guides.Dispose();
            centerer.Dispose();
            base.ExitThreadCore();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new System.Threading.Mutex(true, "WindowsCenter_SingleInstance", out createdNew))
            {
                if (!createdNew) return; // already running
                Native.MakeDpiAware();
                Application.EnableVisualStyles();
                Application.Run(new TrayApp());
            }
        }
    }
}
