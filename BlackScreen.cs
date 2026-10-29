// Pure Win32 on top of the .NET Framework runtime: no WinForms, no GDI+, no visual styles.
// The goal is a tray-blank-screen tool the system does not feel: one idle thread, no timers
// at all while the overlay is hidden, and only user32/gdi32/shell32/comdlg32 mapped in.
// Build: build.ps1 (the csc.exe that ships with Windows, no SDK needed).
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "TrayBlackScreen";

    static string Command { get { return "\"" + Program.ExePath + "\""; } }

    public static bool Enabled
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                var v = k == null ? null : k.GetValue(Name) as string;
                return v != null && v.Trim().Trim('"').Equals(Program.ExePath, StringComparison.OrdinalIgnoreCase);
            }
        }
        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (value) k.SetValue(Name, Command);
                else k.DeleteValue(Name, false);
            }
        }
    }
}

// Same HKCU layout as before: Color is an int ARGB value, AllScreens is 0/1.
static class Config
{
    const string Key = @"Software\TrayBlackScreen";

    public static int Fill
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
            {
                var v = k == null ? null : k.GetValue("Color");
                return v == null ? unchecked((int)0xFF000000) : (int)v;
            }
        }
        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("Color", value);
        }
    }

    public static bool AllScreens
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
            {
                var v = k == null ? null : k.GetValue("AllScreens");
                return v == null || (int)v != 0;
            }
        }
        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("AllScreens", value ? 1 : 0);
        }
    }
}

