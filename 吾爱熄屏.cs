// ============================================================
//  吾爱熄屏 系列 · WuAi ScreenOff  ·  C# 源码 (.NET Framework 4)
// ------------------------------------------------------------
//  一份源码,编译时用 /define 区分三个版本:
//    /define:LOCK  → 吾爱熄屏(纯熄屏:盖黑+阻睡眠,任意键亮,无界面)
//    /define:PWD   → 吾爱熄屏口令版(纯口令:双击直接进口令流程)
//    (默认无 define) → 吾爱熄屏融合版(双模式:选择界面 熄屏/口令)
//  单 exe 自动中英:系统语言 zh* 显中文,其它显英文。
//  共用内核:WMI 背光最低(笔记本) + 每屏一窗 + 阻系统睡眠 + 阻显示器 DPMS。
//  编译见 编译.bat(csc + copy_icon.ps1 灌入 v2 绿色图标)。
// ============================================================

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

#if !LOCK
static class Lang {
    public static readonly bool ZH = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
    public static string T(string zh, string en) { return ZH ? zh : en; }
}
#endif

class ScreenOff {
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint f);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x00000001, ES_DISPLAY_REQUIRED = 0x00000002;

#if PWD
    public const string REG_PATH = "Software\\WuAiScreenOff_Password";
#elif LOCK
    public const string REG_PATH = "Software\\WuAiScreenOff";
#else
    public const string REG_PATH = "Software\\WuAiScreenOff_Combo";
#endif

    static int _orig = -1;
    static ManagementObject _meth = null;
    static bool HasB { get { return _meth != null && _orig >= 0; } }
    public static void SetB(byte b) {
        try { if (HasB) _meth.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, b }); } catch { }
    }
    static void Restore() { if (_orig >= 0) SetB((byte)_orig); }

    [STAThread]
    static void Main() {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

#if LOCK
        RunCore(null, true);                                  // 纯熄屏:任意键亮
#elif PWD
        string pwd = LoadOrSetPwd();                          // 纯口令
        if (pwd == null) return;
        RunCore(pwd, false);
#else
        bool anyWake;                                         // 融合:先选模式
        using (SelectForm sel = new SelectForm()) {
            if (sel.ShowDialog() != DialogResult.OK) return;
            anyWake = sel.AnyWake;
        }
        string pwd = null;
        if (!anyWake) { pwd = LoadOrSetPwd(); if (pwd == null) return; }
        RunCore(pwd, anyWake);
#endif
    }

#if !LOCK
    static string LoadOrSetPwd() {
        string exePath = Application.ExecutablePath;
        string pwd = null;
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(REG_PATH)) if (k != null) pwd = k.GetValue(exePath) as string; } catch { }
        if (string.IsNullOrEmpty(pwd)) {
            using (SettingsForm sf = new SettingsForm()) {
                if (sf.ShowDialog() != DialogResult.OK) return null;
                pwd = sf.Password;
            }
            try {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(REG_PATH)) {
                    foreach (string n in k.GetValueNames())
                        if (!string.IsNullOrEmpty(n) && !File.Exists(n)) k.DeleteValue(n, false);
                    k.SetValue(exePath, pwd);
                }
            } catch { }
        }
        return pwd;
    }
