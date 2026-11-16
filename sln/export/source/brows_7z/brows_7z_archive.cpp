#include "brows_7z_internal.h"

#include "brows_7z_callback.h"
#include "brows_7z_stream.h"

#include <cstring>
#include <memory>
#include <new>

namespace {

/* Clears a property that was read from an archive. */
void clear_property(PROPVARIANT* value) {
    if (value->vt == VT_BSTR && value->bstrVal != NULL) {
        SysFreeString(value->bstrVal);
    }
    value->vt = VT_EMPTY;
    value->bstrVal = NULL;
}

struct PropertyOwner final {
    PROPVARIANT* value;

    ~PropertyOwner() noexcept {
        clear_property(value);
    }
};

struct InArchiveDeleter final {
    void operator()(IInArchive* archive) const noexcept {
        if (archive != NULL) {
            try {
                archive->Close();
            }
            catch (...) {
                brows_7z_set_error("The 7-Zip handler threw while closing an archive.");
            }
            try {
                archive->Release();
            }
            catch (...) {
                brows_7z_set_error("The 7-Zip handler threw while releasing an archive.");
            }
        }
    }
};

struct OutArchiveDeleter final {
    void operator()(IOutArchive* archive) const noexcept {
        if (archive != NULL) {
            try {
                archive->Release();
            }
            catch (...) {
                brows_7z_set_error("The 7-Zip handler threw while releasing an archive.");
            }
        }
    }
};

struct SetPropertiesDeleter final {
    void operator()(ISetProperties* properties) const noexcept {
        if (properties != NULL) {
            try {
                properties->Release();
            }
            catch (...) {
                brows_7z_set_error("The 7-Zip handler threw while releasing compression properties.");
            }
        }
    }
};

struct InStreamDeleter final {
    void operator()(CInFileStream* stream) const noexcept {
        delete stream;
    }
};

struct OutStreamDeleter final {
    void operator()(COutFileStream* stream) const noexcept {
        delete stream;
    }
};

struct FileHandleDeleter final {
    void operator()(HANDLE handle) const noexcept {
        if (handle != INVALID_HANDLE_VALUE && handle != NULL) {
            CloseHandle(handle);
        }
    }
};

struct ArchiveDeleter final {
    void operator()(brows_7z_Archive* archive) const noexcept {
        if (archive == NULL) {
            return;
        }
        InArchiveDeleter{}(archive->in_archive);
        InStreamDeleter{}(archive->in_stream);
        delete archive;
    }
};

/* Translates the HRESULTs used by callbacks into the native API's stable errors. */
brows_ERROR callback_error(HRESULT result, brows_ERROR fallback, const char* context) {
    if (result == E_ABORT) {
        brows_7z_set_error("The archive operation was cancelled.");
        return brows_7z_ERROR_cancelled;
    }
    if (result == E_OUTOFMEMORY) {
        brows_7z_set_error("Not enough memory for the archive operation.");
        return brows_7z_ERROR_memory;
    }
    if (result != S_OK) {
        brows_7z_set_error("%s (0x%08lx)", context, (unsigned long)result);
        return fallback;
    }
    return brows_ERROR_NONE;
}

bool is_unsupported_case_sensitive_query(DWORD error) {
    return error == ERROR_CALL_NOT_IMPLEMENTED ||
           error == ERROR_INVALID_FUNCTION ||
           error == ERROR_INVALID_LEVEL ||
           error == ERROR_INVALID_PARAMETER ||
           error == ERROR_NOT_SUPPORTED;
}

/* Reads a property that holds text. */
bool read_text(IInArchive* archive, UInt32 index, PROPID prop_id, std::string& out) {
    PROPVARIANT value;
    memset(&value, 0, sizeof(value));
    value.vt = VT_EMPTY;
    PropertyOwner owner{ &value };
    if (FAILED(archive->GetProperty(index, prop_id, &value))) {
        return false;
    }
    bool read = false;
    if (value.vt == VT_BSTR && value.bstrVal != NULL) {
        read = brows_7z_to_utf8(value.bstrVal, out);
    }
    return read;
}

/* Reads a property that holds a number. */
bool read_number(IInArchive* archive, UInt32 index, PROPID prop_id, UInt64& out) {
    PROPVARIANT value;
    memset(&value, 0, sizeof(value));
    value.vt = VT_EMPTY;
    PropertyOwner owner{ &value };
    if (FAILED(archive->GetProperty(index, prop_id, &value))) {
        return false;
    }
    bool read = false;
    if (value.vt == VT_UI8) {
        out = (UInt64)value.uhVal.QuadPart;
        read = true;
    }
    else if (value.vt == VT_UI4) {
        out = (UInt64)value.ulVal;
        read = true;
    }
    return read;
}

/* Reads a property that holds a number that fits into 32 bits. */
bool read_small_number(IInArchive* archive, UInt32 index, PROPID prop_id, UInt32& out) {
    UInt64 number = 0;
    if (!read_number(archive, index, prop_id, number)) {
        return false;
    }
    out = (UInt32)number;
    return true;
}

/* Reads a property that holds a flag. */
bool read_flag(IInArchive* archive, UInt32 index, PROPID prop_id, bool& out) {
    PROPVARIANT value;
    memset(&value, 0, sizeof(value));
    value.vt = VT_EMPTY;
    PropertyOwner owner{ &value };
    if (FAILED(archive->GetProperty(index, prop_id, &value))) {
        return false;
    }
    bool read = false;
    if (value.vt == VT_BOOL) {
        out = value.boolVal != VARIANT_FALSE;
        read = true;
    }
    return read;
}

/* Reads a property that holds a time. */
bool read_time(IInArchive* archive, UInt32 index, PROPID prop_id, Int64& out) {
    PROPVARIANT value;
    memset(&value, 0, sizeof(value));
    value.vt = VT_EMPTY;
    PropertyOwner owner{ &value };
    if (FAILED(archive->GetProperty(index, prop_id, &value))) {
        return false;
    }
    bool read = false;
    if (value.vt == VT_FILETIME) {
        out = (Int64)(((UInt64)value.filetime.dwHighDateTime << 32) | value.filetime.dwLowDateTime);
        read = true;
    }
    return read;
}

/* Reads the items of an archive into the given lists. */
brows_ERROR read_entries(IInArchive* archive,
                         std::vector<std::string>& paths,
                         std::vector<brows_7z_EntryInfo>& entries) {
    UInt32 count = 0;
    HRESULT result = archive->GetNumberOfItems(&count);
    if (FAILED(result)) {
        brows_7z_set_error("Could not read the items of the archive. (0x%08lx)", (unsigned long)result);
        return brows_7z_ERROR_archive_open;
    }
    paths.reserve(count);
    entries.reserve(count);
    for (UInt32 index = 0; index < count; index++) {
        brows_7z_EntryInfo entry;
        memset(&entry, 0, sizeof(entry));
        std::string path;
        if (!read_text(archive, index, kpidPath, path)) {
            brows_7z_set_error("Could not read the path of an item of the archive.");
            return brows_7z_ERROR_archive_open;
        }
        /* The managed side expects the names of the items to use forward slashes. */
        brows_7z_normalize_archive_path(path);
        bool is_directory = false;
        read_flag(archive, index, kpidIsDir, is_directory);
        UInt64 size = 0;
        read_number(archive, index, kpidSize, size);
        UInt64 packed_size = 0;
        read_number(archive, index, kpidPackSize, packed_size);
        UInt32 attributes = 0;
        read_small_number(archive, index, kpidAttrib, attributes);
        UInt32 crc = 0;
        read_small_number(archive, index, kpidCRC, crc);
        Int64 modified_time = 0;
        read_time(archive, index, kpidMTime, modified_time);

        paths.push_back(path);
        entry.size = size;
        entry.packed_size = packed_size;
        entry.attributes = attributes;
        entry.modified_time = modified_time;
        entry.crc = crc;
        entry.is_directory = is_directory ? 1 : 0;
        entries.push_back(entry);
    }
    /*
     * The paths are kept in a list of their own, so that the entries can point
     * at them without moving the text of every path.
     */
    for (size_t index = 0; index < entries.size(); index++) {
        entries[index].path = paths[index].c_str();
    }
    return brows_ERROR_NONE;
}

/* Sets the time of a file that was extracted. */
void apply_modified_time(const CExtractTarget& target) {
    if (target.modified_time == 0 || target.is_directory) {
        return;
    }
    FILETIME time;
    time.dwLowDateTime = (DWORD)((UInt64)target.modified_time & 0xFFFFFFFF);
    time.dwHighDateTime = (DWORD)(((UInt64)target.modified_time >> 32) & 0xFFFFFFFF);
    HANDLE handle = CreateFileW(target.path.c_str(),
                                FILE_WRITE_ATTRIBUTES,
                                FILE_SHARE_READ | FILE_SHARE_WRITE,
                                NULL,
                                OPEN_EXISTING,
                                FILE_ATTRIBUTE_NORMAL,
                                NULL);
    if (handle == INVALID_HANDLE_VALUE) {
        return;
    }
    std::unique_ptr<void, FileHandleDeleter> owner(handle);
    SetFileTime(handle, NULL, NULL, &time);
}

/*
 * Tells the 7z handler how much the content should be compressed.
 *
 * The level of -1 means that the handler uses its own default.
 */
void set_compression_level(IOutArchive* out_archive, int32_t level) {
    ISetProperties* raw_properties = NULL;
    const HRESULT result = out_archive->QueryInterface(IID_ISetProperties, (void**)&raw_properties);
    std::unique_ptr<ISetProperties, SetPropertiesDeleter> properties(raw_properties);
    if (FAILED(result) || !properties) {
        return;
    }
    /*
     * The query interface of 7-Zip adds a reference to the object it returns,
     * so the reference is given back when the properties are not needed
     * anymore.
     */
    const wchar_t* names[1];
    names[0] = L"x";
    PROPVARIANT value;
    memset(&value, 0, sizeof(value));
    value.vt = VT_UI4;
    value.ulVal = (ULONG)((level < 0) ? 5 : level);
    properties->SetProperties(names, &value, 1);
}

/*
 * Reads the items of an update request. The items of the request are the
 * items that are added to the archive and the items of the archive that are
 * kept.
 */
brows_ERROR read_update_items(const brows_7z_UpdateRequest* request,
                               std::vector<CUpdateItem>& items) {
    items.reserve(request->kept_count + request->item_count);
    for (uint32_t index = 0; index < request->kept_count; index++) {
        CUpdateItem item;
        item.is_kept = true;
        item.archive_index = request->kept_indices[index];
        item.modified_time = 0;
        item.size = 0;
        item.attributes = 0;
        item.is_directory = false;
        items.push_back(item);
    }
    for (uint32_t index = 0; index < request->item_count; index++) {
        const brows_7z_UpdateItem& source = request->items[index];
        if (source.path == NULL || source.path[0] == 0) {
            brows_7z_set_error("An item of the archive has no path.");
            return brows_7z_ERROR_argument;
        }
        CUpdateItem item;
        item.is_kept = false;
        item.archive_index = 0;
        item.modified_time = source.modified_time;
        item.size = source.size;
        item.attributes = source.attributes;
        item.is_directory = source.is_directory != 0;
        /*
         * The names of the items of an archive always use forward slashes, so
         * the name of the item is made uniform before it is stored.
         */
        std::string path = source.path;
        brows_7z_normalize_archive_path(path);
        if (!brows_7z_to_wide(path.c_str(), item.path)) {
            brows_7z_set_error("The path of an item of the archive is not valid UTF-8.");
            return brows_7z_ERROR_argument;
        }
        if (!item.is_directory) {
            if (source.source_path == NULL || source.source_path[0] == 0) {
                brows_7z_set_error("An item of the archive has no source file.");
                return brows_7z_ERROR_argument;
            }
            if (!brows_7z_to_wide(source.source_path, item.source_path)) {
                brows_7z_set_error("The source of an item of the archive is not valid UTF-8.");
                return brows_7z_ERROR_argument;
            }
        }
        items.push_back(item);
    }
    return brows_ERROR_NONE;
}

/* Writes an archive. */
brows_ERROR write_archive(const char* path,
                          const brows_7z_UpdateRequest* request,
                          bool update,
                          brows_7z_ProgressCallback progress,
                          void* progress_state) {
    if (path == NULL || path[0] == 0 || request == NULL) {
        brows_7z_set_error("The path of the archive is missing.");
        return brows_7z_ERROR_argument;
    }
    if ((request->item_count > 0 && request->items == NULL) ||
        (request->kept_count > 0 && request->kept_indices == NULL)) {
        brows_7z_set_error("The archive update request is incomplete.");
        return brows_7z_ERROR_argument;
    }
    std::wstring wide_path;
    if (!brows_7z_to_wide(path, wide_path)) {
        brows_7z_set_error("The path of the archive is not valid UTF-8.");
        return brows_7z_ERROR_argument;
    }

    std::vector<CUpdateItem> items;
    brows_ERROR error = read_update_items(request, items);
    if (error != brows_ERROR_NONE) {
        return error;
    }

    CUpdateCallback callback(items, progress, progress_state);
    HRESULT sources_result = callback.OpenSources();
    if (sources_result != S_OK) {
        return callback_error(sources_result, brows_7z_ERROR_file_io, "Could not open the source files.");
    }

    if (!brows_7z_ensure_library()) {
        return brows_7z_ERROR_library;
    }

    std::unique_ptr<CInFileStream, InStreamDeleter> in_stream;
    brows_7z_TemporaryFile staged_file;
    std::unique_ptr<COutFileStream, OutStreamDeleter> out_stream;
    std::unique_ptr<IInArchive, InArchiveDeleter> in_archive;
    std::unique_ptr<IOutArchive, OutArchiveDeleter> out_archive;

    if (update) {
        in_stream.reset(new CInFileStream());
        if (!in_stream->open(wide_path)) {
            brows_7z_set_win32_error("Could not open the archive", GetLastError());
            return brows_7z_ERROR_archive_open;
        }
        IInArchive* raw_in_archive = NULL;
        HRESULT result = brows_7z_create_object(&CLSID_Format7z, &IID_IInArchive, (void**)&raw_in_archive);
        if (FAILED(result) || raw_in_archive == NULL) {
            brows_7z_set_error("Could not create the 7z handler. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_library;
        }
        in_archive.reset(raw_in_archive);
        result = in_archive->Open(in_stream.get(), NULL, NULL);
        if (result != S_OK) {
            brows_7z_set_error("Could not read the archive. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_archive_open;
        }
        /*
         * The archive has to be updated by the handler that read it, because
         * that handler holds the items that are copied over.
         *
         * The query interface of 7-Zip adds a reference to the object it
         * returns, so the reference is given back when the handler is done.
         */
        IOutArchive* raw_out_archive = NULL;
        result = in_archive->QueryInterface(IID_IOutArchive, (void**)&raw_out_archive);
        if (FAILED(result) || raw_out_archive == NULL) {
            brows_7z_set_error("The 7z handler cannot update an archive. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_update;
        }
        out_archive.reset(raw_out_archive);
    }
    else {
        IOutArchive* raw_out_archive = NULL;
        HRESULT result = brows_7z_create_object(&CLSID_Format7z, &IID_IOutArchive, (void**)&raw_out_archive);
        if (FAILED(result) || raw_out_archive == NULL) {
            brows_7z_set_error("Could not create the 7z handler. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_library;
        }
        out_archive.reset(raw_out_archive);
    }

    set_compression_level(out_archive.get(), request->compression_level);

    /* The output is staged beside the archive before it replaces the destination. */
    std::wstring wide_output;
    out_stream.reset(new COutFileStream());
    if (!out_stream->open_temporary_sibling(wide_path, wide_output)) {
        brows_7z_set_win32_error("Could not create the archive", GetLastError());
        return brows_7z_ERROR_file_io;
    }
    staged_file.own(std::move(wide_output));
    brows_7z_maybe_throw_test_fault(brows_7z_TestFaultPoint::AfterOutputStaged);

    HRESULT result = out_archive->UpdateItems(out_stream.get(), (UInt32)items.size(), &callback);

    /*
     * The handler is closed before the streams are deleted, because the
     * handler still refers to them until then.
     */
    out_archive.reset();
    in_archive.reset();
    out_stream->close();
    if (in_stream) {
        in_stream->close();
    }
    callback.CloseSources();

    if (callback.exception_result() != S_OK) {
        return callback_error(callback.exception_result(),
                              brows_7z_ERROR_compression,
                              "The archive update callback failed.");
    }
    HRESULT progress_result = callback.CheckProgress();
    if (progress_result != S_OK) {
        return callback_error(progress_result,
                              brows_7z_ERROR_compression,
                              "The archive progress callback failed.");
    }
    if (result != S_OK) {
        return callback_error(result,
                              brows_7z_ERROR_compression,
                              "Could not write the archive.");
    }

    if (!MoveFileExW(staged_file.path().c_str(), wide_path.c_str(), MOVEFILE_REPLACE_EXISTING)) {
        brows_7z_set_win32_error("Could not replace the archive", GetLastError());
        return brows_7z_ERROR_file_io;
    }
    staged_file.release();
    return brows_ERROR_NONE;
}

}

brows_7z brows_ERROR brows_7z_init(void) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        if (!brows_7z_ensure_library()) {
            return brows_7z_ERROR_library;
        }
        return brows_ERROR_NONE;
    });
}

brows_7z brows_ERROR brows_7z_exit(void) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        brows_7z_release_library();
        return brows_ERROR_NONE;
    });
}

