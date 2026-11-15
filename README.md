# QuietFan

[![CI](https://github.com/HicirTech/QuietFan/actions/workflows/ci.yml/badge.svg)](https://github.com/HicirTech/QuietFan/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/HicirTech/QuietFan)](https://github.com/HicirTech/QuietFan/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

English | [简体中文](#简体中文)

QuietFan is a small Windows tray app that keeps the fans of ASUS Vivobook laptops quiet. It keeps CPU boost off
during everyday work, turns it back on only while the CPU stays busy, and switches the ASUS fan profile without
opening MyASUS.

## Why not a custom fan curve?

On these Vivobooks the embedded controller (EC) drives the fan, and the BIOS does not expose the custom fan-curve
interface that ROG and TUF laptops have. What makes the noise is CPU boost: the fan spins up about two seconds after
the CPU starts boosting and keeps running long after the load has ended. Limiting boost changes the fan behaviour
more than any fan profile can.

Measured on a Vivobook S 16 (M5606WA) in the Whisper fan profile, 40 s of full CPU load:

| | Boost on (Windows default) | Boost off |
|---|---|---|
| Fan starts | 2 s after the load starts | 8 s after the load starts |
| Peak fan speed | 3700 rpm | 2900 rpm |
| Peak temperature | 63 °C | 55 °C |
| 40 s after the load ends | 3300 rpm | 900 rpm |

Bursts shorter than about 8 s no longer start the fan at all. The price is lower CPU performance while boost is off,
which is why the default Smart policy turns boost back on under sustained load.

## Features

- Tray icon showing the board temperature (green below 60 °C, orange below 75 °C, red above), with fan speed, fan
  profile and boost state in the tooltip.
- Three boost policies:
  - **Smart** (default): boost is off; it turns on when the CPU has averaged 50 % or more for 20 s and is still busy,
    and off again when it has averaged 20 % or less for 30 s.
  - **Quiet**: boost is always off.
  - **Full**: the power plan's original boost setting.
- ASUS fan profile switching: Whisper, Standard or Performance, the same setting as in MyASUS.
- Optional start with Windows.
- Restores the original boost setting when it exits, on the next start after a crash or shutdown, and on uninstall.
- Per-user install, no administrator rights, no background service. Menus follow the Windows display language
  (Simplified Chinese or English).

## Install

1. Download `QuietFan-Setup-<version>.exe` from the [latest release](https://github.com/HicirTech/QuietFan/releases/latest).
2. Run it. The installer is not code-signed, so Windows SmartScreen may say "Windows protected your PC": choose
   **More info → Run anyway**. You can check the file against `SHA256SUMS.txt` from the same release.
3. QuietFan installs into `%LOCALAPPDATA%\Programs\QuietFan` and starts in the tray.

Each release also has a portable `QuietFan-<version>-portable.zip`: unzip it anywhere and run `QuietFan.exe`.

## Use

Click the tray icon (left or right button) to open the menu:

- the current temperature, fan speed, CPU load and boost state;
- the boost policy: Smart, Quiet or Full;
- the ASUS fan profile: Whisper, Standard or Performance (disabled on machines without the ASUS interface);
- Start with Windows, About, and Exit, which restores the original boost setting.

Settings and a log are kept in `%APPDATA%\QuietFan`.

## Uninstall

Settings → Apps → Installed apps → QuietFan → Uninstall, or Start → QuietFan → Uninstall QuietFan. The uninstaller
closes QuietFan, restores the original boost setting, and removes its settings and the autostart entry.

To put the Windows default boost setting back by hand:

```powershell
powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2
powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2
powercfg /setactive SCHEME_CURRENT
```

## Command line

| Command | Effect |
|---|---|
| `QuietFan.exe` | Starts the tray app; a second start does nothing while one is running. |
| `QuietFan.exe --exit` | Closes the running QuietFan, which restores the boost setting, and waits until it has exited. |
| `QuietFan.exe --restore` | Like `--exit`; when QuietFan is not running, restores the saved original boost setting. |

## Compatibility

Tested on an ASUS Vivobook S 16 (M5606WA, Ryzen AI 9 365, BIOS 316) with Windows 11. Other ASUS laptops that expose
the Vivobook fan-profile device (`0x00110019`) through the ASUS System Control Interface should work as well. On
other PCs the fan-profile items are disabled and the boost policies still work.

Requirements: Windows 10 or 11 with .NET Framework 4.8, which is part of Windows.

## How it works

- **Boost**: the "Processor performance boost mode" of the active power plan (`powercfg` alias `PERFBOOSTMODE`),
  written through the Windows power management API. No administrator rights are needed.
- **Fan profile and fan speed**: the ASUS ATK ACPI interface (`\\.\ATKACPI`, `DSTS`/`DEVS` calls), the channel
  MyASUS uses. QuietFan writes only the fan-profile device `0x00110019` (values 0, 1 and 2) and reads the fan speed
  from `0x00110013`.
- **Temperature**: the ACPI thermal zone (`\_TZ.THRM`), read from Windows performance counters.
- QuietFan never writes EC registers directly, so the EC's own overheat protection stays in charge.

## Build from source

Windows already includes the C# compiler QuietFan needs, so no SDK is required:

```powershell
./build.ps1                        # dist\QuietFan.exe
./build.ps1 -Test                  # also build and run the unit tests
./build.ps1 -Installer -Package    # installer, portable zip and checksums (needs Inno Setup 6)
```

CI builds, tests and packages every push and pull request, and installs and uninstalls the setup on a clean runner.
Pushing a `vX.Y.Z` tag publishes a GitHub release with the installer, the portable zip and `SHA256SUMS.txt`.

## Disclaimer

QuietFan is not affiliated with or endorsed by ASUS. It changes a Windows power setting and the ASUS fan profile
through the same interfaces Windows and MyASUS use. Use it at your own risk.

## License

[MIT](LICENSE)

---

## 简体中文

QuietFan 是一个 Windows 托盘小工具,让 ASUS Vivobook 笔记本的风扇安静下来:平时关闭 CPU 睿频,只有 CPU 持续忙碌时才打开;
还可以不打开 MyASUS 直接切换 ASUS 风扇模式。

### 为什么不是自定义风扇曲线?

这类 Vivobook 的风扇由 EC 控制,BIOS 没有开放 ROG、TUF 那样的自定义风扇曲线接口。真正让风扇吵的是 CPU 睿频:睿频开始约 2 秒后风扇
就转起来,负载结束后还会转很久。所以限制睿频比切换风扇模式更有效。

在 Vivobook S 16(M5606WA)上、静音风扇模式下满载 40 秒的实测:

| | 睿频开(Windows 默认) | 睿频关 |
|---|---|---|
| 风扇开始转 | 负载开始后 2 秒 | 负载开始后 8 秒 |
| 最高转速 | 3700 rpm | 2900 rpm |
| 最高温度 | 63 °C | 55 °C |
| 负载结束后 40 秒 | 3300 rpm | 900 rpm |

短于约 8 秒的负载完全不会让风扇转起来。代价是关闭睿频时 CPU 性能下降,所以默认的“智能”策略在持续高负载时会重新打开睿频。

### 功能

- 托盘图标显示主板温度(低于 60 °C 绿色、低于 75 °C 橙色、更高红色),提示里有风扇转速、风扇模式和睿频状态。
- 三种睿频策略:
  - **智能**(默认):平时关闭睿频;CPU 20 秒平均不低于 50% 且仍然忙碌时打开,30 秒平均不高于 20% 时再关闭。
  - **静音**:始终关闭睿频。
  - **性能**:使用电源计划原本的睿频设置。
- 切换 ASUS 风扇模式:静音、标准、性能,和 MyASUS 里是同一个设置。
- 可选开机自动启动。
- 退出时、崩溃或关机后的下次启动时、以及卸载时,都会恢复原来的睿频设置。
- 按用户安装,不需要管理员权限,没有后台服务。菜单跟随 Windows 显示语言(简体中文或英文)。

### 安装

1. 从[最新版本](https://github.com/HicirTech/QuietFan/releases/latest)下载 `QuietFan-Setup-<版本>.exe`。
2. 运行安装程序。安装程序没有代码签名,Windows SmartScreen 可能提示“Windows 已保护你的电脑”:点 **更多信息 → 仍要运行**。
   可以用同一版本里的 `SHA256SUMS.txt` 校验文件。
3. QuietFan 会安装到 `%LOCALAPPDATA%\Programs\QuietFan` 并在托盘启动。

每个版本也提供便携版 `QuietFan-<版本>-portable.zip`,解压后直接运行 `QuietFan.exe`。

### 卸载

设置 → 应用 → 已安装的应用 → QuietFan → 卸载,或开始菜单 → QuietFan → Uninstall QuietFan。卸载程序会关闭 QuietFan、恢复原来的
睿频设置,并删除它的设置和开机启动项。手动恢复 Windows 默认睿频设置的命令见上文英文部分的 Uninstall。

### 兼容性

在 ASUS Vivobook S 16(M5606WA,Ryzen AI 9 365,BIOS 316)+ Windows 11 上测试。其他通过 ASUS System Control Interface 提供
Vivobook 风扇模式设备(`0x00110019`)的 ASUS 笔记本应该也能用;其他电脑上风扇模式菜单会变灰,睿频策略照常工作。需要 Windows 10 或
11(自带 .NET Framework 4.8)。

### 免责声明

QuietFan 与 ASUS 无关,也未获 ASUS 认可。它通过 Windows 和 MyASUS 使用的同一接口修改电源设置和 ASUS 风扇模式,使用风险自负。