#endif

    const string BRI_KEY = "Software\\WuAiScreenOff";
    static void RunCore(string pwd, bool anyWake) {
        try {
            foreach (ManagementObject o in new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness").Get()) { _orig = (byte)o["CurrentBrightness"]; break; }
            foreach (ManagementObject o in new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods").Get()) { _meth = o; break; }
        } catch { }
        if (HasB) {                                            // 亮度保险:上次异常退出(崩溃/被强杀)没还原 → 本次启动先还原
            try {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(BRI_KEY))
                    if (k != null && "1".Equals(k.GetValue("Pending") as string)) {
                        int saved; if (int.TryParse(k.GetValue("OrigBri") as string, out saved) && saved > 0) { SetB((byte)saved); _orig = saved; }
                    }
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(BRI_KEY)) { k.SetValue("OrigBri", _orig.ToString()); k.SetValue("Pending", "1"); }
            } catch { }
        }
        bool useDPMS = !HasB;                                  // 台式/外接屏(无背光控制)→DPMS 真关(熄屏/口令都);笔记本→WMI 盖黑
        uint es = ES_CONTINUOUS | ES_SYSTEM_REQUIRED;
        if (!useDPMS) es |= ES_DISPLAY_REQUIRED;               // 盖黑窗模式才阻 DPMS 防闪;真关模式不能阻(否则关不掉)
        SetThreadExecutionState(es);
        AppDomain.CurrentDomain.ProcessExit += delegate { Restore(); SetThreadExecutionState(ES_CONTINUOUS); };
        try { Application.Run(new BlackForm(pwd, anyWake, useDPMS)); }
        finally {
            Restore();
            if (HasB) { try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(BRI_KEY)) k.SetValue("Pending", "0"); } catch { } }   // 正常退出,清标志
            SetThreadExecutionState(ES_CONTINUOUS); Cursor.Show();
        }
    }
}

// 全屏纯黑置顶窗:anyWake=true 任意键鼠即亮;false 口令亮屏
class BlackForm : Form {
#if !LOCK
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    const int VK_ESCAPE = 0x1B;
#endif
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    const int WM_SYSCOMMAND = 0x0112, SC_MONITORPOWER = 0xF170;
#if !LOCK
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc fn, IntPtr mod, uint tid);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
    delegate IntPtr LowLevelKeyboardProc(int code, IntPtr w, IntPtr l);
    const int WH_KEYBOARD_LL = 13;
    IntPtr _hook = IntPtr.Zero;
    LowLevelKeyboardProc _hookProc;
#endif
    readonly bool _anyWake, _useDPMS;
    int _baseline;
    readonly System.Collections.Generic.List<Form> _covers = new System.Collections.Generic.List<Form>();
#if !LOCK
    readonly string _pwd;
    readonly StringBuilder _buf = new StringBuilder();
    int _escHeld = 0;
    Timer _escTimer;
#endif

    public BlackForm(string pwd, bool anyWake, bool useDPMS) {
        _anyWake = anyWake; _useDPMS = useDPMS;
#if !LOCK
        _pwd = (pwd ?? "").ToLowerInvariant();
#endif
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black; ShowInTaskbar = false; TopMost = true;
        KeyPreview = true; ImeMode = ImeMode.Disable;
        StartPosition = FormStartPosition.Manual; Bounds = Screen.PrimaryScreen.Bounds;
        if (_anyWake) { MouseMove += delegate { Wake(); }; MouseDown += delegate { Wake(); }; }
#if !LOCK
        else { _escTimer = new Timer(); _escTimer.Interval = 100; _escTimer.Tick += EscTick; }
#endif
    }
    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        foreach (Screen s in Screen.AllScreens) { if (s.Primary) continue; CoverForm c = new CoverForm(s.Bounds); c.Show(); _covers.Add(c); }
        Activate(); Cursor.Hide(); _baseline = Environment.TickCount;
        ScreenOff.SetB(0);                                                                     // 笔记本:背光最低(台式 WMI 不支持会自动跳过)
        if (_useDPMS) SendMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);  // 台式/外接屏:DPMS 真关显示器
#if !LOCK
        _hookProc = HookCb;                                                                   // 拦 Win/菜单键,防熄屏期间弹开始菜单(口令/融合版才需要)
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
        if (_escTimer != null) _escTimer.Start();
#endif
    }
    void Wake() { if (Environment.TickCount - _baseline >= 500) Close(); }
#if !LOCK
    IntPtr HookCb(int code, IntPtr w, IntPtr l) {
        if (code >= 0) { int vk = Marshal.ReadInt32(l); if (vk == 0x5B || vk == 0x5C || vk == 0x5D) return (IntPtr)1; }  // LWin/RWin/Apps 吞掉
        return CallNextHookEx(_hook, code, w, l);
    }
