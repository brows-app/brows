#ifndef BROWS_7Z_INTERNAL_H
#define BROWS_7Z_INTERNAL_H

#include "brows_7z.h"

#include "Common/MyWindows.h"

/* The 7-Zip objects are used from more than one thread by the managed side. */
#define Z7_COM_USE_ATOMIC
#include "Common/MyCom.h"

#include "7zip/Archive/IArchive.h"
#include "7zip/IProgress.h"
#include "7zip/PropID.h"

#include <exception>
#include <atomic>
#include <new>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

/*
 * The class id of the 7z archive handler. It is defined in brows_7z_guids.cpp,
 * which is the only translation unit that defines the 7-Zip interface ids.
 */
extern "C" const GUID CLSID_Format7z;

/* The stream that reads the archive file. It is defined in brows_7z_stream.h. */
class CInFileStream;

/*
 * An opened archive. The entry list is read once when the archive is opened
 * and then kept in memory so that the managed side can walk it cheaply.
 */
struct brows_7z_Archive {
    IInArchive*                      in_archive;
    CInFileStream*                   in_stream;
    std::vector<std::string>         paths;
    std::vector<brows_7z_EntryInfo>  entries;
};

/*
 * The helpers below are internal to this library. They are declared with plain
 * C++ linkage, because some of them exchange std::wstring values.
 */

/* The signature of the CreateObject function that 7zip.dll exports. */
typedef HRESULT (STDMETHODCALLTYPE* brows_7z_Func_CreateObject)(const GUID* clsid, const GUID* iid, void** out_object);

/* Owns only a path created by CREATE_NEW and removes it unless it is committed. */
class brows_7z_TemporaryFile final {
public:
    brows_7z_TemporaryFile() noexcept :
        Owned(false)
    {
    }

    explicit brows_7z_TemporaryFile(std::wstring&& path) noexcept :
        Path(std::move(path)),
        Owned(true)
    {
    }

    ~brows_7z_TemporaryFile() noexcept {
        if (Owned) {
            DeleteFileW(Path.c_str());
        }
    }

    const std::wstring& path(void) const noexcept {
        return Path;
    }

    void own(std::wstring&& path) noexcept {
        Path = std::move(path);
        Owned = true;
    }

    void release(void) noexcept {
        Owned = false;
    }

private:
    std::wstring Path;
    bool Owned;
};

/* Loads 7zip.dll when it is not loaded yet. */
bool brows_7z_ensure_library(void);

/* Unloads 7zip.dll. */
void brows_7z_release_library(void);

/* Creates a 7-Zip object. */
HRESULT brows_7z_create_object(const GUID* clsid, const GUID* iid, void** out_object);

/* Records the message that describes the most recent failure. */
void brows_7z_set_error(const char* format, ...) noexcept;

/* Returns the message that describes the most recent failure. */
const char* brows_7z_current_error(void);

/* Records the message of a Win32 error code. */
void brows_7z_set_win32_error(const char* context, DWORD code);

/* Converts UTF-8 text to UTF-16 text. */
bool brows_7z_to_wide(const char* text, std::wstring& out);

/* Converts UTF-16 text to UTF-8 text. */
bool brows_7z_to_utf8(const wchar_t* text, std::string& out);

/* Creates a directory and every missing parent of it. */
bool brows_7z_create_directories(const std::wstring& path);

/* Joins a directory and a relative path. */
std::wstring brows_7z_combine_path(const std::wstring& directory, const std::wstring& relative);

/*
 * Turns the path of an archive item into a relative path that is safe to
 * write below the extraction directory. Returns false when the path cannot be
 * used at all.
 */
bool brows_7z_sanitize_archive_path(const std::string& path, std::wstring& out);

/*
 * Turns the path of an archive item into a path that uses forward slashes.
 *
 * 7-Zip reports the names of the items of an archive with the separators of
 * the system it runs on, so the names have to be made uniform before they are
 * handed to the managed side.
 */
void brows_7z_normalize_archive_path(std::string& path);

/* Returns the last component of a path. */
std::wstring brows_7z_path_name(const std::wstring& path);

/* Calls the managed progress bridge and converts callback failures to HRESULTs. */
HRESULT brows_7z_report_progress(const brows_7z_ProgressCallback progress,
                                 void* state,
                                 UInt64 completed,
                                 UInt64 total) noexcept;

