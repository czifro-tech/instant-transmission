
include(CheckFunctionExists)

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    set(PAL_UNIX_NAME "LINUX")
elseif (CMAKE_SYSTEM_NAME STREQUAL Darwin)
    set(PAL_UNIX_NAME "OSX")
else ()
    message(FATAL_ERROR "Unknown platform.  Cannot define PAL_UNIX_NAME, used by RuntimeInformation.")
endif ()

check_function_exists(
    posix_fadvise
    HAVE_POSIX_ADVISE)

configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/Common/pal_config.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/Common/pal_config.h)