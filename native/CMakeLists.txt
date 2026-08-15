cmake_minimum_required(VERSION 3.25)
project(PwaDropNative LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

add_library(PwaDropHook SHARED Hook.cpp)
set_target_properties(PwaDropHook PROPERTIES OUTPUT_NAME "PwaDrop.Hook")
target_link_libraries(PwaDropHook PRIVATE ole32 uuid user32)

add_executable(PwaDropHookHost HookHost.cpp)
set_target_properties(PwaDropHookHost PROPERTIES OUTPUT_NAME "PwaDrop.HookHost")
target_link_libraries(PwaDropHookHost PRIVATE advapi32 bcrypt wintrust)

add_executable(PwaDropNativeProbe NativeProbe.cpp)
set_target_properties(PwaDropNativeProbe PROPERTIES OUTPUT_NAME "PwaDrop.NativeProbe")
target_link_libraries(PwaDropNativeProbe PRIVATE ole32 uuid delayimp shell32 user32)
target_link_options(PwaDropNativeProbe PRIVATE /DELAYLOAD:ole32.dll)

foreach(target PwaDropHook PwaDropHookHost PwaDropNativeProbe)
    target_compile_definitions(${target} PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
    target_compile_options(${target} PRIVATE /W4 /WX /permissive- /guard:cf)
    target_link_options(${target} PRIVATE /guard:cf /CETCOMPAT)
    set_target_properties(${target} PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")
endforeach()
