#include "main.h"

static int recvwait(int sock, void *buffer, int len);
static int recvbyte(int sock);
static int sendwait(int sock, const void *buffer, int len);
static int sendbyte(int sock, unsigned char value);

static int cafiine_handshake(int sock);

#define CHECK_ERROR(cond) if (cond) { goto error; }

#define BYTE_NORMAL 0xff
#define BYTE_SPECIAL 0xfe
#define BYTE_OPEN 0x00
#define BYTE_READ 0x01
#define BYTE_CLOSE 0x02
#define BYTE_OK 0x03
#define BYTE_SETPOS 0x04
#define BYTE_STATFILE 0x05
#define BYTE_EOF 0x06
#define BYTE_GETPOS 0x07

void GX2WaitForVsync(void);

void cafiine_connect(int *psock) {
	extern unsigned int server_ip;
	struct sockaddr_in addr;
	int sock, ret;

	socket_lib_init();

	sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	CHECK_ERROR(sock == -1);

	addr.sin_family = AF_INET;
	addr.sin_port = 7332;
	addr.sin_addr.s_addr = server_ip;

	ret = connect(sock, (void *)&addr, sizeof(addr));
	CHECK_ERROR(ret < 0);
	ret = cafiine_handshake(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);

	*psock = sock;
	return;
error:
	if (sock != -1)
		socketclose(sock);
	*psock = -1;
}

void cafiine_disconnect(int sock) {
	CHECK_ERROR(sock == -1);
	socketclose(sock);
error:
	return;
}

static int cafiine_handshake(int sock) {
	int ret;
	unsigned char buffer[16];

	memcpy(buffer, &title_id, 16);

	ret = sendwait(sock, buffer, sizeof(buffer));
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	return ret;
error:
	return ret;
}

int cafiine_fopen(int sock, int *result, const char *path, const char *mode, int *handle) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	int len_path = 0;
	while (path[len_path++]);
	int len_mode = 0;
	while (mode[len_mode++]);

	{
		char buffer[1 + 8 + len_path + len_mode];
		buffer[0] = BYTE_OPEN;
		*(int *)(buffer + 1) = len_path;
		*(int *)(buffer + 5) = len_mode;
		for (ret = 0; ret < len_path; ret++)
			buffer[9 + ret] = path[ret];
		for (ret = 0; ret < len_mode; ret++)
			buffer[9 + len_path + ret] = mode[ret];

		ret = sendwait(sock, buffer, 1 + 8 + len_path + len_mode);
	}
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);
	ret = recvwait(sock, handle, 4);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_fread(int sock, int *result, void *ptr, int size, int count, int fd) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 12];
	buffer[0] = BYTE_READ;
	*(int *)(buffer + 1) = size;
	*(int *)(buffer + 5) = count;
	*(int *)(buffer + 9) = fd;
	ret = sendwait(sock, buffer, 1 + 12);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);
	int sz;
	ret = recvwait(sock, &sz, 4);
	CHECK_ERROR(ret < 0);
	ret = recvwait(sock, ptr, sz);
	CHECK_ERROR(ret < 0);
	ret = sendbyte(sock, BYTE_OK);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_fclose(int sock, int *result, int fd) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 4];
	buffer[0] = BYTE_CLOSE;
	*(int *)(buffer + 1) = fd;
	ret = sendwait(sock, buffer, 1 + 4);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_fsetpos(int sock, int *result, int fd, int set) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 8];
	buffer[0] = BYTE_SETPOS;
	*(int *)(buffer + 1) = fd;
	*(int *)(buffer + 5) = set;
	ret = sendwait(sock, buffer, 1 + 8);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_fgetpos(int sock, int *result, int fd, int *pos) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 4];
	buffer[0] = BYTE_GETPOS;
	*(int *)(buffer + 1) = fd;
	ret = sendwait(sock, buffer, 1 + 4);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);
	ret = recvwait(sock, pos, 4);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_fstat(int sock, int *result, int fd, void *ptr) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 4];
	buffer[0] = BYTE_STATFILE;
	*(int *)(buffer + 1) = fd;
	ret = sendwait(sock, buffer, 1 + 4);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);
	int sz;
	ret = recvwait(sock, &sz, 4);
	CHECK_ERROR(ret < 0);
	if (ptr) {
		ret = recvwait(sock, ptr, sz);
		CHECK_ERROR(ret < 0);
	}

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

int cafiine_feof(int sock, int *result, int fd) {
	while (bss.lock) GX2WaitForVsync();
	bss.lock = 1;

	CHECK_ERROR(sock == -1);

	int ret;
	char buffer[1 + 4];
	buffer[0] = BYTE_EOF;
	*(int *)(buffer + 1) = fd;
	ret = sendwait(sock, buffer, 1 + 4);
	CHECK_ERROR(ret < 0);
	ret = recvbyte(sock);
	CHECK_ERROR(ret < 0);
	CHECK_ERROR(ret == BYTE_NORMAL);
	ret = recvwait(sock, result, 4);
	CHECK_ERROR(ret < 0);

	bss.lock = 0;
	return 0;
error:
	bss.lock = 0;
	return -1;
}

static int recvwait(int sock, void *buffer, int len) {
	int ret;
	while (len > 0) {
		ret = recv(sock, buffer, len, 0);
		CHECK_ERROR(ret < 0);
		len -= ret;
		buffer += ret;
	}
	return 0;
error:
	return ret;
}

static int recvbyte(int sock) {
	unsigned char buffer[1];
	int ret;

	ret = recvwait(sock, buffer, 1);
	if (ret < 0) return ret;
	return buffer[0];
}

static int sendwait(int sock, const void *buffer, int len) {
	int ret;
	while (len > 0) {
		ret = send(sock, buffer, len, 0);
		CHECK_ERROR(ret < 0);
		len -= ret;
		buffer += ret;
	}
	return 0;
error:
	return ret;
}

static int sendbyte(int sock, unsigned char byte) {
	unsigned char buffer[1];

	buffer[0] = byte;
	return sendwait(sock, buffer, 1);
}
