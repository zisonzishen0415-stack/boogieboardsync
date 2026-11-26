# BoogieBoardSync

把一台停产多年的 **Boogie Board Sync 9.7** 电子手写板，重新变成 Windows 上带压感的手写板。

USB 插线或经典蓝牙连接，压感、笔尖、侧键、悬停直接进 Windows Ink —— OneNote、Whiteboard、Photoshop、Krita 等应用都能把它当数位板用。不需要安装内核驱动，不需要管理员权限。

> 当前版本：**v0.6.0** · Windows 10 1809+ / Windows 11 · .NET Framework 4.8

## 为什么会有这个项目

Boogie Board Sync 9.7 是 Kent Displays 十多年前的产品（USB 名称 `Improv Sync`，`VID_2914 / PID_0100`），早已停产。官方配套的桌面同步软件和驱动也停止维护，官网下载不到了 —— 在现在的 Windows 上，插上板子只会多出一个 U 盘（里面是板上存下的笔记），实时笔迹没有任何出路，也找不到能替代的第三方驱动。

但板子本身没坏：压感电磁层、笔、按键都工作正常，厂商的笔迹协议也一直公开在他们的 Chrome / Android / iOS SDK 和 Linux 社区驱动里。既然软件没人管了，就自己写一个：

- **纯用户态**：直接打开设备第二个 HID 集合（vendor-defined `FF00:0000`），不碰被系统独占的 pen 集合，不需要签名内核驱动，也不需要管理员权限；
- **蓝牙无线**：优先复用 Windows 已绑定的蓝牙 HID 集合，未绑定时直连经典蓝牙 RFCOMM 的 Sync Streaming Profile（不是 BLE GATT），拔掉 USB 也能实时出笔迹；
- **真 Windows Ink**：读到的 8 字节笔迹报文通过 `InjectSyntheticPointerInput` 注入成真实手写笔事件，压感、笔尖、侧键全系统生效；
- **日常可用**：附带托盘程序，开机自启、托盘菜单、全局热键、图形设置窗口一应俱全，日常使用不用开控制台。

## 支持的功能

### 连接

- **USB HID**：从 vendor-defined 集合（`COL02`）读取笔迹，避开被 Windows 手写笔栈独占的 `COL01`。
- **蓝牙（两条通路自动选择）**：Windows 已把板子绑定为「蓝牙 HID 设备」时，直接读取系统暴露的 vendor HID 集合（优先，不会被系统占用卡住）；否则直连经典蓝牙 RFCOMM 的 Sync Streaming Profile（服务 UUID `d6a56f80-...`，通道自动识别，Sync 为 3）。支持 `--bt-scan` 枚举、按地址/名称过滤、手动指定通道。
- **连接方式即时切换**：自动（USB 优先）/ 仅 USB / 仅蓝牙，托盘和设置窗口都能随时切。
- **自动重连**：拔插线、蓝牙断链、锁屏解锁、休眠恢复后自动重建连接；插着 USB 时固件会停蓝牙流，程序会自动切 USB，拔线后自动回蓝牙。
- **单实例**：重复启动托盘程序会唤醒已运行的设置窗口，不会出现「程序在跑但看不到界面」。

### 压感与笔事件

- **16 位力度 → Windows Ink 0..1024**，默认线性映射，不增加延迟。
- **力度校准**：`--pressure-min` / `--pressure-max` 按实测原始力度范围重新标定。
- **力度曲线**：柔和（gamma 0.70）/ 线性（1.00）/ 硬朗（1.40），或自定义 gamma 0.1–4.0。
- **死区与平滑**：`--pressure-deadzone` 忽略轻触噪声，`--pressure-smoothing` 可选平滑（默认关闭，不引入延迟）。
- **完整笔状态**：笔尖接触、悬停（hover）、侧键（映射为 Windows Ink 笔第二按键）。
- **力度曲线可视化**：设置窗口画出原始力度 → Windows Ink 输出的曲线（含死区），实时笔尖落点画在曲线上。
- **实时力度条**：同时显示原始力度和映射后的输出。
- **力度校准向导**：照提示用最轻力度画 3 秒、再用最大力度画 3 秒，自动填入原始力度范围。

