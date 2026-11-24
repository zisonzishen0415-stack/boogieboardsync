# BoogieBoardSync Windows Driver

Windows 用户态驱动，把 **Boogie Board Sync 9.7**（USB 名称 `Improv Sync`，`VID_2914 / PID_0100`）当作带压感的 Windows Ink 手写板使用。

不需要安装内核驱动，不需要管理员权限。程序读取设备 vendor HID 集合的 8 字节报文，再通过 Windows 的 `InjectSyntheticPointerInput` 注入真实手写笔事件，因此 OneNote、Whiteboard、Photoshop、Krita 等应用可以直接识别压感、笔尖和侧键。

## 环境要求

- Windows 10 1809 或更高版本（Windows 11 已验证）
- .NET Framework 4.8（Windows 自带）
- 设备固件 1.25 已验证
- USB 数据线；设备需要开机/有电

## 构建

```powershell
.\build.ps1
```

生成两个无第三方依赖的程序：

- `bin\BoogieBoardSync.exe`：控制台版，用于诊断和调试。
- `bin\BoogieBoardSyncTray.exe`：无窗口托盘版，用于日常输入。

## 首次使用

1. 插上 Boogie Board Sync，确认 Windows 能看到它的 U 盘。
2. 先跑诊断：

```powershell
.\bin\BoogieBoardSync.exe --doctor
```

`--doctor` 会依次检查 HID 集合、读取 feature 5、释放 U 盘卷、发送日期/主机类型握手，并尝试 `DIGITIZER(3)`、`CAPTURE(4)`、`FILE(5)` 三种模式。

3. 看到 `PASS: device accepted mode 3` 后，日常使用启动托盘版：

```powershell
.\bin\BoogieBoardSyncTray.exe
```

4. 用笔在板子上画。默认是 `digitizer` 模式（3），压感会传给当前应用。

控制台版仍然可用：

```powershell
.\bin\BoogieBoardSync.exe
```

如果设备仍停留在 U 盘模式，程序默认会自动弹出它的卷。自动识别只接受包含 `SYNC` 目录或卷标中含 `BOOGIE`/`SYNC` 的卷，避免误弹其他 U 盘；如果板子的卷没有这些特征，请手动指定盘符：

```powershell
.\bin\BoogieBoardSync.exe --eject=D --set-mode=3
```

## 托盘版

`BoogieBoardSyncTray.exe` 启动后常驻系统托盘，不显示控制台窗口。托盘菜单可以：

- 暂停/恢复输入
- 在 Windows Ink 数位板和鼠标之间即时切换
- 旋转 0/90/180/270 度
- 映射到全部屏幕或指定显示器
- 切换柔和、线性、硬朗力度曲线
- 开启开机自动启动
- 打开设置文件夹

全局热键：

```text
Ctrl+Alt+P  暂停/恢复
Ctrl+Alt+M  数位板/鼠标切换
Ctrl+Alt+R  旋转 90 度
```

设置保存在：

```text
%LOCALAPPDATA%\BoogieBoardSync\settings.ini
```

鼠标模式使用绝对坐标移动，侧键映射为鼠标右键。

## 力度映射

驱动读取输入报文中的 16 位原始力度值，再转换成 Windows Ink 的 `0..1024` 压感。默认是线性映射，不增加延迟。

```text
--pressure-min=0       原始力度下限
--pressure-max=1023    原始力度上限
--pressure-curve=soft|linear|firm|N
--pressure-deadzone=0  忽略低于该比例的力度
--pressure-smoothing=0 力度平滑，默认关闭
```

如果需要校准，先用 `--probe` 画轻、中、重三组笔迹，观察 `pressure` 的稳定范围，再把范围填入 `--pressure-min` / `--pressure-max`。托盘版可以即时切换柔和、线性、硬朗三种曲线。

## 常用参数

```text
--mode=3            默认，digitizer，实时笔迹
--mode=4            capture，按键/保存事件
--mode=5            file，仅保存完成事件
--eject=D           切换模式前释放指定盘符
--no-eject          不自动释放 U 盘卷
--no-handshake      不发送日期/主机类型 feature 6/8
--transport=auto    auto / feature / output / both
--retries=80        每种传输方式的尝试次数
--retry-delay=60    每次尝试间隔毫秒
--mouse             注入鼠标而不是 Windows Ink 手写笔（无压感）
--rotate=90         旋转坐标（0/90/180/270）
--flip-x / --flip-y 镜像坐标轴
--raw-max=20280x13942  原始坐标最大值，可按实测校准
--raw-min=0x0       原始坐标最小值
--screen=all|1      映射全部屏幕或第 1 个显示器
--pressure-min=0    原始力度下限
--pressure-max=1023 原始力度上限
--pressure-curve=linear  soft / linear / firm / gamma
--pressure-deadzone=0   力度死区，0.0..0.95
--pressure-smoothing=0  力度平滑，0.0..0.95，默认关闭
--debug             打印原始报文和映射后的力度
--no-tablet-mode    不主动切换模式（已用官方软件切好时使用）
--help              查看全部参数
```

## 诊断工具

