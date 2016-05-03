#include "coreinit.h"
#include "socket.h"

#define assert(x) \
    do { \
        if (!(x)) \
            OSFatal("Assertion failed " #x ".\n"); \
    } while (0)

#if VER == 410
  #include "cafiine410.h"

  #define socket_lib_finish ((void (*)(void))0x10b45d8)
  
  /* Where to install the cafiine handler. */
  #define INSTALL_ADDR ((void *)0x011da800)
#elif VER == 500
  #include "cafiine500.h"

  #define socket_lib_finish ((void (*)(void))0x10bf404)
  
  /* Where to install the cafiine handler. */
  #define INSTALL_ADDR ((void *)0x011dcc00)
#elif VER == 532
  #include "cafiine532.h"

  #define socket_lib_finish ((void (*)(void))0x10c0404)
  
  /* Where to install the cafiine handler. */
  #define INSTALL_ADDR ((void *)0x011dcc00)
#endif

void start() {
    /* Load a good stack */
    asm(
        "lis %r1, 0x1ab5 ;"
        "ori %r1, %r1, 0xd138 ;"
    );

    /* Get a handle to coreinit.rpl. */
    unsigned int coreinit_handle;
    OSDynLoad_Acquire("coreinit.rpl", &coreinit_handle);

    /* Load a few useful symbols. */
    void (*_Exit)(void) __attribute__ ((noreturn));
    void (*DCFlushRange)(const void *, int);
    void *(*OSEffectiveToPhysical)(const void *);
    
    OSDynLoad_FindExport(coreinit_handle, 0, "_Exit", &_Exit);
    OSDynLoad_FindExport(coreinit_handle, 0, "DCFlushRange", &DCFlushRange);
    OSDynLoad_FindExport(
        coreinit_handle, 0, "OSEffectiveToPhysical", &OSEffectiveToPhysical);
    
    assert(_Exit);
    assert(DCFlushRange);
    assert(OSEffectiveToPhysical);
    
    /* Exploit proper begins here. */
    if (OSEffectiveToPhysical((void *)0xa0000000) != (void *)0x31000000) {
        OSFatal("You must run ksploit before installing cafiine.");
    } else {
        /* Second run. */
        
        /* Copy in our resident cafiine client. */
        unsigned int len = sizeof(cafiine_text_bin);
        unsigned char *loc = (unsigned char *)((char *)INSTALL_ADDR + 0xa0000000);
        
        while (len--) {
            loc[len] = cafiine_text_bin[len];
        }

        /* server IP address */
        ((unsigned int *)loc)[0] = PC_IP;
        
        DCFlushRange(loc, sizeof(cafiine_text_bin));
        
        struct magic_t {
            void *real;
            void *replacement;
            void *call;
        } *magic = (struct magic_t *)cafiine_magic_bin;
        len = sizeof(cafiine_magic_bin) / sizeof(struct magic_t);
        
        int *space = (int *)(loc + sizeof(cafiine_text_bin));
        /* Patch branches to it. */
        while (len--) {
            *(int *)(0xa0000000 | (int)magic[len].call) =
                (int)space - 0xa0000000;
                
            *space =
                *(int *)(0xa0000000 | (int)magic[len].real);
            space++;
            *space =
                0x48000002 | (((int)magic[len].real + 4) & 0x03fffffc);
            space++;
            DCFlushRange(space - 2, 8);
            
            *(int *)(0xa0000000 | (int)magic[len].real) =
                0x48000002 | ((int)magic[len].replacement & 0x03fffffc);
            DCFlushRange((int *)(0xa0000000 | (int)magic[len].real), 4);
        }
        
        unsigned int *socket_finish = (unsigned int *)((char *)socket_lib_finish + 0xa0000000);
        socket_finish[0] = 0x38600000;
        socket_finish[1] = 0x4e800020;
    }

    _Exit();
}
