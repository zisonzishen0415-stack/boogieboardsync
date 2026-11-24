# Pressure and Tray Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or superpowers:subagent-driven-development.

**Goal:** Add calibrated pressure mapping, runtime pen/mouse switching, tray controls, hotkeys, settings persistence, and mouse right-button support.

**Architecture:** Keep HID reading and Windows Ink injection in the existing reader loop. Add a `DriverRuntime` snapshot object, a dedicated `PressureMapper`, and a separate WinForms tray executable that shares the driver core.

**Tech Stack:** .NET Framework 4.8, C# 5, Win32 HID/Pointer APIs, WinForms `NotifyIcon`/`NativeWindow`.

**Spec:** `docs/superpowers/specs/2026-11-24-pressure-tray-design.md`

## Global Constraints

- C# 5 only: no interpolation, null-conditional operators, or expression-bodied members.
- No third-party packages.
- Console diagnostics must keep working.
- Default pressure smoothing is off.
- No admin requirement for normal use or autostart.

## Review Focus

- Pressure below min, above max, zero, and maximum input.
- Switching pen/mouse while the pen is down.
- Invalid or missing screen index.
- Hotkey conflicts or registration failure.
- Multiple instances and shutdown with a blocked HID read.

---

### Task 1: Pressure mapping core

**Files:**
- Create: `src/PressureMapper.cs`
- Modify: `src/Program.cs` self-test and pressure call site

**Interfaces:**
- Produces: `PressureMapper(int rawMin, int rawMax, double gamma, double deadZone, double smoothing)`
- Produces: `int PressureMapper.Map(int raw)`
- Produces: `void PressureMapper.ResetSmoothing()`

- [ ] Implement clamped normalization, dead-zone rescale, gamma, and optional exponential smoothing.
- [ ] Add CLI options `--pressure-min`, `--pressure-max`, `--pressure-curve`, `--pressure-deadzone`, `--pressure-smoothing`.
- [ ] Replace `ScalePressure` usage in the live path with `PressureMapper`.
- [ ] Add self-tests for min, max, gamma, dead zone, and smoothing reset.

### Task 2: Runtime settings and mapper selection

**Files:**
- Create: `src/DriverRuntime.cs`
- Modify: `src/TabletMapper.cs`
- Modify: `src/Program.cs`

**Interfaces:**
- Produces: `DriverRuntime.Snapshot Current`
- Produces: `void DriverRuntime.SetMouseMode(bool)`
- Produces: `void DriverRuntime.SetPaused(bool)`
- Produces: `void DriverRuntime.SetRotate(int)`
- Produces: `void DriverRuntime.SetScreenIndex(int)`
- Produces: `void DriverRuntime.SetPressurePreset(double gamma)`
- Produces: immutable snapshot containing `TabletMapper`, `PressureMapper`, `MouseMode`, `Paused`.

- [ ] Add screen-bounds constructor to `TabletMapper`.
- [ ] Build immutable snapshots and replace them atomically.
- [ ] Make `ReadReports` consume one snapshot per report.
- [ ] Add `--screen=all|1|2...` support.

### Task 3: Pen/mouse runtime switching

**Files:**
- Modify: `src/PenInjector.cs`
- Modify: `src/Program.cs`

- [ ] Make injection mode mutable behind a lock.
- [ ] Release active buttons on mode change.
- [ ] Map side button to mouse right button in mouse mode.
- [ ] Keep pen mode pressure injection unchanged.

### Task 4: Tray application and hotkeys

**Files:**
- Create: `src/SettingsStore.cs`
- Create: `src/TrayContext.cs`
- Create: `src/TrayProgram.cs`
- Modify: `src/Program.cs`
- Modify: `build.ps1`

- [ ] Add `BoogieBoardSyncTray.exe` winexe build with WinForms/Drawing references.
- [ ] Add `NotifyIcon` menu for pause, pen/mouse, rotation, screen, pressure preset, autostart, and exit.
- [ ] Add message-only window hotkeys: Ctrl+Alt+P/M/R.
- [ ] Add single-instance mutex and clean shutdown.

### Task 5: Settings, docs, validation, release

**Files:**
- Modify: `README.md`
- Modify: `build.ps1`

- [ ] Persist settings under `%LOCALAPPDATA%\BoogieBoardSync\settings.ini`.
- [ ] Add autostart registry toggle.
- [ ] Build console and tray executables.
- [ ] Run `--selftest`, `--test`, and `--test --mouse`.
- [ ] Update README and push a new release.
