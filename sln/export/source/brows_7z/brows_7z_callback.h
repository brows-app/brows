#ifndef BROWS_7Z_CALLBACK_H
#define BROWS_7Z_CALLBACK_H

#include "brows_7z_internal.h"
#include "brows_7z_stream.h"

#include <memory>
#include <mutex>

/* An item of an archive that is extracted. */
struct CExtractTarget {
    UInt32      index;
    std::wstring path;
    Int64       modified_time;
    bool        is_directory;
    bool        overwrite;
};

/* A file that has been extracted to a temporary path. */
struct CExtractOutput {
    const CExtractTarget* target;
    std::wstring          temporary_path;
    std::unique_ptr<COutFileStream> stream;
};

/*
 * The callback that writes the items of an archive to the file system.
 *
 * The callback is owned by the code that created it. 7-Zip only refers to it,
 * so the reference count never deletes the callback.
 */
class CExtractCallback Z7_final :
    public IArchiveExtractCallback,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN
        Z7_COM_QI_ENTRY_UNKNOWN(IArchiveExtractCallback)
        Z7_COM_QI_ENTRY(IArchiveExtractCallback)
        Z7_COM_QI_ENTRY(IProgress)
    Z7_COM_QI_END
    Z7_IFACE_COM7_IMP(IProgress)
    Z7_IFACE_COM7_IMP(IArchiveExtractCallback)
public:
    explicit CExtractCallback(const std::vector<CExtractTarget>& targets,
                              brows_7z_ProgressCallback progress,
                              void* progress_state);
    virtual ~CExtractCallback();

    STDMETHODIMP_(ULONG) AddRef() throw() override;
    STDMETHODIMP_(ULONG) Release() throw() override;

    /* Returns the item that belongs to the given index, or NULL. */
    const CExtractTarget* find_target(UInt32 index) const;
    Int32 operation_result(void) const;
    HRESULT commit_outputs(void);
    HRESULT CheckProgress(void);
    HRESULT exception_result(void) const;

private:
    std::vector<CExtractTarget> targets;

    /* The staged files and streams that were handed to 7-Zip. */
    std::vector<CExtractOutput> outputs;
    Int32 last_operation_result;
    brows_7z_ProgressCallback progress;
    void* progress_state;
    UInt64 progress_total;
    UInt64 progress_completed;
    HRESULT progress_result;
    std::mutex progress_mutex;
    std::atomic<HRESULT> callback_exception;
};

/* An item that is written to an archive. */
struct CUpdateItem {
    bool        is_kept;
    UInt32      archive_index;
    std::wstring path;
    std::wstring source_path;
    Int64       modified_time;
    UInt64      size;
    UInt32      attributes;
    bool        is_directory;
};

/*
 * The callback that provides the items that are written to an archive.
 *
 * The callback is owned by the code that created it, just like the callback
 * that extracts the items of an archive.
 */
class CUpdateCallback Z7_final :
    public IArchiveUpdateCallback,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN
        Z7_COM_QI_ENTRY_UNKNOWN(IArchiveUpdateCallback)
        Z7_COM_QI_ENTRY(IArchiveUpdateCallback)
        Z7_COM_QI_ENTRY(IProgress)
    Z7_COM_QI_END
    Z7_IFACE_COM7_IMP(IProgress)
    Z7_IFACE_COM7_IMP(IArchiveUpdateCallback)
public:
    explicit CUpdateCallback(const std::vector<CUpdateItem>& items,
                             brows_7z_ProgressCallback progress,
                             void* progress_state);
    virtual ~CUpdateCallback();

    HRESULT OpenSources();
    void CloseSources();
    HRESULT CheckProgress(void);
    HRESULT exception_result(void) const;

    STDMETHODIMP_(ULONG) AddRef() throw() override;
    STDMETHODIMP_(ULONG) Release() throw() override;

private:
    std::vector<CUpdateItem> items;

    /* The streams that were handed to 7-Zip. They are deleted with the callback. */
    std::vector<std::unique_ptr<CInFileStream>> streams;
    brows_7z_ProgressCallback progress;
    void* progress_state;
    UInt64 progress_total;
    UInt64 progress_completed;
    HRESULT progress_result;
    std::mutex progress_mutex;
    std::atomic<HRESULT> callback_exception;
};

#endif
