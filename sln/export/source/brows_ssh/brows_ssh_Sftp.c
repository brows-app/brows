#include "brows_ssh_internal.h"

#include <inttypes.h>
#include <stdlib.h>

#define brows_ssh_SFTP_NAME_CAPACITY 65536
#define brows_ssh_Sftp_CLEANUP_TIMEOUT_MS 1000
/*
 * Terminal transport callbacks should make libssh2 consume each handle and
 * SFTP shutdown state with a non-EAGAIN result. Keep a finite cap defensively.
 */
#define brows_ssh_Sftp_ABORT_RETRY_COUNT 16

struct brows_ssh_Sftp {
    brows_ssh_Conn* conn;
    LIBSSH2_SFTP*   sftp;
    uint32_t        last_error;
};

struct brows_ssh_SftpDir {
    brows_ssh_Sftp*      sftp;
    char*                path;
    LIBSSH2_SFTP_HANDLE* handle;
};

struct brows_ssh_SftpFile {
    brows_ssh_Sftp*      sftp;
    char*                path;
    uint32_t             flags;
    uint32_t             mode;
    LIBSSH2_SFTP_HANDLE* handle;
};

struct brows_ssh_SftpItem {
    char*    name;
    uint32_t flags;
    uint64_t size;
    uint32_t uid;
    uint32_t gid;
    uint32_t permissions;
    uint32_t atime;
    uint32_t mtime;
};

static char* brows_ssh_Sftp_copy_string(const char* value) {
    if (!value) {
        return NULL;
    }
    size_t length = strlen(value) + 1;
    char* copy = calloc(length, sizeof(char));
    if (!copy) {
        return NULL;
    }
    memcpy(copy, value, length);
    return copy;
}

static void brows_ssh_Sftp_set_item(brows_ssh_SftpItem* item,
                                    const char* name,
                                    const LIBSSH2_SFTP_ATTRIBUTES* attributes) {
    assert(item);
    assert(attributes);

    free(item->name);
    item->name = brows_ssh_Sftp_copy_string(name);
    item->flags = (uint32_t)attributes->flags;
    item->size = attributes->filesize;
    item->uid = (uint32_t)attributes->uid;
    item->gid = (uint32_t)attributes->gid;
    item->permissions = (uint32_t)attributes->permissions;
    item->atime = (uint32_t)attributes->atime;
    item->mtime = (uint32_t)attributes->mtime;
}

static brows_ERROR brows_ssh_Sftp_wait(brows_ssh_Sftp* p, brows_Canceler* cancel) {
    assert(p);
    brows_ERROR error = brows_ssh_Conn_wait_socket(p->conn, cancel);
    if (error) {
        /*
         * libssh2 retains the pending request across EAGAIN. Once its wait is
         * abandoned, another operation must never resume it with a new path.
         */
        brows_ssh_Conn_abort_transport(p->conn);
    }
    return error;
}

