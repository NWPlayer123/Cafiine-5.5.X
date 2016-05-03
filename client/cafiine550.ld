OUTPUT(cafiine550.elf);

SECTIONS {
	.text 0x011dcc00 : {
		server_ip = .;
		. = . + 4;
		*(.text._start);
		*(.text*);
		*(.magicptr*);
	}
	.magic : {
		*(.magic*);
	}
	/DISCARD/ : {
		*(*);
	}
}

/* FSA methods */
PROVIDE(FSAInit = 0x10612F4);
PROVIDE(FSAShutdown = 0x10613BC);
PROVIDE(FSAAddClient = 0x1065EB4);
PROVIDE(FSADelClient = 0x10614EC);
PROVIDE(FSAOpenFile = 0x1062C40);

/* FS methods */
PROVIDE(FSInit = 0x1068E10);
PROVIDE(FSShutdown = 0x1068F80);
PROVIDE(FSAddClientEx = 0x1069044);
PROVIDE(FSDelClient = 0x1069450);
PROVIDE(FSOpenFile = 0x106F9C4);
PROVIDE(FSCloseFile = 0x106FAD0);
PROVIDE(FSReadFile = 0x106FB50);
PROVIDE(FSReadFileWithPos = 0x106FBDC);
PROVIDE(FSGetPosFile = 0x106FF08);
PROVIDE(FSSetPosFile = 0x106FF78);
PROVIDE(FSGetStatFile = 0x106FFE8);
PROVIDE(FSIsEof = 0x1070058);

/* GX2 methods */
PROVIDE(GX2WaitForVsync = 0x1151964);

/* Socket methods */
PROVIDE(socket_lib_init = 0x10C02F4);
PROVIDE(socket = 0x10C21C8);
PROVIDE(socketclose = 0x10C2314);
PROVIDE(connect = 0x10C0828);
PROVIDE(send = 0x10C16AC);
PROVIDE(recv = 0x10C0AEC);

/* Standard library methods */
PROVIDE(memcpy = 0x1035FC8);
PROVIDE(memset = 0x1035FB4);
PROVIDE(MEMAllocFromDefaultHeapEx = 0x1004F870);

/* OS data */
PROVIDE(title_id = 0x10013C10);
