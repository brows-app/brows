#include "brows_7z_callback.h"

#include <new>
#include <utility>

CExtractCallback::CExtractCallback(const std::vector<CExtractTarget>& targets,
                                   brows_7z_ProgressCallback progress,
                                   void* progress_state) :
    targets(targets),
    last_operation_result(NArchive::NExtract::NOperationResult::kOK),
    progress(progress),
    progress_state(progress_state),
    progress_total(0),
    progress_completed(0),
    progress_result(S_OK),
    callback_exception(S_OK)
{
    outputs.reserve(targets.size());
}

CExtractCallback::~CExtractCallback() {
    for (size_t index = 0; index < outputs.size(); index++) {
        if (outputs[index].stream != NULL) {
            outputs[index].stream->close();
        }
    }
    for (size_t index = 0; index < outputs.size(); index++) {
        if (!outputs[index].temporary_path.empty()) {
            DeleteFileW(outputs[index].temporary_path.c_str());
        }
    }
}

ULONG CExtractCallback::AddRef(void) throw() {
    return (ULONG)InterlockedIncrement((LONG*)&_m_RefCount);
}

ULONG CExtractCallback::Release(void) throw() {
    const LONG count = InterlockedDecrement((LONG*)&_m_RefCount);
    if (count < 0) {
        /*
         * 7-Zip does not own the callback, so the callback is not deleted when
         * the count reaches zero. The owner of the callback deletes it.
         */
        InterlockedIncrement((LONG*)&_m_RefCount);
        return 0;
    }
    return (ULONG)count;
}

const CExtractTarget* CExtractCallback::find_target(UInt32 index) const {
    for (size_t i = 0; i < targets.size(); i++) {
        if (targets[i].index == index) {
            return &targets[i];
        }
    }
    return NULL;
}

Int32 CExtractCallback::operation_result(void) const {
    return last_operation_result;
}

HRESULT CExtractCallback::commit_outputs(void) {
    HRESULT result = CheckProgress();
    if (result != S_OK) {
        return result;
    }
    for (size_t index = 0; index < outputs.size(); index++) {
        outputs[index].stream->close();
    }
    for (size_t index = 0; index < outputs.size(); index++) {
        CExtractOutput& output = outputs[index];
        const DWORD flags = output.target->overwrite ? MOVEFILE_REPLACE_EXISTING : 0;
        if (!MoveFileExW(output.temporary_path.c_str(), output.target->path.c_str(), flags)) {
            const DWORD error = GetLastError();
            if (error == ERROR_SUCCESS) {
                brows_7z_set_error("Could not commit an extracted file.");
                return E_FAIL;
            }
            brows_7z_set_win32_error("Could not commit an extracted file", error);
            return HRESULT_FROM_WIN32(error);
        }
        output.temporary_path.clear();
    }
    return S_OK;
}

HRESULT CExtractCallback::CheckProgress(void) {
    std::lock_guard<std::mutex> lock(progress_mutex);
    if (progress_result != S_OK) {
        return progress_result;
    }
    progress_result = brows_7z_report_progress(
        progress,
        progress_state,
        progress_completed,
        progress_total);
    return progress_result;
}

HRESULT CExtractCallback::exception_result(void) const {
    return callback_exception.load();
}

Z7_COM7F_IMF(CExtractCallback::GetStream(UInt32 index, ISequentialOutStream** out_stream, Int32 ask_extract_mode)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (out_stream == NULL) {
            return E_POINTER;
        }
        *out_stream = NULL;
        if (ask_extract_mode != NArchive::NExtract::NAskMode::kExtract) {
            return S_OK;
        }
        const CExtractTarget* target = find_target(index);
        if (target == NULL) {
            /* The item is not wanted, so it is skipped. */
            return S_OK;
        }
        if (target->is_directory) {
            if (!brows_7z_create_directories(target->path)) {
                const DWORD error = GetLastError();
                brows_7z_set_win32_error("Could not create the directory", error);
                return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
            }
            return S_OK;
        }
        std::wstring parent = target->path;
        size_t separator = parent.find_last_of(L'\\');
        if (separator != std::wstring::npos && separator > 0) {
            parent = parent.substr(0, separator);
            if (!brows_7z_create_directories(parent)) {
                const DWORD error = GetLastError();
                brows_7z_set_win32_error("Could not create the directory", error);
                return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
            }
        }
        brows_7z_TemporaryFile temporary;
        auto stream = std::make_unique<COutFileStream>();
        std::wstring temporary_path;
        if (!stream->open_temporary_sibling(target->path, temporary_path)) {
            const DWORD error = GetLastError();
            brows_7z_set_win32_error("Could not create the file", error);
            return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
        }
        temporary.own(std::move(temporary_path));
        outputs.push_back({target, temporary.path(), std::move(stream)});
        temporary.release();
        *out_stream = outputs.back().stream.get();
        return S_OK;
    }, &callback_exception);
}