static brows_ERROR brows_ssh_Sftp_wait_handle(brows_ssh_Sftp* p,
                                              LIBSSH2_SFTP_HANDLE** handle,
                                              brows_Canceler* cancel,
                                              int close_handle) {
    assert(p);
    assert(handle);
    if (!*handle) {
        return brows_ERROR_NONE;
    }
    ULONGLONG deadline = GetTickCount64() + brows_ssh_Sftp_CLEANUP_TIMEOUT_MS;
    int retried_without_cancellation = 0;
    int abort_retry_count = 0;
    for (;;) {
        int result = libssh2_sftp_close_handle(*handle);
        if (result != LIBSSH2_ERROR_EAGAIN) {
            *handle = NULL;
        }
        if (result >= 0) {
            p->last_error = 0;
            return brows_ERROR_NONE;
        }
        if (result != LIBSSH2_ERROR_EAGAIN) {
            p->last_error = libssh2_sftp_last_error(p->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_CLOSE;
        }
        if (brows_ssh_Conn_is_aborted(p->conn)) {
            if (++abort_retry_count >= brows_ssh_Sftp_ABORT_RETRY_COUNT) {
                brows_LOG_ERROR("%s", "libssh2_sftp_close_handle remained pending after transport abort");
                return brows_ERROR_SSH_CONN_WAIT_TIMEOUT;
            }
            continue;
        }
        brows_ERROR wait_error = brows_ssh_Conn_wait_socket_until(p->conn, cancel, deadline);
        if (wait_error) {
            if (wait_error == brows_ERROR_CANCELED && cancel && close_handle && !retried_without_cancellation) {
                brows_LOG_INFO("%s", "SFTP close canceled; retrying close without cancellation");
                cancel = NULL;
                deadline = GetTickCount64() + brows_ssh_Sftp_CLEANUP_TIMEOUT_MS;
                retried_without_cancellation = 1;
                continue;
            }
            if (close_handle) {
                brows_LOG_ERROR("SFTP handle close timed out or canceled > %" PRId32, wait_error);
                brows_ssh_Conn_abort_transport(p->conn);
                abort_retry_count = 0;
                continue;
            }
            return wait_error;
        }
    }
}

brows_ssh_Sftp* brows_ssh_Sftp_create(brows_ssh_Conn* conn) {
    if (!conn) {
        return NULL;
    }
    brows_ssh_Sftp* p = calloc(1, sizeof(brows_ssh_Sftp));
    if (p) {
        p->conn = conn;
        p->sftp = NULL;
        p->last_error = 0;
    }
    return p;
}

brows_ERROR brows_ssh_Sftp_ready(brows_ssh_Sftp* p, brows_Canceler* cancel) {
    assert(p);
    if (p->sftp) {
        return brows_ERROR_NONE;
    }
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    if (!session) {
        return brows_ERROR_SSH_CONN_SESSION_IS_NULL;
    }
    for (;;) {
        p->sftp = libssh2_sftp_init(session);
        if (p->sftp) {
            p->last_error = 0;
            return brows_ERROR_NONE;
        }
        int error = libssh2_session_last_errno(session);
        if (error != LIBSSH2_ERROR_EAGAIN) {
            brows_LOG_ERROR("libssh2_sftp_init > %" PRId32, error);
            return brows_ssh_ERROR_LIBSSH2_SFTP_INIT;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

void brows_ssh_Sftp_destroy(brows_ssh_Sftp* p) {
    if (!p) {
        return;
    }
    if (p->sftp) {
        ULONGLONG deadline = GetTickCount64() + brows_ssh_Sftp_CLEANUP_TIMEOUT_MS;
        int abort_retry_count = 0;
        for (;;) {
            int result = libssh2_sftp_shutdown(p->sftp);
            if (result != LIBSSH2_ERROR_EAGAIN) {
                if (result < 0) {
                    brows_LOG_ERROR("libssh2_sftp_shutdown > %" PRId32, result);
                }
                break;
            }
            if (brows_ssh_Conn_is_aborted(p->conn)) {
                if (++abort_retry_count >= brows_ssh_Sftp_ABORT_RETRY_COUNT) {
                    brows_LOG_ERROR("%s", "libssh2_sftp_shutdown remained pending after transport abort");
                    break;
                }
                continue;
            }
            brows_ERROR wait_error = brows_ssh_Conn_wait_socket_until(p->conn, NULL, deadline);
            if (wait_error) {
                brows_LOG_ERROR("SFTP shutdown timed out or failed > %" PRId32, wait_error);
                brows_ssh_Conn_abort_transport(p->conn);
                abort_retry_count = 0;
                continue;
            }
            if (brows_ssh_Conn_is_aborted(p->conn)) {
                break;
            }
        }
    }
    free(p);
}

uint32_t brows_ssh_Sftp_get_last_error(brows_ssh_Sftp* p) {
    assert(p);
    return p->last_error;
}

brows_ssh_SftpDir* brows_ssh_SftpDir_create(brows_ssh_Sftp* sftp, const char* path) {
    if (!sftp || !path) {
        return NULL;
    }
    brows_ssh_SftpDir* p = calloc(1, sizeof(brows_ssh_SftpDir));
    if (p) {
        p->sftp = sftp;
        p->path = brows_ssh_Sftp_copy_string(path);
        if (!p->path) {
            free(p);
            return NULL;
        }
        p->handle = NULL;
    }
    return p;
}

brows_ERROR brows_ssh_SftpDir_ready(brows_ssh_SftpDir* p, brows_Canceler* cancel) {
    assert(p);
    if (p->handle) {
        return brows_ERROR_NONE;
    }
    brows_ERROR sftp_ready_error = brows_ssh_Sftp_ready(p->sftp, cancel);
    if (sftp_ready_error) {
        return sftp_ready_error;
    }
    for (;;) {
        p->handle = libssh2_sftp_opendir(p->sftp->sftp, p->path);
        if (p->handle) {
            p->sftp->last_error = 0;
            return brows_ERROR_NONE;
        }
        int error = libssh2_session_last_errno(brows_ssh_Conn_get_session(p->sftp->conn));
        if (error != LIBSSH2_ERROR_EAGAIN) {
            p->sftp->last_error = libssh2_sftp_last_error(p->sftp->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_OPENDIR;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p->sftp, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

brows_ERROR brows_ssh_SftpDir_next(brows_ssh_SftpDir* p,
                                   brows_ssh_SftpItem* item,
                                   int32_t* end,
                                   brows_Canceler* cancel) {
    assert(p);
    assert(item);
    assert(end);
    if (!p->handle) {
        return brows_ssh_ERROR_LIBSSH2_SFTP_OPENDIR;
    }
    char* name = calloc(brows_ssh_SFTP_NAME_CAPACITY, sizeof(char));
    if (!name) {
        return brows_ssh_ERROR_CALLOC;
    }
    LIBSSH2_SFTP_ATTRIBUTES attributes = { 0 };
    for (;;) {
        int result = libssh2_sftp_readdir_ex(p->handle,
                                             name,
                                             brows_ssh_SFTP_NAME_CAPACITY - 1,
                                             NULL,
                                             0,
                                             &attributes);
        if (result == 0) {
            *end = 1;
            free(name);
            return brows_ERROR_NONE;
        }
        if (result > 0) {
            if ((size_t)result >= brows_ssh_SFTP_NAME_CAPACITY - 1) {
                free(name);
                return brows_ssh_ERROR_SFTP_NAME_TOO_LONG;
            }
            name[result] = '\0';
            brows_ssh_Sftp_set_item(item, name, &attributes);
            free(name);
            if (!item->name) {
                return brows_ssh_ERROR_CALLOC;
            }
            *end = 0;
            p->sftp->last_error = 0;
            return brows_ERROR_NONE;
        }
        int error = libssh2_session_last_errno(brows_ssh_Conn_get_session(p->sftp->conn));
        if (error != LIBSSH2_ERROR_EAGAIN) {
            p->sftp->last_error = libssh2_sftp_last_error(p->sftp->sftp);
            free(name);
            return brows_ssh_ERROR_LIBSSH2_SFTP_READDIR;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p->sftp, cancel);
        if (wait_error) {
            free(name);
            return wait_error;
        }
        memset(&attributes, 0, sizeof(attributes));
    }
}

brows_ERROR brows_ssh_SftpDir_close(brows_ssh_SftpDir* p, brows_Canceler* cancel) {
    assert(p);
    return brows_ssh_Sftp_wait_handle(p->sftp, &p->handle, cancel, 1);
}

void brows_ssh_SftpDir_destroy(brows_ssh_SftpDir* p) {
    if (p) {
        brows_ssh_Sftp_wait_handle(p->sftp, &p->handle, NULL, 1);
        free(p->path);
        free(p);
    }
}

brows_ssh_SftpItem* brows_ssh_SftpItem_create(void) {
    return calloc(1, sizeof(brows_ssh_SftpItem));
}

void brows_ssh_SftpItem_destroy(brows_ssh_SftpItem* p) {
    if (p) {
        free(p->name);
        free(p);
    }
}

brows_ERROR brows_ssh_Sftp_stat(brows_ssh_Sftp* p,
                                const char* path,
                                int32_t lstat,
                                brows_ssh_SftpItem* item,
                                brows_Canceler* cancel) {
    assert(p);
    assert(item);
    if (!path) {
        return brows_ssh_ERROR_STRCPY_S;
    }
    brows_ERROR ready_error = brows_ssh_Sftp_ready(p, cancel);
    if (ready_error) {
        return ready_error;
    }
    LIBSSH2_SFTP_ATTRIBUTES attributes = { 0 };
    int type = lstat ? LIBSSH2_SFTP_LSTAT : LIBSSH2_SFTP_STAT;
    for (;;) {
        int result = libssh2_sftp_stat_ex(p->sftp,
                                          path,
                                          (unsigned int)strlen(path),
                                          type,
                                          &attributes);
        if (result >= 0) {
            brows_ssh_Sftp_set_item(item, NULL, &attributes);
            if (item->name) {
                free(item->name);
                item->name = NULL;
            }
            p->last_error = 0;
            return brows_ERROR_NONE;
        }
        if (result != LIBSSH2_ERROR_EAGAIN) {
            p->last_error = libssh2_sftp_last_error(p->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_STAT;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p, cancel);
        if (wait_error) {
            p->last_error = (uint32_t)wait_error;
            return wait_error;
        }
        memset(&attributes, 0, sizeof(attributes));
    }
}

const char* brows_ssh_SftpItem_get_name(brows_ssh_SftpItem* p) {
    assert(p);
    return p->name;
}

uint32_t brows_ssh_SftpItem_get_flags(brows_ssh_SftpItem* p) {
    assert(p);
    return p->flags;
}

uint64_t brows_ssh_SftpItem_get_size(brows_ssh_SftpItem* p) {
    assert(p);
    return p->size;
}

uint32_t brows_ssh_SftpItem_get_uid(brows_ssh_SftpItem* p) {
    assert(p);
    return p->uid;
}

uint32_t brows_ssh_SftpItem_get_gid(brows_ssh_SftpItem* p) {
    assert(p);
    return p->gid;
}

uint32_t brows_ssh_SftpItem_get_permissions(brows_ssh_SftpItem* p) {
    assert(p);
    return p->permissions;
}

uint32_t brows_ssh_SftpItem_get_atime(brows_ssh_SftpItem* p) {
    assert(p);
    return p->atime;
}

uint32_t brows_ssh_SftpItem_get_mtime(brows_ssh_SftpItem* p) {
    assert(p);
    return p->mtime;
}

brows_ssh_SftpFile* brows_ssh_SftpFile_create(brows_ssh_Sftp* sftp,
                                              const char* path,
                                              uint32_t flags,
                                              uint32_t mode) {
    if (!sftp || !path) {
        return NULL;
    }
    brows_ssh_SftpFile* p = calloc(1, sizeof(brows_ssh_SftpFile));
    if (p) {
        p->sftp = sftp;
        p->path = brows_ssh_Sftp_copy_string(path);
        p->flags = flags;
        p->mode = mode;
        p->handle = NULL;
        if (!p->path) {
            free(p);
            return NULL;
        }
    }
    return p;
}

brows_ERROR brows_ssh_SftpFile_ready(brows_ssh_SftpFile* p, brows_Canceler* cancel) {
    assert(p);
    if (p->handle) {
        return brows_ERROR_NONE;
    }
    brows_ERROR sftp_ready_error = brows_ssh_Sftp_ready(p->sftp, cancel);
    if (sftp_ready_error) {
        return sftp_ready_error;
    }
    for (;;) {
        p->handle = libssh2_sftp_open_ex(p->sftp->sftp,
                                         p->path,
                                         (unsigned int)strlen(p->path),
                                         p->flags,
                                         p->mode,
                                         LIBSSH2_SFTP_OPENFILE);
        if (p->handle) {
            p->sftp->last_error = 0;
            return brows_ERROR_NONE;
        }
        int error = libssh2_session_last_errno(brows_ssh_Conn_get_session(p->sftp->conn));
        if (error != LIBSSH2_ERROR_EAGAIN) {
            p->sftp->last_error = libssh2_sftp_last_error(p->sftp->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_OPEN;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p->sftp, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

brows_ERROR brows_ssh_SftpFile_read(brows_ssh_SftpFile* p,
                                    char* buffer,
                                    size_t capacity,
                                    size_t* result,
                                    brows_Canceler* cancel) {
    assert(p);
    assert(result);
    if (!p->handle) {
        return brows_ssh_ERROR_LIBSSH2_SFTP_OPEN;
    }
    for (;;) {
        ssize_t read = libssh2_sftp_read(p->handle, buffer, capacity);
        if (read >= 0) {
            *result = (size_t)read;
            return brows_ERROR_NONE;
        }
        if (read != LIBSSH2_ERROR_EAGAIN) {
            p->sftp->last_error = libssh2_sftp_last_error(p->sftp->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_READ;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p->sftp, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

brows_ERROR brows_ssh_SftpFile_write(brows_ssh_SftpFile* p,
                                     const char* buffer,
                                     size_t length,
                                     size_t* result,
                                     brows_Canceler* cancel) {
    assert(p);
    assert(result);
    if (!p->handle) {
        return brows_ssh_ERROR_LIBSSH2_SFTP_OPEN;
    }
    for (;;) {
        ssize_t written = libssh2_sftp_write(p->handle, buffer, length);
        if (written >= 0) {
            *result = (size_t)written;
            return brows_ERROR_NONE;
        }
        if (written != LIBSSH2_ERROR_EAGAIN) {
            p->sftp->last_error = libssh2_sftp_last_error(p->sftp->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_WRITE;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p->sftp, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

brows_ERROR brows_ssh_SftpFile_close(brows_ssh_SftpFile* p, brows_Canceler* cancel) {
    assert(p);
    return brows_ssh_Sftp_wait_handle(p->sftp, &p->handle, cancel, 1);
}

void brows_ssh_SftpFile_destroy(brows_ssh_SftpFile* p) {
    if (p) {
        brows_ssh_Sftp_wait_handle(p->sftp, &p->handle, NULL, 1);
        free(p->path);
        free(p);
    }
}

static brows_ERROR brows_ssh_Sftp_path_operation(brows_ssh_Sftp* p,
                                                 const char* path,
                                                 uint32_t mode,
                                                 brows_Canceler* cancel,
                                                 int operation) {
    assert(p);
    if (!path) {
        return brows_ssh_ERROR_STRCPY_S;
    }
    brows_ERROR ready_error = brows_ssh_Sftp_ready(p, cancel);
    if (ready_error) {
        return ready_error;
    }
    for (;;) {
        int result =
            operation == 0 ? libssh2_sftp_mkdir_ex(p->sftp, path, (unsigned int)strlen(path), mode) :
            operation == 1 ? libssh2_sftp_unlink_ex(p->sftp, path, (unsigned int)strlen(path)) :
            libssh2_sftp_rmdir_ex(p->sftp, path, (unsigned int)strlen(path));
        if (result >= 0) {
            p->last_error = 0;
            return brows_ERROR_NONE;
        }
        if (result != LIBSSH2_ERROR_EAGAIN) {
            p->last_error = libssh2_sftp_last_error(p->sftp);
            return
                operation == 0 ? brows_ssh_ERROR_LIBSSH2_SFTP_MKDIR :
                operation == 1 ? brows_ssh_ERROR_LIBSSH2_SFTP_UNLINK :
                brows_ssh_ERROR_LIBSSH2_SFTP_RMDIR;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}

brows_ERROR brows_ssh_Sftp_mkdir(brows_ssh_Sftp* p, const char* path, uint32_t mode, brows_Canceler* cancel) {
    return brows_ssh_Sftp_path_operation(p, path, mode, cancel, 0);
}

brows_ERROR brows_ssh_Sftp_unlink(brows_ssh_Sftp* p, const char* path, brows_Canceler* cancel) {
    return brows_ssh_Sftp_path_operation(p, path, 0, cancel, 1);
}

brows_ERROR brows_ssh_Sftp_rmdir(brows_ssh_Sftp* p, const char* path, brows_Canceler* cancel) {
    return brows_ssh_Sftp_path_operation(p, path, 0, cancel, 2);
}

brows_ERROR brows_ssh_Sftp_rename(brows_ssh_Sftp* p,
                                  const char* source,
                                  const char* destination,
                                  brows_Canceler* cancel) {
    assert(p);
    if (!source || !destination) {
        return brows_ssh_ERROR_STRCPY_S;
    }
    brows_ERROR ready_error = brows_ssh_Sftp_ready(p, cancel);
    if (ready_error) {
        return ready_error;
    }
    for (;;) {
        int result = libssh2_sftp_rename_ex(p->sftp,
                                            source,
                                            (unsigned int)strlen(source),
                                            destination,
                                            (unsigned int)strlen(destination),
                                            0);
        if (result >= 0) {
            p->last_error = 0;
            return brows_ERROR_NONE;
        }
        if (result != LIBSSH2_ERROR_EAGAIN) {
            p->last_error = libssh2_sftp_last_error(p->sftp);
            return brows_ssh_ERROR_LIBSSH2_SFTP_RENAME;
        }
        brows_ERROR wait_error = brows_ssh_Sftp_wait(p, cancel);
        if (wait_error) {
            return wait_error;
        }
    }
}
