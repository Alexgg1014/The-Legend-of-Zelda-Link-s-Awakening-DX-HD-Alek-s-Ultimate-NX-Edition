/*
 * nativeaot_shims.c
 *
 * Stub/shim implementations for glibc/POSIX symbols that the NativeAOT runtime
 * (compiled for linux-arm64) expects but are not available in newlib/libnx.
 *
 * FIXES vs previous version:
 *  - stderr initialisation: no longer uses __sf[] directly (unsafe ordering).
 *    Instead we provide a late-init via __attribute__((constructor(200))) and
 *    fall back to /dev/null if the real stderr isn't ready yet.
 *  - mmap64: improved to better match GC expectations (MAP_FIXED, MAP_ANON).
 *  - Added early crash-log to SD card so panics before Main() are visible.
 */
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <errno.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <time.h>
#include <pthread.h>
#include <signal.h>
#include <stdarg.h>

extern void *__real_malloc(size_t size);
extern void *__real_calloc(size_t num, size_t size);
extern void *__real_realloc(void *ptr, size_t size);

void *__wrap_malloc(size_t size) {
    void *ptr = __real_malloc(size);
    if (ptr) memset(ptr, 0, size);
    return ptr;
}

void *__wrap_calloc(size_t num, size_t size) {
    void *ptr = __real_calloc(num, size);
    if (ptr) memset(ptr, 0, num * size); // Calloc naturally zeroes, but enforcing
    return ptr;
}

void *__wrap_realloc(void *ptr, size_t size) {
    void *new_ptr = __real_realloc(ptr, size);
    return new_ptr;
}

int posix_memalign(void **memptr, size_t alignment, size_t size) {
    if (!memptr) return EINVAL;
    void *ptr = aligned_alloc(alignment, size);
    if (!ptr) return ENOMEM;
    memset(ptr, 0, size);
    *memptr = ptr;
    return 0;
}

/* ========================================================================== */
/*  Early crash log — writes a marker to SD card as soon as possible.        */
/*  This lets us see crashes that happen before Main() / C# code runs.       */
/* ========================================================================== */

/* ========================================================================== */
/*  TLS (Thread Local Storage) setup for NativeAOT on Switch                  */
/*                                                                            */
/*  NativeAOT linux-arm64 uses TPIDR_EL0 as a pointer to the ELF TLS block.  */
/*  __GetThreadStaticBase reads: tpidr_el0 + offset -> thread static pointer  */
/*                                                                            */
/*  On Switch, TPIDR_EL0 is unused by Horizon OS (TPIDRRO_EL0 is used for    */
/*  the kernel TLS). We allocate our own TLS block and set TPIDR_EL0 to it.  */
/* ========================================================================== */

#define AOT_TLS_SIZE  65536  /* 64KB for NativeAOT thread statics */

static __attribute__((aligned(4096))) char g_main_tls_block[AOT_TLS_SIZE] = {0};

static inline void setup_aot_tls(void *tls_block) {
    /* NativeAOT reads from tpidr_el0 + small_offset to get thread static bases.
       Set tpidr_el0 to point to our zeroed TLS block. The runtime will
       populate it via GetInlinedThreadStaticBaseSlow on first access. */
    __asm__ volatile ("msr tpidr_el0, %0" : : "r" (tls_block));
}

/* Module base address for dl_iterate_phdr */
static void *s_module_base = NULL;

static void _atexit_handler(void) {
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) { fprintf(f, "[shim] atexit handler - process terminating\n"); fflush(f); fclose(f); }
}

/* Flag to reduce logging after boot completes (volatile so all threads + compiler see the write) */
static volatile int s_boot_complete = 0;

/* Forward declaration so _early_log_init (constructor) can call _trace before it is defined */
static void _trace(const char *msg);

__attribute__((constructor(101)))
static void _early_log_init(void) {
    /* Set up TLS for the main thread */
    memset(g_main_tls_block, 0, AOT_TLS_SIZE);
    setup_aot_tls(g_main_tls_block);
    atexit(_atexit_handler);

    /* Store module base for dl_iterate_phdr.
       _start is at the base of the NRO in memory. */
    extern char _start[];
    s_module_base = (void *)_start;

    /* GCHeapHardLimit: 16 MB (decimal "16000000") — the practical maximum.
       Scaling table (confirmed on hardware):
       - 8 MB  limit -> 656 MB mmap   (ratio ~82x, works)
       - 16 MB limit -> 1776 MB mmap  (ratio ~111x, works)
       - 24 MB limit -> 2896 MB mmap  (ratio ~121x, FAILS — exceeds Switch ~3GB)
       - 512 MB limit -> 106 GB       (FAILS spectacularly)
       Stay at 16 MB; rely on Draw try/catch for OOM during post-load. */
    setenv("DOTNET_GCHeapHardLimit", "16000000", 1);

    /* CRITICAL: Disable concurrent (background) GC.
       The NativeAOT workstation GC uses PalHijack() to suspend game threads
       via pthread_kill signals for background collection. Switch's HOS does
       not reliably deliver these signals, causing PalHijack to time out and
       call abort(). With gcConcurrent=0, all GC runs are stop-the-world and
       PalHijack is never called. GC pauses may be slightly longer but the
       game will not crash from signal delivery failures. */
    setenv("DOTNET_gcConcurrent",   "0",       1);

    /* Write initial message to buffer; will be flushed by __wrap_main */
    _trace("[boot] nativeaot_shims constructors running");
}

/* ========================================================================== */
/*  errno - glibc uses __errno_location(), newlib uses a different mechanism  */
/* ========================================================================== */

/* ========================================================================== */
/*  Boot log — buffered to avoid fopen/fclose on every shim call.            */
/*  After s_boot_complete=1, _trace() becomes a no-op so area transitions   */
/*  don't stall on SD-card writes.                                           */
/* ========================================================================== */

#define TRACE_BUF_SIZE (64 * 1024)   /* 64 KB ring buffer */
static char  s_trace_buf[TRACE_BUF_SIZE];
static int   s_trace_len = 0;
static int   s_trace_flushed = 0;

static void _trace_flush(void) {
    if (s_trace_len == 0) return;
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", s_trace_flushed ? "a" : "w");
    if (f) {
        fwrite(s_trace_buf, 1, s_trace_len, f);
        fflush(f);
        fclose(f);
    }
    s_trace_len = 0;
    s_trace_flushed = 1;
}

static void _trace(const char *msg) {
    /* After boot is complete, stop logging shim noise */
    if (s_boot_complete) return;

    int needed = snprintf(s_trace_buf + s_trace_len,
                          TRACE_BUF_SIZE - s_trace_len,
                          "%s\n", msg);
    if (needed < 0) return;
    s_trace_len += needed;
    if (s_trace_len >= TRACE_BUF_SIZE - 512) {
        /* Buffer nearly full — flush now */
        _trace_flush();
    }
}

int *__errno_location(void) {
    return &errno;
}

/* ========================================================================== */
/*  stderr as a symbol                                                        */
/*                                                                            */
/*  OLD (BROKEN) approach: used &__sf[2] in a constructor, which races with  */
/*  the newlib init order and could fire before stdio was ready.              */
/*                                                                            */
/*  NEW approach: we declare stderr as a weak alias pointing to a safe        */
/*  fallback. Priority 200 fires after newlib's own priority-100 inits.      */
/*  We read the actual stderr pointer from the reent struct at that point.   */
/* ========================================================================== */

#include <sys/reent.h>
#undef stderr
FILE *stderr = NULL;   /* starts NULL — safe if accessed before init */

__attribute__((constructor(200)))
static void _init_stderr(void) {
    /* _REENT is the newlib per-thread reent pointer; _stderr is the file
       pointer stored inside it. This is safe at priority 200 because newlib
       initialises its reent structure during C runtime startup (crt0). */
    struct _reent *r = _REENT;
    if (r && r->_stderr) {
        stderr = r->_stderr;
    }
    
    /* Redirect stderr to SD card so NativeAOT fatal errors are captured! */
    if (stderr != NULL) {
        stderr = freopen("sdmc:/switch/zelda-ladxhd/native_stderr.log", "w", stderr);
    } else {
        stderr = fopen("sdmc:/switch/zelda-ladxhd/native_stderr.log", "w");
    }
    if (!stderr) {
        stderr = fopen("/dev/null", "w");
    }
    if (stderr) {
        /* Disable buffering so abort() doesn't lose logs */
        setvbuf(stderr, NULL, _IONBF, 0);
    }

    /* Update the boot log now that stderr is set up (use trace buffer). */
    _trace("[boot] stderr redirected to native_stderr.log");
}

/* ========================================================================== */
/*  __security_cookie override                                                */
/*                                                                            */
/*  The NativeAOT runtime's __security_cookie lands in .data.rel.ro which     */
/*  the NRO loader (hbl) maps as Read-only (type=9/CodeMutable, perm=1).     */
/*  PalVirtualProtect tries mprotect -> svcSetMemoryPermission but fails      */
/*  because CodeMutable doesn't allow permission changes.                     */
/*                                                                            */
/*  Fix: provide our own __security_cookie in .data (truly RW). With          */
/*  --allow-multiple-definition, our definition wins because nativeaot_shims  */
/*  comes first in link order.                                                */
/* ========================================================================== */

unsigned long long __security_cookie __attribute__((section(".data"), used)) = 0;

/* ========================================================================== */
/*  Exit interception — log when the runtime exits so we know why             */
/* ========================================================================== */

void _exit(int status) __attribute__((noreturn));

/* Override abort() to log before terminating */
void abort(void) __attribute__((noreturn));
void abort(void) {
    /* Flush all streams first */
    if (stderr) fflush(stderr);
    fflush(NULL); /* flush all open streams */

    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[shim] abort() called!\n");

        void *lr = __builtin_return_address(0);
        fprintf(f, "[shim] abort caller: %p\n", lr);

        /* Read NativeAOT crash info buffer (weak symbol - may not exist) */
        extern char g_CrashInfoBuffer[] __attribute__((weak));
        if (&g_CrashInfoBuffer != NULL && g_CrashInfoBuffer[0] != '\0') {
            fprintf(f, "[FAILFAST] %s\n", g_CrashInfoBuffer);
        }

        fflush(f);
        fclose(f);
    }

    /* Copy native_stderr.log content to boot.log */
    FILE *src = fopen("sdmc:/switch/zelda-ladxhd/native_stderr.log", "r");
    if (src) {
        f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) {
            char buf[512];
            while (fgets(buf, sizeof(buf), src))
                fprintf(f, "[stderr] %s", buf);
            fflush(f);
            fclose(f);
        }
        fclose(src);
    }
    _exit(134);
}