### 坐标映射

- **旋转** 0/90/180/270°，支持 `--flip-x` / `--flip-y` 镜像。
- **多显示器**：映射到全部屏幕或指定某个显示器，显示器/分辨率变化后自动重新计算。
- **可视化预览**：平板和屏幕并排显示，实时笔尖点、目标区域、留边或裁剪范围一眼可见。
- **点选区域**：点「框选屏幕区域…」，在屏幕上点击左上角、右下角两下即可框出映射区域；也可以勾选「用笔点选」，直接在板上移动笔定位、落笔确认。
- **区域验证**：点「预览区域（3 秒）」，目标屏幕上会叠加显示实际映射边框和等比内容区，3 秒后自动消失。
- **显示器编号**：设置窗口和托盘里的「显示器 N」与 Windows 的显示器编号一致，并标注主屏、横屏/竖屏和分辨率。
- **映射区域**：也可以直接填矩形（X / Y / 宽 / 高），只让笔迹落在屏幕的一块区域里；宽或高填 0 表示铺满整块屏幕。
- **映射比例**：拉伸铺满、等比适应（保持平板纵横比，居中留边），或等比填充（裁掉平板多出的部分，屏幕填满且不变形）。
- **原始坐标校准**：平板 X/Y 范围可在设置窗口或命令行调整；点「捕获平板范围…」在板上沿边缘画一圈即可自动填入。

### 两种输入模式

- **数位板模式**（默认）：Windows Ink 手写笔，带压感和侧键，适用于 OneNote、Whiteboard、PS、Krita 等。
- **鼠标模式**：绝对坐标移动，笔尖=左键，侧键=右键，无压感；兼容不认 Windows Ink 的老软件。
- 两种模式可在托盘/设置窗口/热键即时切换，切换时自动释放按键，不会卡住。

### 托盘与设置

- 无窗口托盘常驻，状态栏实时显示连接链路、输入模式、最近输入时间。
- 设置窗口按 **输入 / 映射 / 力度 / 诊断** 分页，所有修改即时生效。
- 托盘菜单：暂停/恢复、连接方式、重新连接、输入模式、旋转、映射屏幕、映射比例、映射区域与校准…、力度曲线、力度校准…、开机自启、打开设置文件夹、诊断信息、打开日志、关于、退出。
- 全局热键：`Ctrl+Alt+S` 设置 / `Ctrl+Alt+P` 暂停 / `Ctrl+Alt+M` 数位板↔鼠标 / `Ctrl+Alt+R` 旋转。
- 设置立即生效并持久化到 `%LOCALAPPDATA%\BoogieBoardSync\settings.ini`。
- 日志写入 `%LOCALAPPDATA%\BoogieBoardSync\boogieboardsync.log`，超过 1 MB 自动轮转。
- 诊断信息窗口：版本、系统、连接状态、USB/蓝牙枚举、最近日志，一键复制。
- 开机自启走 `HKCU\...\Run`，无需管理员权限。

**设置窗口里的交互**

- 映射页有平板 ↔ 屏幕的实时预览：笔在板上移动时，两侧的圆点同步移动。
- 「框选屏幕区域…」会打开半透明全屏遮罩：鼠标模式点两下（左上、右下）即可；勾选「用笔点选」后，移动笔把光标定位到目标角落，落笔确认。Esc 或右键取消。
- 用笔点选时会把平板临时铺满目标屏幕（方便够到任意角落），取消后恢复原来的映射；鼠标模式则自动暂停笔迹注入，避免误触。
- 「预览区域（3 秒）」在目标屏幕上叠加实际映射边框，等比模式下还会画出内容区，方便确认映射落在哪块屏幕、哪个范围。
- 「捕获平板范围…」打开校准窗口：沿你想使用的区域画一圈，点“完成”自动填入平板 X/Y 范围。
- 「力度校准…」分两步采样最轻/最大力度，各自画 3 秒即可，应用后写入原始力度范围。
- 映射页和力度页的预览都是实时的，改数字或用托盘切换比例会立刻反映在图上。