brows_7z const char* brows_7z_last_error_message(void) {
    return brows_7z_value_boundary<const char*>(
        [&]() -> const char* { return brows_7z_current_error(); },
        "The native error message is unavailable.");
}

brows_7z brows_ERROR brows_7z_directory_is_case_sensitive(const char* directory,
                                                           int32_t* is_case_sensitive) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        if (is_case_sensitive == NULL) {
            brows_7z_set_error("The directory case-sensitivity result was not given.");
            return brows_7z_ERROR_argument;
        }
        *is_case_sensitive = 0;
        if (directory == NULL || directory[0] == 0) {
            brows_7z_set_error("The directory path is missing.");
            return brows_7z_ERROR_argument;
        }
        std::wstring wide_directory;
        if (!brows_7z_to_wide(directory, wide_directory)) {
            brows_7z_set_error("The directory path is not valid UTF-8.");
            return brows_7z_ERROR_argument;
        }
        HANDLE handle = CreateFileW(wide_directory.c_str(),
                                    FILE_READ_ATTRIBUTES,
                                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                    NULL,
                                    OPEN_EXISTING,
                                    FILE_FLAG_BACKUP_SEMANTICS,
                                    NULL);
        if (handle == INVALID_HANDLE_VALUE) {
            const DWORD error = GetLastError();
            brows_7z_set_win32_error("Could not open the directory", error);
            return brows_7z_ERROR_file_io;
        }
        std::unique_ptr<void, FileHandleDeleter> owner(handle);

        FILE_STANDARD_INFO standard_info = {};
        if (!GetFileInformationByHandleEx(handle,
                                          FileStandardInfo,
                                          &standard_info,
                                          sizeof(standard_info))) {
            const DWORD error = GetLastError();
            brows_7z_set_win32_error("Could not read the directory information", error);
            return brows_7z_ERROR_file_io;
        }
        if (!standard_info.Directory) {
            brows_7z_set_error("The path is not a directory.");
            return brows_7z_ERROR_argument;
        }

        FILE_CASE_SENSITIVE_INFO case_info = {};
        if (!GetFileInformationByHandleEx(handle,
                                          FileCaseSensitiveInfo,
                                          &case_info,
                                          sizeof(case_info))) {
            const DWORD error = GetLastError();
            if (is_unsupported_case_sensitive_query(error)) {
                /* Unsupported file systems use Windows' default insensitive lookup behavior. */
                *is_case_sensitive = 0;
                return brows_ERROR_NONE;
            }
            brows_7z_set_win32_error("Could not read directory case-sensitivity information", error);
            return brows_7z_ERROR_file_io;
        }
        *is_case_sensitive = (case_info.Flags & FILE_CS_FLAG_CASE_SENSITIVE_DIR) != 0 ? 1 : 0;
        return brows_ERROR_NONE;
    });
}