/* Override exit() to log before terminating */
void exit(int status) __attribute__((noreturn));
void exit(int status) {
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[shim] exit(%d) called!\n", status);
        fflush(f);
        fclose(f);
    }
    /* Also try to flush native_stderr in case there's an exception message */
    if (stderr) fflush(stderr);
    _exit(status);
}

/* ========================================================================== */
/*  NativeAOT FailFast interception                                           */
/*  RaiseFailFastException is called for fatal runtime errors.                */
/*  Intercept the crash dump function to log the error before abort.          */
/* ========================================================================== */

/* Intercept RaiseFailFastException to capture caller info */
extern void __real_RaiseFailFastException(void);

void __wrap_RaiseFailFastException(void) {
    void *caller = __builtin_return_address(0);
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[CRASH] RaiseFailFastException! caller=%p\n", caller);
        fflush(f);
        fclose(f);
    }
    if (stderr) { fprintf(stderr, "[CRASH] RaiseFailFastException caller=%p\n", caller); fflush(stderr); }
    __builtin_trap();
}

/* ========================================================================== */
/*  GC Thread Suspension — Switch HOS compatibility                           */
/*                                                                            */
/*  PalHijack() is called by ThreadStore::SuspendAllThreads() for every       */
/*  stop-the-world GC collection (both triggered and compulsory). It sends a  */
/*  POSIX signal via pthread_kill() to redirect the target thread to a GC     */
/*  safepoint. On Switch HOS, this signal is never delivered to the thread:   */
/*  WaitForSingleObjectEx() times out waiting for acknowledgment and calls    */
/*  RaiseFailFastException()→abort(). This happens even with gcConcurrent=0  */
/*  because stop-the-world GC still requires ThreadStore::SuspendAllThreads.  */
/*                                                                            */
/*  Fix: returning 0 (false) tells the GC that the thread is ALREADY in       */
/*  preemptive mode (safe to collect without suspension). This is valid for   */
/*  all Switch threads other than the main game thread:                       */
/*   - Audio thread: blocked in OpenAL/libnx native code (preemptive)        */
/*   - Timer threads: sleeping in native libnx syscalls (preemptive)          */
/*   - IO threads: blocked on file I/O (preemptive)                           */
/*  The main game thread (the one triggering the allocation→GC) is already   */
/*  at a safepoint by virtue of being inside RhpNewObject/GCHeap::Alloc.     */
/* ========================================================================== */

int __wrap_PalHijack(void *pThread, void *callback, void *pCallbackContext) {
    (void)pThread; (void)callback; (void)pCallbackContext;
    /* Signal-based thread hijacking is not supported on Switch HOS.
       Return -1 to tell NativeAOT that hijacking failed.
       NativeAOT will then gracefully fallback to cooperative thread suspension,
       waiting for the managed thread to reach a GC Poll (RhpGcPoll) or P/Invoke. */
    return -1;
}

/* ========================================================================== */
/*  Dynamic loading - Switch has no dynamic linker                            */
/* ========================================================================== */

/* MonoGame uses FuncLoader which calls dlopen/dlsym at runtime to load
   SDL2, OpenAL, etc. Since we link statically, we need dlopen to return
   a fake handle and dlsym to resolve symbols from our static link. */

/* Fake handle values */
#define FAKE_HANDLE_SDL2    ((void *)0x1)
#define FAKE_HANDLE_OPENAL  ((void *)0x2)
#define FAKE_HANDLE_SELF    ((void *)0x3)

static char s_dlerror_msg[256] = "";

void *dlopen(const char *filename, int flags) {
    (void)flags;
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");

    if (!filename) {
        if (f) { fprintf(f, "[shim] dlopen(NULL) -> SELF\n"); fclose(f); }
        return FAKE_HANDLE_SELF;
    }

    if (f) { fprintf(f, "[shim] dlopen(%s)\n", filename); fclose(f); }

    if (strstr(filename, "SDL2") || strstr(filename, "sdl2"))
        return FAKE_HANDLE_SDL2;
    if (strstr(filename, "openal") || strstr(filename, "OpenAL") || strstr(filename, "soft_oal"))
        return FAKE_HANDLE_OPENAL;
    /* For any other library, return a generic handle - symbols are all
       statically linked so dlsym will find them regardless */
    if (strstr(filename, ".so") || strstr(filename, ".dylib"))
        return FAKE_HANDLE_SELF;

    snprintf(s_dlerror_msg, sizeof(s_dlerror_msg), "dlopen: %s not found", filename);
    return NULL;
}

int dlclose(void *handle) { (void)handle; return 0; }

void *dlsym(void *handle, const char *symbol) {
    (void)handle;
    if (!symbol) return NULL;

    /* All SDL2, OpenAL, and GL functions are statically linked.
       Use the linker-generated symbol table to find them.
       We declare them as weak externs so missing ones return NULL
       instead of causing a link error. */

    /* Use a helper macro to look up symbols by name */
    #define TRY_SYM(name) do { \
        extern void name(void) __attribute__((weak)); \
        if (strcmp(symbol, #name) == 0) return (void *)&name; \
    } while(0)

    /* For SDL2, OpenAL, GL - the functions are already linked.
       We can use __attribute__((weak)) extern declarations to probe,
       but that requires listing every function.

       Better approach: the symbol is in our global symbol table since
       everything is statically linked. We can use a different trick:
       scan the ELF symbol table at runtime. But that's complex.

       Simplest approach: just return the address of the symbol using
       a GNU extension or dlsym(RTLD_DEFAULT). Since we don't have a
       real dlsym, we need another way.

       PRAGMATIC FIX: MonoGame's FuncLoader calls dlsym and stores
       the result as a function pointer. Since all SDL2/OpenAL/GL
       functions are statically linked, we can use the linker's
       --export-dynamic to make them findable, and use our own
       simple symbol lookup table for the most critical ones. */

    /* First check system/shim functions that the runtime looks up dynamically.
       These are our own shim functions defined later in this file. */
    extern int uname(void *);
    extern char *dlerror(void);
    extern int stat64(const char *, void *);
    extern int open64(const char *, int, ...);
    extern FILE *fopen64(const char *, const char *);
    extern const unsigned char *__wrap_glGetString(unsigned int name);

    /* Return our wrapped glGetString instead of the real one */
    if (strcmp(symbol, "glGetString") == 0)
        return (void *)(uintptr_t)&__wrap_glGetString;
    /* Return our wrapped SDL_GL_GetProcAddress that intercepts GL lookups */
    extern void *__wrap_SDL_GL_GetProcAddress(const char *proc);
    if (strcmp(symbol, "SDL_GL_GetProcAddress") == 0)
        return (void *)(uintptr_t)&__wrap_SDL_GL_GetProcAddress;
    /* Return our wrapped SDL_PollEvent that injects controller events */
    extern int __wrap_SDL_PollEvent(void *event);
    if (strcmp(symbol, "SDL_PollEvent") == 0)
        return (void *)(uintptr_t)&__wrap_SDL_PollEvent;

    #define SYS_SYM(name) if (strcmp(symbol, #name) == 0) return (void *)(uintptr_t)&name
    SYS_SYM(dlopen);
    SYS_SYM(dlclose);
    SYS_SYM(dlsym);
    SYS_SYM(dlerror);
    SYS_SYM(uname);
    SYS_SYM(getcwd);
    SYS_SYM(realpath);
    SYS_SYM(stat64);
    SYS_SYM(open64);
    SYS_SYM(fopen64);
    #undef SYS_SYM

    /* Look up in our static symbol table (generated from libSDL2.a, libopenal.a, etc) */
    extern void *dlsym_lookup(const char *name);
    void *addr = dlsym_lookup(symbol);

    if (!s_boot_complete) {
        FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) { fprintf(f, "[shim] dlsym(%s) -> %p\n", symbol, addr); fclose(f); }
    }
    if (!addr)
        snprintf(s_dlerror_msg, sizeof(s_dlerror_msg), "dlsym: %s not found", symbol);
    return addr;
}

char *dlerror(void) { return s_dlerror_msg[0] ? s_dlerror_msg : "no error"; }
int dladdr(const void *addr, void *info) { return 0; }

/* ---- dl_iterate_phdr ---- */
/* NativeAOT uses this to find .eh_frame for stack unwinding during exception
   handling. We need to provide the NRO's program headers so the unwinder
   can locate the EH frame data. */

/* ELF types for dl_iterate_phdr (newlib doesn't have link.h) */
#include <elf.h>

typedef Elf64_Phdr ElfPhdr;
typedef Elf64_Addr ElfAddr;

struct dl_phdr_info {
    ElfAddr        dlpi_addr;
    const char    *dlpi_name;
    const ElfPhdr *dlpi_phdr;
    uint16_t       dlpi_phnum;
};

extern char __rodata_start[];
extern char __eh_frame_hdr_start[];
extern char __eh_frame_hdr_end[];

int dl_iterate_phdr(int (*callback)(struct dl_phdr_info *info, size_t size, void *data),
                    void *data) {
    if (!s_module_base) {
        _trace("dl_iterate_phdr (no base!)");
        return 0;
    }

    /* Build a minimal set of program headers matching our NRO layout.
       The key one NativeAOT needs is PT_GNU_EH_FRAME or PT_LOAD covering
       the .eh_frame section. We provide the LOAD segments. */
    #define PT_GNU_EH_FRAME 0x6474e550

    ElfAddr base = (ElfAddr)(uintptr_t)s_module_base;
    ElfAddr rodata_off = (ElfAddr)(uintptr_t)__rodata_start - base;

    ElfPhdr phdrs[4];
    memset(phdrs, 0, sizeof(phdrs));

    /* All p_vaddr are offsets relative to module base (dlpi_addr) */

    /* Segment 0: .text (RE) */
    phdrs[0].p_type = PT_LOAD;
    phdrs[0].p_flags = PF_R | PF_X;
    phdrs[0].p_vaddr = 0;
    phdrs[0].p_memsz = rodata_off;
    phdrs[0].p_offset = 0;
    phdrs[0].p_filesz = phdrs[0].p_memsz;
    phdrs[0].p_align = 0x1000;

    /* Segment 1: rodata (R) - contains .eh_frame */
    phdrs[1].p_type = PT_LOAD;
    phdrs[1].p_flags = PF_R;
    phdrs[1].p_vaddr = rodata_off;
    phdrs[1].p_memsz = 0x100000;
    phdrs[1].p_offset = rodata_off;
    phdrs[1].p_filesz = phdrs[1].p_memsz;
    phdrs[1].p_align = 0x1000;

    /* Segment 2: data (RW) */
    phdrs[2].p_type = PT_LOAD;
    phdrs[2].p_flags = PF_R | PF_W;
    phdrs[2].p_vaddr = rodata_off + phdrs[1].p_memsz;
    phdrs[2].p_memsz = 0x1000000;
    phdrs[2].p_offset = phdrs[2].p_vaddr;
    phdrs[2].p_filesz = phdrs[2].p_memsz;
    phdrs[2].p_align = 0x1000;

    /* PT_GNU_EH_FRAME - points to .eh_frame_hdr for stack unwinding.
       p_vaddr must be RELATIVE to module base (dlpi_addr), not absolute. */
    ElfAddr eh_hdr_vaddr = (ElfAddr)(uintptr_t)__eh_frame_hdr_start - base;
    ElfAddr eh_hdr_size  = (ElfAddr)((uintptr_t)__eh_frame_hdr_end - (uintptr_t)__eh_frame_hdr_start);
    phdrs[3].p_type = PT_GNU_EH_FRAME;
    phdrs[3].p_flags = PF_R;
    phdrs[3].p_vaddr = eh_hdr_vaddr;
    phdrs[3].p_offset = eh_hdr_vaddr;
    phdrs[3].p_filesz = eh_hdr_size;
    phdrs[3].p_memsz = eh_hdr_size;
    phdrs[3].p_align = 4;

    struct dl_phdr_info info;
    memset(&info, 0, sizeof(info));
    info.dlpi_addr = (ElfAddr)(uintptr_t)s_module_base;
    info.dlpi_name = "ProjectZ.Switch";
    info.dlpi_phdr = phdrs;
    info.dlpi_phnum = 4;

    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[shim] dl_iterate_phdr: base=%p, phnum=3\n", s_module_base);
        fclose(f);
    }

    return callback(&info, sizeof(info), data);
}

