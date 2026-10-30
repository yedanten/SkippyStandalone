cmake_minimum_required(VERSION 3.20)

project(SkippyNative LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if (NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "Skippy.Native 只能编译为 64 位（游戏进程为 x64）。")
endif()

set(MINHOOK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/minhook")

add_library(minhook STATIC
    "${MINHOOK_DIR}/src/hook.c"
    "${MINHOOK_DIR}/src/buffer.c"
    "${MINHOOK_DIR}/src/trampoline.c"
    "${MINHOOK_DIR}/src/hde/hde64.c"
)

target_include_directories(minhook PUBLIC
    "${MINHOOK_DIR}/include"
    "${MINHOOK_DIR}/src"
    "${MINHOOK_DIR}/src/hde"
)

set_target_properties(minhook PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
)

add_library(Skippy.Native SHARED
    src/dllmain.cpp
    src/Worker.cpp
    src/core/GameProbe.cpp
    src/core/HookManager.cpp
    src/core/MemoryPatch.cpp
    src/core/ModuleInfo.cpp
    src/core/Signature.cpp
    src/ipc/SharedStateStore.cpp
)

target_include_directories(Skippy.Native PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/src"
)

target_compile_definitions(Skippy.Native PRIVATE
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    _CRT_SECURE_NO_WARNINGS
)

target_compile_options(Skippy.Native PRIVATE
    /W3
    /permissive-
    /Zc:__cplusplus
    /utf-8
    $<$<CONFIG:Release>:/O2>
)

target_link_libraries(Skippy.Native PRIVATE
    minhook
)

set_target_properties(Skippy.Native PROPERTIES
    PREFIX ""
    OUTPUT_NAME "Skippy.Native"
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
)

# 离线签名验证工具：不需要运行游戏即可检查当前客户端是否仍匹配所有签名。
add_executable(SignatureProbe
    "${CMAKE_CURRENT_SOURCE_DIR}/../../tools/SignatureProbe/main.cpp"
    src/core/Signature.cpp
)

target_include_directories(SignatureProbe PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/src"
)

target_compile_definitions(SignatureProbe PRIVATE
    UNICODE
    _UNICODE
)

target_compile_options(SignatureProbe PRIVATE
    /W3
    /utf-8
)

set_target_properties(SignatureProbe PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
)