```powershell
.\bin\BoogieBoardSync.exe --list          # 列出所有 HID 集合和打开状态
.\bin\BoogieBoardSync.exe --caps          # 打印 input/output/feature 的 HID value caps
.\bin\BoogieBoardSync.exe --features      # 读取 feature 1/4/5/6/7/8
.\bin\BoogieBoardSync.exe --diag          # 依次尝试所有模式写入方式并读回
.\bin\BoogieBoardSync.exe --doctor        # 一键诊断：卷、握手、模式切换
.\bin\BoogieBoardSync.exe --verify --duration=20   # 切模式后实际读取输入报文
.\bin\BoogieBoardSync.exe --verify --blind --duration=20  # 不依赖 feature 5 读回
.\bin\BoogieBoardSync.exe --pointers      # 查看 Windows 识别到的 pointer 设备
.\bin\BoogieBoardSync.exe --pointer-watch --duration=20
.\bin\BoogieBoardSync.exe --probe --duration=20 --debug
.\bin\BoogieBoardSync.exe --selftest      # 不接设备，验证报文解析和压感换算
.\bin\BoogieBoardSync.exe --test          # 不接设备，注入一个测试方块
.\bin\BoogieBoardSync.exe --test --mouse
```

`--probe` 会打印原始 HID 报文，并统计 x/y/pressure 的范围、touch/hover/side 计数。第一次校准时可以画满整个板子，然后把统计到的范围填入 `--raw-min` / `--raw-max`。

## 协议说明

设备在 Windows 上暴露两个 HID 集合：

- `COL01`：`HID-compliant pen`，被 Windows 手写笔栈独占，用户态打不开（`ERROR_SHARING_VIOLATION`）。
- `COL02`：vendor-defined `FF00:0000`，用户态可读写，驱动实际使用这个集合。

HID 报告：

- 输入报告 `report ID 3`，8 字节（含 report ID）：
  `03 | xL xH | yL yH | pL pH | flags`
  - `flags bit0`：笔尖接触
  - `flags bit1`：侧键
  - `flags bit2`：悬停
  - `flags bit6/7`：保存/擦除按键（capture 模式）
- feature `report ID 5`，模式控制：
  - 主机发送 4 字节：`05 05 00 mode`
  - 设备读回 7 字节：`05 05 00 mode 00 00 00`
  - `mode`：`1=NONE`、`3=DIGITIZER`、`4=CAPTURE`、`5=FILE`
- feature `report ID 6`：日期时间，数据格式见 Kent Displays 官方 Android/iOS SDK
- feature `report ID 8`：主机类型；默认发送 Android 类型 `8`
- feature `report ID 4`：屏幕擦除等操作请求

模式切换会依次尝试：

1. 自动释放设备的 U 盘卷（锁定、dismount、eject）。
2. 发送 feature 6 日期和 feature 8 主机类型。
3. 直接设置目标模式。
4. 失败时尝试 `MODE_FILE(5) -> 目标模式`。
5. 失败时尝试 `MODE_NONE(1) -> MODE_FILE(5) -> 目标模式`。
6. 每种阶段都同时尝试 feature 报告和 output 报告。

Windows 的 HID 栈和 Linux/libusb 不完全一样，某些机器上 feature 写入会返回成功但设备不采纳。因此程序内置了 `feature`、`feature-pad`、`output`、`output-id`、`output-pad` 多种写入方式；`--diag` 可以逐个查看哪种方式能让 feature 5 读回目标模式。

官方协议来源：

- Chrome SDK `kent-displays/boogie-board-sync-sdk-chrome`（`sync-streaming.js`、`sync-capture-report.js`）
- Android SDK `kent-displays/boogie-board-sync-sdk-android`（`HIDSetReport.java`、`SyncStreamingService.java`）
- iOS SDK `kent-displays/boogie-board-sync-sdk-ios`（`BBSyncStreamingClient.m`）

Linux 社区驱动参考：

- `jbedo/boogiesync-tablet`
- `arjo129/boogieboardsync`
- `nenadalm/boogie-board-sync-driver`

## 当前验证状态

- Windows Ink 手写笔注入：已实测通过（`--test` 注入 6 次失败 0）。
- 鼠标回退注入：已实测通过（`--test --mouse`）。
- 报文解析、力度曲线、坐标映射：已通过 `--selftest`。
- 模式切换：已在真机验证，feature 5 读回 `03`，OneNote 中可观察到压感。
- 托盘版：全局热键注册成功；暂停、数位板/鼠标切换、旋转回调与配置持久化已通过运行验证。
- 鼠标回退与侧键右键映射：已通过注入测试。
- 官方 SDK 从不读取 feature 5 的模式值，因此部分固件可能让 feature 5 一直读回 `01`。请以 `--verify` 是否收到真实输入报文为准，而不是只看读回值。

## 已知限制

- 设备模式切换可能被 Windows 挂载的 U 盘卷或占用该卷的程序阻塞。先关闭资源管理器窗口，再运行 `--doctor`；必要时加 `--no-eject` 后手动弹出卷。
- 如果 `--doctor` 显示 `UNCONFIRMED`，先运行 `--verify --duration=20` 并在板子上画线。收到报文就说明模式切换成功，feature 5 只是不可靠。
- 如果 `--verify` 也收不到报文，依次尝试 `--verify --blind`、`--verify --transport=output`、`--verify --no-handshake --transport=feature --retries=200`。
- 如果设备从 USB 上消失，优先检查电量/USB 线；驱动会自动等待并重连。
- Windows 的 `COL01` 笔集合由系统独占，因此不能直接读取它；我们读取 `COL02`。
- 侧键映射为手写笔第二按键，不同应用的行为可能不同。
- 压感范围不同批次可能需要用 `--pressure-min` / `--pressure-max` 校准；托盘版提供柔和、线性、硬朗三种曲线。