brows_7z brows_ERROR brows_7z_Archive_open(const char* path, brows_7z_Archive** out) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        if (out == NULL) {
            brows_7z_set_error("The archive was not given.");
            return brows_7z_ERROR_argument;
        }
        *out = NULL;
        if (path == NULL || path[0] == 0) {
            brows_7z_set_error("The path of the archive is missing.");
            return brows_7z_ERROR_argument;
        }
        std::wstring wide_path;
        if (!brows_7z_to_wide(path, wide_path)) {
            brows_7z_set_error("The path of the archive is not valid UTF-8.");
            return brows_7z_ERROR_argument;
        }
        if (!brows_7z_ensure_library()) {
            return brows_7z_ERROR_library;
        }

        std::unique_ptr<CInFileStream, InStreamDeleter> in_stream(new CInFileStream());
        if (!in_stream->open(wide_path)) {
            brows_7z_set_win32_error("Could not open the archive", GetLastError());
            return brows_7z_ERROR_archive_open;
        }
        IInArchive* raw_in_archive = NULL;
        HRESULT result = brows_7z_create_object(&CLSID_Format7z, &IID_IInArchive, (void**)&raw_in_archive);
        if (FAILED(result) || raw_in_archive == NULL) {
            brows_7z_set_error("Could not create the 7z handler. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_library;
        }
        std::unique_ptr<IInArchive, InArchiveDeleter> in_archive(raw_in_archive);
        result = in_archive->Open(in_stream.get(), NULL, NULL);
        if (result != S_OK) {
            brows_7z_set_error("Could not read the archive. (0x%08lx)", (unsigned long)result);
            return brows_7z_ERROR_archive_open;
        }

        std::unique_ptr<brows_7z_Archive, ArchiveDeleter> archive(new brows_7z_Archive{});
        archive->in_archive = in_archive.release();
        archive->in_stream = in_stream.release();
        brows_7z_maybe_throw_test_fault(brows_7z_TestFaultPoint::AfterArchiveCreated);
        brows_ERROR error = read_entries(archive->in_archive, archive->paths, archive->entries);
        if (error != brows_ERROR_NONE) {
            return error;
        }
        *out = archive.release();
        return brows_ERROR_NONE;
    });
}