Z7_COM7F_IMF(CExtractCallback::PrepareOperation(Int32 ask_extract_mode)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        return S_OK;
    }, &callback_exception);
}

Z7_COM7F_IMF(CExtractCallback::SetOperationResult(Int32 operation_result)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        std::lock_guard<std::mutex> lock(progress_mutex);
        if (operation_result != NArchive::NExtract::NOperationResult::kOK) {
            if (last_operation_result == NArchive::NExtract::NOperationResult::kOK) {
                last_operation_result = operation_result;
            }
            return E_FAIL;
        }
        if (progress_result != S_OK) {
            return progress_result;
        }
        return last_operation_result == NArchive::NExtract::NOperationResult::kOK ? S_OK : E_FAIL;
    }, &callback_exception);
}

Z7_COM7F_IMF(CExtractCallback::SetTotal(UInt64 total)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        std::lock_guard<std::mutex> lock(progress_mutex);
        progress_total = total;
        progress_result = brows_7z_report_progress(progress, progress_state, progress_completed, progress_total);
        return progress_result;
    }, &callback_exception);
}

Z7_COM7F_IMF(CExtractCallback::SetCompleted(const UInt64* complete_value)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        std::lock_guard<std::mutex> lock(progress_mutex);
        if (complete_value != NULL) {
            progress_completed = *complete_value;
        }
        progress_result = brows_7z_report_progress(progress, progress_state, progress_completed, progress_total);
        return progress_result;
    }, &callback_exception);
}

CUpdateCallback::CUpdateCallback(const std::vector<CUpdateItem>& items,
                                 brows_7z_ProgressCallback progress,
                                 void* progress_state) :
    items(items),
    progress(progress),
    progress_state(progress_state),
    progress_total(0),
    progress_completed(0),
    progress_result(S_OK),
    callback_exception(S_OK)
{
}

CUpdateCallback::~CUpdateCallback() {
}

HRESULT CUpdateCallback::OpenSources() {
    streams.resize(items.size());
    for (size_t index = 0; index < items.size(); index++) {
        const CUpdateItem& item = items[index];
        if (item.is_kept || item.is_directory) {
            continue;
        }
        auto stream = std::make_unique<CInFileStream>();
        if (!stream->open(item.source_path)) {
            const DWORD error = GetLastError();
            brows_7z_set_win32_error("Could not open the source file", error);
            if (error == ERROR_SUCCESS) {
                return E_FAIL;
            }
            return HRESULT_FROM_WIN32(error);
        }
        streams[index] = std::move(stream);
    }
    return S_OK;
}

void CUpdateCallback::CloseSources() {
    for (size_t index = 0; index < streams.size(); index++) {
        if (streams[index] != NULL) {
            streams[index]->close();
        }
    }
}

HRESULT CUpdateCallback::CheckProgress(void) {
    std::lock_guard<std::mutex> lock(progress_mutex);
    if (progress_result != S_OK) {
        return progress_result;
    }
    progress_result = brows_7z_report_progress(
        progress,
        progress_state,
        progress_completed,
        progress_total);
    return progress_result;
}

HRESULT CUpdateCallback::exception_result(void) const {
    return callback_exception.load();
}

ULONG CUpdateCallback::AddRef(void) throw() {
    return (ULONG)InterlockedIncrement((LONG*)&_m_RefCount);
}

ULONG CUpdateCallback::Release(void) throw() {
    const LONG count = InterlockedDecrement((LONG*)&_m_RefCount);
    if (count < 0) {
        /*
         * 7-Zip does not own the callback, so the callback is not deleted when
         * the count reaches zero. The owner of the callback deletes it.
         */
        InterlockedIncrement((LONG*)&_m_RefCount);
        return 0;
    }
    return (ULONG)count;
}

