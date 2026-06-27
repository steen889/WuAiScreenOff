// ============================================================
//  吾爱熄屏 系列 · WuAi ScreenOff · v2.0 · C# 源码 (.NET Framework 4)
// ------------------------------------------------------------
//  纯 DPMS 物理关屏,无任何可见黑窗。隐藏控制窗(屏外1x1不可见) + 全局键鼠钩子承载保护逻辑。
//  /define:LOCK → 吾爱熄屏(纯熄屏挂机:关屏+阻睡眠,动键鼠即亮即退,无界面)
//  /define:PWD  → 吾爱熄屏口令版(熄屏+乱按保持黑;盲打口令/长按Esc解锁;无系统锁屏)
//  (默认)        → 吾爱熄屏融合版(选择:纯熄屏 / 锁屏 / 休眠,回车默认纯熄屏)
//  v3.3 关键修正(经 Gemini×Codex 双子审):
//   1. 关屏/催亮一律用 PostMessage(异步,自有隐藏窗 Handle),绝不在 Timer 里同步 SendMessage —— 防 UI 线程阻塞致低级钩子超时被系统摘钩。
//   2. 防误触期(熄屏后1秒)无条件低频重发关屏压黑(节流700ms),不赌"复亮一定伴随键鼠钩子事件" —— 覆盖驱动/电源策略导致的无输入复亮;1秒后才放行真实唤醒。
//   3. 钩子过滤注入输入(LLMHF/LLKHF_INJECTED) —— 防 Nudge 自激与第三方模拟输入误唤醒。
//   4. 鼠标移动需 >8px 才算唤醒 —— 防显示器唤醒时光标重对齐的虚假微动误退。
//   5. 钩子安装失败 → 催亮退出/拒进黑屏,绝不把用户困死。
//   6. 口令版监听 SessionLock(Ctrl+Alt+Del/Win+L) → 退出让出,作独立逃生路径。
//  阻睡眠 = ES_CONTINUOUS|ES_SYSTEM_REQUIRED;绝不加 ES_DISPLAY_REQUIRED(否则关不掉屏)。
//  编译见 编译.bat(csc,无需 System.Management)。
// ============================================================

using System;
using System.Text;
using System.Globalization;
using System.Windows.Forms;
using System.Runtime.InteropServices;
#if !LOCK
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
#endif

[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyTitle("吾爱熄屏 · WuAi ScreenOff")]
[assembly: System.Reflection.AssemblyProduct("吾爱熄屏 WuAi ScreenOff v2.0")]

#if !LOCK
static class Lang {
    public static readonly bool ZH = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
    public static string T(string zh, string en) { return ZH ? zh : en; }
}
#endif
#if !LOCK && !PWD
enum Mode { Blank, Password, Lock, Hibernate, Shutdown, Restart }
#endif

static class ScreenOff {
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint f);
    [DllImport("user32.dll")]   static extern bool SetProcessDPIAware();
#if !LOCK && !PWD
    [DllImport("user32.dll")]   static extern bool LockWorkStation();
    [DllImport("user32.dll")]   static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr res);
#endif
    const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x00000001;
#if !LOCK
    public const string REG_PATH =
  #if PWD
        "Software\\WuAiScreenOff_Password";
  #else
        "Software\\WuAiScreenOff_Combo";
  #endif
#endif

    [STAThread]
    static void Main() {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
#if LOCK
        RunBlank(null, true);
#elif PWD
        string pwd = LoadOrSetPwd();
        if (pwd == null) return;
        RunBlank(pwd, false);
#else
        SelectForm sel = new SelectForm();
        Application.Run(sel);
        if (sel.Cancelled) return;
        Mode mode = sel.SelectedMode;
        switch (mode) {
            case Mode.Blank: RunBlank(null, true); break;
            case Mode.Password:
                string pwd = LoadOrSetPwd();
                if (pwd != null) RunBlank(pwd, false);
                break;
            case Mode.Lock:      DoLock();      break;
            case Mode.Hibernate: DoHibernate(); break;
            case Mode.Shutdown:  DoShutdown();  break;
            case Mode.Restart:   DoRestart();   break;
        }
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

    static void RunBlank(string pwd, bool anyWake) {
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        AppDomain.CurrentDomain.ProcessExit += delegate { SetThreadExecutionState(ES_CONTINUOUS); };
        try { Application.Run(new BlankForm(pwd, anyWake)); }
        finally { SetThreadExecutionState(ES_CONTINUOUS); }
    }

#if !LOCK && !PWD
    static void DoLock() {
        try { LockWorkStation(); } catch { }
        System.Threading.Thread.Sleep(400);
        IntPtr r; try { SendMessageTimeout((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF170, (IntPtr)2, 0x0002, 1000, out r); } catch { }
    }
    static void DoHibernate() {
        IntPtr r; try { SendMessageTimeout((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF170, (IntPtr)2, 0x0002, 1000, out r); } catch { }
        try { Application.SetSuspendState(PowerState.Hibernate, true, false); } catch { }
    }
    static void DoShutdown() { try { Process.Start("shutdown", "/s /t 0"); } catch { } }
    static void DoRestart() { try { Process.Start("shutdown", "/r /t 0"); } catch { } }
#endif
}

// 隐藏控制窗:屏外 1x1、不可见、不抢焦点;只用来发 DPMS(PostMessage 到自有 Handle)+承载 Timer。键鼠靠全局钩子。
class BlankForm : Form {
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
#if !LOCK
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inp, int cbSize);
#endif
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr mod, uint tid);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
    delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
#if !LOCK
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public MOUSEINPUT mi; }
#endif
    const int WM_SYSCOMMAND = 0x0112, SC_MONITORPOWER = 0xF170;
