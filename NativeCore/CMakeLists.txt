cmake_minimum_required(VERSION 3.20)
project(StreamDVR_Native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Setup vcpkg integration
# Note: User must run cmake with -DCMAKE_TOOLCHAIN_FILE=[path to vcpkg.cmake]

# Qt 6
find_package(Qt6 REQUIRED COMPONENTS Core Gui Qml Quick Multimedia)

# Third Party
find_package(nlohmann_json REQUIRED)
find_package(spdlog REQUIRED)
find_package(fmt REQUIRED)

# Output directory
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# --- Dependencies Setup ---
# Assuming libobs and libmpv are provided via external means (setup_native_deps.ps1)
# or system paths for now, as they are not standard vcpkg ports.
set(DEPS_DIR "${CMAKE_SOURCE_DIR}/deps")
set(LIBOBS_DIR "${DEPS_DIR}/libobs")
set(LIBMPV_DIR "${DEPS_DIR}/libmpv")

include_directories(
    ${LIBOBS_DIR}/include
    ${LIBMPV_DIR}/include
    src
)

link_directories(
    ${LIBOBS_DIR}/bin/64bit
    ${LIBMPV_DIR}
)

# --- Source Management ---
# Controllers
set(CONTROLLERS
    src/controllers/ApplicationController.hpp
    src/controllers/ApplicationController.cpp
)

# Core Logic
set(CORE
    src/core/obs_manager.hpp
    src/core/obs_manager.cpp
    src/core/player_wrapper.hpp
    src/core/player_wrapper.cpp
)

# UI Resources
# qt_add_qml_module requires Qt 6.2+
qt_add_qml_module(StreamDVR
    URI StreamDVR
    VERSION 1.0
    QML_FILES src/ui/Main.qml
)

# --- Executable ---
add_executable(StreamDVR WIN32
    src/main.cpp
    ${CONTROLLERS}
    ${CORE}
)

# --- Linking ---
target_link_libraries(StreamDVR PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Qml
    Qt6::Quick
    Qt6::Multimedia
    nlohmann_json::nlohmann_json
    spdlog::spdlog
    fmt::fmt
    obs
    mpv
)

# --- Post-Build Copies ---
# Copy OBS binaries
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${LIBOBS_DIR}/bin/64bit $<TARGET_FILE_DIR:StreamDVR>
)
# Copy OBS Data
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${LIBOBS_DIR}/data $<TARGET_FILE_DIR:StreamDVR>/data
)
# Copy MPV DLL
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
    ${LIBMPV_DIR}/mpv-2.dll $<TARGET_FILE_DIR:StreamDVR>/mpv-2.dll
)

if(MSVC)
    target_compile_options(StreamDVR PRIVATE /W4 /EHsc /UTF-8)
endif()
