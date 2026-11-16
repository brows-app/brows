#include "brows_7z_stream.h"


CInFileStream::CInFileStream() :
    handle(INVALID_HANDLE_VALUE)
{
}

CInFileStream::~CInFileStream() {
    close();
}

ULONG CInFileStream::AddRef(void) throw() {
    return (ULONG)InterlockedIncrement((LONG*)&_m_RefCount);
}

ULONG CInFileStream::Release(void) throw() {
    const LONG count = InterlockedDecrement((LONG*)&_m_RefCount);
    if (count < 0) {
        /*
         * 7-Zip does not own the stream, so the stream is not deleted when the
         * count reaches zero. The owner of the stream deletes it.
         */
        InterlockedIncrement((LONG*)&_m_RefCount);
        return 0;
    }
    return (ULONG)count;
}

bool CInFileStream::open(const std::wstring& path) {
    close();
    handle = CreateFileW(path.c_str(),
                         GENERIC_READ,
                         FILE_SHARE_READ,
                         NULL,
                         OPEN_EXISTING,
                         FILE_ATTRIBUTE_NORMAL,
                         NULL);
    return handle != INVALID_HANDLE_VALUE;
}

void CInFileStream::close(void) {
    if (handle != INVALID_HANDLE_VALUE) {
        CloseHandle(handle);
        handle = INVALID_HANDLE_VALUE;
    }
}

bool CInFileStream::is_open(void) const {
    return handle != INVALID_HANDLE_VALUE;
}

Z7_COM7F_IMF(CInFileStream::Read(void* data, UInt32 size, UInt32* processedSize)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (processedSize != NULL) {
            *processedSize = 0;
        }
        if (handle == INVALID_HANDLE_VALUE) {
            return E_FAIL;
        }
        if (size == 0) {
            return S_OK;
        }
        DWORD read = 0;
        if (!ReadFile(handle, data, size, &read, NULL)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (processedSize != NULL) {
            *processedSize = read;
        }
        return S_OK;
    });
}

Z7_COM7F_IMF(CInFileStream::Seek(Int64 offset, UInt32 seekOrigin, UInt64* newPosition)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (handle == INVALID_HANDLE_VALUE) {
            return E_FAIL;
        }
        if (seekOrigin >= 3) {
            return STG_E_INVALIDFUNCTION;
        }
        LARGE_INTEGER distance;
        distance.QuadPart = offset;
        LARGE_INTEGER position;
        if (!SetFilePointerEx(handle, distance, &position, (DWORD)seekOrigin)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (newPosition != NULL) {
            *newPosition = (UInt64)position.QuadPart;
        }
        return S_OK;
    });
}

COutFileStream::COutFileStream() :
    handle(INVALID_HANDLE_VALUE)
{
}

COutFileStream::~COutFileStream() {
    close();
}

ULONG COutFileStream::AddRef(void) throw() {
    return (ULONG)InterlockedIncrement((LONG*)&_m_RefCount);
}

ULONG COutFileStream::Release(void) throw() {
    const LONG count = InterlockedDecrement((LONG*)&_m_RefCount);
    if (count < 0) {
        /*
         * 7-Zip does not own the stream, so the stream is not deleted when the
         * count reaches zero. The owner of the stream deletes it.
         */
        InterlockedIncrement((LONG*)&_m_RefCount);
        return 0;
    }
    return (ULONG)count;
}

bool COutFileStream::open(const std::wstring& path) {
    close();
    handle = CreateFileW(path.c_str(),
                         GENERIC_WRITE,
                         FILE_SHARE_READ,
                         NULL,
                         CREATE_ALWAYS,
                         FILE_ATTRIBUTE_NORMAL,
                         NULL);
    return handle != INVALID_HANDLE_VALUE;
}

bool COutFileStream::open_temporary_sibling(const std::wstring& path, std::wstring& output_path) {
    close();
    static volatile LONG sequence = 0;
    std::wstring prefix;
    const size_t separator = path.find_last_of(L"\\/");
    if (separator != std::wstring::npos) {
        prefix.assign(path, 0, separator + 1);
    }
    for (unsigned int attempt = 0; attempt < 128; attempt++) {
        const ULONG id = (ULONG)InterlockedIncrement(&sequence);
        std::wstring candidate = prefix;
        candidate += L"b7-";
        candidate += std::to_wstring(GetCurrentProcessId());
        candidate += L"-";
        candidate += std::to_wstring(id);
        candidate += L".tmp";
        HANDLE file = CreateFileW(candidate.c_str(),
                                  GENERIC_WRITE,
                                  FILE_SHARE_READ,
                                  NULL,
                                  CREATE_NEW,
                                  FILE_ATTRIBUTE_NORMAL,
                                  NULL);
        if (file != INVALID_HANDLE_VALUE) {
            handle = file;
            output_path.swap(candidate);
            return true;
        }
        const DWORD error = GetLastError();
        if (error != ERROR_FILE_EXISTS && error != ERROR_ALREADY_EXISTS) {
            SetLastError(error);
            return false;
        }
    }
    SetLastError(ERROR_FILE_EXISTS);
    return false;
}

void COutFileStream::close(void) {
    if (handle != INVALID_HANDLE_VALUE) {
        CloseHandle(handle);
        handle = INVALID_HANDLE_VALUE;
    }
}

bool COutFileStream::is_open(void) const {
    return handle != INVALID_HANDLE_VALUE;
}

Z7_COM7F_IMF(COutFileStream::Write(const void* data, UInt32 size, UInt32* processedSize)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (processedSize != NULL) {
            *processedSize = 0;
        }
        if (handle == INVALID_HANDLE_VALUE) {
            return E_FAIL;
        }
        if (size == 0) {
            return S_OK;
        }
        DWORD written = 0;
        if (!WriteFile(handle, data, size, &written, NULL)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (processedSize != NULL) {
            *processedSize = written;
        }
        return S_OK;
    });
}

Z7_COM7F_IMF(COutFileStream::Seek(Int64 offset, UInt32 seekOrigin, UInt64* newPosition)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (handle == INVALID_HANDLE_VALUE) {
            return E_FAIL;
        }
        if (seekOrigin >= 3) {
            return STG_E_INVALIDFUNCTION;
        }
        LARGE_INTEGER distance;
        distance.QuadPart = offset;
        LARGE_INTEGER position;
        if (!SetFilePointerEx(handle, distance, &position, (DWORD)seekOrigin)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (newPosition != NULL) {
            *newPosition = (UInt64)position.QuadPart;
        }
        return S_OK;
    });
}

Z7_COM7F_IMF(COutFileStream::SetSize(UInt64 newSize)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (handle == INVALID_HANDLE_VALUE) {
            return E_FAIL;
        }
        /*
         * The size of the file is changed behind the position of the file, so the
         * position is remembered and restored afterwards. 7-Zip expects the
         * position of the stream to stay where it was.
         */
        LARGE_INTEGER position;
        LARGE_INTEGER zero;
        zero.QuadPart = 0;
        if (!SetFilePointerEx(handle, zero, &position, FILE_CURRENT)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        LARGE_INTEGER size;
        size.QuadPart = (Int64)newSize;
        if (!SetFilePointerEx(handle, size, NULL, FILE_BEGIN)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (!SetEndOfFile(handle)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (!SetFilePointerEx(handle, position, NULL, FILE_BEGIN)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        return S_OK;
    });
}