#if !LOCK
    const uint MOUSEEVENTF_MOVE = 0x0001;
#endif
    const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_MOUSEMOVE = 0x200;
    const int LLKHF_INJECTED = 0x10, LLMHF_INJECTED = 0x01;
    const int GUARD_MS = 1000;           // 防误触期:熄屏后 1 秒内只压黑、不响应唤醒/解锁
    const int GUARD_REBLANK = 700;       // 防误触期重发关屏节流(温和压黑,避免 IRP 风暴)
    const int MOVE_THRESHOLD = 8;        // 鼠标移动 >8px 才算唤醒(滤掉光标重对齐微动)

    readonly bool _anyWake;
    int _baseTick, _unlockTick, _lastBlankTick = int.MinValue, _lastMx = int.MinValue, _lastMy;
    volatile bool _activity, _unlocking;
    IntPtr _kbHook = IntPtr.Zero, _msHook = IntPtr.Zero;
    HookProc _kbProc, _msProc;
    Timer _timer;
#if !LOCK
    readonly System.Collections.Generic.List<Form> _covers = new System.Collections.Generic.List<Form>();
#endif
#if !LOCK
    const int ESC_HOLD = 3000, REBLANK_THROTTLE = 250, VK_ESCAPE = 0x1B;   // 乱按重关节流250ms:背光亮一下立即压黑(背光时间最短),黑窗已保证不露桌面
    // 口令版黑窗的 DPMS 关屏级别(两版对照,只此一处不同):2=全关 / 0=不发DPMS纯黑窗。纯熄屏(_anyWake)恒=2不受此控。
  #if PWD_NODPMS
    const int DPMS_OFF = 0;
  #else
    const int DPMS_OFF = 2;
  #endif
    readonly string _pwd;
    readonly StringBuilder _buf = new StringBuilder();
    int _escDownTick;
    volatile bool _unlockReq, _allowClose;
#endif

    public BlankForm(string pwd, bool anyWake) {
        _anyWake = anyWake;
#if !LOCK
        _pwd = (pwd ?? "").ToLowerInvariant();
#endif
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        if (_anyWake) { Bounds = new System.Drawing.Rectangle(-32000, -32000, 1, 1); }                          // 纯熄屏:屏外隐藏控制窗(无遮挡,动则亮退)
#if !LOCK
        else { BackColor = Color.Black; TopMost = true; Bounds = Screen.PrimaryScreen.Bounds; }                 // 口令版:主屏全屏黑窗遮挡(乱按时看到黑、不露桌面)
#endif
    }
    protected override bool ShowWithoutActivation { get { return _anyWake; } }                                  // 纯熄屏不抢焦点;口令版要激活置顶遮挡
    protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= _anyWake ? (0x80 | 0x08000000) : 0x00000008; return cp; } }  // 纯熄屏TOOLWINDOW|NOACTIVATE;口令版TOPMOST

    void MonitorOff() {                                                                                                    // 异步发自有 Handle,不阻塞 UI 线程(防低级钩子超时摘钩);视觉黑靠黑窗遮挡
        if (!IsHandleCreated) return;
#if LOCK
        PostMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
#else
        int lv = _anyWake ? 2 : DPMS_OFF;                                                                                  // 纯熄屏恒=2(无黑窗,全靠DPMS);口令版黑窗按编译版本(2/1/0)
        if (lv > 0) PostMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)lv);                               // lv==0=纯黑窗模式,根本不发DPMS