/* ========================================================================== */
/*  Signals                                                                   */
/* ========================================================================== */

__attribute__((weak))
int sigaction(int signum, const struct sigaction *act, struct sigaction *oldact) {
    (void)signum; (void)act; (void)oldact;
    return 0;
}

int pthread_kill(pthread_t thread, int sig) {
    (void)thread; (void)sig;
    return ENOSYS;
}

int __libc_current_sigrtmin(void) { return 32; }
int __libc_current_sigrtmax(void) { return 64; }

/* ========================================================================== */
/*  Memory mapping — GC uses mmap for heap management.                        */
/*                                                                            */
/*  Simple strategy: every mmap is backed by aligned_alloc immediately.       */
/*  MAP_FIXED ("commit") is a no-op because the full range was already        */
/*  physically allocated by the initial VirtualReserve call.                  */
/* ========================================================================== */

#include <switch.h>

#ifndef MAP_ANONYMOUS
#define MAP_ANONYMOUS 0x20
#endif
#ifndef MAP_ANON
#define MAP_ANON MAP_ANONYMOUS
#endif
#define GLIBC_MAP_FIXED 0x10

void *mmap(void *addr, size_t length, int prot, int flags, int fd, off_t offset) {
    (void)prot; (void)fd; (void)offset;
    if (length == 0) return (void*)(uintptr_t)-1;

    /* MAP_FIXED = GC committing sub-pages from an already-allocated block.
       POSIX/Linux MAP_ANON semantics: committed pages MUST be zero-filled.
       Since our munmap is a no-op (we can't free sub-ranges of aligned_alloc),
       pages may hold stale object data from before they were decommitted.
       The GC's mark phase walks heap segments linearly and misinterprets
       that stale data as live objects, reading garbage MT pointers and
       crashing in mark_object_simple1.
       Zero the memory on every commit to restore POSIX semantics. */
    if (flags & GLIBC_MAP_FIXED) {
        if (!addr) return (void*)(uintptr_t)-1;
        memset(addr, 0, length);
        return addr;
    }

    size_t aligned = (length + 0xFFFFFFULL) & ~0xFFFFFFULL;

    void *p = aligned_alloc(0x1000000, aligned);

    /* DIAGNOSTIC: always log every non-MAP_FIXED mmap so we can see the GC's
       allocation pattern even after boot_complete. Remove once stable. */
    {
        FILE *lf = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (lf) {
            fprintf(lf, "[mmap] %zuMB -> %s\n",
                    aligned >> 20, p ? "OK" : "FAILED");
            fflush(lf);
            fclose(lf);
        }
    }

    if (!p) { errno = ENOMEM; return (void*)(uintptr_t)-1; }
    memset(p, 0, aligned);
    return p;
}

void *mmap64(void *addr, size_t length, int prot, int flags, int fd, long long offset) {
    return mmap(addr, length, prot, flags, fd, (off_t)offset);
}

int munmap(void *addr, size_t length) {
    (void)addr; (void)length;
    /* Do NOT free: GC munmaps sub-ranges of a large aligned_alloc block.
       Freeing a sub-pointer corrupts the malloc heap. */
    return 0;
}

/* PROT_* constants from glibc (NativeAOT uses these) */
#define GLIBC_PROT_NONE  0x0
#define GLIBC_PROT_READ  0x1
#define GLIBC_PROT_WRITE 0x2
#define GLIBC_PROT_EXEC  0x4

int mprotect(void *addr, size_t len, int prot) {
    /* Align address down and length up to page boundary */
    uintptr_t page_addr = (uintptr_t)addr & ~0xFFFULL;
    size_t page_len = ((uintptr_t)addr + len - page_addr + 0xFFF) & ~0xFFFULL;

    /* Convert glibc PROT_* to Switch Perm_* */
    u32 nx_perm = 0; /* Perm_None */
    if (prot & GLIBC_PROT_READ)  nx_perm |= 1; /* Perm_R */
    if (prot & GLIBC_PROT_WRITE) nx_perm |= 2; /* Perm_W */
    /* Note: Perm_X (4) is NOT allowed by svcSetMemoryPermission */

    if (!s_boot_complete) {
        FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) {
            fprintf(f, "[shim] mprotect(%p, 0x%lx, %d) -> svc page=%p len=0x%lx perm=%u\n",
                    addr, (unsigned long)len, prot,
                    (void*)page_addr, (unsigned long)page_len, nx_perm);
            fclose(f);
        }
    }

    /* NEVER take write permission away from the NRO's own image.
       Root cause of "touching the screen kills the game" (crash report 2168-0002,
       Data Abort at .data+0xc0, symbolized to the finger-event log counter
       decrement in __wrap_SDL_PollEvent): the NativeAOT runtime unprotects the
       page holding __security_cookie, writes the cookie, then re-protects it
       READ-ONLY. On Linux that page is a private relro page; here the cookie
       lives in .data (see the __security_cookie override) and ModuleCodeMutable
       pages DO accept svcSetMemoryPermission, so the whole first .data page -
       s_phase, the touch log counters, __GCStaticRegion, __EagerCctor - became
       read-only after boot. The first write to any initialized static after
       that was a Data Abort, and the first such write in this port happened to
       be the finger-event counter on the first touch.
       A read-only request on module memory is a no-op here: nothing on this
       platform relies on it, and honoring it is what broke. */
    {
        MemoryInfo qi; u32 qp;
        if (R_SUCCEEDED(svcQueryMemory(&qi, &qp, page_addr)) &&
            (qi.type == MemType_CodeStatic || qi.type == MemType_CodeMutable ||
             qi.type == MemType_ModuleCodeStatic || qi.type == MemType_ModuleCodeMutable) &&
            !(nx_perm & 2)) {
            static int s_logged_ro_refusals = 0;   /* .bss on purpose */
            if (s_logged_ro_refusals < 8) {
                s_logged_ro_refusals++;
                FILE *f3 = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
                if (f3) {
                    fprintf(f3, "[shim] mprotect(%p, 0x%lx, %d): module page type=%u, refusing to drop write perm (no-op)\n",
                            addr, (unsigned long)len, prot, qi.type);
                    fclose(f3);
                }
            }
            return 0;
        }
    }

    /* Try the real svcSetMemoryPermission */
    Result rc = svcSetMemoryPermission((void*)page_addr, page_len, nx_perm);

    if (!s_boot_complete) {
        FILE *f4 = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f4) {
            fprintf(f4, "[shim] svcSetMemoryPermission result: 0x%x %s\n",
                    rc, R_SUCCEEDED(rc) ? "OK" : "FAILED");
            fclose(f4);
        }
    }

    if (R_SUCCEEDED(rc)) return 0;

    /* SVC failed (0xd401 = InvalidMemState). This happens for NRO binary
       pages which are mapped as CodeMutable type - svcSetMemoryPermission
       only works on Heap-type memory. However, the NRO loader already maps
       the .data segment as RW, so the page likely ALREADY has the right
       permissions. Query the actual memory info to check. */
    MemoryInfo meminfo;
    u32 pageinfo;
    Result qrc = svcQueryMemory(&meminfo, &pageinfo, page_addr);

    if (!s_boot_complete) {
        FILE *f2 = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f2) {
            if (R_SUCCEEDED(qrc)) {
                fprintf(f2, "[shim] mprotect svc failed 0x%x, queried page %p: type=%u perm=%u\n",
                        rc, (void*)page_addr, meminfo.type, meminfo.perm);
            } else {
                fprintf(f2, "[shim] mprotect svc failed 0x%x, query also failed 0x%x\n", rc, qrc);
            }
            fclose(f2);
        }
    }

    /* If the page already has write permission, return success */
    if (R_SUCCEEDED(qrc) && (meminfo.perm & 2) /* Perm_W */) {
        return 0; /* Already writable - OK */
    }

    /* Truly not writable and can't change - return failure */
    errno = EACCES;
    return -1;
}

int madvise(void *addr, size_t length, int advice) {
    (void)addr; (void)length; (void)advice;
    return 0;
}

int posix_madvise(void *addr, size_t length, int advice) {
    (void)addr; (void)length; (void)advice;
    return 0;
}

int mlock(const void *addr, size_t len) { (void)addr; (void)len; return 0; }
int munlock(const void *addr, size_t len) { (void)addr; (void)len; return 0; }

/* ========================================================================== */
/*  File I/O - glibc 64-bit file variants                                     */
/* ========================================================================== */

/* glibc file flag constants */
#define GLIBC_O_RDONLY    00
#define GLIBC_O_WRONLY    01
#define GLIBC_O_RDWR      02
#define GLIBC_O_CREAT     0100
#define GLIBC_O_EXCL      0200
#define GLIBC_O_NOCTTY    0400
#define GLIBC_O_TRUNC     01000
#define GLIBC_O_APPEND    02000
#define GLIBC_O_NONBLOCK  04000
#define GLIBC_O_SYNC      010000
#define GLIBC_O_ASYNC     020000

