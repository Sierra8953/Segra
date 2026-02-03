cmake_minimum_required(VERSION 3.20)
project(StreamDVR_Native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Setup vcpkg integration
# Note: User must run cmake with -DCMAKE_TOOLCHAIN_FILE=[path to vcpkg.cmake]

# Qt 6
find_package(Qt6 REQUIRED COMPONENTS Core Gui Widgets)

# JSON
find_package(nlohmann_json REQUIRED)

# Logging
find_package(spdlog REQUIRED)

# Output directory
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# --- Dependencies Setup (Manual for libobs/libmpv as they are complex to build via vcpkg on all systems) ---
# We assume they are in 'deps' for now, or installed on system.
# Enhancing this: We can use 'find_library' or assume a standard layout provided by 'setup_native_deps.ps1'

set(DEPS_DIR "${CMAKE_SOURCE_DIR}/deps")
set(LIBOBS_DIR "${DEPS_DIR}/libobs")
set(LIBMPV_DIR "${DEPS_DIR}/libmpv")

# Include Directories
include_directories(
    ${LIBOBS_DIR}/include
    ${LIBMPV_DIR}/include
    src
)

# Library Directories
link_directories(
    ${LIBOBS_DIR}/bin/64bit
    ${LIBMPV_DIR}
)

# --- Sources ---
set(SOURCES
    src/main.cpp
    src/ui/main_window.cpp
    src/ui/mpv_widget.cpp
    src/core/obs_manager.cpp
    src/core/player_wrapper.cpp
)

set(HEADERS
    src/ui/main_window.hpp
    src/ui/mpv_widget.hpp
    src/core/obs_manager.hpp
    src/core/player_wrapper.hpp
)

# --- Qt MOC ---
qt_wrap_cpp(MOC_SOURCES ${HEADERS})

# --- Executable ---
# WIN32 option prevents console window opening
add_executable(StreamDVR WIN32 ${SOURCES} ${MOC_SOURCES} ${HEADERS})

# --- Linking ---
target_link_libraries(StreamDVR PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Widgets
    nlohmann_json::nlohmann_json
    spdlog::spdlog
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

# Configuration for MSVC
if(MSVC)
    target_compile_options(StreamDVR PRIVATE /W4 /EHsc /UTF-8)
endif()
