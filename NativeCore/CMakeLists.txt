cmake_minimum_required(VERSION 3.20)
project(StreamDVR_Native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Output directory for binaries
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Dependencies Paths (Assumed to be in 'deps' folder relative to source)
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

# Source Files
file(GLOB_RECURSE SOURCES "src/*.cpp" "src/*.hpp")

# Executable
add_executable(StreamDVR WIN32 ${SOURCES})

# Libraries to link
target_link_libraries(StreamDVR
    obs
    mpv
)

# Copy Dependencies to Output Directory
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${LIBOBS_DIR}/bin/64bit $<TARGET_FILE_DIR:StreamDVR>
)
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${LIBOBS_DIR}/data $<TARGET_FILE_DIR:StreamDVR>/data
)
add_custom_command(TARGET StreamDVR POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
    ${LIBMPV_DIR}/mpv-2.dll $<TARGET_FILE_DIR:StreamDVR>/mpv-2.dll
)

# Configuration for Debug/Release
if(MSVC)
    target_compile_options(StreamDVR PRIVATE /W4 /EHsc)
endif()