#endif
    }
    void MonitorOn() {
        if (!IsHandleCreated) return;
#if !LOCK
        if (!_anyWake && DPMS_OFF == 0) return;                                                                            // 纯黑窗模式从没关过屏,不发On(免无谓信号重同步)
#endif
        PostMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)(-1));
    }
#if !LOCK
    void Nudge() {
        try {
            INPUT[] inp = new INPUT[2];
            inp[0].type = 0; inp[0].mi.dx = 1;  inp[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            inp[1].type = 0; inp[1].mi.dx = -1; inp[1].mi.dwFlags = MOUSEEVENTF_MOVE;
            SendInput(2, inp, Marshal.SizeOf(typeof(INPUT)));
        } catch { }
    }
#endif

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        _baseTick = Environment.TickCount;
#if !LOCK
        if (!_anyWake) {                                                                              // 口令版:先把所有屏铺黑窗、确保黑帧已绘制,再关 DPMS(否则唤醒瞬间露旧桌面帧)
            foreach (Screen s in Screen.AllScreens) if (!s.Primary) { CoverForm c = new CoverForm(s.Bounds); c.Show(); _covers.Add(c); }
            Activate(); BringToFront(); Update();
        }
#endif
        IntPtr mod = GetModuleHandle(null);
        _kbProc = KbProc; _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, mod, 0);
        _msProc = MsProc; _msHook = SetWindowsHookEx(WH_MOUSE_LL, _msProc, mod, 0);
        bool kbOk = _kbHook != IntPtr.Zero, msOk = _msHook != IntPtr.Zero;
#if !LOCK
        if (!_anyWake && (!kbOk || !msOk)) { CloseCovers(); MonitorOn(); _allowClose = true; Close(); return; }   // 口令版需键鼠钩子俱全(缺鼠标=能操作电脑,缺键盘=无法解锁),拒进
        if (!_anyWake) { try { SystemEvents.SessionSwitch += OnSessionSwitch; } catch { } }
#endif
        if (_anyWake && !kbOk && !msOk) { MonitorOn(); Close(); return; }                             // 纯熄屏键鼠钩子全失败=无法唤醒,退出
#if !LOCK
        if (!_anyWake) Cursor.Hide();                                                                 // 口令版:钩子确认成功后隐藏光标(防黑窗上显示不动的光标)
#endif
        MonitorOff();                                                                                 // 黑窗就位 + 钩子装好后,最后才关 DPMS
        _timer = new Timer(); _timer.Interval = 50; _timer.Tick += Tick; _timer.Start();
    }

    // 钩子回调:只设标志/累积口令(轻量),过滤注入输入;绝不在此发 DPMS(放 Timer)避免回调超时被摘钩
    IntPtr KbProc(int code, IntPtr w, IntPtr l) {
        if (code >= 0) {
            int flags = Marshal.ReadInt32(l, 8);                                                      // KBDLLHOOKSTRUCT.flags @8
            bool injected = (flags & LLKHF_INJECTED) != 0;
            int vk = Marshal.ReadInt32(l, 0); int msg = (int)w;
            if (_anyWake) { if (!injected && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)) _activity = true; }
#if !LOCK
            else {                                                                                    // 口令模式:吞掉所有按键(含注入);仅真实键参与口令/逃生
                if (!injected) _activity = true;
                if (vk == 0x5B || vk == 0x5C || vk == 0x5D) return (IntPtr)1;                          // 吞 Win/Apps
                if (vk == VK_ESCAPE) {
                    if (!injected) {                                                                   // 注入的 Esc 不算逃生
                        if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) { if (_escDownTick == 0) _escDownTick = Environment.TickCount; }
                        else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) _escDownTick = 0;
                    }
                    return (IntPtr)1;
                }
                bool guard = unchecked(Environment.TickCount - _baseTick) < GUARD_MS;
                if (!injected && !guard && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)) {              // 真实键、防误触期外才累积口令(防模拟输入口令/防余键污染)
                    char ch = MapKey(vk);
                    if (ch != '\0') {
                        _buf.Append(ch);
                        if (_buf.Length > _pwd.Length) _buf.Remove(0, _buf.Length - _pwd.Length);
                        if (_pwd.Length > 0 && _buf.ToString() == _pwd) _unlockReq = true;
                    }
                }
                return (IntPtr)1;                                                                      // 吞掉所有按键(含注入:锁键盘+防模拟+防漏入前台)
            }