/* Convert POSIX paths to sdmc: paths for libnx filesystem access.
   Paths starting with /switch/ need sdmc: prefix for libnx devoptab. */
static const char *_fix_path(const char *path, char *buf, size_t bufsz) {
    if (!path) return path;
    /* Already has sdmc: prefix anywhere in the path */
    if (strstr(path, "sdmc:") != NULL) {
        /* Extract from sdmc: onwards - handles cases like /switch/zelda-ladxhd/sdmc:/... */
        const char *sdmc = strstr(path, "sdmc:");
        if (sdmc != path) {
            /* sdmc: is embedded - use just the sdmc: part */
            return sdmc;
        }
        return path;
    }
    /* Virtual filesystems - return as-is (will fail gracefully) */
    if (strncmp(path, "/proc", 5) == 0 || strncmp(path, "/sys", 4) == 0 ||
        strncmp(path, "/dev", 4) == 0) return path;
    /* Absolute paths -> prepend sdmc: */
    if (path[0] == '/') {
        snprintf(buf, bufsz, "sdmc:%s", path);
        return buf;
    }
    /* Relative paths -> prepend sdmc:/switch/zelda-ladxhd/ */
    snprintf(buf, bufsz, "sdmc:/switch/zelda-ladxhd/%s", path);
    return buf;
}

int open64(const char *path, int flags, ...) {
    char fixbuf[512];
    const char *fixed = _fix_path(path, fixbuf, sizeof(fixbuf));

    if (!s_boot_complete) {
        FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) { fprintf(f, "[shim] open64(%s) -> %s\n", path, fixed); fclose(f); }
    }

    if (strncmp(fixed, "/proc", 5) == 0 || strncmp(fixed, "/sys", 4) == 0) {
        errno = ENOENT; return -1;
    }

    int newlib_flags = flags & 3;
    if (flags & GLIBC_O_CREAT)    newlib_flags |= O_CREAT;
    if (flags & GLIBC_O_EXCL)     newlib_flags |= O_EXCL;
    if (flags & GLIBC_O_TRUNC)    newlib_flags |= O_TRUNC;
    if (flags & GLIBC_O_APPEND)   newlib_flags |= O_APPEND;
    if (flags & GLIBC_O_NONBLOCK) newlib_flags |= O_NONBLOCK;
    if (flags & GLIBC_O_SYNC)     newlib_flags |= O_SYNC;

    va_list args; va_start(args, flags); int mode = va_arg(args, int); va_end(args);
    int fd = open(fixed, newlib_flags, mode);

    if (!s_boot_complete) {
        FILE *f3 = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f3) { fprintf(f3, "[shim] open64 returned fd=%d (errno=%d)\n", fd, fd < 0 ? errno : 0); fclose(f3); }
    }

    return fd;
}

FILE *fopen64(const char *path, const char *mode) {
    char fixbuf[512];
    const char *fixed = _fix_path(path, fixbuf, sizeof(fixbuf));

    if (!s_boot_complete) {
        char msg[600];
        snprintf(msg, sizeof(msg), "[shim] fopen64(%s) -> %s", path, fixed);
        _trace(msg);
    }

    return fopen(fixed, mode);
}

long long lseek64(int fd, long long offset, int whence) {
    return (long long)lseek(fd, (off_t)offset, whence);
}

int ftruncate64(int fd, long long length) {
    return ftruncate(fd, (off_t)length);
}

long long pread64(int fd, void *buf, size_t count, long long offset) {
    return (long long)pread(fd, buf, count, (off_t)offset);
}

long long pwrite64(int fd, const void *buf, size_t count, long long offset) {
    return (long long)pwrite(fd, buf, count, (off_t)offset);
}

long long preadv64(int fd, const void *iov, int iovcnt, long long offset) {
    (void)fd; (void)iov; (void)iovcnt; (void)offset;
    errno = ENOSYS; return -1;
}

long long pwritev64(int fd, const void *iov, int iovcnt, long long offset) {
    (void)fd; (void)iov; (void)iovcnt; (void)offset;
    errno = ENOSYS; return -1;
}

long long sendfile64(int out_fd, int in_fd, long long *offset, size_t count) {
    (void)out_fd; (void)in_fd; (void)offset; (void)count;
    errno = ENOSYS; return -1;
}

int fallocate64(int fd, int mode, long long offset, long long len) {
    (void)fd; (void)mode; (void)offset; (void)len;
    return ENOSYS;
}

int posix_fadvise64(int fd, long long offset, long long len, int advice) {
    (void)fd; (void)offset; (void)len; (void)advice;
    return 0;
}

/* ========================================================================== */
/*  stat conversion: newlib struct stat -> glibc struct stat                   */
/*                                                                            */
/*  NativeAOT (linux-arm64) expects glibc's struct stat layout (128 bytes).   */
/*  Newlib uses a completely different layout (104 bytes).                     */
/*  We must convert between the two formats.                                  */
/* ========================================================================== */

/* glibc aarch64 struct stat layout */
struct glibc_stat {
    unsigned long  st_dev;       /* 0 */
    unsigned long  st_ino;       /* 8 */
    unsigned int   st_mode;      /* 16 */
    unsigned int   st_nlink;     /* 20 */
    unsigned int   st_uid;       /* 24 */
    unsigned int   st_gid;       /* 28 */
    unsigned long  st_rdev;      /* 32 */
    unsigned long  __pad1;       /* 40 */
    long           st_size;      /* 48 */
    int            st_blksize;   /* 56 */
    int            __pad2;       /* 60 */
    long           st_blocks;    /* 64 */
    long           st_atime_sec; /* 72 */
    long           st_atime_nsec;/* 80 */
    long           st_mtime_sec; /* 88 */
    long           st_mtime_nsec;/* 96 */
    long           st_ctime_sec; /* 104 */
    long           st_ctime_nsec;/* 112 */
    int            _reserved[2]; /* 120 */
}; /* 128 bytes total */

static void newlib_to_glibc_stat(const struct stat *src, struct glibc_stat *dst) {
    memset(dst, 0, sizeof(*dst));
    dst->st_dev     = src->st_dev;
    dst->st_ino     = src->st_ino;
    dst->st_mode    = src->st_mode;
    dst->st_nlink   = src->st_nlink;
    dst->st_uid     = src->st_uid;
    dst->st_gid     = src->st_gid;
    dst->st_rdev    = src->st_rdev;
    dst->st_size    = src->st_size;
    dst->st_blksize = src->st_blksize;
    dst->st_blocks  = src->st_blocks;
    dst->st_atime_sec = src->st_atime;
    dst->st_mtime_sec = src->st_mtime;
    dst->st_ctime_sec = src->st_ctime;
}

int stat64(const char *path, void *buf) {
    char fixbuf[512];
    const char *fixed = _fix_path(path, fixbuf, sizeof(fixbuf));
    struct stat newlib_buf;
    int rc = stat(fixed, &newlib_buf);
    if (rc == 0 && buf)
        newlib_to_glibc_stat(&newlib_buf, (struct glibc_stat *)buf);
    return rc;
}

int fstat64(int fd, void *buf) {
    struct stat newlib_buf;
    int rc = fstat(fd, &newlib_buf);
    if (rc == 0 && buf)
        newlib_to_glibc_stat(&newlib_buf, (struct glibc_stat *)buf);
    return rc;
}

int lstat64(const char *path, void *buf) {
    char fixbuf[512];
    const char *fixed = _fix_path(path, fixbuf, sizeof(fixbuf));
    struct stat newlib_buf;
    int rc = stat(fixed, &newlib_buf);  /* no lstat on Switch */
    if (rc == 0 && buf)
        newlib_to_glibc_stat(&newlib_buf, (struct glibc_stat *)buf);
    return rc;
}

int __xstat64(int ver, const char *path, void *buf) {
    (void)ver;
    return stat64(path, buf);
}

int __lxstat64(int ver, const char *path, void *buf) {
    (void)ver;
    return lstat64(path, buf);
}

int __fxstat64(int ver, int fd, void *buf) {
    (void)ver;
    return fstat64(fd, buf);
}

int chdir(const char *path) {
    char fixbuf[512];
    const char *fixed = _fix_path(path, fixbuf, sizeof(fixbuf));
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) { fprintf(f, "[shim] chdir(%s) -> %s\n", path, fixed); fclose(f); }
    return 0; // faked - Switch has no real cwd concept
}

char *getcwd(char *buf, size_t size) {
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) { fprintf(f, "[shim] getcwd\n"); fclose(f); }

    /* Return a clean POSIX path. The sdmc:/ prefix is for libnx devoptab
       but NativeAOT expects standard POSIX paths. */
    const char *cwd = "/switch/zelda-ladxhd";
    size_t len = strlen(cwd) + 1;
    if (!buf) {
        buf = __real_malloc(size > len ? size : len);
    } else if (size < len) {
        errno = ERANGE;
        return NULL;
    }
    if (buf) strcpy(buf, cwd);
    return buf;
}

int fstatfs64(int fd, void *buf) {
    (void)fd;
    if (buf) memset(buf, 0, 120);
    return 0;
}

int statfs64(const char *path, void *buf) {
    (void)path;
    if (buf) memset(buf, 0, 120);
    return 0;
}

int getrlimit64(int resource, void *rlim) {
    (void)resource; (void)rlim;
    return -1;
}

#include <dirent.h>

/* opendir needs path fixing too - NativeAOT uses it for Directory.Enumerate */
extern DIR *__real_opendir(const char *name);
DIR *__wrap_opendir(const char *name) {
    char fixbuf[512];
    const char *fixed = _fix_path(name, fixbuf, sizeof(fixbuf));
    /* Only log during boot; after boot opendir is called every frame for Mods */
    if (!s_boot_complete) {
        char msg[600];
        snprintf(msg, sizeof(msg), "[shim] opendir(%s) -> %s", name, fixed);
        _trace(msg);
    }
    return __real_opendir(fixed);
}

/* glibc struct dirent64 layout for readdir64/readdir64_r */
struct glibc_dirent64 {
    unsigned long  d_ino;       /* 0 */
    long           d_off;       /* 8 */
    unsigned short d_reclen;    /* 16 */
    unsigned char  d_type;      /* 18 */
    char           d_name[256]; /* 19 */
}; /* ~280 bytes */

static void newlib_to_glibc_dirent(const struct dirent *src, struct glibc_dirent64 *dst) {
    /* Don't memset the entire struct - just set the fields we need.
       The caller's buffer may have a stack canary right after it. */
    dst->d_ino = src->d_ino;
    dst->d_off = 0;
    dst->d_reclen = 280; /* sizeof struct glibc_dirent64 */
    dst->d_type = src->d_type;
    /* Copy name carefully - only the exact length needed */
    size_t namelen = strlen(src->d_name);
    if (namelen > 255) namelen = 255;
    memcpy(dst->d_name, src->d_name, namelen);
    dst->d_name[namelen] = '\0';
}

