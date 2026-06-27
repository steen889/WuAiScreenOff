# 吾爱熄屏 · WuAi ScreenOff

> 一键熄屏,**不锁屏、不休眠**。屏幕真黑省电,后台任务照跑;唤醒直接回桌面,不用输系统密码。

> A tiny Windows screen-off utility (single file, portable, offline) — **no lock, no sleep**. Saves power while background tasks keep running; wake straight back to the desktop, no system password needed.

![version](https://img.shields.io/badge/version-2.0-blue)
![platform](https://img.shields.io/badge/Windows-7%20~%2011-0078d6)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.0-512bd4)
![size](https://img.shields.io/badge/size-single%20file-green)
![license](https://img.shields.io/badge/license-MIT-green)

**简体中文** ｜ [English](README.en.md)

---

## 这是什么

一个极小的 Windows 熄屏工具(单文件、绿色、离线)。和系统"锁屏"不同——它**只把屏幕熄掉、不锁电脑**:后台的下载 / 渲染 / 远程 / 挂机任务一秒不停,唤醒后**直接回桌面,无需重输开机密码**。界面随系统语言自动中 / 英切换。

<img width="525" height="365" alt="吾爱熄屏" src="https://pic1.imgdb.cn/item/6a282cd8edae85a6285131f7.gif" />



## 六个程序

| 文件 | 功能 | 唤醒 |
|---|---|---|
| `吾爱熄屏.exe` 🟢 | **纯熄屏** —— 双击即黑,动键鼠就亮回桌面,无界面、最省事 | 任意键鼠 |
| `吾爱熄屏融合版.exe` 🟠 | **多功能** —— 选 纯熄屏/口令/锁屏/休眠/关机/重启,支持定时 | 视所选模式 |
| `吾爱熄屏口令版A-纯黑窗不关屏.exe` 🟣 | **口令熄屏(纯黑窗)** —— 不发 DPMS 信号,兼容性最好 | 盲打口令 / 长按 Esc 3s |
| `吾爱熄屏口令版B-现状DPMS全关.exe` 🔵 | **口令熄屏(DPMS 全关)** —— 真关屏更省电 | 盲打口令 / 长按 Esc 3s |
| `吾爱熄屏-一键锁屏熄屏.exe` 🔴 | **锁屏+关屏** —— 用系统密码保护桌面 | 动键鼠 → 输系统密码 |
| `吾爱熄屏-一键休眠黑屏.exe` 🟢 | **休眠** —— 关屏后进 S4 休眠,最省电;后台任务会暂停 | 按电源键 |

> 前四个由**同一份源码** `吾爱熄屏.cs` 用 `/define` 编出;锁屏、休眠是两个独立单文件。

<img width="625" height="485" alt="PixPin_53-49" src="https://github.com/user-attachments/assets/da15506d-5b82-46c8-8769-d1ce1a25044f" />


<img width="550" height="290" alt="吾爱熄屏融合版" src="https://github.com/user-attachments/assets/251108a7-e5ae-4c94-bc00-9193b5c70c87" />

**口令说明**(口令版 A/B / 融合版口令模式通用):字母/数字,**不分大小写**,首次设一次即记住;忘口令长按 **Esc 3 秒**强制亮屏;换目录/改文件名自动要求重设;口令只存注册表 `HKCU\Software\WuAiScreenOff_*`,exe 目录不留文件。

<img width="525" height="365" alt="口令设置" src="https://github.com/user-attachments/assets/429516b2-517c-4d33-bf4d-576e5d244113" />

**技术内核**:纯 DPMS 关屏(`SC_MONITORPOWER=2`),零外部依赖;阻睡眠 `ES_CONTINUOUS|ES_SYSTEM_REQUIRED`(绝不加 `ES_DISPLAY_REQUIRED`);多屏主窗+副屏黑罩全覆盖。

## 下载

到 [**Releases**](../../releases) 下载 zip,解压即用。单文件免安装,不想用直接删 exe。

## 从源码编译

无需任何开发环境(用 Windows 自带的 .NET Framework 4 编译器):

```bat
:: 双击 编译.bat,或命令行:
编译.bat
```

一次编出 6 个程序,图标分别用 `app.ico`(绿) / `app_blue`(蓝) / `app_orange`(橙) / `app_red`(红) / `app_purple`(紫)。

## 常见问题

**Q:杀毒软件报毒?**
口令版 / 融合版用了底层键盘钩子(熄屏时拦 Win 键,防弹开始菜单泄露桌面),这个 API 和"键盘记录器"同款,个别杀软会**误报**。源码完全开放,可自行审阅、自行编译。纯熄屏版不含钩子。

**Q:会联网 / 上传数据吗?**
不会。全程离线,无任何网络行为。

**Q:在注册表写了什么?**
仅口令版 / 融合版各写一条口令(`WuAiScreenOff_Password` / `WuAiScreenOff_Combo`)。纯熄屏 / 锁屏 / 休眠**不写注册表**。

**Q:关屏后点不亮?**
极个别显示器 / 核显的 DPMS 唤醒兼容问题(新机基本无此问题)。动键鼠无效时,按一下电源键通常即可唤醒。

## 许可证

[MIT](LICENSE) —— 自由使用、修改、分发(含商用),保留版权声明即可。

## 作者

by [**吾爱破解**](https://www.52pojie.cn/forum.php?mod=viewthread&tid=2111561)

> 本项目代码由 [**Claude Code**](https://claude.com/claude-code)(Anthropic 的 AI 编程助手)辅助编写,软件界面也带 "Powered by Claude Code" 署名。

如果对你有用,点个 ⭐ Star 支持一下。