static class Program
{
    #region Win32

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSW
    {
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DRAWITEMSTRUCT
    {
        public int CtlType, CtlID, ItemID, ItemAction, ItemState;
        public IntPtr hwndItem;
        public IntPtr hdc;
        public RECT rcItem;
        public IntPtr itemData;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot, yHotspot;
        public IntPtr hbmMask, hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CHOOSECOLORW
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public uint[] lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr RegisterClassW(ref WNDCLASSW c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateWindowExW(int ex, string cls, string title, int style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetTimer(IntPtr h, UIntPtr id, int ms, IntPtr proc);
    [DllImport("user32.dll")] static extern bool KillTimer(IntPtr h, UIntPtr id);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern IntPtr SetCursor(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr LoadCursorW(IntPtr h, IntPtr id);
    [DllImport("user32.dll")] static extern bool PtInRect(ref RECT r, POINT p);
    [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr h, ref RECT r, bool erase);
    [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr h, IntPtr r, bool erase);
    [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern int GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] static extern uint SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, int min, int max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] static extern bool PeekMessage(out MSG m, IntPtr h, int min, int max, int remove);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr h, uint msg, IntPtr w, IntPtr l);

    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr h, int flags, UIntPtr id, string text);
    [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr h, int flags, int x, int y, int res, IntPtr wnd, IntPtr rect);

    [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("user32.dll")] static extern bool FillRect(IntPtr dc, ref RECT r, IntPtr brush);
    [DllImport("gdi32.dll")] static extern bool MoveToEx(IntPtr dc, int x, int y, IntPtr old);
    [DllImport("gdi32.dll")] static extern bool LineTo(IntPtr dc, int x, int y);
    [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int w, int h, int planes, int bpp, byte[] bits);
    [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    [DllImport("gdi32.dll")] static extern IntPtr GetStockObject(int stock);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int cap);

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    [DllImport("shell32.dll")] static extern bool Shell_NotifyIconW(int action, ref NOTIFYICONDATAW data);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] static extern bool ChooseColorW(ref CHOOSECOLORW c);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetModuleFileNameW(IntPtr mod, StringBuilder sb, int cap);

    const uint WM_NULL = 0x0000, WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_CLOSE = 0x0010,
        WM_ERASEBKGND = 0x0014, WM_SETCURSOR = 0x0020, WM_DRAWITEM = 0x002B,
        WM_SETFONT = 0x0030, BM_GETCHECK = 0x00F0, BM_SETCHECK = 0x00F1,
        WM_COMMAND = 0x0111, WM_TIMER = 0x0113, WM_MOUSEMOVE = 0x0200, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;

    const int WM_APP_ICON = unchecked((int)0x8000) + 1;
    const int ID_TOGGLE = 1001, ID_SETTINGS = 1002, ID_QUIT = 1003;
    const int TID_IDLE = 2; // close-button fade-out timer

    const int SW_HIDE = 0, SW_SHOWNA = 8;
    const uint SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 0x4;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
    const int NIM_ADD = 0, NIM_DELETE = 2;
    const int NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002;
    const int SM_CXSCREEN = 0, SM_CYSCREEN = 1,
        SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    const int LOGPIXELSX = 88;
    // Decimal float luminance weights stay as in the old version; a COLORREF is 0x00BBGGRR.
    const uint WIN11_CLOSE_RED = 0x1C2BC4u; // R=196 G=43 B=28

    #endregion

    #region state

    static IntPtr trayWnd, overlayWnd, settingsWnd;
    static WndProc trayProc, overlayProc, settingsProc; // class procs must outlive the window
    static IntPtr trayIcon;
    static IntPtr fillBrush;
    static uint fillColorRef;   // COLORREF currently backing fillBrush
    static bool shown, closeVisible, closeHot;
    static RECT closeRect;      // client coordinates inside the overlay
    static POINT lastMove;
    static IntPtr cursorArrow, cursorHand, guiFont;
    static uint[] customColors = new uint[16];
    static uint settingsColorRef;

    public static string ExePath
    {
        get
        {
            var sb = new StringBuilder(1024);
            GetModuleFileNameW(IntPtr.Zero, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    static uint ColorRefOf(int argb)
    {
        // 0xAARRGGBB -> 0x00BBGGRR
        return (uint)(((argb & 0xFF0000) >> 16) | (argb & 0xFF00) | ((argb & 0xFF) << 16));
    }

    static int ArgbFrom(uint colorRef)
    {
        // 0x00BBGGRR -> 0xFFRRGGBB
        return unchecked((int)(0xFF000000u | ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF)));
    }

    static IntPtr FillBrush
    {
        get
        {
            uint c = fillColorRef;
            if (fillBrush == IntPtr.Zero)
            {
                if (fillBrush != IntPtr.Zero) DeleteObject(fillBrush);
                fillColorRef = c;
                fillBrush = CreateSolidBrush(c);
            }
            return fillBrush;
        }
    }

    static void RefreshFillBrush()
    {
        uint c = ColorRefOf(Config.Fill);
        if (fillBrush != IntPtr.Zero && c != fillColorRef)
        {
            DeleteObject(fillBrush);
            fillBrush = IntPtr.Zero;
        }
        fillColorRef = c;
    }

    static uint Readable(uint colorRef)
    {
        long r = colorRef & 0xFF, g = (colorRef >> 8) & 0xFF, b = (colorRef >> 16) & 0xFF;
        return (r * 0.299 + g * 0.587 + b * 0.114) > 140 ? 0u : 0xFFFFFFu;
    }

    static int DpiOf(IntPtr h)
    {
        try { int d = GetDpiForWindow(h); if (d > 0) return d; }
        catch (EntryPointNotFoundException) { } // pre-1607 Windows
        var dc = GetDC(IntPtr.Zero);
        int dpi = GetDeviceCaps(dc, LOGPIXELSX);
        ReleaseDC(IntPtr.Zero, dc);
        return dpi > 0 ? dpi : 96;
    }

    // An unhandled exception inside a window procedure brings the whole process down;
    // record exactly what threw instead, so a misbehaving feature is diagnosable.
    static void logCrash(string where, Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "TrayBlackScreen.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + where + "\r\n" + ex + "\r\n\r\n");
        }
        catch { }
    }

    #endregion

    [STAThread]
    static int Main(string[] args)
    {
        // Headless switches so the parts that need a live desktop can be checked without the UI.
        if (args.Length == 2 && args[0] == "--autostart") { Autostart.Enabled = args[1] == "on"; return 0; }
        if (args.Length == 1 && args[0] == "--selftest") return SelfTest();
        if (args.Length >= 2 && args[0] == "--selftest")
        {
            for (int i = 1; i < args.Length; i++) { } // back-compat: extra args ignored
        }

        bool first;
        using (var mutex = new Mutex(true, @"Local\TrayBlackScreen", out first))
        {
            if (!first) return 0;
            Boot();
            var msg = new MSG();
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            RemoveTrayIcon();
        }
        return 0;
    }

    static void Boot()
    {
        var inst = GetModuleHandleW(null);
        cursorArrow = LoadCursorW(IntPtr.Zero, (IntPtr)32512); // IDC_ARROW
        cursorHand = LoadCursorW(IntPtr.Zero, (IntPtr)32649); // IDC_HAND
        guiFont = GetStockObject(17); // DEFAULT_GUI_FONT, used for dialogs/labels

        trayProc = TrayProc;
        var trayClass = new WNDCLASSW
        {
            hInstance = inst,
            lpszClassName = "TrayBlackScreenTray",
            lpfnWndProc = trayProc,
        };
        RegisterClassW(ref trayClass);
        trayWnd = CreateWindowExW(0, "TrayBlackScreenTray", "", 0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero); // never shown; icon + timers + menu host

        overlayProc = OverlayProc;
        var overlayClass = new WNDCLASSW
        {
            hInstance = inst,
            lpszClassName = "TrayBlackScreenOverlay",
            lpfnWndProc = overlayProc,
            hCursor = cursorArrow,
            style = 0x00000001 | 0x00000002, // CS_VREDRAW | CS_HREDRAW
        };
        RegisterClassW(ref overlayClass);
        // Topmost popup that never activates (mouse clicks do not make it the foreground
        // window) and never appears in Alt+Tab. The auto-hidden taskbar does not reveal
        // under a fullscreen window; that is accepted by design - the overlay's close
        // button or the tray icon turns the fill off.
        const int WS_POPUP = unchecked((int)0x80000000);
        const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_TOPMOST = 0x00000008;
        overlayWnd = CreateWindowExW(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST, "TrayBlackScreenOverlay", "",
            WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);

        AddTrayIcon();
    }

    #region tray

    static IntPtr TrayProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        try
        {
            switch (msg)
            {
                case WM_NULL:
                    return IntPtr.Zero;
                case WM_APP_ICON:
                    if ((uint)((long)l & 0xFFFF) == WM_LBUTTONUP) Toggle();
                    else if ((uint)((long)l & 0xFFFF) == WM_RBUTTONUP) ShowMenu(h);
                    return IntPtr.Zero;
                case WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }
            return DefWindowProcW(h, msg, w, l);
        }
        catch (Exception ex) { logCrash("TrayProc msg=" + msg, ex); return IntPtr.Zero; }
    }

    static void AddTrayIcon()
    {
        if (trayIcon == IntPtr.Zero) trayIcon = MakeIcon();
        var ni = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
            hWnd = trayWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_APP_ICON,
            hIcon = trayIcon,
            szTip = "Чёрный экран: клик — вкл/выкл",
        };
        Shell_NotifyIconW(NIM_ADD, ref ni);
    }

