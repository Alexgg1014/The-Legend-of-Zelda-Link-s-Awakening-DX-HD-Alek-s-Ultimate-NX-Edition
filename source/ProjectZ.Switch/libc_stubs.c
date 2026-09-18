/*
 * libc_stubs.c - POSIX functions that devkitA64's newlib does not provide.
 *
 * The .NET 9 runtime (required since upstream v2.0.5 moved every project to net9.0)
 * references a handful of POSIX calls that the .NET 8 runtime did not. None of them are
 * meaningful on Switch, and none are reachable on the paths this game actually runs:
 *
 *   sigaddset / pthread_sigmask  - PalAttachThread's activation-signal masking. The Switch
 *                                  port already routes thread creation through the shim and
 *                                  has no POSIX signal delivery, so this is a no-op.
 *   shm_open / shm_unlink        - POSIX shared memory (SystemNative_ShmOpen). Unused; fail.
 *   msync                        - flushing a file-backed mapping. Our mappings come from the
 *                                  shim's mmap emulation and are plain anonymous memory, so
 *                                  there is nothing to write back: reporting success is correct.
 *
 * Returning failure rather than aborting keeps a stray call diagnosable instead of fatal.
 */
#include <stddef.h>
#include <errno.h>

int sigaddset(void *set, int signo)
{
    (void)set; (void)signo;
    return 0;
}

int pthread_sigmask(int how, const void *set, void *oldset)
{
    (void)how; (void)set; (void)oldset;
    return 0;
}

int shm_open(const char *name, int oflag, unsigned int mode)
{
    (void)name; (void)oflag; (void)mode;
    errno = ENOSYS;
    return -1;
}

int shm_unlink(const char *name)
{
    (void)name;
    errno = ENOSYS;
    return -1;
}

int msync(void *addr, size_t length, int flags)
{
    (void)addr; (void)length; (void)flags;
    return 0;
}
