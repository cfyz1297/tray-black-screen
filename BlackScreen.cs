using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "TrayBlackScreen";

    static string Command { get { return "\"" + Application.ExecutablePath + "\""; } }

    public static bool Enabled
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                var v = k == null ? null : k.GetValue(Name) as string;
                return v != null && v.Trim().Trim('"').Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
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

static class Config
{
    const string Key = @"Software\TrayBlackScreen";

    public static Color Fill
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(Key))
            {
                var v = k == null ? null : k.GetValue("Color");
                return v == null ? Color.Black : Color.FromArgb((int)v);
            }
        }
        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("Color", value.ToArgb());
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

static class Colors
{
    public static Color Readable(Color c)
    {
        return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 140 ? Color.Black : Color.White;
    }
}

static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string win);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern IntPtr GetTopWindow(IntPtr h);

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr h, uint cmd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr h, out RECT r);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr h);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;


    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, GW_HWNDNEXT = 2;
    static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [STAThread]
    static void Main(string[] args)
    {
        // Headless switches so the parts that need a live desktop can be checked without the UI.
        if (args.Length == 2 && args[0] == "--autostart") { Autostart.Enabled = args[1] == "on"; return; }
        if (args.Length == 1 && args[0] == "--selftest") { Environment.Exit(SelfTest()); return; }

        bool first;
        using (var mutex = new Mutex(true, @"Local\TrayBlackScreen", out first))
        {
            if (!first) return;
            Application.EnableVisualStyles();
            Application.Run(new Tray());
        }
    }

    static int SelfTest()
    {
        Application.EnableVisualStyles();
        var tray = new Tray();
        var savedColor = Config.Fill;
        var savedScreens = Config.AllScreens;
        int failures = 0;

        Config.AllScreens = false; Config.AllScreens = true;
        if (!Config.AllScreens) { Console.WriteLine("FAIL: AllScreens did not round-trip"); failures++; }
        Config.Fill = Color.DarkRed;
        if (Config.Fill.ToArgb() != Color.DarkRed.ToArgb()) { Console.WriteLine("FAIL: Fill did not round-trip"); failures++; }

        var cursor = Cursor.Position;
        try
        {
            Config.AllScreens = false;
            tray.Toggle();
            Pump(800);
            var primary = Screen.PrimaryScreen.Bounds;
            var taskbar = FindWindow("Shell_TrayWnd", null);
            RECT tbRect, bounds;
            GetWindowRect(taskbar, out tbRect);
            GetWindowRect(tray.OverlayHandle, out bounds);

            if (GetForegroundWindow() == tray.OverlayHandle) { Console.WriteLine("FAIL: overlay took the foreground (blocks taskbar auto-show)"); failures++; }

            if (bounds.Left != primary.Left || bounds.Top != primary.Top ||
                bounds.Right != primary.Right || bounds.Bottom != primary.Bottom)
            {
                Console.WriteLine("FAIL: single-screen overlay " + bounds.Left + "," + bounds.Top + "," + bounds.Right + "," + bounds.Bottom +
                                  " does not cover the primary screen " + primary);
                failures++;
            }

            // Auto-hidden taskbar hangs off the screen bottom; hovering the edge must slide it
            // back in and it must be above the overlay, or the user cannot click it.
            bool wasHidden = tbRect.Bottom > primary.Bottom;
            Cursor.Position = new Point(primary.Left + primary.Width / 2, primary.Bottom - 1);
            bool revealed = false;
            for (int i = 0; i < 40 && !revealed; i++)
            {
                Pump(100);
                RECT r;
                if (GetWindowRect(taskbar, out r)) revealed = r.Bottom <= primary.Bottom;
            }
            int overlayIdx = IndexInZOrder(tray.OverlayHandle);
            int taskbarIdx = IndexInZOrder(taskbar);
            Console.WriteLine("taskbarWasHidden=" + wasHidden + " revealed=" + revealed +
                              " overlay z=" + overlayIdx + " taskbar z=" + taskbarIdx);
            if (wasHidden && !revealed) { Console.WriteLine("FAIL: taskbar did not reveal on hover"); failures++; }
            if (taskbarIdx < 0 || overlayIdx < 0) { Console.WriteLine("FAIL: taskbar/overlay not found in z-order"); failures++; }
            else if (overlayIdx < taskbarIdx) { Console.WriteLine("FAIL: overlay covers the revealed taskbar"); failures++; }

            Pump(700);
            RECT stay;
            GetWindowRect(taskbar, out stay);
            if (wasHidden && stay.Bottom > primary.Bottom) { Console.WriteLine("FAIL: taskbar slid back while hovered"); failures++; }

            tray.Toggle();
            Pump(300);
            if (IsWindowVisible(tray.OverlayHandle)) { Console.WriteLine("FAIL: second toggle did not hide the overlay"); failures++; }

            Config.AllScreens = true;
            tray.Toggle();
            Pump(400);
            RECT all;
            GetWindowRect(tray.OverlayHandle, out all);
            var vs = SystemInformation.VirtualScreen;
            if (all.Left != vs.Left || all.Top != vs.Top || all.Right != vs.Right || all.Bottom != vs.Bottom)
            {
                Console.WriteLine("FAIL: all-screens overlay " + all.Left + "," + all.Top + "," + all.Right + "," + all.Bottom +
                                  " does not cover " + vs);
                failures++;
            }

            tray.Toggle();
            Pump(200);

            // Close button: top-right corner of the primary screen, shown only while the mouse
            // moves, and it must hide the overlay when clicked.
            Config.AllScreens = false;
            // Park the real cursor inside the overlay, away from the auto-hidden taskbar edge,
            // so it does not leave the window while the synthetic messages are tested.
            Cursor.Position = new Point(primary.Left + 600, primary.Top + 400);
            Pump(300);
            tray.Toggle();
            Pump(400);
            var close = tray.CloseButtonScreenBounds;
            Console.WriteLine("closeButton=" + close + " primary=" + primary);
            if (close.Right != primary.Right || close.Top != primary.Top)
            {
                Console.WriteLine("FAIL: close button is not in the primary screen's top-right corner");
                failures++;
            }
            if (tray.CloseButtonVisible) { Console.WriteLine("FAIL: close button is drawn before the mouse moves"); failures++; }

            var center = tray.CloseButtonCenterClient;
            var closeScreenPoint = new Point(close.Left + close.Width / 2, close.Top + close.Height / 2);
            var inside = new Point(primary.Left + 600, primary.Top + 400);
            // Real cursor moves and clicks: the button is driven by genuine hover input.
            Action<Point> hover = p => { Cursor.Position = p; Pump(150); Cursor.Position = new Point(p.X + 8, p.Y + 6); Pump(400); };
            Action<Point> clickAt = p =>
            {
                Cursor.Position = p;
                Pump(200);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Pump(60);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                Pump(400);
            };

            // Nothing is painted until the mouse moves.
            if (tray.CloseButtonVisible) { Console.WriteLine("FAIL: close button is drawn before the mouse moves"); failures++; }

            hover(new Point(primary.Left + 1800, primary.Top + 1000));
            if (!tray.CloseButtonVisible) { Console.WriteLine("FAIL: close button did not appear on mouse movement"); failures++; }

            Pump(1800);
            if (tray.CloseButtonVisible) { Console.WriteLine("FAIL: close button did not fade out while idle"); failures++; }

            clickAt(closeScreenPoint);
            if (IsWindowVisible(tray.OverlayHandle)) { Console.WriteLine("FAIL: close button did not hide the overlay"); failures++; }

            // A click away from the button must not close it.
            tray.Toggle();
            Pump(300);
            clickAt(inside);
            if (!IsWindowVisible(tray.OverlayHandle)) { Console.WriteLine("FAIL: a click outside the close button hid the overlay"); failures++; }
            tray.Toggle();
            Pump(200);
        }
        finally
        {
            Cursor.Position = cursor;
            Config.Fill = savedColor;
            Config.AllScreens = savedScreens;
            tray.Quit();
        }

        Console.WriteLine(failures == 0 ? "PASS" : "FAILURES: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static void Pump(int ms)
    {
        var until = Environment.TickCount + ms;
        do { Application.DoEvents(); Thread.Sleep(10); } while (Environment.TickCount < until);
    }

    static int IndexInZOrder(IntPtr target)
    {
        if (target == IntPtr.Zero) return -1;
        int i = 0;
        for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT), i++)
            if (h == target) return i;
        return -1;
    }

    sealed class Tray : ApplicationContext
    {
        readonly NotifyIcon icon;
        readonly Overlay screen;
        readonly System.Windows.Forms.Timer keeper = new System.Windows.Forms.Timer { Interval = 500 };
        bool shown;

        public Tray()
        {
            screen = new Overlay();
            screen.FormClosing += (s, e) => { e.Cancel = true; Hide(); };
            screen.CloseClicked += (s, e) => Hide();
            // Windows lets other apps raise themselves above a non-topmost overlay, so keep
            // it on top of the normal window band for as long as it is shown.
            keeper.Tick += (s, e) => KeepTaskbarAbove();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Вкл/Выкл", null, (s, e) => Toggle());
            menu.Items.Add("Настройки", null, (s, e) => OpenSettings());
            menu.Items.Add("Выйти", null, (s, e) => Quit());

            icon = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "Чёрный экран: клик — вкл/выкл",
                ContextMenuStrip = menu,
                Visible = true,
            };
            icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        }

        public IntPtr OverlayHandle { get { return screen.Handle; } }

        public Rectangle CloseButtonScreenBounds { get { return screen.CloseButtonScreenBounds; } }

        public Point CloseButtonCenterClient { get { return screen.CloseButtonCenterClient; } }

        public bool CloseButtonVisible { get { return screen.CloseButtonVisible; } }

        public void Toggle()
        {
            if (shown) { Hide(); return; }
            screen.Bounds = Config.AllScreens ? SystemInformation.VirtualScreen : Screen.PrimaryScreen.Bounds;
            screen.Show();
            screen.ResetHover();
            KeepTaskbarAbove();
            keeper.Start();
            shown = true;
        }

        void Hide()
        {
            keeper.Stop();
            screen.Hide();
            screen.ResetHover();
            shown = false;
        }

        void OpenSettings()
        {
            using (var dlg = new SettingsForm(Config.Fill, Config.AllScreens))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                Config.Fill = dlg.Fill;
                Config.AllScreens = dlg.AllScreens;
                screen.BackColor = dlg.Fill;
                if (shown)
                {
                    screen.Bounds = dlg.AllScreens ? SystemInformation.VirtualScreen : Screen.PrimaryScreen.Bounds;
                    KeepTaskbarAbove();
                }
            }
        }

        // The overlay stays a normal (non-topmost) window on top of other normal windows:
        // the auto-hidden taskbar is not topmost while hidden, so a topmost overlay would
        // sit above it and swallow the reveal. On hover the taskbar raises itself to topmost.
        public void KeepTaskbarAbove()
        {
            SetWindowPos(screen.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void Quit()
        {
            keeper.Stop();
            icon.Visible = false;
            icon.Dispose();
            screen.Hide();
            ExitThread();
        }

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    g.DrawRectangle(Pens.Gray, 0, 0, 15, 15);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    sealed class Overlay : Form
    {
        readonly System.Windows.Forms.Timer idle = new System.Windows.Forms.Timer { Interval = 1200 };
        bool closeHot;
        bool closeVisible;
        Rectangle closeRect;
        Point lastPos;

        public event EventHandler CloseClicked;

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Config.Fill;
            ShowInTaskbar = false;
            TopMost = false;
            DoubleBuffered = true;
            ResizeRedraw = true;
            idle.Tick += (s, e) => { idle.Stop(); HideClose(); };
        }

        // Never take the foreground: Windows hides the auto-show taskbar whenever the
        // foreground window is fullscreen, so activating this window would trap the user.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        public Rectangle CloseButtonScreenBounds
        {
            get { return new Rectangle(Left + closeRect.Left, Top + closeRect.Top, closeRect.Width, closeRect.Height); }
        }

        public Point CloseButtonCenterClient
        {
            get { return new Point(closeRect.Left + closeRect.Width / 2, closeRect.Top + closeRect.Height / 2); }
        }

        // Like a maximized window: a caption-height close button flush with the top-right
        // corner of the primary screen (the overlay may span several screens).
        void LayoutCloseButton()
        {
            float scale = DeviceDpi / 96f;
            int w = (int)(46 * scale), h = (int)(32 * scale);
            var primary = Screen.PrimaryScreen.Bounds;
            var r = new Rectangle(primary.Right - Left - w, primary.Top - Top, w, h);
            if (r != closeRect)
            {
                closeRect = r;
                closeHot = false;
                Invalidate();
            }
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutCloseButton(); }
        protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); LayoutCloseButton(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!closeVisible) return;
            var g = e.Graphics;
            if (closeHot)
                using (var b = new SolidBrush(Color.FromArgb(196, 43, 28))) g.FillRectangle(b, closeRect); // Win11 close red

            var glyph = closeHot ? Color.White : Colors.Readable(BackColor);
            using (var pen = new Pen(glyph, Math.Max(1.2f, closeRect.Width / 30f)))
            {
                int d = (int)(closeRect.Width * 0.22f);
                var c = new Point(closeRect.Left + closeRect.Width / 2, closeRect.Top + closeRect.Height / 2);
                g.DrawLine(pen, c.X - d, c.Y - d, c.X + d, c.Y + d);
                g.DrawLine(pen, c.X - d, c.Y + d, c.X + d, c.Y - d);
            }
        }

        // The button only shows up while the mouse is being moved, then fades away again:
        // anything permanently drawn on the screen burns into an OLED panel.
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // Showing the overlay under the cursor makes Windows send a move message for a
            // cursor that has not actually moved; only a changed position counts as movement.
            if (e.Location == lastPos) return;
            lastPos = e.Location;

            idle.Stop();
            idle.Start();
            bool hot = closeRect.Contains(e.Location);
            if (!closeVisible)
            {
                closeVisible = true;
                Invalidate(closeRect);
            }
            if (hot != closeHot)
            {
                closeHot = hot;
                Cursor = hot ? Cursors.Hand : Cursors.Default;
                Invalidate(closeRect);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && closeVisible && closeRect.Contains(e.Location))
                CloseClicked(this, EventArgs.Empty);
        }

        void HideClose()
        {
            idle.Stop();
            closeHot = false;
            Cursor = Cursors.Default;
            if (!closeVisible) return;
            closeVisible = false;
            Invalidate(closeRect);
        }

        public bool CloseButtonVisible { get { return closeVisible; } }

        public void ResetHover()
        {
            lastPos = PointToClient(Cursor.Position);
            HideClose();
        }
    }

    sealed class SettingsForm : Form
    {
        Color fill;
        readonly Button colorButton;
        readonly CheckBox allScreens;
        readonly CheckBox startup;

        public SettingsForm(Color current, bool allScreensOn)
        {
            fill = current;
            Text = "Настройки";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(300, 156);
            TopMost = true;

            colorButton = new Button
            {
                Text = "Цвет заливки",
                Location = new Point(16, 16),
                Size = new Size(268, 34),
                BackColor = fill,
                ForeColor = Colors.Readable(fill),
                FlatStyle = FlatStyle.Flat,
            };
            colorButton.Click += OnPickColor;

            allScreens = new CheckBox
            {
                Text = "Заливать все экраны",
                Location = new Point(16, 60),
                AutoSize = true,
                Checked = allScreensOn,
            };

            startup = new CheckBox
            {
                Text = "Автозапуск с Windows",
                Location = new Point(16, 86),
                AutoSize = true,
                Checked = Autostart.Enabled,
            };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(136, 118), Size = new Size(70, 26) };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(214, 118), Size = new Size(70, 26) };

            Controls.AddRange(new Control[] { colorButton, allScreens, startup, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public Color Fill { get { return fill; } }
        public bool AllScreens { get { return allScreens.Checked; } }

        void OnPickColor(object s, EventArgs e)
        {
            using (var dlg = new ColorDialog { Color = fill, FullOpen = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                fill = dlg.Color;
                colorButton.BackColor = fill;
                colorButton.ForeColor = Colors.Readable(fill);
            }
        }
    }
}