enum class brows_7z_TestFaultPoint : uint32_t {
    None = 0,
    AfterArchiveCreated = 1,
    AfterOutputStaged = 2,
    UpdateCallback = 3,
};

enum class brows_7z_TestFaultKind : uint32_t {
    None = 0,
    BadAlloc = 1,
    LengthError = 2,
};

/* Test-only fault injection. This state is scoped to the calling native thread. */
brows_7z void brows_7z_test_set_fault(uint32_t point, uint32_t kind);
void brows_7z_maybe_throw_test_fault(brows_7z_TestFaultPoint point);

template <typename Callable>
HRESULT brows_7z_callback_boundary(Callable&& callable,
                                  std::atomic<HRESULT>* caught_exception = NULL) noexcept {
    try {
        return callable();
    }
    catch (const std::bad_alloc&) {
        brows_7z_set_error("Not enough memory in a 7-Zip callback.");
        if (caught_exception != NULL) {
            HRESULT expected = S_OK;
            caught_exception->compare_exchange_strong(expected, E_OUTOFMEMORY);
        }
        return E_OUTOFMEMORY;
    }
    catch (const std::length_error&) {
        brows_7z_set_error("A 7-Zip callback exceeded a native size limit.");
        if (caught_exception != NULL) {
            HRESULT expected = S_OK;
            caught_exception->compare_exchange_strong(expected, E_OUTOFMEMORY);
        }
        return E_OUTOFMEMORY;
    }
    catch (const std::exception& exception) {
        brows_7z_set_error("A 7-Zip callback failed: %s", exception.what());
        if (caught_exception != NULL) {
            HRESULT expected = S_OK;
            caught_exception->compare_exchange_strong(expected, E_FAIL);
        }
        return E_FAIL;
    }
    catch (...) {
        brows_7z_set_error("A 7-Zip callback failed with an unknown native exception.");
        if (caught_exception != NULL) {
            HRESULT expected = S_OK;
            caught_exception->compare_exchange_strong(expected, E_FAIL);
        }
        return E_FAIL;
    }
}

template <typename Callable>
brows_ERROR brows_7z_export_boundary(Callable&& callable) noexcept {
    try {
        return callable();
    }
    catch (const std::bad_alloc&) {
        brows_7z_set_error("Not enough memory for the 7-Zip operation.");
        return brows_7z_ERROR_memory;
    }
    catch (const std::length_error&) {
        brows_7z_set_error("The 7-Zip operation exceeded a native size limit.");
        return brows_7z_ERROR_memory;
    }
    catch (const std::exception& exception) {
        brows_7z_set_error("The 7-Zip operation failed: %s", exception.what());
        return brows_7z_ERROR_native_exception;
    }
    catch (...) {
        brows_7z_set_error("The 7-Zip operation failed with an unknown native exception.");
        return brows_7z_ERROR_native_exception;
    }
}

template <typename Result, typename Callable>
Result brows_7z_value_boundary(Callable&& callable, Result fallback) noexcept {
    try {
        return callable();
    }
    catch (const std::bad_alloc&) {
        brows_7z_set_error("Not enough memory for the 7-Zip operation.");
        return fallback;
    }
    catch (const std::length_error&) {
        brows_7z_set_error("The 7-Zip operation exceeded a native size limit.");
        return fallback;
    }
    catch (const std::exception& exception) {
        brows_7z_set_error("The 7-Zip operation failed: %s", exception.what());
        return fallback;
    }
    catch (...) {
        brows_7z_set_error("The 7-Zip operation failed with an unknown native exception.");
        return fallback;
    }
}

template <typename Callable>
void brows_7z_void_boundary(Callable&& callable) noexcept {
    try {
        callable();
    }
    catch (const std::bad_alloc&) {
        brows_7z_set_error("Not enough memory for the 7-Zip operation.");
    }
    catch (const std::length_error&) {
        brows_7z_set_error("The 7-Zip operation exceeded a native size limit.");
    }
    catch (const std::exception& exception) {
        brows_7z_set_error("The 7-Zip operation failed: %s", exception.what());
    }
    catch (...) {
        brows_7z_set_error("The 7-Zip operation failed with an unknown native exception.");
    }
}

#endif
