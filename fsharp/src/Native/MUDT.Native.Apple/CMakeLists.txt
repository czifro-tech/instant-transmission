project(MUDT.Native.Apple)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

add_definitions(-DPIC=1)

set(NATIVEAPPLE_SOURCES
    pal_io.cpp
)

add_library(MUDT.Native.Apple
    SHARED
    ${NATIVEAPPLE_SOURCES}
    ${VERSION_FILE_PATH}
)

# Disable the "lib" prefix.
set_target_properties(MUDT.Native.Apple PROPERTIES PREFIX "")
set_target_properties(MUDT.Native.Apple PROPERTIES LINKER_LANGUAGE CXX)

target_link_libraries(MUDT.Native.Apple
)

if (APPLE)
    add_custom_command(TARGET MUDT.Native.Apple POST_BUILD
        COMMAND ${CMAKE_INSTALL_NAME_TOOL} -change /usr/lib/libc++.1.dylib @rpath/libc++.1.dylib $<TARGET_FILE:MUDT.Native.Apple>
        COMMAND ${CMAKE_INSTALL_NAME_TOOL} -change /usr/lib/libSystem.B.dylib @rpath/libSystem.B.dylib $<TARGET_FILE:MUDT.Native.Apple>
        COMMAND ${CMAKE_INSTALL_NAME_TOOL} -add_rpath @loader_path $<TARGET_FILE:MUDT.Native.Apple>
        )
endif()

install_library_and_symbols (MUDT.Native.Apple)