#endif
        }
        return CallNextHookEx(_kbHook, code, w, l);
    }
    IntPtr MsProc(int code, IntPtr w, IntPtr l) {
        if (code >= 0) {
            int flags = Marshal.ReadInt32(l, 12);                                                      // MSLLHOOKSTRUCT.flags @12
            if ((flags & LLMHF_INJECTED) == 0) {                                                       // 仅真实鼠标计入唤醒(滤注入/Nudge)
                int msg = (int)w;
                if (msg == WM_MOUSEMOVE) {
                    int mx = Marshal.ReadInt32(l, 0), my = Marshal.ReadInt32(l, 4);
                    if (_lastMx == int.MinValue) { _lastMx = mx; _lastMy = my; }
                    else if (Math.Abs(mx - _lastMx) > MOVE_THRESHOLD || Math.Abs(my - _lastMy) > MOVE_THRESHOLD) { _activity = true; _lastMx = mx; _lastMy = my; }
                } else { _activity = true; }                                                           // 鼠标键/滚轮 → 唤醒
            }
#if !LOCK
            if (!_anyWake && !_unlocking) return (IntPtr)1;                                            // 口令模式:吞掉所有鼠标(真实+注入,不能操作电脑);解锁中放行让 Nudge 催亮
#endif
        }
        return CallNextHookEx(_msHook, code, w, l);
    }
#if !LOCK
    static char MapKey(int vk) {
        if (vk >= 0x41 && vk <= 0x5A) return (char)('a' + (vk - 0x41));
        if (vk >= 0x30 && vk <= 0x39) return (char)('0' + (vk - 0x30));
        if (vk >= 0x60 && vk <= 0x69) return (char)('0' + (vk - 0x60));
        return '\0';
    }
    void OnSessionSwitch(object s, SessionSwitchEventArgs e) {                                          // Win+L / Ctrl+Alt+Del → 退出让出(逃生 + 不黑掉登录界面)
        if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.SessionLogoff) {
            _allowClose = true; if (_timer != null) _timer.Stop(); CloseSelf();
        }
    }
#endif

    void Tick(object s, EventArgs e) {
        int now = Environment.TickCount;
        if (_unlocking) {
#if LOCK
            _unlocking = false; UnhookAll(); MonitorOn(); _timer.Stop(); CloseSelf();
#else
            _unlocking = false; UnhookAll(); CloseCovers();
            if (!_anyWake) { try { Cursor.Show(); } catch { } Hide(); }
            MonitorOn(); Nudge(); _timer.Stop(); CloseSelf();
#endif
            return;
        }
        bool guard = unchecked(now - _baseTick) < GUARD_MS;
        if (guard) {                                                                                   // 防误触期:无条件低频重发压黑(覆盖无输入复亮),不响应唤醒/解锁
            if (_lastBlankTick == int.MinValue || unchecked(now - _lastBlankTick) >= GUARD_REBLANK) { _lastBlankTick = now; MonitorOff(); }
            _activity = false;
            return;
        }
        if (_anyWake) { if (_activity) BeginWake(); return; }                                          // 纯熄屏:真实键鼠 → 催亮退出
#if !LOCK
        if (_unlockReq) { BeginWake(); return; }                                                       // 口令对 → 解锁
        if (_escDownTick != 0 && unchecked(now - _escDownTick) >= ESC_HOLD) { BeginWake(); return; }   // 长按 Esc 3 秒
        if (_activity) {                                                                               // 乱按 → 重关压黑(1 秒节流)
            _activity = false;
            if (_lastBlankTick == int.MinValue || unchecked(now - _lastBlankTick) >= REBLANK_THROTTLE) { _lastBlankTick = now; MonitorOff(); }
        }
#endif
    }

    void BeginWake() {
        _unlocking = true; _unlockTick = Environment.TickCount;
#if !LOCK
        _allowClose = true;
#endif
    }
    void CloseSelf() { try { Close(); } catch { } }
#if !LOCK
    void CloseCovers() { foreach (Form c in _covers) { try { c.Close(); } catch { } } _covers.Clear(); }
#endif