Z7_COM7F_IMF(CUpdateCallback::GetUpdateItemInfo(UInt32 index, Int32* new_data, Int32* new_props, UInt32* index_in_archive)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (new_data == NULL || new_props == NULL || index_in_archive == NULL) {
            return E_POINTER;
        }
        if (index >= items.size()) {
            return E_INVALIDARG;
        }
        const CUpdateItem& item = items[index];
        if (item.is_kept) {
            /*
             * The item is copied from the archive that is updated. The properties
             * of the item are taken from that archive as well.
             */
            *new_data = 0;
            *new_props = 0;
            *index_in_archive = item.archive_index;
        }
        else {
            *new_data = 1;
            *new_props = 1;
            *index_in_archive = (UInt32)(Int32)(-1);
        }
        return S_OK;
    }, &callback_exception);
}

Z7_COM7F_IMF(CUpdateCallback::GetProperty(UInt32 index, PROPID prop_id, PROPVARIANT* value)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (value == NULL) {
            return E_POINTER;
        }
        if (index >= items.size()) {
            return E_INVALIDARG;
        }
        memset(value, 0, sizeof(*value));
        value->vt = VT_EMPTY;
        brows_7z_maybe_throw_test_fault(brows_7z_TestFaultPoint::UpdateCallback);
        const CUpdateItem& item = items[index];
        switch (prop_id) {
            case kpidPath: {
                BSTR text = SysAllocString(item.path.c_str());
                if (text == NULL) {
                    return E_OUTOFMEMORY;
                }
                value->vt = VT_BSTR;
                value->bstrVal = text;
                break;
            }
            case kpidIsDir:
                value->vt = VT_BOOL;
                value->boolVal = item.is_directory ? VARIANT_TRUE : VARIANT_FALSE;
                break;
            case kpidSize:
                if (item.is_directory) {
                    break;
                }
                value->vt = VT_UI8;
                value->uhVal.QuadPart = (LONGLONG)item.size;
                break;
            case kpidAttrib:
                value->vt = VT_UI4;
                value->ulVal = item.attributes;
                break;
            case kpidMTime:
                if (item.modified_time == 0) {
                    break;
                }
                value->vt = VT_FILETIME;
                value->filetime.dwLowDateTime = (DWORD)((UInt64)item.modified_time & 0xFFFFFFFF);
                value->filetime.dwHighDateTime = (DWORD)(((UInt64)item.modified_time >> 32) & 0xFFFFFFFF);
                break;
            case kpidIsAnti:
                value->vt = VT_BOOL;
                value->boolVal = VARIANT_FALSE;
                break;
            default:
                break;
        }
        return S_OK;
    }, &callback_exception);
}

Z7_COM7F_IMF(CUpdateCallback::GetStream(UInt32 index, ISequentialInStream** in_stream)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        if (in_stream == NULL) {
            return E_POINTER;
        }
        *in_stream = NULL;
        if (index >= items.size()) {
            return E_INVALIDARG;
        }
        const CUpdateItem& item = items[index];
        if (item.is_kept || item.is_directory) {
            /* The content of a kept item is copied by 7-Zip itself. */
            return S_OK;
        }
        CInFileStream* stream = streams[index].get();
        if (stream == NULL) {
            brows_7z_set_error("The source file stream was not opened.");
            return E_FAIL;
        }
        *in_stream = stream;
        return S_OK;
    }, &callback_exception);
}

Z7_COM7F_IMF(CUpdateCallback::SetOperationResult(Int32 operation_result)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        (void)operation_result;
        std::lock_guard<std::mutex> lock(progress_mutex);
        return progress_result;
    }, &callback_exception);
}

Z7_COM7F_IMF(CUpdateCallback::SetTotal(UInt64 total)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        std::lock_guard<std::mutex> lock(progress_mutex);
        progress_total = total;
        progress_result = brows_7z_report_progress(progress, progress_state, progress_completed, progress_total);
        return progress_result;
    }, &callback_exception);
}

Z7_COM7F_IMF(CUpdateCallback::SetCompleted(const UInt64* complete_value)) {
    return brows_7z_callback_boundary([&]() -> HRESULT {
        std::lock_guard<std::mutex> lock(progress_mutex);
        if (complete_value != NULL) {
            progress_completed = *complete_value;
        }
        progress_result = brows_7z_report_progress(progress, progress_state, progress_completed, progress_total);
        return progress_result;
    }, &callback_exception);
}
