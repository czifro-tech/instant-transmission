// This is an extension to .NET Core native libraries
// This adds the functionality of opening a file
//  with disk cache turned off (O_NONBLOCK)
// MUDT needs this and .Net Core does not currently
//  offer said feature

#ifdef __APPLE__
#include "pal_io.h"
#include "pal_io_common.h"
#include "pal_utilities.h"

static int32_t ConvertOpenFlags(int32_t flags)
{
    int32_t ret = Common_ConvertOpenFlags(flags);

    // Handle platform specific
    if (flags & ~(Common_KnownOpenFlags() | PAL_O_NONBLOCK))
    {
        assert(false && "Unknown Open flag.");
        return -1;
    }

    if (flags & PAL_O_NONBLOCK)
        ret |= O_NONBLOCK;

    assert(ret != -1);
    return ret;
}

extern "C" intptr_t MUDTNative_Open(const char* path, int32_t flags, int32_t mode)
{
    flags = ConvertOpenFlags(flags);
    if (flags == -1)
    {
        errno = EINVAL;
        return -1;
    }

    int result;
    while (CheckInterrupted(result = open(path, flags, static_cast<mode_t>(mode))));
    return result;
}
#endif
