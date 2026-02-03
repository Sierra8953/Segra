# StreamDVR Native Architecture

This document outlines the architectural decisions for the transition to a fully native C++ application.

## 1. Core Principles

*   **Native Performance**: Minimize overhead by using direct C/C++ APIs (`libobs`, `libmpv`, `Win32`).
*   **Robustness**: The recording process must survive application crashes.
*   **Seamlessness**: The user should not perceive the boundaries between "Live", "Buffer", and "Saved Clip".

## 2. Technology Stack

*   **Language**: C++20 (for modern features and standard library improvements).
*   **Build System**: CMake (Industry standard, cross-platform capable).
*   **Dependency Manager**: vcpkg (Reliable package management for Windows).
*   **UI Framework**: **Qt 6**.
    *   *Rationale*: Used by OBS Studio itself. Provides hardware-accelerated rendering, simplified window management for embedding MPV, and a robust signal/slot mechanism for async operations.
*   **Recording Engine**: **libobs** (OBS Studio Core).
    *   *Direct Integration*: Bypass C# wrappers for direct pointer access and lower latency.
    *   *Output*: `ffmpeg_muxer` configured for **Fragmented MP4 (fMP4)**.
*   **Playback Engine**: **libmpv**.
    *   *Direct Integration*: Embedded via `wid` (Window ID) into a generic `QWidget` or `QWindow`.
    *   *Capabilities*: Hardware decoding (D3D11VA/NVDEC), accurate seeking, and "tailing" of growing files.

## 3. Module Breakdown

### 3.1. Application Core (`App`)
*   Manages the `QApplication` lifecycle.
*   Initializes global singletons (`ObsManager`, `SettingsManager`).
*   Handles system tray integration and single-instance enforcement.

### 3.2. OBS Manager (`ObsManager`)
*   **Responsibilities**:
    *   `Initialize()`: Loads libobs, configures video/audio contexts.
    *   `StartRecording()`: Sets up the fMP4 output pipeline.
    *   `StopRecording()`: Finalizes the file (if possible) and releases resources.
    *   `GetState()`: Returns current recording stats (FPS, Dropped Frames, Disk Space).
*   **Rolling Buffer Strategy**:
    *   Instead of a single indefinite file, `ObsManager` will implement a "Segmented Recorder".
    *   It will trigger a seamless file cut every X minutes (e.g., 5 min) or Y Gigabytes.
    *   Old segments will be pruned automatically to maintain the configured "Buffer Window" (e.g., 1 hour).
    *   *Benefit*: Prevents data loss of the entire session if corruption occurs and simplifies file management.

### 3.3. MPV Player (`MpvWidget`)
*   Inherits from `QWidget`.
*   **Responsibilities**:
    *   Initializes `mpv_handle`.
    *   Embeds video output into the widget's native window handle (`winId()`).
    *   Exposes `Load(path)`, `Seek(time)`, `Pause()`, `Play()`.
    *   **Live Preview**: When "Live", it loads the *current active segment* and appends new segments to the playlist automatically, or uses an EDL (Edit Decision List) for virtual stitching.

### 3.4. Game Detection (`GameDetector`)
*   Uses `SetWinEventHook` (User32) to detect foreground window changes efficiently.
*   Matches process names/window titles against a local database (IGDB dump or user config).
*   Triggers `ObsManager` start/stop signals.

## 4. Data Flow

1.  **Game Launch**: `GameDetector` -> Signal `GameDetected` -> `App`.
2.  **Auto-Record**: `App` -> `ObsManager::StartRecording(GameName)`.
3.  **Live View**: User opens UI -> `MpvWidget` -> `Load(CurrentFile)`.
4.  **Clip Save**:
    *   User selects range [A, B].
    *   `ClipManager` identifies relevant segments (fMP4s).
    *   `ClipManager` invokes `ffmpeg` (via library or process) to *stream copy* (`-c copy`) the exact range into a new file.

## 5. Directory Structure

```
NativeCore/
├── src/
│   ├── main.cpp          # Entry point
│   ├── app.hpp/cpp       # Application class
│   ├── ui/
│   │   ├── main_window.hpp/cpp  # Qt Main Window
│   │   ├── mpv_widget.hpp/cpp   # Custom MPV Widget
│   ├── core/
│   │   ├── obs_manager.hpp/cpp  # libobs wrapper
│   │   ├── game_detector.hpp/cpp
│   │   ├── settings.hpp/cpp
│   ├── utils/
├── vcpkg.json            # Dependencies
├── CMakeLists.txt        # Build config
└── docs/
```
