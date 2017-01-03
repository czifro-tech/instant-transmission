// This contains operations that are common between
//  the platform specific IO operations

#include "pal_utilities.h"

#include <fcntl.h>
#include <stdlib.h>

/**
 * Constants for interpreting the flags passed to Open or ShmOpen.
 * There are several other values defined by POSIX but not implemented
 * everywhere. The set below is restricted to the current needs of
 * COREFX, which increases portability and speeds up conversion. We
 * can add more as needed.
 */
enum
{
    // Access modes (mutually exclusive).
    PAL_O_RDONLY = 0x0000, // Open for read-only
    PAL_O_WRONLY = 0x0001, // Open for write-only
    PAL_O_RDWR = 0x0002,   // Open for read-write

    // Mask to get just the access mode. Some room is left for more.
    // POSIX also defines O_SEARCH and O_EXEC that are not available
    // everywhere.
    PAL_O_ACCESS_MODE_MASK = 0x000F,

    // Flags (combinable)
    // These numeric values are not defined by POSIX and vary across targets.
    PAL_O_CLOEXEC = 0x0010, // Close-on-exec
    PAL_O_CREAT = 0x0020,   // Create file if it doesn't already exist
    PAL_O_EXCL = 0x0040,    // When combined with CREAT, fails if file already exists
    PAL_O_TRUNC = 0x0080,   // Truncate file to length 0 if it already exists
    PAL_O_SYNC = 0x0100,    // Block writes call will block until physically written
};

/**
 * Constants passed to posix_advise to give hints to the kernel about the type of I/O
 * operations that will occur.
 */
enum FileAdvice : int32_t
{
    PAL_POSIX_FADV_NORMAL = 0,     /* no special advice, the default value */
    PAL_POSIX_FADV_RANDOM = 1,     /* random I/O access */
    PAL_POSIX_FADV_SEQUENTIAL = 2, /* sequential I/O access */
    PAL_POSIX_FADV_WILLNEED = 3,   /* will need specified pages */
    PAL_POSIX_FADV_DONTNEED = 4,   /* don't need the specified pages */
    PAL_POSIX_FADV_NOREUSE = 5,    /* data will only be acessed once */
};

/**
 * Constants from sys/file.h for lock types
 */
enum LockOperations : int32_t
{
    PAL_LOCK_SH = 1, /* shared lock */
    PAL_LOCK_EX = 2, /* exclusive lock */
    PAL_LOCK_NB = 4, /* don't block when locking*/
    PAL_LOCK_UN = 8, /* unlock */
};

/**
 * These flags will for the most part be found in Unix based
 *  OSes. So having a common function to parse flags makes sense
 */
static int32_t Common_ConvertOpenFlags(int32_t flags)
{
    int32_t ret;
    switch (flags & PAL_O_ACCESS_MODE_MASK)
    {
        case PAL_O_RDONLY:
            ret = O_RDONLY;
            break;
        case PAL_O_RDWR:
            ret = O_RDWR;
            break;
        case PAL_O_WRONLY:
            ret = O_WRONLY;
            break;
        default:
            assert(false && "Unknown Open access mode.");
            return -1;
    }

    // Handle this on platform specific implementations
    // if (flags & ~(PAL_O_ACCESS_MODE_MASK | PAL_O_CLOEXEC | PAL_O_CREAT | PAL_O_EXCL | PAL_O_TRUNC | PAL_O_SYNC))
    // {
    //     assert(false && "Unknown Open flag.");
    //     return -1;
    // }

    if (flags & PAL_O_CLOEXEC)
        ret |= O_CLOEXEC;
    if (flags & PAL_O_CREAT)
        ret |= O_CREAT;
    if (flags & PAL_O_EXCL)
        ret |= O_EXCL;
    if (flags & PAL_O_TRUNC)
        ret |= O_TRUNC;
    if (flags & PAL_O_SYNC)
        ret |= O_SYNC;

    assert(ret != -1);
    return ret;
}

static int32_t Common_KnownOpenFlags() {
  return PAL_O_ACCESS_MODE_MASK | PAL_O_CLOEXEC | PAL_O_CREAT | PAL_O_EXCL | PAL_O_TRUNC | PAL_O_SYNC;
}


extern "C" intptr_t MUDTNative_Open(const char* path, int32_t flags, int32_t mode);

extern "C" int32_t MUDTNative_PosixFAdvise(intptr_t fd, int64_t offset, int64_t length, FileAdvice advice)
{
#if HAVE_POSIX_ADVISE
    int32_t result;
    while (CheckInterrupted(result = posix_fadvise(ToFileDescriptor(fd), offset, length, advice)));
    return result;
#else
    // Not supported on this platform. Caller can ignore this failure since it's just a hint.
    (void)fd, (void)offset, (void)length, (void)advice;
    return ENOTSUP;
#endif
}

extern "C" int32_t SystemNative_FLock(intptr_t fd, LockOperations operation)
{
    int32_t result;
    while (CheckInterrupted(result = flock(ToFileDescriptor(fd), operation)));
    return result;
}
