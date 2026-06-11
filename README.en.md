# WuAi ScreenOff · 吾爱熄屏

> A tiny Windows screen-off utility (single file, portable, offline).— **no lock, no sleep**. Saves power while background tasks keep running; wake straight back to the desktop, no system password needed.

![version](https://img.shields.io/badge/version-1.3-blue)
![platform](https://img.shields.io/badge/Windows-10%20%2F%2011-0078d6)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.0-512bd4)
![size](https://img.shields.io/badge/size-~10--23%20KB-green)
![license](https://img.shields.io/badge/license-MIT-green)

[简体中文](README.md) ｜ **English**

---

## What is this

A tiny Windows screen-off utility (single file, portable, offline). Unlike the system "Lock" — it **only turns the screen off, it does not lock the PC**: background downloads / renders / remote sessions / AFK tasks keep running, and on wake you go **straight back to the desktop with no password to re-enter**. UI auto-switches CN / EN by system language.

## Three editions

| File | What it does |
|---|---|
| `吾爱熄屏.exe` | **Blank only** — wakes on any key / mouse, simplest |
| `吾爱熄屏口令版.exe` | **Password** — wakes only when you blind-type your password (anti-touch) |
| `吾爱熄屏融合版.exe` | **Combo** — pick Blank or Password on launch |

> All three are built from a **single source** `吾爱熄屏.cs` via `/define`.

> **🔑 Before using the Password / Combo edition, remember these three:**
> - **Case-insensitive** — wrong input won't wake.
> - **Forgot your password?** Hold **Esc for 3s** to force-wake.
> - **Moved the exe to a new folder / renamed it?** The old password is cleared and you'll be asked to set a new one (so it can't lock you out).

<img width="525" height="365" alt="image" src="https://github.com/user-attachments/assets/99ce02d8-0c25-4546-a31f-9d33a206fbd1" />


## When to use

Background **download / render / remote desktop / AFK** tasks running while you step away: blank the screen for power saving, eye comfort and privacy; tasks never pause; wake instantly when you're back.

**"Password" mode feels like a lock screen but doesn't actually lock** — only your blind-typed password wakes it, blocking cat paws, accidental touches and curious coworkers, while sparing you the system password every time.

## Password (Password / Combo edition)

- Letters / digits, as short as **1 char** or a complex combo
- **Case-insensitive**, wrong input won't wake
- Forgot it? Hold **Esc for 3s** to force wake
- Moving / renaming the exe triggers a reset prompt (so a stale password can't lock you out)
- Stored as a single registry value; **nothing is left in the exe folder**

## Laptop / Desktop (auto-adaptive)

| Device | How it blanks | Notes |
|---|---|---|
| **Laptop** (brightness controllable) | Backlight to minimum + black overlay | Dodges the old-iGPU "DPMS won't wake" trap; a faint glow remains |
| **Desktop / external** (no brightness control) | DPMS truly powers the display off | Real power saving (same as Windows' "turn off display after X min"), wakes on key / mouse |

Detected automatically at startup — no manual setup.

## Brightness safety net

Blanking dims the laptop backlight and restores it on normal exit. If the app is **force-killed or crashes** (skipping the restore step), it remembers the original brightness in the registry and **restores it on next launch** — the screen can never be stuck dark.

## Download & use

Grab from [**Releases**](../../releases) — download names map to editions:

| Release file | Edition |
|---|---|
| `WuAiScreenOff.exe` | Blank only |
| `WuAiScreenOff-Password.exe` | Password |
| `WuAiScreenOff-Combo.exe` | Combo |

1. Download the edition you want, double-click to run
2. Wake methods: see "Three editions" above

> Single file, no install, writes nothing to system folders. To remove, just delete the exe (password / brightness live in registry `HKCU\Software\WuAiScreenOff*`, delete if you care).

## Build from source

No dev environment needed (uses the built-in .NET Framework 4 compiler):

```bat
:: double-click 编译.bat, or from a shell:
编译.bat
```

One `吾爱熄屏.cs` builds all three editions, with icons `app.ico` (green) / `app_blue.ico` (blue) / `app_orange.ico` (orange).

## FAQ

**Q: Antivirus flags it?**
The Password / Combo editions use a low-level keyboard hook (to swallow the Win key during blanking so the Start menu can't leak your desktop). That API is the same kind used by keyloggers, so some AVs may **flag it as a false positive**. The source is fully open — review and build it yourself. The Blank-only edition has no hook.

**Q: Does it phone home / upload anything?**
No. Fully offline, zero network activity.

**Q: What does it write to the registry?**
At most two keys: `WuAiScreenOff_Password` (your password) and `WuAiScreenOff` (brightness safety net). Deleting the keys removes everything.

**Q: Desktop screen won't wake after off?**
A DPMS quirk on some monitors / iGPUs. Laptops default to the safer "black overlay" mode to avoid it; on desktop, reboot if wake fails.

## Compatibility & self-tuning

Windows machines vary wildly in GPU / driver / monitor / power policy, so low-level behavior like blanking, waking and brightness restore **may differ across setups** — e.g. DPMS wake on certain iGPUs, external / multi-monitor differences, or brightness under specific power plans can all cause edge-case quirks. These are usually tied closely to the specific hardware, so no single setting fits every machine.

The good news: the source is **a single, readable `吾爱熄屏.cs`**. If something doesn't fit your machine, tweak it against your own config and rebuild — common knobs:

- **Blanking strategy**: black overlay vs real DPMS off (see the `useDPMS` check)
- **Brightness logic**: WMI minimum backlight, restore timing
- **Multi-monitor / wake**: overlay coverage, wake triggers

It's all in one file — edit, double-click `编译.bat`, done.

## License

[MIT](LICENSE) — free to use, modify and distribute (including commercially), just keep the copyright notice.

## Author

by [**52pojie**](https://www.52pojie.cn/forum.php?mod=viewthread&tid=2111561)

> The code was written with [**Claude Code**](https://claude.com/claude-code) (Anthropic's AI coding assistant); the app UI also carries a "Powered by Claude Code" credit.

If it helps, a ⭐ Star is appreciated.