brows_7z void brows_7z_Archive_close(brows_7z_Archive* archive) {
    brows_7z_void_boundary([&]() {
        ArchiveDeleter{}(archive);
    });
}

brows_7z uint32_t brows_7z_Archive_get_entry_count(brows_7z_Archive* archive) {
    return brows_7z_value_boundary<uint32_t>(
        [&]() -> uint32_t {
            return archive == NULL ? 0 : (uint32_t)archive->entries.size();
        },
        0);
}

brows_7z const brows_7z_EntryInfo* brows_7z_Archive_get_entry(brows_7z_Archive* archive, uint32_t index) {
    return brows_7z_value_boundary<const brows_7z_EntryInfo*>(
        [&]() -> const brows_7z_EntryInfo* {
            if (archive == NULL || index >= archive->entries.size()) {
                return NULL;
            }
            return &archive->entries[index];
        },
        NULL);
}

brows_7z brows_ERROR brows_7z_Archive_extract_entry(brows_7z_Archive* archive,
                                                     uint32_t index,
                                                     const char* destination,
                                                     int32_t overwrite,
                                                     brows_7z_ProgressCallback progress,
                                                     void* state) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        if (archive == NULL || archive->in_archive == NULL) {
            brows_7z_set_error("The archive is not open.");
            return brows_7z_ERROR_archive_not_open;
        }
        if (index >= archive->entries.size()) {
            brows_7z_set_error("The item is not part of the archive.");
            return brows_7z_ERROR_index_out_of_range;
        }
        if (destination == NULL || destination[0] == 0) {
            brows_7z_set_error("The destination of the item is missing.");
            return brows_7z_ERROR_argument;
        }
        std::wstring wide_destination;
        if (!brows_7z_to_wide(destination, wide_destination)) {
            brows_7z_set_error("The destination of the item is not valid UTF-8.");
            return brows_7z_ERROR_argument;
        }
        const brows_7z_EntryInfo& entry = archive->entries[index];
        const bool is_directory = entry.is_directory != 0;
        if (!is_directory && !overwrite) {
            const DWORD attributes = GetFileAttributesW(wide_destination.c_str());
            if (attributes != INVALID_FILE_ATTRIBUTES) {
                brows_7z_set_error("The file already exists.");
                return brows_7z_ERROR_target_exists;
            }
        }
        CExtractTarget target;
        target.index = index;
        target.path = wide_destination;
        target.modified_time = entry.modified_time;
        target.is_directory = is_directory;
        target.overwrite = overwrite != 0;
        std::vector<CExtractTarget> targets;
        targets.push_back(target);

        CExtractCallback callback(targets, progress, state);
        UInt32 indices[1] = { index };
        const HRESULT result = archive->in_archive->Extract(indices, 1, 0, &callback);
        if (callback.exception_result() != S_OK) {
            return callback_error(callback.exception_result(),
                                  brows_7z_ERROR_file_io,
                                  "The extraction callback failed.");
        }
        if (result != S_OK) {
            return callback_error(result, brows_7z_ERROR_file_io, "Could not extract the item.");
        }
        if (callback.operation_result() != NArchive::NExtract::NOperationResult::kOK) {
            brows_7z_set_error("The item could not be extracted. (result %d)", callback.operation_result());
            return brows_7z_ERROR_file_io;
        }
        const HRESULT commit_result = callback.commit_outputs();
        if (commit_result != S_OK) {
            return callback_error(commit_result,
                                  brows_7z_ERROR_file_io,
                                  "Could not commit the extracted item.");
        }
        apply_modified_time(target);
        return brows_ERROR_NONE;
    });
}