### 诊断与调试

- `--doctor` 一键诊断：HID 集合、feature 5、释放 U 盘卷、日期/主机握手、依次尝试切模式。
- `--probe` 打印原始报文并统计 x/y/pressure 范围、touch/hover/side 计数，用于校准。
- `--verify` 切换模式后实测是否真的收到笔迹报文（不盲信 feature 5 读回值）。
- `--selftest` 不接设备验证报文解析、蓝牙帧/CRC8、压感曲线、注入结构体布局。
- `--test` 不接设备注入一个测试方块，验证 Windows Ink 注入链路。
- `--list` / `--caps` / `--features` / `--diag` / `--pointers` / `--pointer-watch` / `--bt-scan` 等细分工具。
- 模式切换内置多种传输写法（`feature`、`feature-pad`、`feature-ioctl`、`output`、`output-write`、`output-pad`、`output-id`），应对 Windows HID 栈上「feature 写入返回成功但设备不采纳」的机型差异。

## 环境要求

- Windows 10 1809 或更高版本（Windows 11 已验证）
- .NET Framework 4.8（Windows 自带）
- 设备固件 1.25 已验证
- USB 数据线，或已配对的经典蓝牙设备
- 设备需要开机/有电

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

## 蓝牙使用

Boogie Board Sync 的实时笔迹走**经典蓝牙 RFCOMM**（Sync Streaming Profile，UUID `d6a56f80-88f8-11e3-baa8-0800200c9a66`，通道 3），不是 BLE GATT。使用前先在 Windows 的"设置 > 蓝牙和其他设备"里配对板子，然后扫描确认：

```powershell
.\bin\BoogieBoardSync.exe --bt-scan
```

如果能看到 `Sync` / `Boogie Board Sync` / `Improv Sync`，可以直接用蓝牙启动：

```powershell
.\bin\BoogieBoardSync.exe --connection=bluetooth
.\bin\BoogieBoardSyncTray.exe --connection=bluetooth
```

蓝牙模式会自动使用 **capture 模式 4**，由本程序读取原始 8 字节笔迹并注入 Windows Ink，因此压感、笔尖、侧键、旋转、力度曲线和鼠标模式都可用。USB 模式仍然使用 digitizer 模式 3。

如果 Windows 配对板子后自动挂上了「蓝牙 HID 设备」或「HID over SPP」节点，系统会占用板子的蓝牙服务，直连 RFCOMM 会报 `蓝牙错误 10064`。这时驱动会自动改用 Windows 暴露的 vendor HID 集合（`VID_00F3 / PID_0100`，`usage FF00:0000`），不需要删除配对，状态栏和诊断信息里显示为「蓝牙 HID」。这条路径同样默认 capture 模式 4，也可以用 `--mode` 显式覆盖。USB 插着时仍然优先 USB。

如果名称不是自动识别的，可以指定地址或名称：

```powershell
.\bin\BoogieBoardSync.exe --connection=bluetooth --bt-address=AA:BB:CC:DD:EE:FF
.\bin\BoogieBoardSync.exe --connection=bluetooth --bt-name=Sync
```

如果板子插着 USB 线（例如充电），固件会停止蓝牙流；程序会自动切换到 USB，拔掉 USB 后下一次重连会自动回到蓝牙。

蓝牙连接是独占的：官方 App、其他驱动或 Windows 自带的"蓝牙 HID 设备"可能占用同一个 RFCOMM 通道。遇到 `蓝牙连接被拒绝` 或 `蓝牙连接超时` 时，先关闭官方 App/其他驱动，等几秒后托盘会自动重连。托盘在链路断开时会在约 3 秒内检测到并自动恢复。