#endif
#if !LOCK
    void EscTick(object s, EventArgs e) {
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0) { _escHeld += _escTimer.Interval; if (_escHeld >= 3000) Close(); }
        else { _escHeld -= _escTimer.Interval * 3; if (_escHeld < 0) _escHeld = 0; }   // 松手缓慢衰减,容许极短松动
    }
#endif
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (Environment.TickCount - _baseline >= 500) {
            if (_anyWake) { Close(); return true; }
#if !LOCK
            Keys k = keyData & Keys.KeyCode;
            char ch = '\0';
            if (k >= Keys.A && k <= Keys.Z) ch = (char)('a' + (int)(k - Keys.A));
            else if (k >= Keys.D0 && k <= Keys.D9) ch = (char)('0' + (int)(k - Keys.D0));
            else if (k >= Keys.NumPad0 && k <= Keys.NumPad9) ch = (char)('0' + (int)(k - Keys.NumPad0));
            if (ch != '\0') {
                _buf.Append(ch);
                if (_buf.Length > _pwd.Length) _buf.Remove(0, _buf.Length - _pwd.Length);
                if (_pwd.Length > 0 && _buf.Length == _pwd.Length && _buf.ToString() == _pwd) Close();
                return true;
            }
#endif
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
#if !LOCK
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        if (_escTimer != null) { _escTimer.Stop(); _escTimer.Dispose(); }
#endif
        foreach (Form c in _covers) { try { c.Close(); } catch { } }
        base.OnFormClosed(e);
    }
}

class CoverForm : Form {
    public CoverForm(Rectangle b) {
        FormBorderStyle = FormBorderStyle.None; BackColor = Color.Black;
        ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; Bounds = b;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; } }
}

#if !LOCK
static class RR {
    public static GraphicsPath Round(RectangleF r, float radius) {
        float d = radius * 2; GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

class RoundField : Panel {
    public float Radius = 11f;
    public Color Fill = Color.FromArgb(42, 46, 56);
    public Color Border = Color.FromArgb(58, 63, 75);
    public Color FocusColor = Color.FromArgb(79, 140, 255);
    public bool Active = false;
    public RoundField() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(BackColor);
        RectangleF r = new RectangleF(0.6f, 0.6f, Width - 1.7f, Height - 1.7f);
        using (GraphicsPath p = RR.Round(r, Radius)) {
            using (SolidBrush b = new SolidBrush(Fill)) e.Graphics.FillPath(b, p);
            using (Pen pen = new Pen(Active ? FocusColor : Border, Active ? 1.6f : 1.2f)) e.Graphics.DrawPath(pen, p);
        }
    }
}

class RoundButton : Button {
    public float Radius = 12f;
    public Color GradFrom = Color.FromArgb(36, 150, 158);
    public Color GradTo = Color.FromArgb(60, 98, 200);
    bool _h;
    public RoundButton() {
        DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent; FlatAppearance.MouseDownBackColor = Color.Transparent;
        SetStyle(ControlStyles.Selectable, false);
        MouseEnter += delegate { _h = true; Invalidate(); }; MouseLeave += delegate { _h = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(BackColor);
        RectangleF r = new RectangleF(0, 0, Width, Height);
        using (GraphicsPath p = RR.Round(r, Radius)) {
            using (LinearGradientBrush lgb = new LinearGradientBrush(r, GradFrom, GradTo, LinearGradientMode.Horizontal)) e.Graphics.FillPath(lgb, p);
            if (_h) using (SolidBrush ov = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) e.Graphics.FillPath(ov, p);
        }
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (SolidBrush br = new SolidBrush(ForeColor)) e.Graphics.DrawString(Text, Font, br, new RectangleF(0, 0, Width, Height), sf);
    }
}

// 深色圆角底座:标题/提示自绘 + 底部"作者 · GitHub"双链接 + 拖动 + DWM 圆角 + DPI
abstract class CardForm : Form {
    [DllImport("user32.dll")] protected static extern bool ReleaseCapture();
    [DllImport("user32.dll")] protected static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    protected const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
    const string AUTHOR_URL = "https://www.52pojie.cn/forum.php?mod=viewthread&tid=2111561";
    const string GITHUB_URL = "https://github.com/steen889/WuAiScreenOff";

