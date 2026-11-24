# BoogieBoardSync Pressure and Tray Design

## Intent

Make the Boogie Board Sync a practical daily Windows input device: low-latency pen input with calibrated pressure, instant pen/mouse switching, and a background tray control surface.

## Goals

- Keep the existing vendor HID report path and Windows Ink injection unchanged in latency-critical code.
- Add calibrated raw pressure mapping with configurable min/max, dead zone, gamma curve, and optional smoothing.
- Add runtime switching between Windows Ink pen and absolute mouse mode.
- Add a tray application with pause, input mode, rotation, target screen, pressure preset, autostart, and exit.
- Add global hotkeys: Ctrl+Alt+P pause, Ctrl+Alt+M pen/mouse, Ctrl+Alt+R rotate.
- Persist settings under `%LOCALAPPDATA%\BoogieBoardSync\settings.ini`.
- Map the pen side button to the mouse right button in mouse mode.
- Preserve the console executable for diagnostics.

## Non-goals

- Kernel-mode virtual HID driver.
- File sync from the board's U-disk.
- Touch/gesture recognition or palm rejection beyond the device's own flags.
- Changing the BoogieBoard protocol or firmware behavior.

## Approaches

- A: CLI-only pressure flags and hotkeys. Smallest change, but no background control surface.
- B: Separate tray executable sharing the driver core. Recommended; gives daily usability without changing protocol code.
- C: Kernel-level virtual HID service. Requires signing, admin install, and much higher maintenance cost.

Chosen: B.

## Design Decisions

- Pressure conversion: `normalize(raw, min, max) -> dead zone rescale -> pow(gamma) -> 0..1024`.
- Defaults: raw range `0..1023`, gamma `1.0`, dead zone `0`, smoothing `0` so existing latency is not increased.
- Curve presets: soft `0.70`, linear `1.00`, firm `1.40`.
- Tray settings are runtime snapshots; the reader thread takes one atomic snapshot per report.
- `BoogieBoardSync.exe` remains a console diagnostic binary. `BoogieBoardSyncTray.exe` is a Windows GUI binary.
- Screen mapping supports the full virtual desktop or one monitor. Screen selection is persisted.
- Mouse mode uses absolute `SetCursorPos` and `SendInput`; pen mode remains `InjectSyntheticPointerInput`.
- Autostart uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key, no admin rights.

## Constraints

- .NET Framework 4.8 and C# 5 syntax only.
- No third-party dependencies.
- Build must produce both executables with the existing `csc.exe` toolchain.
- Existing diagnostic flags and protocol commands must remain compatible.