诊断蓝牙原始笔迹：

```powershell
.\bin\BoogieBoardSync.exe --connection=bluetooth --probe --duration=20 --debug
```

输出中的 `side=False` 表示没有按侧键；`pressure` 是原始 16 位力度。若通道自动识别失败，可以手动指定：

```powershell
.\bin\BoogieBoardSync.exe --connection=bluetooth --bt-channel=3
```

设置窗口和托盘菜单里的"连接方式"可以在 **自动（USB 优先）/ 仅 USB / 仅蓝牙** 之间即时切换。

## 托盘版

`BoogieBoardSyncTray.exe` 启动后常驻系统托盘，不显示控制台窗口。

双击托盘图标，或者再次运行 `BoogieBoardSyncTray.exe`，会打开完整的图形设置窗口。关闭设置窗口只会隐藏它，不会退出驱动。

托盘菜单和设置窗口都可以：

- 暂停/恢复输入
- 连接方式：自动、USB、蓝牙
- 在 Windows Ink 数位板和鼠标之间即时切换
- 旋转 0/90/180/270 度
- 映射到全部屏幕或指定显示器
- 可视化调整映射区域（屏幕点选或直接填 X/Y/宽/高）和映射比例（拉伸 / 等比适应 / 等比填充）
- 捕获平板原始坐标范围
- 实时力度曲线和力度校准向导
- 切换柔和、线性、硬朗力度曲线
- 开启开机自动启动
- 手动"重新连接"，无需退出程序
- 打开设置文件夹、打开日志、查看并复制诊断信息
- 单实例运行：再次启动托盘程序会唤醒已经运行的设置窗口
- 在设置窗口中精确调整力度范围、Gamma、死区和平滑
- 实时力度条：在板上书写时直接查看原始力度和映射后的输出

全局热键：

```text
Ctrl+Alt+S  打开设置窗口
Ctrl+Alt+P  暂停/恢复
Ctrl+Alt+M  数位板/鼠标切换
Ctrl+Alt+R  旋转 90 度
```

设置保存在：

```text
%LOCALAPPDATA%\BoogieBoardSync\settings.ini
```

日志保存在：

```text
%LOCALAPPDATA%\BoogieBoardSync\boogieboardsync.log
```

日志超过 1 MB 会自动轮转为 `boogieboardsync.log.old`。设置窗口和托盘菜单都有"打开日志"；"诊断信息…"会汇总版本、连接方式、USB/蓝牙设备枚举和最近日志，并可一键复制。

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

## 映射区域与比例

默认是平板整块映射到目标屏幕整块，X/Y 各自拉伸。三种比例模式：

- `stretch`：拉伸铺满，可能变形。
- `uniform`：等比适应，保持平板纵横比，在目标区域内居中，多余部分留边。
- `fill`：等比填充，按屏幕比例裁掉平板多出的部分，屏幕填满且不变形；屏幕宽、板子窄时最有用（裁掉板子上下两条）。

```powershell
# 只映射到 1 号屏幕上从 (600,0) 开始的 1200x800 区域
.\bin\BoogieBoardSync.exe --screen=1 --map-x=600 --map-y=0 --map-width=1200 --map-height=800

# 等比适应：保持平板纵横比，在目标区域内居中，多余部分留边
.\bin\BoogieBoardSync.exe --map-fit=uniform

# 等比填充：裁掉平板上下多出的部分，屏幕填满且不变形
.\bin\BoogieBoardSync.exe --map-fit=fill

# 只使用平板中间一块（原始坐标范围）
.\bin\BoogieBoardSync.exe --raw-min=1000x800 --raw-max=19000x13000
```

区域坐标相对所选屏幕左上角，宽或高填 0 表示铺满整块屏幕。设置窗口「映射」页可以实时修改这些值，也可以直接点「框选屏幕区域…」在屏幕上点两下选左上/右下角；映射页的预览会用阴影标出「等比填充」时平板哪一段被裁掉。托盘菜单的「映射比例」可以快速切换三种模式，所有设置都会写入 `settings.ini`。