brows_7z brows_ERROR brows_7z_Archive_extract_all(brows_7z_Archive* archive,
                                                  const char* directory,
                                                  int32_t overwrite,
                                                  brows_7z_ProgressCallback progress,
                                                  void* state) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        if (archive == NULL || archive->in_archive == NULL) {
            brows_7z_set_error("The archive is not open.");
            return brows_7z_ERROR_archive_not_open;
        }
        if (directory == NULL || directory[0] == 0) {
            brows_7z_set_error("The folder the archive is extracted to is missing.");
            return brows_7z_ERROR_argument;
        }
        std::wstring wide_directory;
        if (!brows_7z_to_wide(directory, wide_directory)) {
            brows_7z_set_error("The folder the archive is extracted to is not valid UTF-8.");
            return brows_7z_ERROR_argument;
        }
        if (!brows_7z_create_directories(wide_directory)) {
            brows_7z_set_win32_error("Could not create the folder", GetLastError());
            return brows_7z_ERROR_file_io;
        }
        std::vector<CExtractTarget> targets;
        targets.reserve(archive->entries.size());
        for (size_t index = 0; index < archive->entries.size(); index++) {
            const brows_7z_EntryInfo& entry = archive->entries[index];
            std::wstring relative;
            if (!brows_7z_sanitize_archive_path(entry.path, relative)) {
                brows_7z_set_error("The archive contains an item with an unusable path.");
                return brows_7z_ERROR_argument;
            }
            CExtractTarget target;
            target.index = (UInt32)index;
            target.path = brows_7z_combine_path(wide_directory, relative);
            target.modified_time = entry.modified_time;
            target.is_directory = entry.is_directory != 0;
            target.overwrite = overwrite != 0;
            if (!target.is_directory && !overwrite) {
                const DWORD attributes = GetFileAttributesW(target.path.c_str());
                if (attributes != INVALID_FILE_ATTRIBUTES) {
                    brows_7z_set_error("The file already exists.");
                    return brows_7z_ERROR_target_exists;
                }
            }
            targets.push_back(std::move(target));
        }
        CExtractCallback callback(targets, progress, state);
        const HRESULT result = archive->in_archive->Extract(NULL, (UInt32)(Int32)(-1), 0, &callback);
        if (callback.exception_result() != S_OK) {
            return callback_error(callback.exception_result(),
                                  brows_7z_ERROR_file_io,
                                  "The extraction callback failed.");
        }
        if (result != S_OK) {
            return callback_error(result, brows_7z_ERROR_file_io, "Could not extract the archive.");
        }
        if (callback.operation_result() != NArchive::NExtract::NOperationResult::kOK) {
            brows_7z_set_error("An item of the archive could not be extracted. (result %d)", callback.operation_result());
            return brows_7z_ERROR_file_io;
        }
        const HRESULT commit_result = callback.commit_outputs();
        if (commit_result != S_OK) {
            return callback_error(commit_result,
                                  brows_7z_ERROR_file_io,
                                  "Could not commit the extracted files.");
        }
        for (size_t index = 0; index < targets.size(); index++) {
            apply_modified_time(targets[index]);
        }
        return brows_ERROR_NONE;
    });
}

brows_7z brows_ERROR brows_7z_Archive_create(const char* path,
                                              const brows_7z_UpdateRequest* request,
                                              brows_7z_ProgressCallback progress,
                                              void* state) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        return write_archive(path, request, false, progress, state);
    });
}

brows_7z brows_ERROR brows_7z_Archive_update(const char* path,
                                              const brows_7z_UpdateRequest* request,
                                              brows_7z_ProgressCallback progress,
                                              void* state) {
    return brows_7z_export_boundary([&]() -> brows_ERROR {
        return write_archive(path, request, true, progress, state);
    });
}