    protected readonly float S;
    protected int R(float v) { return (int)Math.Round(v * S); }
    protected readonly Font fTitle = new Font("Microsoft YaHei UI", 16.5f, FontStyle.Bold);
    protected readonly Font fTip = new Font("Microsoft YaHei UI", 10f);
    protected readonly Font fLink = new Font("Microsoft YaHei UI", 8f);
    protected readonly Font fClose = new Font("Segoe UI", 12f);

    protected static readonly Color CARD = Color.FromArgb(32, 35, 43);
    protected static readonly Color TITLE = Color.FromArgb(198, 204, 213);
    protected static readonly Color TIP = Color.FromArgb(182, 189, 200);
    protected static readonly Color LINK = Color.FromArgb(122, 152, 255);
    protected static readonly Color LINK_H = Color.FromArgb(43, 210, 210);
    protected static readonly Color SEP = Color.FromArgb(96, 102, 112);
    protected static readonly Color CLOSE = Color.FromArgb(150, 157, 168);
    protected static readonly Color CLOSE_H = Color.FromArgb(240, 242, 245);

    string AuthorText { get { return Lang.T("吾爱破解 v1.3", "52pojie v1.3"); } }
    RectangleF _authRect, _ghRect, _closeRect;
    bool _overAuth, _overGh, _overClose;

    protected CardForm() {
        using (Graphics gg = Graphics.FromHwnd(IntPtr.Zero)) S = gg.DpiX / 96f;
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None; BackColor = CARD; ForeColor = TITLE; Font = fTip;
        DoubleBuffered = true; TopMost = true;
    }

    protected void DrawCenter(Graphics g, string s, Font f, Color c, int y) {
        using (StringFormat cen = new StringFormat { Alignment = StringAlignment.Center })
        using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, new RectangleF(0, y, ClientSize.Width, R(34)), cen);
    }
    // 底部:by 作者  ·  GitHub(整体居中,两段各自可点)
    protected void DrawChrome(Graphics g, int linkY) {
        string sep = "   ·   ";
        SizeF aw = g.MeasureString(AuthorText, fLink), sw = g.MeasureString(sep, fLink), gw = g.MeasureString("GitHub", fLink);
        float total = aw.Width + sw.Width + gw.Width, x = ClientSize.Width - total - R(14), y = ClientSize.Height - R(19);
        _authRect = new RectangleF(x, y, aw.Width, aw.Height);
        _ghRect = new RectangleF(x + aw.Width + sw.Width, y, gw.Width, gw.Height);
        using (SolidBrush ba = new SolidBrush(_overAuth ? LINK_H : LINK)) g.DrawString(AuthorText, fLink, ba, x, y);
        using (SolidBrush bs = new SolidBrush(SEP)) g.DrawString(sep, fLink, bs, x + aw.Width, y);
        using (SolidBrush bg = new SolidBrush(_overGh ? LINK_H : LINK)) g.DrawString("GitHub", fLink, bg, x + aw.Width + sw.Width, y);
        _closeRect = new RectangleF(ClientSize.Width - R(34), R(10), R(24), R(24));
        using (StringFormat cc = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (SolidBrush bc = new SolidBrush(_overClose ? CLOSE_H : CLOSE)) g.DrawString("✕", fClose, bc, _closeRect, cc);
        using (Font fpb = new Font("Microsoft YaHei UI", 7.5f))                                    // 左下角署名
        using (SolidBrush bpb = new SolidBrush(Color.FromArgb(86, 92, 102)))
            g.DrawString("Powered by Claude Code", fpb, bpb, R(10), ClientSize.Height - R(17));
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        bool oa = _authRect.Contains(e.Location), og = _ghRect.Contains(e.Location), oc = _closeRect.Contains(e.Location);
        if (oa != _overAuth || og != _overGh || oc != _overClose) {
            _overAuth = oa; _overGh = og; _overClose = oc;
            Cursor = (oa || og || oc) ? Cursors.Hand : Cursors.Default; Invalidate();
        }
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (_closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
        if (_authRect.Contains(e.Location)) { try { Process.Start(AUTHOR_URL); } catch { } return; }
        if (_ghRect.Contains(e.Location)) { try { Process.Start(GITHUB_URL); } catch { } return; }
        ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); BringToFront(); }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try { int pref = DWMWCP_ROUND; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4); } catch { }
    }
    protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
}