## 常用参数

```text
--mode=3            USB 默认，digitizer；蓝牙自动使用 4（capture）
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
--map-x=0           目标区域左上角 X（相对所选屏幕）
--map-y=0           目标区域左上角 Y
--map-width=0       目标区域宽度，0 = 铺满屏幕
--map-height=0      目标区域高度，0 = 铺满屏幕
--map-fit=stretch   拉伸铺满；uniform = 等比适应（居中留边）；fill = 等比填充（裁剪到屏幕比例）
--pressure-min=0    原始力度下限
--pressure-max=1023 原始力度上限
--pressure-curve=linear  soft / linear / firm / gamma
--pressure-deadzone=0   力度死区，0.0..0.95
--pressure-smoothing=0  力度平滑，0.0..0.95，默认关闭
--debug             打印原始报文和映射后的力度
--no-tablet-mode    不主动切换模式（已用官方软件切好时使用）
--connection=auto   auto / usb / bluetooth，默认 USB 优先
--bt-scan           扫描附近经典蓝牙设备并退出
--bt-address=ADDR   指定蓝牙地址，例如 AA:BB:CC:DD:EE:FF
--bt-name=NAME      指定蓝牙名称过滤
--bt-channel=N      手动指定 RFCOMM 通道，默认自动识别（Sync 为 3）
--tray              启动托盘版
--show-settings     启动并打开设置窗口
--settings-tab=N    直接打开设置窗口的第 N 页：0 输入 / 1 映射 / 2 力度 / 3 诊断
--version           显示版本
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
.\bin\BoogieBoardSync.exe --bt-scan        # 扫描 Windows 经典蓝牙设备
.\bin\BoogieBoardSync.exe --connection=bluetooth --probe --duration=20 --debug  # 蓝牙笔迹诊断
.\bin\BoogieBoardSync.exe --selftest      # 不接设备，验证报文解析、蓝牙帧和压感换算
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

蓝牙协议：

- 服务 UUID：`d6a56f80-88f8-11e3-baa8-0800200c9a66`，Windows 上通过 Winsock `AF_BTH` / `BTHPROTO_RFCOMM` 连接。
- 帧格式：`C0 | 转义 payload | CRC8 low | CRC8 high | C0`，转义规则为 `C0 -> DB DC`、`DB -> DB DD`。
- 输入数据帧 payload：`xL xH | yL yH | pressureL pressureH | flags`，与 USB 完全一致。
- 设置模式：`00 53 05 05 00 mode` 经 CRC8 和帧转义后发送。
- 连接期间每 3 秒发送一次单独的 `C0` keepalive，避免 Windows/蓝牙栈丢帧。

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
- 托盘版：全局热键注册成功；设置窗口、实时力度条、暂停、数位板/鼠标切换、旋转回调与配置持久化已通过运行验证。
- v0.5.0 托盘版：诊断窗口、复制诊断、打开日志、重新连接、单实例唤醒和 `--show-settings` 已在 Windows 11 上通过运行验证。
- v0.6.0 蓝牙 HID 通路：在 Windows 11 + 已配对板子上通过 `bluetooth hid connected`、feature 5 读写（mode 1 → 4）和设置窗口链路显示验证；该路径已实际用于数小时笔迹输入。
- v0.6.0 设置窗口：四个分页、映射预览、力度曲线的布局已通过运行截图检查；区域点选、平板范围捕获和力度校准的交互流程需要真机鼠标/笔操作确认。
- v0.6.0 蓝牙 RFCOMM 真机：连续会话传输 44,945 帧、坏帧 0、注入 45,039 次失败 0；链路断开后自动重连已验证。
- v0.6.0 抬笔：日志确认抬笔前力度逐步下降（如 96 → 50 → 0），没有尖峰；pen-up 现在固定使用最后接触点。多屏注入偏移修复后，测试方块四角落点回到预期坐标。
- v0.6.0 等比填充：设置窗口预览已通过截图检查，裁剪阴影和「铺满 · 等比填充」标签显示正确。
- 蓝牙帧解析、CRC8、RFCOMM 套接字和 16 位力度解析：已通过 `--selftest`。
- 蓝牙设备枚举：已在 Windows 蓝牙适配器上通过 `--bt-scan` 验证；真机笔迹已通过蓝牙 HID 和 RFCOMM 两条通路验证。
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
- 蓝牙必须先配对；如果 `--bt-scan` 看不到板子，先在 Windows 蓝牙设置里删除旧配对记录，再让板子进入配对模式。
- 蓝牙和 USB 同时连接时，自动模式优先 USB；需要强制蓝牙请在设置窗口选择"仅蓝牙"。
- 如果系统蓝牙栈正忙，诊断窗口里的蓝牙枚举会显示 5 秒超时提示；后台查询完成后再次点击"刷新"即可。

## 更新日志

### v0.6.0

- 新增蓝牙 HID 传输：Windows 已把板子绑定为「蓝牙 HID 设备」时直接读取该集合，不再被 RFCOMM 占用卡住（USB → 蓝牙 HID → RFCOMM 自动选择）。
- 新增映射区域：屏幕上的自定义矩形（`--map-x/--map-y/--map-width/--map-height`），设置窗口和托盘可调。
- 新增映射比例：拉伸铺满 / 等比适应（保持平板纵横比，居中留边）/ 等比填充（裁掉平板多出的部分，屏幕填满且不变形）。
- 平板原始 X/Y 范围进入设置窗口，并支持「捕获平板范围」向导。
- 设置窗口重做为 **输入 / 映射 / 力度 / 诊断** 四个分页。
- 映射页新增平板 ↔ 屏幕实时预览，以及「框选屏幕区域…」两点框选。
- 力度页新增力度曲线可视化（含死区）和两步力度校准向导。
- 修复框选/校准对话框关闭后「暂停输入」被留下的问题：对话框期间的临时暂停现在会正确恢复。
- 新增 `--settings-tab=N`，可直接打开指定分页。
- 显示器名称改用 Windows 显示编号（如「显示器 1（主屏·横屏）」），修复多屏枚举顺序不同导致的选错屏。
- 「框选屏幕区域…」支持用笔点选：移动笔定位、落笔确认；取消后恢复原映射。
- 新增「预览区域（3 秒）」：在目标屏幕上叠加显示实际映射边框和等比内容区。
- 修复多显示器虚拟桌面原点不在 (0,0) 时 Windows Ink 注入坐标整体偏移的问题（表现为屏幕一部分区域映射不到）。
- 修复抬笔瞬间使用抬笔报文坐标导致笔迹/光标跳变：pen-up 固定使用最后一个接触点。
- 蓝牙增加看门狗：收到过笔迹后连续 30 秒无数据会强制重连；缩短重连等待，并把断开原因写入日志。
- 新增抬笔诊断日志：记录抬笔前两条与当前原始报文，以及力度突增。

### v0.5.0

- 设置窗口重新整理：状态栏实时显示最近输入时间、原始/映射力度和侧键映射。
- 新增「诊断与恢复」：重新连接、诊断信息、打开日志、打开设置文件夹、打开蓝牙设置。
- 新增诊断窗口：版本、Windows 版本、运行状态、USB HID、蓝牙配对设备和最近日志，一键复制。
- 日志超过 1 MB 自动轮转，避免长期运行无限增长。
- 再次启动 `BoogieBoardSyncTray.exe` 会唤醒已运行的设置窗口，不再出现「程序已在运行但看不到界面」。
- 系统休眠恢复、锁屏解锁后自动刷新连接；显示器/分辨率变化后自动重新计算映射。
- 新增 `--tray`、`--show-settings`、`--version` 命令行入口。
