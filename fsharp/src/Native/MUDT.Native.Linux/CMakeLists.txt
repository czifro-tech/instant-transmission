project(MUDT.Native.Linux)

set(NATIVELINUX_SOURCES
    pal_io.cpp
)

add_library(MUDT.Native.Linux
    SHARED
    ${NATIVELINUX_SOURCES}
    ${VERSION_FILE_PATH}
)

# Disable the "lib" prefix.
set_target_properties(MUDT.Native.Linux PROPERTIES PREFIX "")
set_target_properties(MUDT.Native.Linux PROPERTIES LINKER_LANGUAGE CXX)

target_link_libraries(MUDT.Native.Linux
)

install_library_and_symbols (MUDT.Native.Linux)