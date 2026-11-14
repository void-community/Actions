#include <dlfcn.h>
#include <pthread.h>
#include <stdint.h>
#include <stdlib.h>
void *aligned_alloc(size_t alignment, size_t size);
static void *run(void *argument) { return argument; }
int probe(void) {
    pthread_t thread;
    pthread_attr_t attributes;
    void *memory = aligned_alloc(64, 256);
    if (memory == 0 || ((uintptr_t)memory & 63) != 0) return 2;
    free(memory);
    pthread_attr_init(&attributes);
    if (pthread_create(&thread, &attributes, run, 0) != 0) return 1;
    pthread_join(thread, 0);
    return dlopen("libm.so.6", RTLD_NOW) == 0;
}
