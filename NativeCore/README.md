# StreamDVR Native (C++)

This project is a transition from the C# Segra codebase to a fully native C++ application for Windows 11.

## Prerequisites

*   **CMake** (3.20+)
*   **Visual Studio 2022** (C++ Desktop Development workload)
*   **Dependencies**:
    *   `libobs`: You need the OBS Studio headers and libraries.
    *   `libmpv`: Headers and import library.

## Setup

1.  Run `setup_native_deps.ps1` to prepare the directory structure and download `libmpv`.
2.  **Important**: You must populate `NativeCore/deps/libobs` with a valid OBS Studio development environment (headers in `include`, binaries/libs in `bin/64bit`).
    *   You may need to generate `obs.lib` from `obs.dll` if using a release build.

## Building

1.  Open `NativeCore` folder in Visual Studio or use CMake from command line:
    ```cmd
    cd NativeCore
    mkdir build
    cd build
    cmake ..
    cmake --build . --config Release
    ```

2.  Run the executable from `NativeCore/build/bin/Release/StreamDVR.exe`.

## Architecture

*   **Main**: Native Win32 API window creation and message loop.
*   **Recorder**: Direct integration with `libobs` C API for recording. Configured for Fragmented MP4 (fMP4) for robustness.
*   **Player**: Direct integration with `libmpv` C API, embedded into the application window using the `wid` option.