    static void RemoveTrayIcon()
    {
        var ni = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
            hWnd = trayWnd,
            uID = 1,
        };
        Shell_NotifyIconW(NIM_DELETE, ref ni);
        if (trayIcon != IntPtr.Zero) { DestroyIcon(trayIcon); trayIcon = IntPtr.Zero; }
    }

    static void ShowMenu(IntPtr h)
    {
        var menu = CreatePopupMenu();
        AppendMenuW(menu, 0, (UIntPtr)ID_TOGGLE, "Вкл/Выкл"); // 0 = MF_STRING
        AppendMenuW(menu, 0, (UIntPtr)ID_SETTINGS, "Настройки");
        AppendMenuW(menu, 0, (UIntPtr)ID_QUIT, "Выйти");
        POINT p;
        GetCursorPos(out p);
        // Classic recipe: without this the menu does not close on an outside click.
        SetForegroundWindow(h);
        var picked = TrackPopupMenu(menu, (int)(TPM_RETURNCMD | TPM_RIGHTBUTTON), p.X, p.Y, 0, h, IntPtr.Zero);
        PostMessageW(h, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
        if (picked == ID_TOGGLE) Toggle();
        else if (picked == ID_SETTINGS) OpenSettings();
        else if (picked == ID_QUIT) { RemoveTrayIcon(); PostQuitMessage(0); }
    }

    public static void Toggle()
    {
        if (shown) { HideOverlay(); return; }
        RefreshFillBrush();
        SetOverlayBounds();
        ShowWindow(overlayWnd, SW_SHOWNA);
        ResetHover();
        shown = true;
    }

    static void HideOverlay()
    {
        ShowWindow(overlayWnd, SW_HIDE);
        ResetHover();
        shown = false;
    }

    static void SetOverlayBounds()
    {
        if (Config.AllScreens)
            SetWindowPos(overlayWnd, IntPtr.Zero, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), SWP_NOZORDER | SWP_NOACTIVATE);
        else
            SetWindowPos(overlayWnd, IntPtr.Zero, 0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN), SWP_NOZORDER | SWP_NOACTIVATE);
        LayoutCloseButton();
    }

    #endregion

    #region overlay

    static IntPtr OverlayProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        try
        {
            switch (msg)
        {
            case WM_ERASEBKGND:
                // WM_PAINT fills the whole update rect; erasing first would be double work.
                return (IntPtr)1;
            case WM_PAINT:
                Paint(h);
                return IntPtr.Zero;
            case WM_TIMER:
                HideClose(); // fade-out countdown fired
                return IntPtr.Zero;
            case WM_SETCURSOR:
                if (((long)l & 0xFFFF) == 1) // HTCLIENT: the overlay covers the screen
                {
                    SetCursor(closeHot ? cursorHand : cursorArrow);
                    return (IntPtr)1;
                }
                break;
            case WM_MOUSEMOVE:
                OnMouseMove(h, new POINT { X = (short)((long)l & 0xFFFF), Y = (short)(((long)l >> 16) & 0xFFFF) });
                return IntPtr.Zero;
            case WM_LBUTTONUP:
                {
                    var p = new POINT { X = (short)((long)l & 0xFFFF), Y = (short)(((long)l >> 16) & 0xFFFF) };
                    if (closeVisible && PtInRect(ref closeRect, p)) HideOverlay();
                    return IntPtr.Zero;
                }
        }
        return DefWindowProcW(h, msg, w, l);
        }
        catch (Exception ex) { logCrash("OverlayProc msg=" + msg, ex); return IntPtr.Zero; }
    }

    static void OnMouseMove(IntPtr h, POINT p)
    {
        // Showing the overlay under the cursor makes Windows send a move message for a
        // cursor that has not actually moved; only a changed position counts as movement.
        if (p.X == lastMove.X && p.Y == lastMove.Y) return;
        lastMove = p;

        SetTimer(h, (UIntPtr)TID_IDLE, 1200, IntPtr.Zero); // restart the fade-out countdown
        bool hot = PtInRect(ref closeRect, p);
        if (!closeVisible)
        {
            closeVisible = true;
            InvalidateRect(h, ref closeRect, false);
        }
        if (hot != closeHot)
        {
            closeHot = hot;
            InvalidateRect(h, ref closeRect, false);
        }
    }

    // Like a maximized window: a caption-height close button flush with the top-right
    // corner of the primary screen (the overlay may span several screens).
    static void LayoutCloseButton()
    {
        float s = DpiOf(overlayWnd) / 96f;
        int w = (int)(46 * s), h = (int)(32 * s);
        RECT win;
        GetWindowRect(overlayWnd, out win);
        var r = new RECT
        {
            Left = GetSystemMetrics(SM_CXSCREEN) - win.Left - w,
            Top = -win.Top,
            Right = GetSystemMetrics(SM_CXSCREEN) - win.Left,
            Bottom = -win.Top + h,
        };
        if (closeRect.Left != r.Left || closeRect.Top != r.Top || closeRect.Bottom != r.Bottom ||
            closeRect.Right != r.Right)
        {
            RECT old = closeRect;
            bool wasVisible = closeVisible;
            closeRect = r;
            if (wasVisible && IsWindowVisible(overlayWnd))
            {
                InvalidateRect(overlayWnd, ref old, false);
                InvalidateRect(overlayWnd, ref r, false);
            }
        }
    }

    static void Paint(IntPtr h)
    {
        PAINTSTRUCT ps;
        var dc = BeginPaint(h, out ps);
        var brush = FillBrush;
        FillRect(dc, ref ps.rcPaint, brush);
        if (closeVisible)
        {
            if (closeHot)
            {
                var rb = CreateSolidBrush(WIN11_CLOSE_RED);
                FillRect(dc, ref closeRect, rb);
                DeleteObject(rb);
            }
            int bw = closeRect.Right - closeRect.Left;
            var pen = CreatePen(0, Math.Max(1, bw / 30), closeHot ? 0xFFFFFFu : Readable(fillColorRef)); // PS_SOLID
            var old = SelectObject(dc, pen);
            int d = (int)(bw * 0.22f);
            int cx = (closeRect.Left + closeRect.Right) / 2, cy = (closeRect.Top + closeRect.Bottom) / 2;
            MoveToEx(dc, cx - d, cy - d, IntPtr.Zero);
            LineTo(dc, cx + d, cy + d);
            MoveToEx(dc, cx - d, cy + d, IntPtr.Zero);
            LineTo(dc, cx + d, cy - d);
            SelectObject(dc, old);
            DeleteObject(pen);
        }
        EndPaint(h, ref ps);
    }

    // The button only shows up while the mouse is being moved, then fades away again:
    // anything permanently drawn on the screen burns into an OLED panel.
    static void HideClose()
    {
        KillTimer(overlayWnd, (UIntPtr)TID_IDLE);
        if (!closeVisible) { closeHot = false; return; }
        closeHot = false;
        closeVisible = false;
        InvalidateRect(overlayWnd, ref closeRect, false);
    }

    static void ResetHover()
    {
        POINT cur;
        GetCursorPos(out cur);
        RECT win;
        GetWindowRect(overlayWnd, out win);
        lastMove.X = cur.X - win.Left;
        lastMove.Y = cur.Y - win.Top;
        HideClose();
    }

    #endregion

    #region settings

    const int CTL_COLOR = 101, CTL_ALL = 102, CTL_RUN = 103, CTL_OK = 1, CTL_CANCEL = 2;

    static IntPtr hwndColorBtn, hwndAll, hwndRun;
    static bool settingsClassRegistered;

    static void OpenSettings()
    {
        if (settingsWnd != IntPtr.Zero) { SetForegroundWindow(settingsWnd); return; }

        int s = Math.Max(1, (DpiOf(trayWnd) + 47) / 96); // dial physical sizes at 96/120/144...
        settingsColorRef = ColorRefOf(Config.Fill);

        if (!settingsClassRegistered)
        {
            settingsProc = SettingsProc;
            var settingsClass = new WNDCLASSW
            {
                hInstance = GetModuleHandleW(null),
                lpszClassName = "TrayBlackScreenSettings",
                lpfnWndProc = settingsProc,
            };
            RegisterClassW(ref settingsClass);
            settingsClassRegistered = true;
        }

        const int WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000;
        const int BS_PUSHBUTTON = 0x0, BS_DEFPUSHBUTTON = 0x1, BS_AUTOCHECKBOX = 0x3, BS_OWNERDRAW = 0xB;
        const int WS_EX_TOPMOST = 0x00000008;

        int W = 320 * s, H = 190 * s;
        int x = (GetSystemMetrics(SM_CXSCREEN) - W) / 2, y = (GetSystemMetrics(SM_CYSCREEN) - H) / 2;
        settingsWnd = CreateWindowExW(WS_EX_TOPMOST, "TrayBlackScreenSettings", "Настройки",
            WS_CAPTION | WS_SYSMENU, x, y, W, H, trayWnd, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

        int m = 12 * s, bw = W - 2 * m;
        hwndColorBtn = CreateWindowExW(0, "BUTTON", "Цвет заливки", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
            m, m, bw, 30 * s, settingsWnd, (IntPtr)CTL_COLOR, GetModuleHandleW(null), IntPtr.Zero);
        hwndAll = CreateWindowExW(0, "BUTTON", "Заливать все экраны", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
            m, 52 * s, bw, 22 * s, settingsWnd, (IntPtr)CTL_ALL, GetModuleHandleW(null), IntPtr.Zero);
        hwndRun = CreateWindowExW(0, "BUTTON", "Автозапуск с Windows", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
            m, 80 * s, bw, 22 * s, settingsWnd, (IntPtr)CTL_RUN, GetModuleHandleW(null), IntPtr.Zero);
        CreateWindowExW(0, "BUTTON", "OK", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON,
            W - 84 * s, 122 * s, 64 * s, 26 * s, settingsWnd, (IntPtr)CTL_OK, GetModuleHandleW(null), IntPtr.Zero);
        CreateWindowExW(0, "BUTTON", "Отмена", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            W - 156 * s, 122 * s, 64 * s, 26 * s, settingsWnd, (IntPtr)CTL_CANCEL, GetModuleHandleW(null), IntPtr.Zero);

        SendMessageW(hwndColorBtn, WM_SETFONT, guiFont, (IntPtr)1);
        SendMessageW(hwndAll, WM_SETFONT, guiFont, (IntPtr)1);
        SendMessageW(hwndRun, WM_SETFONT, guiFont, (IntPtr)1);
        SendMessageW(hwndAll, BM_SETCHECK, Config.AllScreens ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
        SendMessageW(hwndRun, BM_SETCHECK, Autostart.Enabled ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);

        ShowWindow(settingsWnd, SW_SHOWNA);
        SetForegroundWindow(settingsWnd);
    }

    static IntPtr SettingsProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        try
        {
            switch (msg)
        {
            case WM_CLOSE:
                DestroyWindow(h);
                return IntPtr.Zero;
            case WM_DESTROY:
                settingsWnd = IntPtr.Zero;
                return IntPtr.Zero;
            case WM_COMMAND:
                switch ((int)((long)w & 0xFFFF))
                {
                    case CTL_COLOR:
                        {
                            var cc = new CHOOSECOLORW
                            {
                                lStructSize = Marshal.SizeOf(typeof(CHOOSECOLORW)),
                                hwndOwner = h,
                                rgbResult = (int)settingsColorRef,
                                lpCustColors = customColors,
                                Flags = 0x00000001 | 0x00000002, // CC_RGBINIT | CC_FULLOPEN
                            };
                            if (ChooseColorW(ref cc)) settingsColorRef = (uint)cc.rgbResult;
                            InvalidateRect(hwndColorBtn, IntPtr.Zero, false);
                            return IntPtr.Zero;
                        }
                    case CTL_OK:
                        Config.Fill = ArgbFrom(settingsColorRef);
                        Config.AllScreens = SendMessageW(hwndAll, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) != 0;
                        Autostart.Enabled = SendMessageW(hwndRun, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) != 0;
                        if (shown)
                        {
                            RefreshFillBrush();
                            var brush = FillBrush; // recreate for the new fill
                            SetOverlayBounds();
                            InvalidateRect(overlayWnd, IntPtr.Zero, false);
                            GC.KeepAlive(brush);
                        }
                        DestroyWindow(h);
                        return IntPtr.Zero;
                    case CTL_CANCEL:
                        DestroyWindow(h);
                        return IntPtr.Zero;
                }
                break;
            case WM_DRAWITEM:
                PaintColorButton(h, (DRAWITEMSTRUCT)Marshal.PtrToStructure(l, typeof(DRAWITEMSTRUCT)));
                return (IntPtr)1;
        }
            return DefWindowProcW(h, msg, w, l);
        }
        catch (Exception ex) { logCrash("SettingsProc msg=" + msg, ex); return IntPtr.Zero; }
    }

    static void PaintColorButton(IntPtr h, DRAWITEMSTRUCT d)
    {
        const uint TRANSPARENT = 1, DT_CENTER = 1, DT_VCENTER = 4, DT_SINGLELINE = 32;
        const int ODS_SELECTED = 0x0001;
        var brush = CreateSolidBrush(settingsColorRef);
        FillRect(d.hdc, ref d.rcItem, brush);
        DeleteObject(brush);
        var text = new RECT { Left = d.rcItem.Left + 4, Top = d.rcItem.Top, Right = d.rcItem.Right - 4, Bottom = d.rcItem.Bottom };
        var font = SelectObject(d.hdc, guiFont);
        SetBkModeW(d.hdc, (int)TRANSPARENT);
        SetTextColorW(d.hdc, (d.ItemState & ODS_SELECTED) != 0 ? 0xFFFFFFu : Readable(settingsColorRef));
        DrawTextW(d.hdc, "Цвет заливки", -1, ref text, (int)(DT_CENTER | DT_VCENTER | DT_SINGLELINE));
        SelectObject(d.hdc, font);
    }

    [DllImport("user32.dll")] static extern int SetBkModeW(IntPtr dc, int mode);
    [DllImport("user32.dll")] static extern uint SetTextColorW(IntPtr dc, uint color);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int DrawTextW(IntPtr dc, string text, int len, ref RECT rect, int flags);

    #endregion

    #region icon

    static IntPtr MakeIcon()
    {
        int n = 16;
        var bits = new byte[n * n * 4]; // BGRA, premultiplied-friendly opaque square
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool border = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                byte v = border ? (byte)0x80 : (byte)0x00;
                int i = (y * n + x) * 4;
                bits[i] = v; bits[i + 1] = v; bits[i + 2] = v; bits[i + 3] = 0xFF;
            }
        var color = CreateBitmap(n, n, 1, 32, bits);
        var mask = CreateBitmap(n, n, 1, 1, new byte[2 * n * 2]); // 16 px @1bpp = 2 bytes/row, all 0 = opaque
        var info = new ICONINFO { fIcon = true, xHotspot = 0, yHotspot = 0, hbmMask = mask, hbmColor = color };
        var hicon = CreateIconIndirect(ref info);
        DeleteObject(mask);
        DeleteObject(color);
        return hicon;
    }

    #endregion

    #region selftest

    static RECT CloseButtonScreenBounds()
    {
        RECT win;
        GetWindowRect(overlayWnd, out win);
        return new RECT
        {
            Left = win.Left + closeRect.Left,
            Top = win.Top + closeRect.Top,
            Right = win.Left + closeRect.Right,
            Bottom = win.Top + closeRect.Bottom,
        };
    }

    static void Quit()
    {
        KillTimer(overlayWnd, (UIntPtr)TID_IDLE);
        RemoveTrayIcon();
        PostQuitMessage(0);
        Pump(80);
    }

    static void Pump(int ms)
    {
        var until = Environment.TickCount + ms;
        var msg = new MSG();
        do
        {
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, 1))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            Thread.Sleep(10);
        } while (Environment.TickCount < until);
    }

    static int SelfTest()
    {
        bool first;
        using (var mutex = new Mutex(true, @"Local\TrayBlackScreen", out first))
        {
            if (!first) { Console.WriteLine("FAIL: another TrayBlackScreen instance is already running"); return 1; }
        }
        Boot();
        var savedColor = Config.Fill;
        var savedScreens = Config.AllScreens;
        int failures = 0;

        Config.AllScreens = false; Config.AllScreens = true;
        if (!Config.AllScreens) { Console.WriteLine("FAIL: AllScreens did not round-trip"); failures++; }
        Config.Fill = unchecked((int)0xFF8B0000); // dark red
        if (Config.Fill != unchecked((int)0xFF8B0000)) { Console.WriteLine("FAIL: Fill did not round-trip"); failures++; }

        POINT savedCursor;
        GetCursorPos(out savedCursor);
        try
        {
            Config.AllScreens = false;
            Toggle();
            Pump(800);
            RECT bounds;
            GetWindowRect(overlayWnd, out bounds);
            var primary = new RECT { Left = 0, Top = 0, Right = GetSystemMetrics(SM_CXSCREEN), Bottom = GetSystemMetrics(SM_CYSCREEN) };
            Console.WriteLine("primary=" + primary.Left + "," + primary.Top + "," + primary.Right + "," + primary.Bottom);

            if (GetForegroundWindow() == overlayWnd) { Console.WriteLine("FAIL: overlay took the foreground"); failures++; }

            if (bounds.Left > primary.Left || bounds.Top > primary.Top ||
                bounds.Right < primary.Right || bounds.Bottom < primary.Bottom)
            {
                Console.WriteLine("FAIL: single-screen overlay " + bounds.Left + "," + bounds.Top + "," + bounds.Right + "," + bounds.Bottom +
                                  " does not cover the primary screen");
                failures++;
            }

            HideOverlay();
            Pump(300);
            if (IsWindowVisible(overlayWnd)) { Console.WriteLine("FAIL: second toggle did not hide the overlay"); failures++; }

            Config.AllScreens = true;
            Toggle();
            Pump(400);
            RECT all;
            GetWindowRect(overlayWnd, out all);
            var vs = new RECT
            {
                Left = GetSystemMetrics(SM_XVIRTUALSCREEN),
                Top = GetSystemMetrics(SM_YVIRTUALSCREEN),
            };
            vs.Right = vs.Left + GetSystemMetrics(SM_CXVIRTUALSCREEN);
            vs.Bottom = vs.Top + GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (all.Left > vs.Left || all.Top > vs.Top || all.Right < vs.Right || all.Bottom < vs.Bottom)
            {
                Console.WriteLine("FAIL: all-screens overlay " + all.Left + "," + all.Top + "," + all.Right + "," + all.Bottom +
                                  " does not cover " + vs.Left + "," + vs.Top + "," + vs.Right + "," + vs.Bottom);
                failures++;
            }

            HideOverlay();
            Pump(200);

            // Close button: top-right corner of the primary screen, shown only while the mouse
            // moves, and it must hide the overlay when clicked.
            Config.AllScreens = false;
            // Park the real cursor inside the overlay, away from the auto-hidden taskbar edge,
            // so it stays on the overlay while the movement is tested.
            POINT inside = new POINT { X = primary.Left + 600, Y = primary.Top + 400 };
            SetCursorPos(inside.X, inside.Y);
            Pump(300);
            Toggle();
            Pump(400);
            var close = CloseButtonScreenBounds();
            Console.WriteLine("closeButton=" + close.Left + "," + close.Top + "," + close.Right + "," + close.Bottom);
            if (close.Right != primary.Right || close.Top != primary.Top)
            {
                Console.WriteLine("FAIL: close button is not in the primary screen's top-right corner");
                failures++;
            }
            if (closeVisible) { Console.WriteLine("FAIL: close button is drawn before the mouse moves"); failures++; }

            var closeScreenPoint = new POINT { X = (close.Left + close.Right) / 2, Y = (close.Top + close.Bottom) / 2 };
            Action<int, int> hoverTo = (x, y) => { SetCursorPos(x, y); Pump(150); SetCursorPos(x + 8, y + 6); Pump(400); };
            Action<POINT> clickAt = p =>
            {
                SetCursorPos(p.X, p.Y);
                Pump(200);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Pump(60);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                Pump(400);
            };

            // Nothing is painted until the mouse moves.
            if (closeVisible) { Console.WriteLine("FAIL: close button is drawn before the mouse moves"); failures++; }

            hoverTo(primary.Left + 1800, primary.Top + 1000);
            if (!closeVisible) { Console.WriteLine("FAIL: close button did not appear on mouse movement"); failures++; }

            Pump(1800);
            if (closeVisible) { Console.WriteLine("FAIL: close button did not fade out while idle"); failures++; }

            clickAt(closeScreenPoint);
            if (IsWindowVisible(overlayWnd)) { Console.WriteLine("FAIL: close button did not hide the overlay"); failures++; }

            // A click away from the button must not close it.
            Toggle();
            Pump(300);
            clickAt(inside);
            if (!IsWindowVisible(overlayWnd)) { Console.WriteLine("FAIL: a click outside the close button hid the overlay"); failures++; }
            HideOverlay();
            Pump(200);
        }
        finally
        {
            SetCursorPos(savedCursor.X, savedCursor.Y);
            Config.Fill = savedColor;
            Config.AllScreens = savedScreens;
            Quit();
        }

        Console.WriteLine(failures == 0 ? "PASS" : "FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }

    #endregion
}