/* Static buffer for readdir64 (not thread-safe but sufficient for game use) */
static struct glibc_dirent64 s_dirent64_buf;

struct glibc_dirent64 *readdir64(void *dirp) {
    struct dirent *entry = readdir((DIR *)dirp);
    if (!entry) return NULL;
    newlib_to_glibc_dirent(entry, &s_dirent64_buf);
    return &s_dirent64_buf;
}

int readdir64_r(void *dirp, void *entry, void **result) {
    /* Use readdir (simpler and safer than readdir_r which is deprecated).
       .NET only has one thread per directory enumeration, so this is safe. */
    struct dirent *newlib_result = readdir((DIR *)dirp);
    if (newlib_result != NULL) {
        newlib_to_glibc_dirent(newlib_result, (struct glibc_dirent64 *)entry);
        if (result) *result = entry;
        return 0;
    }
    if (result) *result = NULL;
    return 0; /* end of directory (not an error) */
}

/* ========================================================================== */
/*  String helpers                                                            */
/* ========================================================================== */

char *__strdup(const char *s) {
    return strdup(s);
}

/* realpath - .NET BCL uses this to resolve paths on Linux.
   Critically, AppContext.BaseDirectory calls realpath("/proc/self/exe")
   to find the executable path. We return the NRO location on SD card. */
char *realpath(const char *restrict path, char *restrict resolved_path) {
    const char *result;

    /* /proc/self/exe -> return the NRO path on SD card */
    if (path && strcmp(path, "/proc/self/exe") == 0) {
        result = "/switch/zelda-ladxhd/ProjectZ.Switch.nro";
    } else {
        static char fixbuf[512];
        result = _fix_path(path, fixbuf, sizeof(fixbuf));
    }

    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) { fprintf(f, "[shim] realpath(%s) -> %s\n", path ? path : "null", result); fclose(f); }

    if (!resolved_path) {
        resolved_path = __real_malloc(512);
        if (!resolved_path) { errno = ENOMEM; return NULL; }
    }
    strncpy(resolved_path, result, 511);
    resolved_path[511] = '\0';
    return resolved_path;
}


/* ========================================================================== */
/*  Stdout/Stderr Syscall Intercepts via Linker Wrapper                       */
/* ========================================================================== */

extern ssize_t __real_write(int fd, const void *buf, size_t count);

ssize_t __wrap_write(int fd, const void *buf, size_t count) {
    if (fd == 1 || fd == 2) {
        FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) {
            fprintf(f, "[fd%d] ", fd);
            fwrite(buf, 1, count, f);
            if (count > 0 && ((const char*)buf)[count-1] != '\n')
                fputc('\n', f);
            fclose(f);
        }
        return count;
    }
    return __real_write(fd, buf, count);
}

extern int __real_main(int argc, char **argv);

/* Fallback argv[0] used when the loader supplies no argument vector.
   libnx leaves argc=0 / argv=NULL unless the launcher passes an Argv config
   entry in the homebrew ABI. hbmenu does; several loaders and emulators
   (e.g. Eden) do not. NativeAOT's StartupCodeHelpers.GetMainMethodArguments()
   unconditionally allocates `new string[argc - 1]` to strip argv[0], so argc==0
   asks for a -1 length array -> EH.FailedAllocation -> OverflowException before
   a single line of game code runs. Guaranteeing argc>=1 keeps that subtraction
   at zero and hands Main an empty args array, which is what it expects. */
static char *s_fallback_argv[2];

int __wrap_main(int argc, char **argv) {
    if (argc < 1 || argv == NULL || argv[0] == NULL) {
        s_fallback_argv[0] = "sdmc:/switch/zelda-ladxhd/ProjectZ.Switch.nro";
        s_fallback_argv[1] = NULL;
        argc = 1;
        argv = s_fallback_argv;
        _trace("[boot] no argv from loader - substituting fallback argv[0]");
    }

    /* Flush all buffered boot-time trace messages in one SD-card write */
    _trace("[boot] calling __real_main");
    {
        char tmp[64];
        snprintf(tmp, sizeof(tmp), "[boot] about to call __real_main at %p", (void*)__real_main);
        _trace(tmp);
    }
    _trace_flush();

    /* After flush, silence per-call shim logging to avoid SD-card stalls */
    s_boot_complete = 1;

    int ret = __real_main(argc, argv);

    /* Log exit code — s_boot_complete is already 1 so write directly */
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[boot] __real_main returned: %d\n", ret);
        fflush(f);
        fclose(f);
    }
    /* Flush stderr to capture any .NET exception output */
    if (stderr) fflush(stderr);
    return ret;
}

/* ========================================================================== */
/*  Process/system                                                            */
/* ========================================================================== */

int execv(const char *path, char *const argv[]) {
    (void)path; (void)argv;
    errno = ENOSYS; return -1;
}

int waitpid(int pid, int *status, int options) {
    (void)pid; (void)status; (void)options;
    errno = ECHILD; return -1;
}

int pipe(int pipefd[2]) {
    (void)pipefd;
    errno = ENOSYS; return -1;
}

int pipe2(int pipefd[2], int flags) {
    (void)pipefd; (void)flags;
    errno = ENOSYS; return -1;
}

int flock(int fd, int operation) {
    (void)fd; (void)operation;
    return 0;
}

unsigned int if_nametoindex(const char *ifname) {
    (void)ifname;
    return 0;
}

int prctl(int option, ...) {
    (void)option;
    return 0;
}

/* ========================================================================== */
/*  sysconf / sysinfo / uname                                                */
/* ========================================================================== */

long sysconf(int name) {
    /* Log only during boot — GC probes this hundreds of times per second during gameplay */
    if (!s_boot_complete) {
        char msg[32];
        snprintf(msg, sizeof(msg), "[shim] sysconf(%d)", name);
        _trace(msg);
    }

    switch (name) {
        case _SC_PAGESIZE:          return 4096;
        case 30:                    return 4096; /* glibc _SC_PAGESIZE */
        case _SC_NPROCESSORS_ONLN:  return 4;
        case 84:                    return 4;    /* glibc _SC_NPROCESSORS_ONLN */
        case _SC_NPROCESSORS_CONF:  return 4;
        case 83:                    return 4;    /* glibc _SC_NPROCESSORS_CONF */
        case 85:                    return 65536;/* glibc _SC_PHYS_PAGES (fake 256MB RAM) */
        case 86:                    return 65536;/* glibc _SC_AVPHYS_PAGES (fake 256MB avail) */
        case 11:                    return 65536;/* newlib _SC_PHYS_PAGES (fake 256MB RAM) */
        default:                    return -1;
    }
}

/* glibc struct sysinfo on aarch64 is 112 bytes */
int sysinfo(void *info) {
    _trace("sysinfo");
    if (info) {
        memset(info, 0, 112);
        unsigned long *p = (unsigned long *)info;
        p[0] = 1000;       /* uptime */
        p[4] = 268435456;  /* totalram (256MB) */
        p[5] = 268435456;  /* freeram */
        /* mem_unit at offset 104 (on aarch64) */
        unsigned int *u = (unsigned int *)((char *)info + 104);
        *u = 1;            /* mem_unit = 1 byte */
    }
    return 0;
}

int uname(void *buf) {
    _trace("uname");
    if (buf) {
        memset(buf, 0, 390);
        /* Report as Linux so .NET and MonoGame use Linux code paths */
        memcpy(buf, "Linux", 5);                    /* sysname */
        memcpy((char *)buf + 65, "switch", 6);      /* nodename */
        memcpy((char *)buf + 130, "5.0.0", 5);      /* release */
        memcpy((char *)buf + 195, "aarch64", 7);    /* machine (at offset 260 on 64-bit) */
        memcpy((char *)buf + 260, "aarch64", 7);    /* machine (alt offset) */
    }
    return 0;
}

/* ARM64 auxiliary vector types the NativeAOT runtime queries */
#define AT_HWCAP  16
#define AT_HWCAP2 26
#define AT_PAGESZ 6

/* ARM64 HWCAP bits for Cortex-A57 (Switch Tegra X1) */
#define HWCAP_FP       (1 << 0)
#define HWCAP_ASIMD    (1 << 1)   /* NEON */
#define HWCAP_AES      (1 << 3)
#define HWCAP_PMULL    (1 << 4)
#define HWCAP_SHA1     (1 << 5)
#define HWCAP_SHA2     (1 << 6)
#define HWCAP_CRC32    (1 << 7)
#define HWCAP_ATOMICS  (1 << 8)

long getauxval(unsigned long type) {
    if (!s_boot_complete) {
        char msg[32];
        snprintf(msg, sizeof(msg), "[shim] getauxval(%lu)", type);
        _trace(msg);
    }

    switch (type) {
        case AT_HWCAP:
            return HWCAP_FP | HWCAP_ASIMD | HWCAP_AES | HWCAP_PMULL |
                   HWCAP_SHA1 | HWCAP_SHA2 | HWCAP_CRC32;
        case AT_HWCAP2:
            return 0;
        case AT_PAGESZ:
            return 4096;
        default:
            return 0;
    }
}

int getrusage(int who, void *usage) {
    _trace("getrusage");
    /* glibc struct rusage on aarch64 = 128 bytes (two struct timeval + 14 longs) */
    if (usage) memset(usage, 0, 128);
    return 0;
}

int getdomainname(char *name, size_t len) {
    if (name && len > 0) name[0] = '\0';
    return 0;
}

/* ========================================================================== */
/*  Epoll                                                                     */
/* ========================================================================== */

int epoll_create1(int flags) {
    (void)flags;
    errno = ENOSYS; return -1;
}

int epoll_ctl(int epfd, int op, int fd, void *event) {
    (void)epfd; (void)op; (void)fd; (void)event;
    errno = ENOSYS; return -1;
}

int epoll_wait(int epfd, void *events, int maxevents, int timeout) {
    (void)epfd; (void)events; (void)maxevents; (void)timeout;
    errno = ENOSYS; return -1;
}

/* ========================================================================== */
/*  Clock                                                                     */
/* ========================================================================== */

int clock_nanosleep(int clock_id, int flags, const struct timespec *request,
                    struct timespec *remain) {
    _trace("clock_nanosleep");
    (void)clock_id; (void)flags;
    return nanosleep(request, remain);
}

/* ========================================================================== */
/*  Thread naming                                                             */
/* ========================================================================== */