#if !PWD
// 选择界面:熄屏 / 口令
class SelectForm : CardForm {
    public bool AnyWake;
    public SelectForm() {
        ClientSize = new Size(R(440), R(232));
        RoundButton bLock = new RoundButton {
            Text = Lang.T("熄屏", "Blank"), Size = new Size(R(176), R(52)), Location = new Point(R(38), R(120)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            GradFrom = Color.FromArgb(86, 96, 112), GradTo = Color.FromArgb(70, 86, 130),
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        bLock.Click += delegate { AnyWake = true; DialogResult = DialogResult.OK; Close(); };
        RoundButton bPwd = new RoundButton {
            Text = Lang.T("口令", "Password"), Size = new Size(R(176), R(52)), Location = new Point(R(226), R(120)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        bPwd.Click += delegate { AnyWake = false; DialogResult = DialogResult.OK; Close(); };
        Controls.Add(bLock); Controls.Add(bPwd);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.AntiAlias;
        DrawCenter(g, Lang.T("吾爱熄屏", "WuAi ScreenOff"), fTitle, TITLE, R(30));
        DrawCenter(g, Lang.T("熄屏 = 动键鼠即亮   ·   口令 = 按对口令才亮", "Blank: any key / mouse wakes   ·   Password: only yours"), fTip, TIP, R(76));
        DrawChrome(g, 196);
    }
}
#endif

// 口令设置框
class SettingsForm : CardForm {
    readonly TextBox _box;
    readonly RoundField _field;
    public string Password { get; private set; }
    public SettingsForm() {
        ClientSize = new Size(R(420), R(292));
        _field = new RoundField { Location = new Point(R(30), R(134)), Size = new Size(R(360), R(50)), BackColor = CARD, Radius = 11f * S };
        _box = new TextBox {
            BorderStyle = BorderStyle.None, BackColor = _field.Fill, ForeColor = Color.FromArgb(238, 240, 244),
            Font = new Font("Microsoft YaHei UI", 14f), Location = new Point(R(18), R(13)), Size = new Size(R(324), R(26))
        };
        _box.KeyPress += delegate (object s, KeyPressEventArgs e) {
            char c = e.KeyChar;
            if (c != '\b' && !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) e.Handled = true;
        };
        _box.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; TrySave(); } };
        _box.GotFocus += delegate { _field.Active = true; _field.Invalidate(); };
        _box.LostFocus += delegate { _field.Active = false; _field.Invalidate(); };
        _field.Controls.Add(_box);
        RoundButton save = new RoundButton {
            Text = Lang.T("保存并熄屏", "Save & turn off"), Size = new Size(R(190), R(48)), Location = new Point(R(115), R(198)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        save.Click += delegate { TrySave(); };
        Controls.Add(_field); Controls.Add(save); AcceptButton = save;
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.AntiAlias;
        DrawCenter(g, Lang.T("吾爱熄屏", "WuAi ScreenOff"), fTitle, TITLE, R(28));
        DrawCenter(g, Lang.T("设置亮屏口令 · 熄屏后按它点亮 · 不分大小写", "Set a wake password · case-insensitive"), fTip, TIP, R(76));
        DrawCenter(g, Lang.T("字母 / 数字 · 长按 Esc 3 秒强制亮", "Letters / digits · hold Esc 3s to force wake"), fTip, TIP, R(98));
        DrawChrome(g, 254);
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); _box.Focus(); }
    void TrySave() {
        string t = _box.Text.Trim();
        if (t.Length == 0) { _field.Border = Color.FromArgb(248, 113, 113); _field.Active = false; _field.Invalidate(); _box.Focus(); return; }
        Password = t.ToLowerInvariant(); DialogResult = DialogResult.OK; Close();
    }
}
#endif
