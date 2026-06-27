# WuAi ScreenOff · 吾爱熄屏

> A tiny Windows screen-off utility (single file, portable, offline) — **no lock, no sleep**. Saves power while background tasks keep running; wake straight back to the desktop, no system password needed.

![version](https://img.shields.io/badge/version-2.0-blue)
![platform](https://img.shields.io/badge/Windows-7%20~%2011-0078d6)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.0-512bd4)
![size](https://img.shields.io/badge/size-single%20file-green)
![license](https://img.shields.io/badge/license-MIT-green)

[简体中文](README.md) ｜ **English**

---

## What is this

A tiny Windows screen-off utility (single file, portable, offline). Unlike the system "Lock" — it **only turns the screen off, it does not lock the PC**: background downloads / renders / remote sessions / AFK tasks keep running, and on wake you go **straight back to the desktop with no password to re-enter**. The UI auto-switches CN / EN by system language.

<img width="525" height="365" alt="WuAi ScreenOff" src="https://github.com/user-attachments/assets/99ce02d8-0c25-4546-a31f-9d33a206fbd1" />

## Why password mode flickers on key mashing

Real screen-off requires **DPMS** — sending the monitor a power-off signal (`SC_MONITORPOWER=2`) to physically cut the backlight. A black overlay window looks black but the backlight stays on — no power saved.

Once DPMS is off, wake is decided by the **display driver / hardware power layer**: as long as the system is awake, any key/mouse activity is treated as "user is back" and the screen lights up instantly — software cannot block it. Password mode detects the wake and immediately re-sends the power-off to push it black. So "mash → flash for an instant → pushed black" loops — **that flash is physically inevitable, not a bug**.

Flicker is throttled to single-frame, 200 ms merging; after unlock the tool holds black for 200 ms to absorb in-flight off commands, then fires exactly one wake — no light/dark tug-of-war. The screen-off-only edition exits on any input and has no flicker.

## Six Tools

| File | What it does | Wake up |
|---|---|---|
| `吾爱熄屏.exe` 🟢 | **Screen off** — instant DPMS off, any input wakes back to desktop, no UI | Any key/mouse |
| `吾爱熄屏融合版.exe` 🟠 | **All-in-one** — choose screen off / password / lock / hibernate / shutdown / restart, with timer | Depends on mode |
| `吾爱熄屏口令版A-纯黑窗不关屏.exe` 🟣 | **Password lock (black window)** — no DPMS signal, best compatibility | Type password / hold Esc 3s |
| `吾爱熄屏口令版B-现状DPMS全关.exe` 🔵 | **Password lock (DPMS off)** — real screen off, saves power | Type password / hold Esc 3s |
| `吾爱熄屏-一键锁屏熄屏.exe` 🔴 | **Lock + screen off** — uses system password | Key/mouse → system login |
| `吾爱熄屏-一键休眠黑屏.exe` 🟢 | **Hibernate** — screen off then S4 hibernate; background tasks pause | Press power button |

> The first four are built from a **single source** `吾爱熄屏.cs` via `/define`; Lock and Hibernate are two standalone single-file programs.

<img width="625" height="485" alt="PixPin_53-49" src="https://github.com/user-attachments/assets/da15506d-5b82-46c8-8769-d1ce1a25044f" />

<img width="550" height="290" alt="Combo edition" src="https://github.com/user-attachments/assets/251108a7-e5ae-4c94-bc00-9193b5c70c87" />

**Password** (Password A/B / Combo password mode): letters/digits, **case-insensitive**, set once and remembered; forgot it → hold **Esc 3s** to force wake; moving/renaming the exe triggers a re-set; stored in registry `HKCU\Software\WuAiScreenOff_*` only.

**Tech core**: pure DPMS off (`SC_MONITORPOWER=2`), zero dependencies; blocks sleep via `ES_CONTINUOUS|ES_SYSTEM_REQUIRED` (never `ES_DISPLAY_REQUIRED`); multi-monitor: primary black window + secondary covers.

## Download

Grab from [**Releases**](../../releases) — single file, no install, just delete the exe to remove.

## Build from source

No dev environment needed (uses the built-in .NET Framework 4 compiler):

```bat
:: double-click 编译.bat, or from a shell:
编译.bat
```

Builds all 6 programs at once, with icons `app.ico` (green) / `app_blue` (blue) / `app_orange` (orange) / `app_red` (red) / `app_purple` (purple).

## FAQ

**Q: Antivirus flags it?**
The Password / Combo editions use a low-level keyboard hook (to swallow the Win key during blanking so the Start menu can't leak your desktop). That API is the same kind keyloggers use, so some AVs may **false-positive**. The source is fully open — review and build it yourself. The Blank-only edition has no hook.

**Q: Does it phone home / upload anything?**
No. Fully offline, zero network activity.

**Q: What does it write to the registry?**
Only the Password / Combo editions write one password value each (`WuAiScreenOff_Password` / `WuAiScreenOff_Combo`). Blank / Lock / Sleep **write nothing**.

**Q: Screen won't wake after off?**
A DPMS wake quirk on a few monitors / iGPUs (rare on newer machines). If key/mouse doesn't wake it, a tap on the power button usually does.

## License

[MIT](LICENSE) — free to use, modify and distribute (including commercially), just keep the copyright notice.

## Author

by [**52pojie**](https://www.52pojie.cn/forum.php?mod=viewthread&tid=2111561)

> The code was written with [**Claude Code**](https://claude.com/claude-code) (Anthropic's AI coding assistant); the app UI also carries a "Powered by Claude Code" credit.

If it helps, a ⭐ Star is appreciated.
