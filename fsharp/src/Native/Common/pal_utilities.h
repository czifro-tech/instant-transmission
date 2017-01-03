// This is borrowed from https://github.com/dotnet/corefx/

#pragma once

#include "pal_config.h"

#include <assert.h>
#include <errno.h>
#include <unistd.h>

/**
* Checks if the IO operation was interupted and needs to be retried.
* Returns true if the operation was interupted; otherwise, false.
*/
template <typename TInt>
static inline bool CheckInterrupted(TInt result)
{
    return result < 0 && errno == EINTR;
}

/**
* Converts an intptr_t to a file descriptor.
* intptr_t is the type used to marshal file descriptors so we can use SafeHandles effectively.
*/
inline static int ToFileDescriptor(intptr_t fd)
{
    assert(0 <= fd && fd < sysconf(_SC_OPEN_MAX));

    return static_cast<int>(fd);
}
