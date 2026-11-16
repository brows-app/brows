#ifndef BROWS_7Z_STREAM_H
#define BROWS_7Z_STREAM_H

#include "brows_7z_internal.h"

/*
 * A file that 7-Zip reads from.
 *
 * The stream is owned by the code that created it. 7-Zip only refers to it,
 * so the reference count never deletes the stream. That keeps the file open
 * until the owner closes it, no matter how 7-Zip manages the references of
 * the streams that it receives.
 */
class CInFileStream Z7_final :
    public IInStream,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN
        Z7_COM_QI_ENTRY_UNKNOWN(IInStream)
        Z7_COM_QI_ENTRY(IInStream)
        Z7_COM_QI_ENTRY(ISequentialInStream)
    Z7_COM_QI_END
    Z7_IFACE_COM7_IMP(ISequentialInStream)
    Z7_IFACE_COM7_IMP(IInStream)
public:
    CInFileStream();
    virtual ~CInFileStream();

    STDMETHODIMP_(ULONG) AddRef() throw() override;
    STDMETHODIMP_(ULONG) Release() throw() override;

    /* Opens the file. The file is closed again when it is destroyed. */
    bool open(const std::wstring& path);
    void close();
    bool is_open(void) const;

private:
    HANDLE handle;
};

/*
 * A file that 7-Zip writes to.
 *
 * The stream is owned by the code that created it, just like the stream that
 * 7-Zip reads from.
 */
class COutFileStream Z7_final :
    public IOutStream,
    public CMyUnknownImp
{
    Z7_COM_QI_BEGIN
        Z7_COM_QI_ENTRY_UNKNOWN(IOutStream)
        Z7_COM_QI_ENTRY(IOutStream)
        Z7_COM_QI_ENTRY(ISequentialOutStream)
    Z7_COM_QI_END
    Z7_IFACE_COM7_IMP(ISequentialOutStream)
    Z7_IFACE_COM7_IMP(IOutStream)
public:
    COutFileStream();
    virtual ~COutFileStream();

    STDMETHODIMP_(ULONG) AddRef() throw() override;
    STDMETHODIMP_(ULONG) Release() throw() override;

    /* Creates the file. The file is closed again when it is destroyed. */
    bool open(const std::wstring& path);
    /* Creates a new temporary file beside the given path and returns its name. */
    bool open_temporary_sibling(const std::wstring& path, std::wstring& output_path);
    void close();
    bool is_open(void) const;

private:
    HANDLE handle;
};

#endif