int pthread_setname_np(pthread_t thread, const char *name) {
    (void)thread; (void)name;
    return 0;
}

int pthread_getattr_np(pthread_t thread, void *attr) {
    (void)thread; (void)attr;
    return ENOSYS;
}

/* ========================================================================== */
/*  Pthread Wrappers for TLS                                                  */
/* ========================================================================== */

extern int __real_pthread_create(pthread_t *thread, const pthread_attr_t *attr,
                                 void *(*start_routine) (void *), void *arg);

typedef struct {
    void *(*start_routine)(void *);
    void *arg;
    char *tls_block;
} PthreadThunkArg;

static void *_pthread_thunk(void *arg) {
    PthreadThunkArg *thunk_arg = (PthreadThunkArg *)arg;
    void *(*real_start)(void *) = thunk_arg->start_routine;
    void *real_arg = thunk_arg->arg;
    char *tls = thunk_arg->tls_block;
    free(thunk_arg);

    /* Set up TLS for this new thread */
    setup_aot_tls(tls);

    return real_start(real_arg);
}

int __wrap_pthread_create(pthread_t *thread, const pthread_attr_t *attr,
                          void *(*start_routine) (void *), void *arg) {
    PthreadThunkArg *thunk_arg = __real_malloc(sizeof(PthreadThunkArg));
    if (!thunk_arg) return ENOMEM;

    /* Allocate a TLS block for the new thread */
    char *tls = (char *)aligned_alloc(4096, AOT_TLS_SIZE);
    if (!tls) { free(thunk_arg); return ENOMEM; }
    memset(tls, 0, AOT_TLS_SIZE);

    thunk_arg->start_routine = start_routine;
    thunk_arg->arg = arg;
    thunk_arg->tls_block = tls;

    /* FORCE 1MB STACK SIZE TO PREVENT STACK OVERFLOWS CORRUPTING ThreadVars */
    pthread_attr_t forced_attr;
    if (attr) {
        forced_attr = *attr;
    } else {
        pthread_attr_init(&forced_attr);
    }
    /* Set stack size to 1MB. If the original attr had a smaller stack, this overrides it. */
    pthread_attr_setstacksize(&forced_attr, 1024 * 1024);

    int rc = __real_pthread_create(thread, &forced_attr, _pthread_thunk, thunk_arg);
    
    if (!attr) {
        pthread_attr_destroy(&forced_attr);
    }
    
    return rc;
}

/* ========================================================================== */
/*  Scheduler                                                                 */
/* ========================================================================== */

int sched_getaffinity(int pid, size_t cpusetsize, void *mask) {
    (void)pid;
    if (mask) memset(mask, 0xFF, cpusetsize);
    return 0;
}

int __sched_cpucount(size_t setsize, const void *set) {
    (void)setsize; (void)set;
    return 4;
}

/* ========================================================================== */
/*  Terminal                                                                  */
/* ========================================================================== */

int tcgetattr(int fd, void *termios_p) {
    (void)fd; (void)termios_p;
    errno = ENOTTY; return -1;
}

int tcsetattr(int fd, int optional_actions, const void *termios_p) {
    (void)fd; (void)optional_actions; (void)termios_p;
    errno = ENOTTY; return -1;
}

/* ========================================================================== */
/*  User/group                                                                */
/* ========================================================================== */

uid_t geteuid(void) { return 0; }
gid_t getegid(void) { return 0; }

int getgroups(int size, gid_t *list) {
    (void)size; (void)list;
    return 0;
}

int getpwuid_r(uid_t uid, void *pwd, char *buf, size_t buflen, void **result) {
    (void)uid; (void)pwd; (void)buf; (void)buflen;
    if (result) *result = NULL;
    return ENOENT;
}

/* ========================================================================== */
/*  OpenGL ES compatibility - MonoGame DesktopGL expects desktop GL           */
/*  extensions that don't exist in GLES. Wrap glGetString to inject them.     */
/* ========================================================================== */

/* The real glGetString from Mesa GLES */
extern const unsigned char *__real_glGetString(unsigned int name);

static const char *s_patched_extensions = NULL;

const unsigned char *__wrap_glGetString(unsigned int name) {
    const unsigned char *result = __real_glGetString(name);

    /* GL_EXTENSIONS = 0x1F03 */
    if (name == 0x1F03 && result) {
        if (!s_patched_extensions) {
            /* Append desktop GL extension names that MonoGame checks for.
               In GLES 2.0/3.0, framebuffer objects are core functionality,
               so the functions exist - MonoGame just needs to see the names. */
            const char *real_ext = (const char *)result;
            size_t len = strlen(real_ext);
            const char *extra = " GL_ARB_framebuffer_object GL_EXT_framebuffer_object"
                                " GL_ARB_texture_multisample"
                                " GL_ARB_depth_texture GL_ARB_texture_float"
                                " GL_ARB_half_float_pixel GL_ARB_half_float_vertex";
            char *buf = __real_malloc(len + strlen(extra) + 2);
            if (buf) {
                strcpy(buf, real_ext);
                strcat(buf, extra);
                s_patched_extensions = buf;
            }
        }
        if (s_patched_extensions)
            return (const unsigned char *)s_patched_extensions;
    }

    return result;
}

/* ========================================================================== */
/*  SDL_PollEvent wrapper - inject controller events on Switch                */
/* ========================================================================== */

/* SDL event type constants, verified against devkitPro SDL2 2.28.5 SDL_events.h
   AND against MonoGame 3.8.5's own Sdl.EventType enum (Platform/SDL/SDL2.cs):
     SDL_JOYAXISMOTION        = 0x600 ... SDL_JOYDEVICEADDED = 0x605
     SDL_CONTROLLERAXISMOTION = 0x650 ... SDL_CONTROLLERDEVICEADDED = 0x653

   MonoGame 3.8.5 declares ControllerDeviceAdded = 0x653 but NEVER handles it in
   SDLGamePlatform's event switch. Controller registration happens exclusively via:
     JoyDeviceAdded -> Joystick.AddDevices() -> Joystick.AddDevice(i)
                    -> if (IsGameController(i)) GamePad.AddDevice(i)
   So JOYDEVICEADDED is the event that actually matters. */
#define SDL_JOYDEVICEADDED        0x605
#define SDL_CONTROLLERDEVICEADDED 0x653

/* Mirrors SDL_ControllerDeviceEvent {Uint32 type; Uint32 timestamp; Sint32 which;}
   padded to sizeof(SDL_Event) == 56 on 64-bit, so we never write past the caller's
   buffer. (The previous declaration padded to 64 bytes, which was wrong.) */
typedef struct {
    unsigned int type;
    unsigned int timestamp;
    int which;         /* joystick device index for the ADDED event */
    char padding[44];
} SwitchSDLEvent;

static void _input_log(const char *msg) {
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/input.log", "a");
    if (f) { fprintf(f, "%s\n", msg); fflush(f); fclose(f); }
}

/* ========================================================================== */
/*  Breadcrumb ring buffer                                                    */
/*                                                                            */
/*  Held natively rather than in managed memory so it survives an abort() or  */
/*  fail-fast, where the CLR is already past the point of running C# code.    */
/*  C# pushes entries through SwitchBreadcrumb; both the managed crash        */
/*  handler and __wrap_abort dump the same buffer.                            */
/* ========================================================================== */

#define BC_SLOTS 512
#define BC_TEXT  224
static char s_bc[BC_SLOTS][BC_TEXT];
static int  s_bc_head = 0;      /* next write slot */
static int  s_bc_total = 0;     /* total ever written */

/* --------------------------------------------------------------------------
   Crash-proof live logger.

   The RAM ring did not survive TEST 1: the process vanished without reaching
   abort(), exit(), RaiseFailFastException or the watchdog, so nothing ever
   dumped it. input.log, however, DID survive earlier failures - and it is
   written with exactly this open/write/flush/close-per-line pattern.

   So anything that must survive an instant kill goes through here. fclose() is
   what commits on the libnx sdmc devoptab; this newlib has no fsync(), so the
   close is the durability barrier. One syscall burst per line is fine because
   only navigation/state-transition boundaries use it - never per frame.
   -------------------------------------------------------------------------- */

#define CRASH_DIR "sdmc:/switch/zelda-ladxhd/crash"

static void _ensure_crash_dir(void) {
    static int done = 0;
    if (done) return;
    done = 1;
    mkdir("sdmc:/switch", 0777);
    mkdir("sdmc:/switch/zelda-ladxhd", 0777);
    mkdir(CRASH_DIR, 0777);
}

/* Exported to C#: append one line to crash/<filename>, flushed and closed. */
void SwitchLiveLog(const char *filename, const char *text) {
    if (!filename || !text) return;
    _ensure_crash_dir();

    char path[256];
    snprintf(path, sizeof(path), "%s/%s", CRASH_DIR, filename);

    FILE *f = fopen(path, "a");
    if (!f) return;
    fputs(text, f);
    fputc('\n', f);
    fflush(f);
    fclose(f);           /* commits to the SD filesystem */
}

/* Current execution phase. Overwritten (never appended), so per-frame position
   tracking costs one strncpy and cannot flood the ring the way the old per-frame
   breadcrumbs did - those filled all 192 slots with ~1.6s of FRAME lines and pushed
   out every event that mattered. */
static char s_phase[192] = "(none)";
static unsigned long s_phase_seq = 0;

void SwitchSetPhase(const char *text) {
    if (!text) return;
    strncpy(s_phase, text, sizeof(s_phase) - 1);
    s_phase[sizeof(s_phase) - 1] = ' ';
    s_phase_seq++;
}

void SwitchBreadcrumb(const char *text) {
    if (!text) return;
    int slot = s_bc_head;
    s_bc_head = (s_bc_head + 1) % BC_SLOTS;
    s_bc_total++;
    strncpy(s_bc[slot], text, BC_TEXT - 1);
    s_bc[slot][BC_TEXT - 1] = '\0';
}

/* Writes the ring (oldest -> newest) to the crash directory. Safe to call from
   an abort path: no allocation, single fopen, flushed and closed immediately. */
static void _dump_impl(const char *path, const char *reason, const char *mode) {
    FILE *f = fopen(path ? path : "sdmc:/switch/zelda-ladxhd/crash/breadcrumbs.log", mode);
    if (!f) return;

    fprintf(f, "\n===== BREADCRUMB DUMP: %s =====\n", reason ? reason : "(no reason)");
    fprintf(f, "LAST PHASE: %s   (phase transitions: %lu)\n", s_phase, s_phase_seq);
    fprintf(f, "total breadcrumbs recorded: %d (ring holds last %d)\n", s_bc_total, BC_SLOTS);

    int count = s_bc_total < BC_SLOTS ? s_bc_total : BC_SLOTS;
    int start = s_bc_total < BC_SLOTS ? 0 : s_bc_head;
    for (int i = 0; i < count; i++) {
        int slot = (start + i) % BC_SLOTS;
        int seq  = s_bc_total - count + i + 1;
        fprintf(f, "%5d | %s\n", seq, s_bc[slot]);
    }
    fprintf(f, "===== END BREADCRUMB DUMP =====\n");
    fflush(f);
    fclose(f);
}

