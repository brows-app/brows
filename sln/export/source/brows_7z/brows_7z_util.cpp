#include "brows_7z_internal.h"

#include <cstdarg>
#include <cstdio>
#include <cstring>

namespace {

/* The message that describes the most recent failure of the calling thread. */
thread_local char g_error_message[1024] = {};

/* The next deterministic test fault for the calling native thread. */
thread_local brows_7z_TestFaultPoint g_test_fault_point = brows_7z_TestFaultPoint::None;
thread_local brows_7z_TestFaultKind g_test_fault_kind = brows_7z_TestFaultKind::None;

/* The 7-Zip library, once it has been loaded. */
HMODULE g_module = NULL;

/* The entry point of the 7-Zip library. */
brows_7z_Func_CreateObject g_create_object = NULL;

/*
 * An address inside this library. It is used to find out where this library
 * was loaded, so that the 7-Zip library can be loaded from the same folder.
 */
int g_module_anchor = 0;

/* Returns the folder that holds this library. */
bool module_directory(std::wstring& out) {
    HMODULE self = NULL;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                            (LPCWSTR)(const void*)&g_module_anchor,
                            &self)) {
        return false;
    }
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(self, path, MAX_PATH);
    if (length == 0 || length >= MAX_PATH) {
        return false;
    }
    size_t index = length;
    while (index > 0) {
        if (path[index - 1] == L'\\') {
            break;
        }
        index--;
    }
    if (index == 0) {
        return false;
    }
    out.assign(path, index);
    return true;
}

/* Loads the 7-Zip library. */
HMODULE load_library(void) {
    std::wstring directory;
    if (module_directory(directory)) {
        std::wstring path = directory + L"7zip.dll";
        HMODULE module = LoadLibraryExW(path.c_str(), NULL, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (module != NULL) {
            return module;
        }
    }
    return LoadLibraryW(L"7zip.dll");
}

}

void brows_7z_set_error(const char* format, ...) noexcept {
    if (format == NULL) {
        g_error_message[0] = 0;
        return;
    }
    va_list args;
    va_start(args, format);
    int written = vsnprintf(g_error_message, sizeof(g_error_message), format, args);
    va_end(args);
    if (written < 0) {
        g_error_message[0] = 0;
        return;
    }
    if ((size_t)written >= sizeof(g_error_message)) {
        g_error_message[sizeof(g_error_message) - 1] = 0;
    }
}

const char* brows_7z_current_error(void) {
    return g_error_message;
}

HRESULT brows_7z_report_progress(const brows_7z_ProgressCallback progress,
                                 void* state,
                                 UInt64 completed,
                                 UInt64 total) noexcept {
    if (progress == NULL) {
        return S_OK;
    }
    try {
        return (HRESULT)progress(state, (uint64_t)completed, (uint64_t)total);
    }
    catch (const std::bad_alloc&) {
        brows_7z_set_error("Not enough memory in the managed progress callback.");
        return E_OUTOFMEMORY;
    }
    catch (const std::length_error&) {
        brows_7z_set_error("The managed progress callback exceeded a native size limit.");
        return E_OUTOFMEMORY;
    }
    catch (const std::exception& exception) {
        brows_7z_set_error("The managed progress callback failed: %s", exception.what());
        return E_FAIL;
    }
    catch (...) {
        brows_7z_set_error("The managed progress callback failed with an unknown exception.");
        return E_FAIL;
    }
}

brows_7z void brows_7z_test_set_fault(uint32_t point, uint32_t kind) {
    brows_7z_void_boundary([&]() {
        g_test_fault_point = (brows_7z_TestFaultPoint)point;
        g_test_fault_kind = (brows_7z_TestFaultKind)kind;
    });
}

void brows_7z_maybe_throw_test_fault(brows_7z_TestFaultPoint point) {
    if (g_test_fault_point != point) {
        return;
    }
    const brows_7z_TestFaultKind kind = g_test_fault_kind;
    g_test_fault_point = brows_7z_TestFaultPoint::None;
    g_test_fault_kind = brows_7z_TestFaultKind::None;
    if (kind == brows_7z_TestFaultKind::BadAlloc) {
        throw std::bad_alloc();
    }
    if (kind == brows_7z_TestFaultKind::LengthError) {
        throw std::length_error("Injected 7-Zip length error.");
    }
}

void brows_7z_set_win32_error(const char* context, DWORD code) {
    char* message = NULL;
    DWORD size = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                                 NULL,
                                 code,
                                 0,
                                 (LPSTR)&message,
                                 0,
                                 NULL);
    if (size > 0 && message != NULL) {
        while (size > 0 && (message[size - 1] == '\r' || message[size - 1] == '\n' || message[size - 1] == ' ')) {
            message[size - 1] = 0;
            size--;
        }
        brows_7z_set_error("%s: %s (%lu)", context, message, (unsigned long)code);
        LocalFree(message);
        return;
    }
    brows_7z_set_error("%s: error %lu", context, (unsigned long)code);
}