#if !LOCK
    protected override void OnFormClosing(FormClosingEventArgs e) {
        if (!_allowClose && !_anyWake && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }
#endif
    void UnhookAll() {
        if (_kbHook != IntPtr.Zero) { UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
        if (_msHook != IntPtr.Zero) { UnhookWindowsHookEx(_msHook); _msHook = IntPtr.Zero; }
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
#if !LOCK
        if (!_anyWake) { try { SystemEvents.SessionSwitch -= OnSessionSwitch; } catch { } try { Cursor.Show(); } catch { } }   // 保险:恢复光标
#endif
        if (_timer != null) { _timer.Stop(); _timer.Dispose(); }
        UnhookAll();
#if !LOCK
        CloseCovers();
#endif
        _kbProc = null; _msProc = null;
        base.OnFormClosed(e);
    }
}

#if !LOCK
// 副屏黑遮挡窗(口令版):纯黑、置顶、不抢焦点;键鼠靠主窗的全局钩子
class CoverForm : Form {
    public CoverForm(System.Drawing.Rectangle b) {
        FormBorderStyle = FormBorderStyle.None; BackColor = Color.Black; ShowInTaskbar = false;
        TopMost = true; StartPosition = FormStartPosition.Manual; Bounds = b;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; } }  // NOACTIVATE
}
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

abstract class CardForm : Form {
    [DllImport("user32.dll")] protected static extern bool ReleaseCapture();
    [DllImport("user32.dll")] protected static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    protected const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
    const string AUTHOR_URL = "https://www.52pojie.cn/thread-2111561-1-1.html";
    const string GITHUB_URL = "https://github.com/steen889/WuAiScreenOff";

    protected readonly float S;
    protected int R(float v) { return (int)Math.Round(v * S); }
    protected readonly Font fTitle = new Font("Microsoft YaHei UI", 16.5f, FontStyle.Bold);
    protected readonly Font fTip = new Font("Microsoft YaHei UI", 10f);
    protected readonly Font fLink = new Font("Microsoft YaHei UI", 7.5f);
    protected readonly Font fClose = new Font("Segoe UI", 12f);

    protected static readonly Color CARD = Color.FromArgb(32, 35, 43);
    protected static readonly Color TITLE = Color.FromArgb(198, 204, 213);
    protected static readonly Color TIP = Color.FromArgb(182, 189, 200);
    protected static readonly Color LINK = Color.FromArgb(130, 140, 162);     // 低调柔和灰蓝,小字无下划线
    protected static readonly Color LINK_H = Color.FromArgb(150, 196, 210);   // hover 柔和青,不刺眼
    protected static readonly Color SEP = Color.FromArgb(96, 102, 112);
    protected static readonly Color CLOSE = Color.FromArgb(150, 157, 168);
    protected static readonly Color CLOSE_H = Color.FromArgb(240, 242, 245);

    string AuthorText { get { return Lang.T("吾爱破解 v2.0", "52pojie v2.0"); } }
    RectangleF _authRect, _ghRect, _closeRect, _minRect;
    bool _overAuth, _overGh, _overClose, _overMin;
    protected bool ShowMinimize;

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
    protected void DrawChrome(Graphics g) {
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
        if (ShowMinimize) {
            _minRect = new RectangleF(ClientSize.Width - R(62), R(10), R(24), R(24));
            using (StringFormat mc = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (SolidBrush bm = new SolidBrush(_overMin ? CLOSE_H : CLOSE)) g.DrawString("─", fClose, bm, _minRect, mc);
        }
        using (Font fpb = new Font("Microsoft YaHei UI", 7.5f))
        using (SolidBrush bpb = new SolidBrush(Color.FromArgb(86, 92, 102)))
            g.DrawString("Powered by Claude Code", fpb, bpb, R(10), ClientSize.Height - R(17));
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        bool oa = _authRect.Contains(e.Location), og = _ghRect.Contains(e.Location), oc = _closeRect.Contains(e.Location);
        bool om = ShowMinimize && _minRect.Contains(e.Location);
        if (oa != _overAuth || og != _overGh || oc != _overClose || om != _overMin) {
            _overAuth = oa; _overGh = og; _overClose = oc; _overMin = om;
            Cursor = (oa || og || oc || om) ? Cursors.Hand : Cursors.Default; Invalidate();
        }
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (_closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
        if (ShowMinimize && _minRect.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
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

// 口令设置框。回车保存走 ProcessCmdKey(避免 KeyPress 把 Enter 当非法字符吞掉)
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
            if (c != '\b' && !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) e.Handled = true;   // 只挡字符输入,回车由 ProcessCmdKey 处理
        };
        _box.GotFocus += delegate { _field.Active = true; _field.Invalidate(); };
        _box.LostFocus += delegate { _field.Active = false; _field.Invalidate(); };
        _field.Controls.Add(_box);
        RoundButton save = new RoundButton {
            Text = Lang.T("保存并熄屏", "Save & turn off"), Size = new Size(R(190), R(48)), Location = new Point(R(115), R(198)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        save.Click += delegate { TrySave(); };
        Controls.Add(_field); Controls.Add(save);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == Keys.Enter)  { TrySave(); return true; }
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.AntiAlias;
        DrawCenter(g, Lang.T("吾爱熄屏", "WuAi ScreenOff"), fTitle, TITLE, R(28));
        DrawCenter(g, Lang.T("设置亮屏口令 · 熄屏后盲打它点亮 · 不分大小写", "Set a wake password · case-insensitive"), fTip, TIP, R(76));
        DrawCenter(g, Lang.T("字母 / 数字 · 回车保存 · 长按 Esc 3 秒强制亮", "Letters / digits · Enter to save · hold Esc 3s to wake"), fTip, TIP, R(98));
        DrawChrome(g);
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e); _box.Focus(); }
    void TrySave() {
        string t = _box.Text.Trim();
        if (t.Length == 0) { _field.Border = Color.FromArgb(248, 113, 113); _field.Active = false; _field.Invalidate(); _box.Focus(); return; }
        Password = t.ToLowerInvariant(); DialogResult = DialogResult.OK; Close();
    }
}
#endif

#if !LOCK && !PWD
class DarkMenuTable : ProfessionalColorTable {
    public override Color MenuItemSelected { get { return Color.FromArgb(58,63,75); } }
    public override Color MenuItemBorder { get { return Color.FromArgb(58,63,75); } }
    public override Color ToolStripDropDownBackground { get { return Color.FromArgb(38,42,52); } }
    public override Color ImageMarginGradientBegin { get { return Color.FromArgb(38,42,52); } }
    public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(38,42,52); } }
    public override Color ImageMarginGradientEnd { get { return Color.FromArgb(38,42,52); } }
    public override Color MenuBorder { get { return Color.FromArgb(58,63,75); } }
}