/* Appending dump: used for real crashes, so history is preserved. */
void SwitchDumpBreadcrumbs(const char *path, const char *reason) {
    _dump_impl(path, reason, "a");
}

/* Truncating dump: used by the 1s periodic mirror so ring_latest.log stays small and
   always shows only the most recent state, instead of growing to 14k lines / 75 dumps. */
void SwitchDumpBreadcrumbsLatest(const char *path, const char *reason) {
    _dump_impl(path, reason, "w");
}

extern int __real_SDL_PollEvent(void *event);
static int s_controllers_injected = 0;

/* Global gamepad state readable from C# via P/Invoke */
static void *s_controllers[8] = {0};
static int s_num_controllers = 0;

/* Exported: get gamepad button state. Returns bitmask of pressed buttons.
   Called from C# InputHandler via P/Invoke. */
unsigned int SwitchGetGamepadButtons(int index) {
    if (index < 0 || index >= s_num_controllers || !s_controllers[index])
        return 0;

    extern unsigned char SDL_GameControllerGetButton(void *gc, int button);
    void *gc = s_controllers[index];

    unsigned int buttons = 0;
    /* SDL_CONTROLLER_BUTTON mapping to game CButtons:
       A=0, B=1, X=2, Y=3, Back=4, Guide=5, Start=6,
       LeftStick=7, RightStick=8, LeftShoulder=9, RightShoulder=10,
       DpadUp=11, DpadDown=12, DpadLeft=13, DpadRight=14 */
    if (SDL_GameControllerGetButton(gc, 0))  buttons |= 0x0001; /* A */
    if (SDL_GameControllerGetButton(gc, 1))  buttons |= 0x0002; /* B */
    if (SDL_GameControllerGetButton(gc, 2))  buttons |= 0x0004; /* X */
    if (SDL_GameControllerGetButton(gc, 3))  buttons |= 0x0008; /* Y */
    if (SDL_GameControllerGetButton(gc, 4))  buttons |= 0x0010; /* Select/Back */
    if (SDL_GameControllerGetButton(gc, 6))  buttons |= 0x0020; /* Start */
    if (SDL_GameControllerGetButton(gc, 9))  buttons |= 0x0040; /* LB */
    if (SDL_GameControllerGetButton(gc, 10)) buttons |= 0x0080; /* RB */
    if (SDL_GameControllerGetButton(gc, 11)) buttons |= 0x0100; /* Up */
    if (SDL_GameControllerGetButton(gc, 12)) buttons |= 0x0200; /* Down */
    if (SDL_GameControllerGetButton(gc, 13)) buttons |= 0x0400; /* Left */
    if (SDL_GameControllerGetButton(gc, 14)) buttons |= 0x0800; /* Right */
    if (SDL_GameControllerGetButton(gc, 7))  buttons |= 0x1000; /* LS */
    if (SDL_GameControllerGetButton(gc, 8))  buttons |= 0x2000; /* RS */
    return buttons;
}

/* Exported: get gamepad axis. axis: 0=LX, 1=LY, 2=RX, 3=RY, 4=LT, 5=RT */
short SwitchGetGamepadAxis(int index, int axis) {
    if (index < 0 || index >= s_num_controllers || !s_controllers[index])
        return 0;
    extern short SDL_GameControllerGetAxis(void *gc, int axis);
    return SDL_GameControllerGetAxis(s_controllers[index], axis);
}

/* Exported: get number of connected controllers */
int SwitchGetNumControllers(void) { return s_num_controllers; }
/* ========================================================================== */
/*  Touch screen — read from libnx HID directly, not from SDL.                */
/*                                                                            */
/*  Same reasoning that forced the synthetic JOYDEVICEADDED below: MonoGame    */
/*  only learns about input from events it observes in its own SDL_PollEvent   */
/*  loop, and this SDL build has already been caught not delivering the ones   */
/*  we needed. Depending on SDL_FINGER* arriving would be betting on the same  */
/*  horse twice. The pads ended up being read straight from HID; so is this.   */
/*                                                                            */
/*  The wrapper below still LOGS any SDL finger event it happens to see, so    */
/*  if SDL does deliver them that is recorded and the path can be simplified   */
/*  later on evidence rather than on hope.                                     */
/*                                                                            */
/*  Coordinates are the PHYSICAL panel's: 0..1279 x 0..719, origin top-left,   */
/*  and they do not rotate with the FLIP canvas. Undoing that rotation is      */
/*  AleksLayout.MapTouch's job, on the C# side.                               */
/* ========================================================================== */
static int s_touch_init = 0;
static int s_touch_logged = 0;          /* .bss: never keep mutable state in .data */
static int s_sdl_finger_logged = 0;
static int s_touch_hints_set = 0;

/* Exported: first active touch point. Returns the number of fingers down
   (0 when the screen is not being touched), and fills out_x/out_y with the
   first one. Called from C# every frame via P/Invoke. */
int SwitchGetTouch(int *out_x, int *out_y) {
    if (!s_touch_init) {
        s_touch_init = 1;
        /* Refcounted service guard in libnx: harmless if SDL already did it,
           and it turns "no touches ever" from a mystery into a logged Result. */
        Result rc = hidInitialize();
        hidInitializeTouchScreen();
        char msg[96];
        snprintf(msg, sizeof(msg),
                 "[touch] hidInitialize rc=0x%X, touchscreen initialised", (unsigned)rc);
        _input_log(msg);
    }

    HidTouchScreenState st;
    memset(&st, 0, sizeof(st));
    if (hidGetTouchScreenStates(&st, 1) < 1) return 0;
    if (st.count < 1) return 0;

    int x = (int)st.touches[0].x;
    int y = (int)st.touches[0].y;
    if (out_x) *out_x = x;
    if (out_y) *out_y = y;

    /* Budgeted: input.log is already hundreds of KB and this runs per frame
       while a finger is down. 60 samples is plenty to see the coordinate space. */
    if (s_touch_logged < 60) {
        s_touch_logged++;
        char msg[128];
        snprintf(msg, sizeof(msg), "[touch] raw fingers=%d x=%d y=%d",
                 (int)st.count, x, y);
        _input_log(msg);
    }
    return (int)st.count;
}


/* One JOYDEVICEADDED still owed to MonoGame (it re-enumerates everything itself). */
static int s_pending_joyadded = 0;
static unsigned int s_last_buttons[8];
static int s_have_last_buttons = 0;

/*
 * Why this wrapper exists.
 *
 * MonoGame's SDL GamePad backend does not enumerate controllers by polling; it builds
 * its device table exclusively from SDL_CONTROLLERDEVICEADDED events seen in its own
 * SDL_PollEvent loop. On Switch the pads are already present when SDL initialises, so
 * by the time MonoGame starts pumping events there is no ADDED event left to observe,
 * and GamePad.GetState(0..3).IsConnected stays false on every index.
 *
 * The previous version of this wrapper opened the controllers here and exposed them to
 * C# through SwitchGetGamepadButtons/Axis. That made input work, but only by bypassing
 * GamePadState entirely - which also meant Triggers.Left/Right were never populated,
 * so ZL/ZR could never drive Game Scale.
 *
 * First attempt injected SDL_CONTROLLERDEVICEADDED, which did nothing. Reading
 * MonoGame 3.8.5's SDLGamePlatform.cs shows why: its event switch handles
 * ControllerDeviceRemoved, ControllerButtonUp/Down and ControllerAxisMotion, but has
 * no case for ControllerDeviceAdded at all - the constant is declared and never used.
 * The only path that ever reaches GamePad.AddDevice is:
 *
 *   JoyDeviceAdded -> Joystick.AddDevices() -> for each joystick index
 *                  -> Joystick.AddDevice(i) -> if IsGameController(i) -> GamePad.AddDevice(i)
 *
 * Since AddDevices() re-enumerates every joystick itself and ignores the event's
 * 'which' field, a single synthetic JOYDEVICEADDED registers all pads at once.
 */