bool brows_7z_ensure_library(void) {
    if (g_create_object != NULL) {
        return true;
    }
    if (g_module == NULL) {
        g_module = load_library();
    }
    if (g_module == NULL) {
        brows_7z_set_win32_error("Could not load 7zip.dll", GetLastError());
        return false;
    }
    g_create_object = (brows_7z_Func_CreateObject)(void*)GetProcAddress(g_module, "CreateObject");
    if (g_create_object == NULL) {
        brows_7z_set_error("Could not find CreateObject in 7zip.dll.");
        FreeLibrary(g_module);
        g_module = NULL;
        return false;
    }
    return true;
}

void brows_7z_release_library(void) {
    if (g_module != NULL) {
        FreeLibrary(g_module);
        g_module = NULL;
    }
    g_create_object = NULL;
}

HRESULT brows_7z_create_object(const GUID* clsid, const GUID* iid, void** out_object) {
    if (!brows_7z_ensure_library()) {
        return E_FAIL;
    }
    return g_create_object(clsid, iid, out_object);
}

bool brows_7z_to_wide(const char* text, std::wstring& out) {
    if (text == NULL) {
        return false;
    }
    int size = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text, -1, NULL, 0);
    if (size <= 0) {
        return false;
    }
    std::wstring buffer((size_t)size, L'\0');
    int written = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text, -1, &buffer[0], size);
    if (written <= 0) {
        return false;
    }
    buffer.resize((size_t)written - 1);
    out.swap(buffer);
    return true;
}

bool brows_7z_to_utf8(const wchar_t* text, std::string& out) {
    if (text == NULL) {
        return false;
    }
    int size = WideCharToMultiByte(CP_UTF8, 0, text, -1, NULL, 0, NULL, NULL);
    if (size <= 0) {
        return false;
    }
    std::string buffer((size_t)size, '\0');
    int written = WideCharToMultiByte(CP_UTF8, 0, text, -1, &buffer[0], size, NULL, NULL);
    if (written <= 0) {
        return false;
    }
    buffer.resize((size_t)written - 1);
    out.swap(buffer);
    return true;
}

bool brows_7z_create_directories(const std::wstring& path) {
    if (path.empty()) {
        return false;
    }
    DWORD attributes = GetFileAttributesW(path.c_str());
    if (attributes != INVALID_FILE_ATTRIBUTES) {
        return (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
    }
    size_t index = path.find_last_of(L'\\');
    if (index != std::wstring::npos && index > 0) {
        std::wstring parent = path.substr(0, index);
        /* A drive root has no parent that needs to be created. */
        bool parent_is_root = parent.size() == 2 && parent[1] == L':';
        if (!parent_is_root && !brows_7z_create_directories(parent)) {
            return false;
        }
    }
    if (CreateDirectoryW(path.c_str(), NULL)) {
        return true;
    }
    DWORD code = GetLastError();
    if (code == ERROR_ALREADY_EXISTS) {
        attributes = GetFileAttributesW(path.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
    }
    return false;
}

std::wstring brows_7z_combine_path(const std::wstring& directory, const std::wstring& relative) {
    if (directory.empty()) {
        return relative;
    }
    if (relative.empty()) {
        return directory;
    }
    if (directory[directory.size() - 1] == L'\\') {
        return directory + relative;
    }
    return directory + L"\\" + relative;
}

bool brows_7z_sanitize_archive_path(const std::string& path, std::wstring& out) {
    std::wstring wide_path;
    if (!brows_7z_to_wide(path.c_str(), wide_path)) {
        return false;
    }
    std::wstring result;
    size_t start = 0;
    while (start <= wide_path.size()) {
        size_t end = wide_path.find_first_of(L"\\/", start);
        std::wstring part = (end == std::wstring::npos)
            ? wide_path.substr(start)
            : wide_path.substr(start, end - start);
        if (!part.empty() && part != L".") {
            if (part == L"..") {
                /* An item must never point outside of the extraction folder. */
                return false;
            }
            for (size_t index = 0; index < part.size(); index++) {
                wchar_t character = part[index];
                bool invalid = character < 32 || character == L':' || character == L'*' ||
                               character == L'?' || character == L'"' || character == L'<' ||
                               character == L'>' || character == L'|';
                if (invalid) {
                    return false;
                }
            }
            if (!result.empty()) {
                result += L'\\';
            }
            result += part;
        }
        if (end == std::wstring::npos) {
            break;
        }
        start = end + 1;
    }
    if (result.empty()) {
        return false;
    }
    out.swap(result);
    return true;
}

std::wstring brows_7z_path_name(const std::wstring& path) {
    size_t index = path.find_last_of(L'\\');
    if (index == std::wstring::npos) {
        return path;
    }
    return path.substr(index + 1);
}

void brows_7z_normalize_archive_path(std::string& path) {
    for (size_t index = 0; index < path.size(); index++) {
        if (path[index] == '\\') {
            path[index] = '/';
        }
    }
}