class SelectForm : CardForm {
    public Mode SelectedMode = Mode.Blank;
    public bool Cancelled = true;

    int _funcIdx;
    RoundButton _funcBtn;
    RoundField _timeField;
    TextBox _timeBox;
    Label _lblMode, _lblTime;
    Timer _countTimer;
    Mode _timedMode;
    int _remainSec;
    bool _counting, _exiting;
    NotifyIcon _tray;
    Control[] _initCtrls, _countCtrls;

    static readonly string[] FN = { "纯熄屏", "口令熄屏", "锁屏", "休眠", "关机", "重启" };
    static readonly string[] FE = { "Screen off", "Password", "Lock", "Hibernate", "Shutdown", "Restart" };
    static readonly Mode[] FM = { Mode.Blank, Mode.Password, Mode.Lock, Mode.Hibernate, Mode.Shutdown, Mode.Restart };

    static ContextMenuStrip MkDarkMenu() {
        return new ContextMenuStrip {
            ShowImageMargin = false, Font = new Font("Microsoft YaHei UI", 10f),
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuTable())
        };
    }

    public SelectForm() {
        ShowMinimize = true; ShowInTaskbar = true; MinimizeBox = true;
        ClientSize = new Size(R(500), R(330));
        int bw = R(76), bh = R(52), gap = R(16), y1 = R(110);
        int x0 = (ClientSize.Width - 5 * bw - 4 * gap) / 2;
        var b0 = MkBtn("口令","Password", x0,                Color.FromArgb(36,150,158), Color.FromArgb(42,120,130), Mode.Password, y1, bw, bh);
        var b1 = MkBtn("锁屏","Lock",     x0 + bw + gap,     Color.FromArgb(198,120,58), Color.FromArgb(176,88,48),  Mode.Lock,     y1, bw, bh);
        var b2 = MkBtn("休眠","Hibernate", x0 + 2*(bw+gap),  Color.FromArgb(124,92,174), Color.FromArgb(92,70,150),  Mode.Hibernate,y1, bw, bh);
        var b3 = MkBtn("关机","Shutdown",  x0 + 3*(bw+gap),  Color.FromArgb(204,68,68),  Color.FromArgb(160,50,50),  Mode.Shutdown, y1, bw, bh);
        var b4 = MkBtn("重启","Restart",   x0 + 4*(bw+gap),  Color.FromArgb(68,120,204), Color.FromArgb(50,88,170),  Mode.Restart,  y1, bw, bh);

        _funcIdx = 0;
        _funcBtn = new RoundButton {
            Text = (Lang.ZH ? FN[0] : FE[0]) + "  ▾", Size = new Size(R(150), R(40)), Location = new Point(R(30), R(204)),
            BackColor = CARD, ForeColor = Color.FromArgb(218,222,230), Radius = 10f * S,
            GradFrom = Color.FromArgb(52,56,68), GradTo = Color.FromArgb(44,48,58),
            Font = new Font("Microsoft YaHei UI", 10.5f), Cursor = Cursors.Hand
        };
        ContextMenuStrip funcMenu = MkDarkMenu();
        for (int i = 0; i < FN.Length; i++) {
            int idx = i;
            var item = funcMenu.Items.Add(Lang.ZH ? FN[i] : FE[i]);
            item.ForeColor = Color.FromArgb(218,222,230);
            item.Click += delegate { _funcIdx = idx; _funcBtn.Text = (Lang.ZH ? FN[idx] : FE[idx]) + "  ▾"; };
        }
        _funcBtn.Click += delegate { funcMenu.Show(_funcBtn, new Point(0, _funcBtn.Height + 2)); };

        _timeField = new RoundField { Location = new Point(R(196), R(204)), Size = new Size(R(80), R(40)), BackColor = CARD, Radius = 10f * S };
        _timeBox = new TextBox {
            BorderStyle = BorderStyle.None, BackColor = _timeField.Fill, ForeColor = Color.FromArgb(238,240,244),
            Font = new Font("Microsoft YaHei UI", 13f), Text = "1", TextAlign = HorizontalAlignment.Center,
            Location = new Point(R(6), R(8)), Size = new Size(R(46), R(24))
        };
        _timeBox.KeyPress += delegate(object s, KeyPressEventArgs ev) { if (ev.KeyChar != '\b' && !char.IsDigit(ev.KeyChar)) ev.Handled = true; };
        _timeBox.GotFocus += delegate { _timeField.Active = true; _timeField.Invalidate(); };
        _timeBox.LostFocus += delegate { _timeField.Active = false; _timeField.Invalidate(); };
        Label ta = new Label {
            Text = "▾", AutoSize = false, Size = new Size(R(20), R(24)), Location = new Point(R(54), R(8)),
            ForeColor = Color.FromArgb(140,148,168), BackColor = _timeField.Fill,
            Font = new Font("Segoe UI", 10f), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand
        };
        ContextMenuStrip timeMenu = MkDarkMenu();
        foreach (int t in new[] { 1, 3, 5, 10, 30, 60 }) {
            int val = t;
            var item = timeMenu.Items.Add(t + Lang.T(" 分钟"," min"));
            item.ForeColor = Color.FromArgb(218,222,230);
            item.Click += delegate { _timeBox.Text = val.ToString(); };
        }
        ta.Click += delegate { timeMenu.Show(_timeField, new Point(0, _timeField.Height + 2)); };
        _timeField.Controls.Add(_timeBox); _timeField.Controls.Add(ta);

        Label ml = new Label { Text = Lang.T("分钟","min"), AutoSize = true, Location = new Point(R(284), R(214)),
            ForeColor = TIP, BackColor = CARD, Font = new Font("Microsoft YaHei UI", 10f) };

        RoundButton sb = new RoundButton {
            Text = Lang.T("开始","Start"), Size = new Size(R(86), R(40)), Location = new Point(R(380), R(204)),
            BackColor = CARD, ForeColor = Color.White, Radius = 10f * S,
            GradFrom = Color.FromArgb(56,142,60), GradTo = Color.FromArgb(46,125,50),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        sb.Click += delegate { StartTimer(); };

        _initCtrls = new Control[] { b0, b1, b2, b3, b4, _funcBtn, _timeField, ml, sb };

        _lblMode = new Label { TextAlign = ContentAlignment.MiddleCenter, AutoSize = false,
            Location = new Point(0, R(80)), Size = new Size(ClientSize.Width, R(40)),
            ForeColor = TIP, Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold), BackColor = CARD };
        _lblTime = new Label { TextAlign = ContentAlignment.MiddleCenter, AutoSize = false,
            Location = new Point(0, R(130)), Size = new Size(ClientSize.Width, R(80)),
            ForeColor = Color.FromArgb(240,245,250), Font = new Font("Consolas", 48f, FontStyle.Bold), BackColor = CARD };
        RoundButton cb = new RoundButton {
            Text = Lang.T("修改","Modify"), Size = new Size(R(110), R(46)), Location = new Point(R(115), R(240)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            GradFrom = Color.FromArgb(120,120,130), GradTo = Color.FromArgb(90,90,100),
            Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        cb.Click += delegate { CancelTimer(); };
        RoundButton mb = new RoundButton {
            Text = Lang.T("执行","Run"), Size = new Size(R(110), R(46)), Location = new Point(R(275), R(240)),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S,
            GradFrom = Color.FromArgb(56,142,60), GradTo = Color.FromArgb(46,125,50),
            Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        mb.Click += delegate { if (_counting) { StopTick(); Execute(_timedMode); } };

        _countCtrls = new Control[] { _lblMode, _lblTime, cb, mb };
        foreach (var c in _countCtrls) c.Visible = false;
        Controls.AddRange(_initCtrls); Controls.AddRange(_countCtrls);

        _tray = new NotifyIcon { Visible = false, Text = Lang.T("吾爱熄屏","WuAi ScreenOff") };
        try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) { Show(); WindowState = FormWindowState.Normal; Activate(); } };
        ContextMenuStrip cm = MkDarkMenu();
        var ci1 = cm.Items.Add(Lang.T("取消定时","Cancel"), null, delegate { CancelTimer(); }); ci1.ForeColor = Color.FromArgb(218,222,230);
        var ci2 = cm.Items.Add(Lang.T("立即执行","Now"), null, delegate { if (_counting) { StopTick(); Execute(_timedMode); } }); ci2.ForeColor = Color.FromArgb(218,222,230);
        _tray.ContextMenuStrip = cm;
    }

    RoundButton MkBtn(string zh, string en, int x, Color from, Color to, Mode m, int y, int w, int h) {
        RoundButton b = new RoundButton {
            Text = Lang.T(zh, en), Size = new Size(w, h), Location = new Point(x, y),
            BackColor = CARD, ForeColor = Color.White, Radius = 12f * S, GradFrom = from, GradTo = to,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), Cursor = Cursors.Hand
        };
        b.Click += delegate { Execute(m); };
        return b;
    }

    void Execute(Mode m) { _exiting = true; Cancelled = false; SelectedMode = m; StopTick(); KillTray(); Location = new Point(-32000, -32000); Close(); }

    void StartTimer() {
        int min; if (!int.TryParse(_timeBox.Text.Trim(), out min) || min < 1) return;
        _timedMode = FM[_funcIdx]; _remainSec = min * 60; _counting = true;
        _lblMode.Text = Lang.ZH ? FN[_funcIdx] : FE[_funcIdx];
        _lblTime.Text = Fmt(_remainSec);
        foreach (var c in _initCtrls) c.Visible = false;
        foreach (var c in _countCtrls) c.Visible = true;
        _tray.Visible = true; _tray.Text = _lblMode.Text + " · " + Fmt(_remainSec);
        _countTimer = new Timer { Interval = 1000 };
        _countTimer.Tick += delegate {
            if (--_remainSec <= 0) { Execute(_timedMode); return; }
            _lblTime.Text = Fmt(_remainSec); _tray.Text = _lblMode.Text + " · " + Fmt(_remainSec);
        };
        _countTimer.Start(); Invalidate();
    }

    void CancelTimer() {
        StopTick(); _counting = false;
        foreach (var c in _countCtrls) c.Visible = false;
        foreach (var c in _initCtrls) c.Visible = true;
        _tray.Visible = false; Show(); WindowState = FormWindowState.Normal; Activate(); Invalidate();
    }

    void StopTick() { if (_countTimer != null) { _countTimer.Stop(); _countTimer.Dispose(); _countTimer = null; } }
    void KillTray() { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } }
    static string Fmt(int s) { return string.Format("{0:D2}:{1:D2}", s / 60, s % 60); }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == Keys.Enter) { if (!_counting) Execute(Mode.Blank); return true; }
        if (keyData == Keys.Escape) { if (_counting) { CancelTimer(); return true; } Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.TextRenderingHint = TextRenderingHint.AntiAlias;
        DrawCenter(g, Lang.T("吾爱熄屏","WuAi ScreenOff"), fTitle, TITLE, R(28));
        if (!_counting) {
            DrawCenter(g, Lang.T("回车 = 熄屏","Enter = Screen off"), fTip, TIP, R(72));
            using (Pen sp = new Pen(SEP, 1f)) g.DrawLine(sp, R(30), R(186), ClientSize.Width - R(30), R(186));
        }
        DrawChrome(g);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        if (_counting && !_exiting) { e.Cancel = true; Hide(); return; }
        base.OnFormClosing(e); StopTick(); KillTray();
    }
}
#endif