int __wrap_SDL_PollEvent(void *event) {
    extern int SDL_NumJoysticks(void);
    extern int SDL_IsGameController(int index);
    extern void *SDL_GameControllerOpen(int index);
    extern const char *SDL_GameControllerNameForIndex(int joystick_index);
    extern int SDL_JoystickGetDeviceInstanceID(int device_index);

    if (!s_controllers_injected) {
        s_controllers_injected = 1;

        int num = SDL_NumJoysticks();
        char msg[192];
        snprintf(msg, sizeof(msg), "[input] startup: SDL_NumJoysticks=%d", num);
        _input_log(msg);

        for (int i = 0; i < num && i < 8; i++) {
            int isgc = SDL_IsGameController(i);
            const char *name = isgc ? SDL_GameControllerNameForIndex(i) : NULL;
            int iid = SDL_JoystickGetDeviceInstanceID(i);

            /* Opened here only so the diagnostic helpers below can read raw state.
               SDL refcounts opens, so MonoGame opening the same device is fine. */
            void *gc = NULL;
            if (isgc && s_num_controllers < 8) {
                gc = SDL_GameControllerOpen(i);
                if (gc) s_controllers[s_num_controllers++] = gc;
            }

            snprintf(msg, sizeof(msg),
                "[input] idx=%d isGameController=%d instanceId=%d opened=%s name=\"%s\"",
                i, isgc, iid, gc ? "yes" : "no", name ? name : "(null)");
            _input_log(msg);

            if (isgc)
                s_pending_joyadded = 1;
        }

        snprintf(msg, sizeof(msg), "[input] will inject JOYDEVICEADDED(0x605): %s",
                 s_pending_joyadded ? "yes" : "no game controllers found");
        _input_log(msg);
    }

    /* Hand MonoGame one synthetic JOYDEVICEADDED. Its handler calls
       Joystick.AddDevices(), which enumerates every joystick and promotes the
       game-controller ones to GamePad, so one event covers all pads. */
    if (s_pending_joyadded && event) {
        SwitchSDLEvent *ev = (SwitchSDLEvent *)event;
        s_pending_joyadded = 0;
        ev->type = SDL_JOYDEVICEADDED;
        ev->timestamp = 0;
        ev->which = 0;   /* device index; MonoGame's handler ignores it */
        _input_log("[input] -> injected JOYDEVICEADDED (MonoGame will enumerate all pads)");
        return 1;
    }

    /* Raw button diagnostics: log only on change, never per frame. */
    if (s_num_controllers > 0) {
        if (!s_have_last_buttons) {
            s_have_last_buttons = 1;
            for (int i = 0; i < 8; i++) s_last_buttons[i] = 0;
        }
        for (int i = 0; i < s_num_controllers; i++) {
            unsigned int b = SwitchGetGamepadButtons(i);
            if (b != s_last_buttons[i]) {
                char msg[160];
                snprintf(msg, sizeof(msg),
                    "[input] raw pad%d buttons=0x%04X  LX=%d LY=%d RX=%d RY=%d LT=%d RT=%d",
                    i, b,
                    SwitchGetGamepadAxis(i, 0), SwitchGetGamepadAxis(i, 1),
                    SwitchGetGamepadAxis(i, 2), SwitchGetGamepadAxis(i, 3),
                    SwitchGetGamepadAxis(i, 4), SwitchGetGamepadAxis(i, 5));
                _input_log(msg);
                s_last_buttons[i] = b;
            }
        }
    }

    /* Touch NEVER reaches MonoGame. Tapping the screen was killing the process:
       MonoGame 3.8.5's SDL platform turns SDL_FINGER* into TouchPanel events and
       SDL additionally synthesizes MOUSE events from touch (which == SDL_TOUCH_MOUSEID),
       neither of which this port wants or has ever exercised. The panel reads the
       touch screen itself through HID (SwitchGetTouch), so every finger event and
       every touch-synthesized mouse event is swallowed here, before MonoGame's event
       switch can see it. Hints are set too so SDL stops generating the mouse ones.
         SDL_MOUSEMOTION=0x400 .. SDL_MOUSEWHEEL=0x403, `which` at offset 12.
         SDL_FINGERDOWN=0x700, FINGERUP=0x701, FINGERMOTION=0x702. */
    if (!s_touch_hints_set) {
        s_touch_hints_set = 1;
        extern int SDL_SetHint(const char *name, const char *value);
        SDL_SetHint("SDL_TOUCH_MOUSE_EVENTS", "0");
        SDL_SetHint("SDL_MOUSE_TOUCH_EVENTS", "0");
    }

    for (;;) {
        int polled = __real_SDL_PollEvent(event);
        if (!polled || !event) return polled;

        unsigned int t = ((SwitchSDLEvent *)event)->type;
        int drop = 0;
        if (t >= 0x700 && t <= 0x702) {
            drop = 1;
        } else if (t >= 0x400 && t <= 0x403) {
            unsigned int which = ((unsigned int *)event)[3];
            if (which == 0xFFFFFFFFu) drop = 1;   /* SDL_TOUCH_MOUSEID */
        }
        if (!drop) return polled;

        if (s_sdl_finger_logged < 20) {
            s_sdl_finger_logged++;
            char msg[96];
            snprintf(msg, sizeof(msg), "[touch] SDL event 0x%X descartado (no llega a MonoGame)", t);
            _input_log(msg);
        }
        /* ...and poll again: the caller never learns this event existed. */
    }
}

/* Wrap SDL_GL_GetProcAddress to intercept GL function lookups */
extern void *__real_SDL_GL_GetProcAddress(const char *proc);

/* GL diagnostic wrappers */
static int s_gl_diag_logged = 0;

extern unsigned int glGetError(void);
static unsigned int (*s_real_glGetError)(void) = NULL;

/* Wrap glCompileShader to log shader compilation */
extern void glCompileShader(unsigned int shader);
extern void glGetShaderiv(unsigned int shader, unsigned int pname, int *params);
extern void glGetShaderInfoLog(unsigned int shader, int maxLength, int *length, char *infoLog);

static void (*s_real_glCompileShader)(unsigned int) = NULL;
static void (*s_real_glGetShaderiv)(unsigned int, unsigned int, int *) = NULL;
static void (*s_real_glGetShaderInfoLog)(unsigned int, int, int *, char *) = NULL;

void __wrap_diag_glCompileShader(unsigned int shader) {
    if (s_real_glCompileShader) s_real_glCompileShader(shader);

    /* Check compilation status */
    if (s_real_glGetShaderiv) {
        int status = 0;
        s_real_glGetShaderiv(shader, 0x8B81 /* GL_COMPILE_STATUS */, &status);

        if (!status) {
            /* Shader FAILED: always write, this is critical data */
            FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
            if (f) {
                fprintf(f, "[GL] glCompileShader(%u) -> FAILED\n", shader);
                if (s_real_glGetShaderInfoLog) {
                    char log[512]; int len = 0;
                    s_real_glGetShaderInfoLog(shader, sizeof(log) - 1, &len, log);
                    log[len] = '\0';
                    fprintf(f, "[GL] Shader error: %s\n", log);
                }
                fclose(f);
            }
        } else {
            /* Shader OK: use buffered trace (no-op after boot) */
            char msg[48];
            snprintf(msg, sizeof(msg), "[GL] glCompileShader(%u) -> OK", shader);
            _trace(msg);
        }
    }
}

void *__wrap_SDL_GL_GetProcAddress(const char *proc) {
    /* Intercept glGetString to return our patched version */
    if (proc && strcmp(proc, "glGetString") == 0)
        return (void *)&__wrap_glGetString;

    void *addr = __real_SDL_GL_GetProcAddress(proc);

    /* Capture real GL function pointers for diagnostics */
    if (proc) {
        if (strcmp(proc, "glGetError") == 0) s_real_glGetError = (void *)addr;
        if (strcmp(proc, "glCompileShader") == 0) {
            s_real_glCompileShader = (void *)addr;
            addr = (void *)&__wrap_diag_glCompileShader; /* intercept */
        }
        if (strcmp(proc, "glGetShaderiv") == 0) s_real_glGetShaderiv = (void *)addr;
        if (strcmp(proc, "glGetShaderInfoLog") == 0) s_real_glGetShaderInfoLog = (void *)addr;
    }

    /* Log GL version once on first proc lookup */
    if (!s_gl_diag_logged && addr) {
        s_gl_diag_logged = 1;
        /* Query GL_VERSION (0x1F02) and GL_RENDERER (0x1F01) */
        const unsigned char *ver = __wrap_glGetString(0x1F02);
        const unsigned char *ren = __wrap_glGetString(0x1F01);
        FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
        if (f) {
            fprintf(f, "[GL] Version: %s\n", ver ? (char*)ver : "NULL");
            fprintf(f, "[GL] Renderer: %s\n", ren ? (char*)ren : "NULL");
            fclose(f);
        }
    }

    return addr;
}

/* ========================================================================== */
/*  Networking stubs                                                          */
/* ========================================================================== */

int getaddrinfo(const char *n, const char *s, const void *h, void **r) {
    (void)n; (void)s; (void)h; (void)r; return -1;
}
void freeaddrinfo(void *r) { (void)r; }
const char *gai_strerror(int e) { (void)e; return "no network"; }
int getnameinfo(const void *a, unsigned int b, char *c, unsigned int d,
                char *e, unsigned int f, int g) {
    (void)a;(void)b;(void)c;(void)d;(void)e;(void)f;(void)g; return -1;
}
int getifaddrs(void **ifap) { (void)ifap; return -1; }
void freeifaddrs(void *ifa) { (void)ifa; }

/* ========================================================================== */
/*  syscall                                                                   */
/* ========================================================================== */

long syscall(long number, ...) {
    if (!s_boot_complete) {
        char msg[32];
        snprintf(msg, sizeof(msg), "[shim] syscall(%ld)", number);
        _trace(msg);
    }

    // sys_sysinfo (179) - CoreCLR queries this for RAM size!
    if (number == 179) {
        va_list args;
        va_start(args, number);
        void *info = va_arg(args, void*);
        va_end(args);

        if (info) {
            memset(info, 0, 112); /* struct sysinfo on aarch64 = 112 bytes */
            unsigned long *ptr = (unsigned long *)info;
            ptr[0] = 1000;                // uptime
            ptr[4] = 262144;              // totalram (1GB in 4KB pages if mem_unit=4096)
            ptr[5] = 262144;              // freeram
            // On aarch64, mem_unit is usually at offset 104, but just to be safe, 
            // since the system might have a different ABI layout, we write it to both 96 and 104
            unsigned int *uptr1 = (unsigned int *)((char*)info + 96);
            unsigned int *uptr2 = (unsigned int *)((char*)info + 104);
            *uptr1 = 4096;
            *uptr2 = 4096;
        }
        return 0;
    }

    errno = ENOSYS;
    return -1;
}

/* ========================================================================== */
/*  OpenSSL/Crypto stubs                                                      */
/* ========================================================================== */

int CryptoNative_OpenSslAvailable(void) { return 0; }
long CryptoNative_OpenSslVersionNumber(void) { return 0; }
int CryptoNative_EnsureOpenSslInitialized(void) { return 1; }
int CryptoNative_EvpCipherFinalEx(void *a, void *b, int *c) { (void)a;(void)b;(void)c; return 0; }

#undef sigemptyset
int sigemptyset(sigset_t *set) {
    if (set) memset(set, 0, sizeof(sigset_t));
    return 0;
}

int futimens(int fd, const struct timespec times[2]) {
    (void)fd; (void)times;
    return 0;
}

/* ========================================================================== */
/*  __wrap_abort: log before dying so we see the abort source                */
/* ========================================================================== */
void __wrap_abort(void) {
    void *caller = __builtin_return_address(0);

    /* Flush every open stream before trapping. The managed unhandled-exception text is
       written to the redirected stderr, which is block-buffered on a file: without this
       the last (most useful) lines of native_stderr.log are lost on abort. fflush(NULL)
       flushes all output streams, so this also preserves any in-flight diagnostic logs. */
    fflush(NULL);

    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/boot.log", "a");
    if (f) {
        fprintf(f, "[shim] abort() caller=%p\n", caller);
        fflush(f);
        fclose(f);
    }

    /* Record the abort itself, then dump the whole ring so the last managed
       navigation/new-game breadcrumbs before the native death are preserved. */
    {
        char m[128];
        snprintf(m, sizeof(m), "NATIVE abort() caller=%p phase=%s", caller, s_phase);
        SwitchBreadcrumb(m);
        SwitchDumpBreadcrumbs("sdmc:/switch/zelda-ladxhd/crash/breadcrumbs.log", m);
    }

    /* Flush again - fprintf above may have re-buffered, and stderr may have received
       more output from the runtime between the first flush and here. */
    fflush(NULL);

    __builtin_trap();
}


