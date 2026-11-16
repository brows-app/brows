#ifndef BROWS_7Z_H
#define BROWS_7Z_H

#include  "brows_framework.h"
#include  <stddef.h>
#include  <stdint.h>

#ifdef  brows_7z_IMPORT
#define brows_7z         brows_DLLIMPORT
#endif

#ifdef  brows_7z_EXPORT
#define brows_7z         brows_DLLEXPORT
#endif

#ifndef brows_7z
#define brows_7z         brows_EXTERN_C
#endif

/*
 * The 7-Zip engine is only reachable through this library. The managed side
 * never talks to the 7-Zip COM interfaces directly.
 */

typedef struct brows_7z_Archive brows_7z_Archive;

#define brows_7z_ERROR_argument              2117001
#define brows_7z_ERROR_library              2117002
#define brows_7z_ERROR_archive_open         2117003
#define brows_7z_ERROR_archive_not_open     2117004
#define brows_7z_ERROR_index_out_of_range   2117005
#define brows_7z_ERROR_file_io              2117006
#define brows_7z_ERROR_compression          2117007
#define brows_7z_ERROR_memory                2117008
#define brows_7z_ERROR_target_exists        2117009
#define brows_7z_ERROR_update               2117010
#define brows_7z_ERROR_entry_not_found      2117011
#define brows_7z_ERROR_native_exception     2117012
#define brows_7z_ERROR_cancelled            2117013

/* Reports completed and total bytes for a long-running archive operation. */
typedef int32_t (__cdecl* brows_7z_ProgressCallback)(void* state, uint64_t completed, uint64_t total);

/*
 * Describes a single item inside an archive.
 *
 * The path is UTF-8 encoded and uses forward slashes as separators, matching
 * the way 7-Zip stores names inside an archive.
 */
typedef struct brows_7z_EntryInfo {
    const char* path;
    uint64_t    size;
    uint64_t    packed_size;
    uint32_t    attributes;
    int64_t     modified_time;
    uint32_t    crc;
    int32_t     is_directory;
} brows_7z_EntryInfo;

/*
 * Describes one item that should end up in an archive.
 *
 * The path is UTF-8 encoded and uses forward slashes as separators. The
 * source path is the location of the file on disk and is NULL for folders.
 */
typedef struct brows_7z_UpdateItem {
    const char* path;
    const char* source_path;
    int32_t     is_directory;
    uint64_t    size;
    int64_t     modified_time;
    uint32_t    attributes;
} brows_7z_UpdateItem;

/*
 * Describes the content of an archive that is about to be written.
 *
 * The kept indices refer to items of the archive that is being updated. They
 * are copied over unchanged. Every item in the item list is added as new
 * content, which also replaces any kept item that uses the same path.
 */
typedef struct brows_7z_UpdateRequest {
    const brows_7z_UpdateItem* items;
    uint32_t                   item_count;
    const uint32_t*            kept_indices;
    uint32_t                   kept_count;
    int32_t                    compression_level;
} brows_7z_UpdateRequest;

brows_7z brows_ERROR           brows_7z_init(void);
brows_7z brows_ERROR           brows_7z_exit(void);
brows_7z const char*           brows_7z_last_error_message(void);
brows_7z brows_ERROR           brows_7z_directory_is_case_sensitive(const char* directory, int32_t* is_case_sensitive);

brows_7z brows_ERROR           brows_7z_Archive_open                     (const char* path, brows_7z_Archive** out);
brows_7z void                  brows_7z_Archive_close                    (brows_7z_Archive* archive);
brows_7z uint32_t              brows_7z_Archive_get_entry_count         (brows_7z_Archive* archive);
brows_7z const brows_7z_EntryInfo* brows_7z_Archive_get_entry          (brows_7z_Archive* archive, uint32_t index);
brows_7z brows_ERROR           brows_7z_Archive_extract_entry          (brows_7z_Archive* archive, uint32_t index, const char* destination, int32_t overwrite, brows_7z_ProgressCallback progress, void* state);
brows_7z brows_ERROR           brows_7z_Archive_extract_all             (brows_7z_Archive* archive, const char* directory, int32_t overwrite, brows_7z_ProgressCallback progress, void* state);

brows_7z brows_ERROR           brows_7z_Archive_create                  (const char* path, const brows_7z_UpdateRequest* request, brows_7z_ProgressCallback progress, void* state);
brows_7z brows_ERROR           brows_7z_Archive_update                  (const char* path, const brows_7z_UpdateRequest* request, brows_7z_ProgressCallback progress, void* state);

#endif
