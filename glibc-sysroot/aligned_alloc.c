#include <stdlib.h>

/*
 * glibc 2.16 added aligned_alloc, and the .NET runtime calls it. The glibc 2.12 sysroot carries this one in libc_nonshared.a, which every
 * link against that glibc pulls in, so a library linked against it binds the call to this copy instead of leaving it unresolved. It is hidden,
 * so the library does not export it to the rest of the process.
 */
__attribute__((visibility("hidden"))) void *aligned_alloc(size_t alignment, size_t size)
{
    void *memory = 0;

    return posix_memalign(&memory, alignment, size) == 0 ? memory : 0;
}
