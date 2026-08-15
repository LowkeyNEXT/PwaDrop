#include <windows.h>
#include <ole2.h>
#include <ShlDisp.h>
#include <shellapi.h>
#include <shlobj.h>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <new>

namespace
{
constexpr wchar_t kEnableEventName[] = L"Local\\PwaDrop.DragBridge.Enabled.v7";
using DoDragDropFunction = HRESULT(WINAPI*)(IDataObject*, IDropSource*, DWORD, DWORD*);

DoDragDropFunction g_originalDoDragDrop = nullptr;
HANDLE g_enabledEvent = nullptr;
HMODULE g_module = nullptr;
DWORD g_patchStatus = 0;
volatile LONG g_traceFlags = 0;

struct BootstrapRequest
{
    ULONGLONG nonce;
    DWORD patchStatus;
    DWORD iatSlotRva;
};

struct AsyncExtractionContext
{
    IStream* dataStream;
    IStream* capabilityStream;
    HANDLE workerReady;
    HANDLE dragCompleted;
    volatile LONG operationOwner;
    HRESULT dragResult;
    DWORD effect;
};

constexpr LONG kOperationOwnerPending = 0;
constexpr LONG kOperationOwnerWorker = 1;
constexpr LONG kOperationOwnerCaller = 2;

void TraceOnce(LONG flag, const char* message) noexcept
{
    const LONG previous = InterlockedOr(&g_traceFlags, flag);
    if ((previous & flag) != 0)
    {
        return;
    }

    wchar_t localAppData[MAX_PATH]{};
    const DWORD length = GetEnvironmentVariableW(
        L"LOCALAPPDATA",
        localAppData,
        MAX_PATH);
    if (length == 0 || length >= MAX_PATH)
    {
        return;
    }

    wchar_t directory[MAX_PATH]{};
    wchar_t path[MAX_PATH]{};
    if (swprintf_s(
            directory,
            L"%s\\PwaDrop",
            localAppData) <= 0 ||
        swprintf_s(
            path,
            L"%s\\hook-runtime-v3.log",
            directory) <= 0)
    {
        return;
    }

    CreateDirectoryW(directory, nullptr);
    const HANDLE file = CreateFileW(
        path,
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return;
    }

    DWORD written = 0;
    WriteFile(
        file,
        message,
        static_cast<DWORD>(std::strlen(message)),
        &written,
        nullptr);
    constexpr char newline[] = "\r\n";
    WriteFile(
        file,
        newline,
        static_cast<DWORD>(sizeof(newline) - 1),
        &written,
        nullptr);
    CloseHandle(file);
}

bool IsBridgeEnabled() noexcept
{
    return g_enabledEvent != nullptr &&
        WaitForSingleObject(g_enabledEvent, 0) == WAIT_OBJECT_0;
}

bool TryPrimeAsyncOperation(
    IDataObject* dataObject,
    IDataObjectAsyncCapability** capability) noexcept
{
    *capability = nullptr;
    if (!IsBridgeEnabled() || dataObject == nullptr)
    {
        TraceOnce(0x0002, "drag bridge_disabled_or_no_data");
        return false;
    }

    TraceOnce(0x0004, "drag bridge_enabled");

    IDataObjectAsyncCapability* candidate = nullptr;
    __try
    {
        FORMATETC fileDropFormat{
            CF_HDROP,
            nullptr,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };
        if (FAILED(dataObject->QueryGetData(&fileDropFormat)))
        {
            TraceOnce(0x200000, "drag cf_hdrop_unavailable");
            return false;
        }

        if (FAILED(dataObject->QueryInterface(
                IID_IDataObjectAsyncCapability,
                reinterpret_cast<void**>(&candidate))) ||
            candidate == nullptr)
        {
            TraceOnce(0x0008, "drag async_capability_unavailable");
            return false;
        }

        BOOL asyncMode = FALSE;
        BOOL inOperation = FALSE;
        const HRESULT modeResult = candidate->GetAsyncMode(&asyncMode);
        const HRESULT operationResult = candidate->InOperation(&inOperation);
        const HRESULT startResult =
            SUCCEEDED(modeResult) &&
                asyncMode &&
                SUCCEEDED(operationResult) &&
                !inOperation
            ? candidate->StartOperation(nullptr)
            : E_FAIL;
        const bool canOwn =
            SUCCEEDED(modeResult) &&
            asyncMode &&
            SUCCEEDED(operationResult) &&
            !inOperation &&
            SUCCEEDED(startResult);
        if (!canOwn)
        {
            if (FAILED(modeResult))
            {
                TraceOnce(0x0010, "drag get_async_mode_failed");
            }
            else if (!asyncMode)
            {
                TraceOnce(0x0020, "drag async_mode_disabled");
            }
            else if (FAILED(operationResult))
            {
                TraceOnce(0x0040, "drag in_operation_failed");
            }
            else if (inOperation)
            {
                TraceOnce(0x0080, "drag already_in_operation");
            }
            else
            {
                TraceOnce(0x0100, "drag start_operation_failed");
            }
            IDataObjectAsyncCapability* releasing = candidate;
            candidate = nullptr;
            __try
            {
                releasing->Release();
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                // Do not retry a Release that may already have destroyed it.
            }
            return false;
        }

        TraceOnce(0x0200, "drag start_operation_succeeded");
        *capability = candidate;
        candidate = nullptr;
        return true;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        if (candidate != nullptr)
        {
            __try
            {
                candidate->Release();
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                // A broken source object must not escape into Chromium.
            }
        }
        return false;
    }
}

void CompleteAsyncOperation(
    IDataObjectAsyncCapability* capability,
    HRESULT result,
    DWORD effect) noexcept
{
    if (capability == nullptr)
    {
        return;
    }

    __try
    {
        capability->EndOperation(result, nullptr, effect);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // Never allow a source data-object failure to escape into Chromium.
    }

    __try
    {
        capability->Release();
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // Release failures are contained independently from EndOperation.
    }
}

HRESULT SafeMarshalInterface(
    REFIID iid,
    IUnknown* source,
    IStream** stream) noexcept
{
    *stream = nullptr;
    __try
    {
        return CoMarshalInterThreadInterfaceInStream(iid, source, stream);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        *stream = nullptr;
        return E_UNEXPECTED;
    }
}

HRESULT SafeUnmarshalInterface(
    IStream** stream,
    REFIID iid,
    void** result) noexcept
{
    *result = nullptr;
    IStream* ownedStream = *stream;
    *stream = nullptr;
    if (ownedStream == nullptr)
    {
        return E_POINTER;
    }

    __try
    {
        return CoGetInterfaceAndReleaseStream(
            ownedStream,
            iid,
            result);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // CoGetInterfaceAndReleaseStream owns the stream once called. If it
        // faults, touching that pointer again could double-release it.
        *result = nullptr;
        return E_UNEXPECTED;
    }
}

HRESULT SafeGetData(
    IDataObject* dataObject,
    FORMATETC* format,
    STGMEDIUM* medium) noexcept
{
    __try
    {
        return dataObject->GetData(format, medium);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        ZeroMemory(medium, sizeof(*medium));
        return E_UNEXPECTED;
    }
}

void SafeReleaseStgMedium(STGMEDIUM* medium) noexcept;

bool ValidateFileDropMedium(
    HGLOBAL memory,
    UINT* fileCount,
    ULONGLONG* totalBytes) noexcept
{
    *fileCount = 0;
    *totalBytes = 0;
    if (memory == nullptr)
    {
        return false;
    }

    const UINT count = DragQueryFileW(
        static_cast<HDROP>(memory),
        0xFFFFFFFF,
        nullptr,
        0);
    if (count == 0 || count > 4096)
    {
        return false;
    }

    ULONGLONG bytes = 0;
    for (UINT index = 0; index < count; ++index)
    {
        const UINT length = DragQueryFileW(
            static_cast<HDROP>(memory),
            index,
            nullptr,
            0);
        if (length == 0 || length >= 32767)
        {
            return false;
        }

        auto path = new (std::nothrow) wchar_t[
            static_cast<SIZE_T>(length) + 1];
        if (path == nullptr)
        {
            return false;
        }
        const UINT copied = DragQueryFileW(
            static_cast<HDROP>(memory),
            index,
            path,
            length + 1);
        WIN32_FILE_ATTRIBUTE_DATA attributes{};
        const bool valid =
            copied == length &&
            GetFileAttributesExW(
                path,
                GetFileExInfoStandard,
                &attributes) &&
            (attributes.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
        delete[] path;
        if (!valid)
        {
            return false;
        }

        bytes +=
            (static_cast<ULONGLONG>(attributes.nFileSizeHigh) << 32) |
            attributes.nFileSizeLow;
    }

    *fileCount = count;
    *totalBytes = bytes;
    return true;
}

bool PreMaterializeFileDrop(
    IDataObject* dataObject,
    IDropSource* dropSource,
    STGMEDIUM* materializedMedium) noexcept
{
    ZeroMemory(materializedMedium, sizeof(*materializedMedium));
    if (dataObject == nullptr || dropSource == nullptr)
    {
        return false;
    }

    HRESULT releaseTransition = E_UNEXPECTED;
    __try
    {
        // Chromium's DragSourceWin clears DataObjectImpl::in_drag_loop_ in
        // OnDragSourceDrop before returning DRAGDROP_S_DROP. Running that
        // transition once here allows Chromium to materialize its own exact
        // temp path before any destination inspects CF_HDROP.
        releaseTransition = dropSource->QueryContinueDrag(FALSE, 0);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        releaseTransition = E_UNEXPECTED;
    }
    if (releaseTransition != DRAGDROP_S_DROP)
    {
        TraceOnce(0x80000, "drag source_release_transition_failed");
        return false;
    }

    FORMATETC format{
        CF_HDROP,
        nullptr,
        DVASPECT_CONTENT,
        -1,
        TYMED_HGLOBAL
    };
    STGMEDIUM medium{};
    const HRESULT result = SafeGetData(dataObject, &format, &medium);
    UINT fileCount = 0;
    ULONGLONG totalBytes = 0;
    const bool succeeded = SUCCEEDED(result) &&
        medium.tymed == TYMED_HGLOBAL &&
        ValidateFileDropMedium(
            medium.hGlobal,
            &fileCount,
            &totalBytes);
    char trace[160]{};
    sprintf_s(
        trace,
        "drag pre_materialize_%s hresult=0x%08lX files=%u bytes=%llu",
        succeeded ? "succeeded" : "failed",
        static_cast<unsigned long>(result),
        fileCount,
        static_cast<unsigned long long>(totalBytes));
    TraceOnce(0x100000, trace);
    if (succeeded)
    {
        *materializedMedium = medium;
        ZeroMemory(&medium, sizeof(medium));
    }
    if (medium.tymed != TYMED_NULL)
    {
        SafeReleaseStgMedium(&medium);
    }
    return succeeded;
}

void SafeEndOperation(
    IDataObjectAsyncCapability* capability,
    HRESULT result,
    DWORD effect) noexcept
{
    __try
    {
        capability->EndOperation(result, nullptr, effect);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // A broken source object must never take Chromium down with it.
    }
}

void SafeReleaseUnknown(IUnknown* object) noexcept
{
    if (object == nullptr)
    {
        return;
    }

    __try
    {
        object->Release();
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // Release is source-controlled for marshaled interface proxies.
    }
}

bool SafeAddRefUnknown(IUnknown* object) noexcept
{
    if (object == nullptr)
    {
        return false;
    }

    __try
    {
        object->AddRef();
        return true;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return false;
    }
}

void SafeReleaseStgMedium(STGMEDIUM* medium) noexcept
{
    __try
    {
        ReleaseStgMedium(medium);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // pUnkForRelease can be supplied by the drag source.
    }
}

HGLOBAL DuplicateFileDropMemory(HGLOBAL source) noexcept
{
    __try
    {
        return static_cast<HGLOBAL>(OleDuplicateData(source, CF_HDROP, 0));
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return nullptr;
    }
}

CLIPFORMAT RendererTaintFormat() noexcept
{
    static const CLIPFORMAT format = static_cast<CLIPFORMAT>(
        RegisterClipboardFormatW(L"chromium/x-renderer-taint"));
    return format;
}

bool IsRendererTaintFormat(const FORMATETC* format) noexcept
{
    const CLIPFORMAT rendererTaint = RendererTaintFormat();
    return format != nullptr &&
        rendererTaint != 0 &&
        format->cfFormat == rendererTaint;
}

class RendererTaintFilteringEnumerator final : public IEnumFORMATETC
{
public:
    static RendererTaintFilteringEnumerator* Create(
        IEnumFORMATETC* inner) noexcept
    {
        if (inner == nullptr ||
            !SafeAddRefUnknown(static_cast<IUnknown*>(inner)))
        {
            return nullptr;
        }
        auto wrapper = new (std::nothrow)
            RendererTaintFilteringEnumerator(inner);
        if (wrapper == nullptr)
        {
            SafeReleaseUnknown(static_cast<IUnknown*>(inner));
        }
        return wrapper;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(
        REFIID iid,
        void** value) override
    {
        if (value == nullptr)
        {
            return E_POINTER;
        }
        *value = nullptr;
        if (iid != IID_IUnknown && iid != IID_IEnumFORMATETC)
        {
            return E_NOINTERFACE;
        }
        *value = static_cast<IEnumFORMATETC*>(this);
        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&references_));
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        const LONG references = InterlockedDecrement(&references_);
        if (references == 0)
        {
            delete this;
            return 0;
        }
        return static_cast<ULONG>(references);
    }

    HRESULT STDMETHODCALLTYPE Next(
        ULONG count,
        FORMATETC* formats,
        ULONG* fetched) override
    {
        if (formats == nullptr || (count != 1 && fetched == nullptr))
        {
            return E_POINTER;
        }
        if (fetched != nullptr)
        {
            *fetched = 0;
        }

        ULONG visibleCount = 0;
        while (visibleCount < count)
        {
            FORMATETC candidate{};
            ULONG innerFetched = 0;
            HRESULT result = E_UNEXPECTED;
            __try
            {
                result = inner_->Next(1, &candidate, &innerFetched);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                result = E_UNEXPECTED;
            }
            if (FAILED(result))
            {
                return result;
            }
            if (result != S_OK || innerFetched != 1)
            {
                if (fetched != nullptr)
                {
                    *fetched = visibleCount;
                }
                return S_FALSE;
            }
            if (IsRendererTaintFormat(&candidate))
            {
                if (candidate.ptd != nullptr)
                {
                    CoTaskMemFree(candidate.ptd);
                }
                TraceOnce(0x4000000, "drag wrapper_renderer_taint_hidden");
                continue;
            }
            formats[visibleCount++] = candidate;
        }

        if (fetched != nullptr)
        {
            *fetched = visibleCount;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Skip(ULONG count) override
    {
        for (ULONG index = 0; index < count; ++index)
        {
            FORMATETC discarded{};
            ULONG fetched = 0;
            const HRESULT result = Next(1, &discarded, &fetched);
            if (discarded.ptd != nullptr)
            {
                CoTaskMemFree(discarded.ptd);
            }
            if (result != S_OK || fetched != 1)
            {
                return FAILED(result) ? result : S_FALSE;
            }
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Reset() override
    {
        __try { return inner_->Reset(); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE Clone(IEnumFORMATETC** enumerator) override
    {
        if (enumerator == nullptr)
        {
            return E_POINTER;
        }
        *enumerator = nullptr;
        IEnumFORMATETC* innerClone = nullptr;
        HRESULT result = E_UNEXPECTED;
        __try { result = inner_->Clone(&innerClone); }
        __except (EXCEPTION_EXECUTE_HANDLER) { result = E_UNEXPECTED; }
        if (FAILED(result) || innerClone == nullptr)
        {
            return FAILED(result) ? result : E_UNEXPECTED;
        }
        auto clone = Create(innerClone);
        SafeReleaseUnknown(static_cast<IUnknown*>(innerClone));
        if (clone == nullptr)
        {
            return E_OUTOFMEMORY;
        }
        *enumerator = static_cast<IEnumFORMATETC*>(clone);
        return S_OK;
    }

private:
    explicit RendererTaintFilteringEnumerator(
        IEnumFORMATETC* inner) noexcept
        : inner_(inner) {}

    ~RendererTaintFilteringEnumerator()
    {
        SafeReleaseUnknown(static_cast<IUnknown*>(inner_));
    }

    volatile LONG references_ = 1;
    IEnumFORMATETC* inner_ = nullptr;
};

class MaterializedFileDataObject final : public IDataObject
{
public:
    static MaterializedFileDataObject* Create(
        IDataObject* original,
        HGLOBAL fileDropMemory) noexcept
    {
        if (original == nullptr || fileDropMemory == nullptr)
        {
            return nullptr;
        }

        HGLOBAL masterCopy = DuplicateFileDropMemory(fileDropMemory);
        if (masterCopy == nullptr || !SafeAddRefUnknown(original))
        {
            if (masterCopy != nullptr)
            {
                GlobalFree(masterCopy);
            }
            return nullptr;
        }

        auto wrapper = new (std::nothrow) MaterializedFileDataObject(
            original,
            masterCopy);
        if (wrapper == nullptr)
        {
            GlobalFree(masterCopy);
            SafeReleaseUnknown(original);
        }
        return wrapper;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(
        REFIID iid,
        void** value) override
    {
        if (value == nullptr)
        {
            return E_POINTER;
        }
        *value = nullptr;
        if (iid != IID_IUnknown && iid != IID_IDataObject)
        {
            // Keep a standalone COM identity. In particular, never expose the
            // source's IMarshal or async interface, which could bypass this
            // fully materialized synchronous IDataObject.
            return E_NOINTERFACE;
        }
        *value = static_cast<IDataObject*>(this);
        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&references_));
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        const LONG references = InterlockedDecrement(&references_);
        if (references == 0)
        {
            delete this;
            return 0;
        }
        return static_cast<ULONG>(references);
    }

    HRESULT STDMETHODCALLTYPE GetData(
        FORMATETC* format,
        STGMEDIUM* medium) override
    {
        if (format == nullptr || medium == nullptr)
        {
            return E_POINTER;
        }
        ZeroMemory(medium, sizeof(*medium));
        if (IsRendererTaintFormat(format))
        {
            TraceOnce(0x4000000, "drag wrapper_renderer_taint_hidden");
            return DV_E_FORMATETC;
        }
        if (format->cfFormat == CF_HDROP)
        {
            TraceOnce(0x800000, "drag wrapper_get_cf_hdrop");
            const HRESULT compatibility = CheckFileDropFormat(format);
            if (FAILED(compatibility))
            {
                return compatibility;
            }
            HGLOBAL duplicate = DuplicateFileDropMemory(masterFileDrop_);
            if (duplicate == nullptr)
            {
                return E_OUTOFMEMORY;
            }
            medium->tymed = TYMED_HGLOBAL;
            medium->hGlobal = duplicate;
            medium->pUnkForRelease = nullptr;
            return S_OK;
        }

        __try
        {
            return original_->GetData(format, medium);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            ZeroMemory(medium, sizeof(*medium));
            return E_UNEXPECTED;
        }
    }

    HRESULT STDMETHODCALLTYPE GetDataHere(
        FORMATETC* format,
        STGMEDIUM* medium) override
    {
        if (format == nullptr || medium == nullptr)
        {
            return E_POINTER;
        }
        if (IsRendererTaintFormat(format))
        {
            TraceOnce(0x4000000, "drag wrapper_renderer_taint_hidden");
            return DV_E_FORMATETC;
        }
        if (format->cfFormat == CF_HDROP)
        {
            return DV_E_TYMED;
        }
        __try { return original_->GetDataHere(format, medium); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE QueryGetData(FORMATETC* format) override
    {
        if (format == nullptr)
        {
            return E_POINTER;
        }
        if (IsRendererTaintFormat(format))
        {
            TraceOnce(0x4000000, "drag wrapper_renderer_taint_hidden");
            return DV_E_FORMATETC;
        }
        if (format->cfFormat == CF_HDROP)
        {
            const HRESULT result = CheckFileDropFormat(format);
            char trace[160]{};
            sprintf_s(
                trace,
                "drag wrapper_query_cf_hdrop hresult=0x%08lX aspect=%lu lindex=%ld tymed=0x%lX",
                static_cast<unsigned long>(result),
                static_cast<unsigned long>(format->dwAspect),
                static_cast<long>(format->lindex),
                static_cast<unsigned long>(format->tymed));
            TraceOnce(0x400000, trace);
            return result;
        }
        __try { return original_->QueryGetData(format); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE GetCanonicalFormatEtc(
        FORMATETC* format,
        FORMATETC* result) override
    {
        if (IsRendererTaintFormat(format))
        {
            return DV_E_FORMATETC;
        }
        __try { return original_->GetCanonicalFormatEtc(format, result); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE SetData(
        FORMATETC* format,
        STGMEDIUM* medium,
        BOOL release) override
    {
        __try { return original_->SetData(format, medium, release); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE EnumFormatEtc(
        DWORD direction,
        IEnumFORMATETC** enumerator) override
    {
        if (enumerator == nullptr)
        {
            return E_POINTER;
        }
        *enumerator = nullptr;
        IEnumFORMATETC* inner = nullptr;
        HRESULT result = E_UNEXPECTED;
        __try { result = original_->EnumFormatEtc(direction, &inner); }
        __except (EXCEPTION_EXECUTE_HANDLER) { result = E_UNEXPECTED; }
        if (FAILED(result) || inner == nullptr)
        {
            return FAILED(result) ? result : E_UNEXPECTED;
        }
        auto filtered = RendererTaintFilteringEnumerator::Create(inner);
        SafeReleaseUnknown(static_cast<IUnknown*>(inner));
        if (filtered == nullptr)
        {
            return E_OUTOFMEMORY;
        }
        *enumerator = static_cast<IEnumFORMATETC*>(filtered);
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE DAdvise(
        FORMATETC* format,
        DWORD flags,
        IAdviseSink* sink,
        DWORD* connection) override
    {
        __try { return original_->DAdvise(format, flags, sink, connection); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE DUnadvise(DWORD connection) override
    {
        __try { return original_->DUnadvise(connection); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE EnumDAdvise(
        IEnumSTATDATA** enumerator) override
    {
        __try { return original_->EnumDAdvise(enumerator); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

private:
    MaterializedFileDataObject(
        IDataObject* original,
        HGLOBAL masterFileDrop) noexcept
        : original_(original), masterFileDrop_(masterFileDrop) {}

    ~MaterializedFileDataObject()
    {
        GlobalFree(masterFileDrop_);
        SafeReleaseUnknown(original_);
    }

    static HRESULT CheckFileDropFormat(FORMATETC* format) noexcept
    {
        if (format->dwAspect != DVASPECT_CONTENT)
        {
            return DV_E_DVASPECT;
        }
        if (format->lindex != -1)
        {
            return DV_E_LINDEX;
        }
        if ((format->tymed & TYMED_HGLOBAL) == 0)
        {
            return DV_E_TYMED;
        }
        return S_OK;
    }

    volatile LONG references_ = 1;
    IDataObject* original_ = nullptr;
    HGLOBAL masterFileDrop_ = nullptr;
};

void ReleaseMarshaledStream(IStream* stream) noexcept
{
    if (stream == nullptr)
    {
        return;
    }

    bool rewound = false;
    __try
    {
        LARGE_INTEGER beginning{};
        rewound = SUCCEEDED(stream->Seek(
            beginning,
            STREAM_SEEK_SET,
            nullptr));
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        rewound = false;
    }

    if (rewound)
    {
        __try
        {
            CoReleaseMarshalData(stream);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            // Keep cleanup best-effort and independently contained.
        }
    }

    SafeReleaseUnknown(stream);
}

bool CreatePlaceholderFile(
    wchar_t* directory,
    SIZE_T directoryLength,
    wchar_t* path,
    SIZE_T pathLength) noexcept
{
    wchar_t temporaryRoot[MAX_PATH]{};
    const DWORD rootLength = GetTempPathW(MAX_PATH, temporaryRoot);
    if (rootLength == 0 || rootLength >= MAX_PATH)
    {
        return false;
    }

    wchar_t bridgeRoot[MAX_PATH]{};
    if (swprintf_s(
            bridgeRoot,
            L"%sPwaDrop",
            temporaryRoot) <= 0)
    {
        return false;
    }
    CreateDirectoryW(bridgeRoot, nullptr);

    static volatile LONG sequence = 0;
    const LONG value = InterlockedIncrement(&sequence);
    if (swprintf_s(
            directory,
            directoryLength,
            L"%s\\Drag-%lu-%08lX",
            bridgeRoot,
            GetCurrentProcessId(),
            static_cast<unsigned long>(value)) <= 0 ||
        !CreateDirectoryW(directory, nullptr))
    {
        return false;
    }

    if (swprintf_s(
            path,
            pathLength,
            L"%s\\Outlook message.eml",
            directory) <= 0)
    {
        RemoveDirectoryW(directory);
        return false;
    }

    const HANDLE file = CreateFileW(
        path,
        GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        CREATE_NEW,
        FILE_ATTRIBUTE_TEMPORARY,
        nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        RemoveDirectoryW(directory);
        return false;
    }
    CloseHandle(file);

    // A drop target can retain the path after DoDragDrop returns. Match
    // Chromium's conservative lifetime and clean up after reboot.
    MoveFileExW(path, nullptr, MOVEFILE_DELAY_UNTIL_REBOOT);
    MoveFileExW(directory, nullptr, MOVEFILE_DELAY_UNTIL_REBOOT);
    return true;
}

HRESULT CreateFileDropMedium(
    const wchar_t* path,
    STGMEDIUM* medium) noexcept
{
    ZeroMemory(medium, sizeof(*medium));
    const SIZE_T pathCharacters = wcslen(path) + 2;
    const SIZE_T bytes = sizeof(DROPFILES) +
        pathCharacters * sizeof(wchar_t);
    const HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, bytes);
    if (memory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    auto drop = static_cast<DROPFILES*>(GlobalLock(memory));
    if (drop == nullptr)
    {
        GlobalFree(memory);
        return E_OUTOFMEMORY;
    }
    drop->pFiles = sizeof(DROPFILES);
    drop->fWide = TRUE;
    auto destination = reinterpret_cast<wchar_t*>(
        reinterpret_cast<BYTE*>(drop) + sizeof(DROPFILES));
    wcscpy_s(destination, pathCharacters, path);
    GlobalUnlock(memory);

    medium->tymed = TYMED_HGLOBAL;
    medium->hGlobal = memory;
    medium->pUnkForRelease = nullptr;
    return S_OK;
}

class BridgedDataObject final :
    public IDataObject,
    public IDataObjectAsyncCapability
{
public:
    static BridgedDataObject* Create(
        IDataObject* original,
        IDataObjectAsyncCapability* capability) noexcept
    {
        if (original == nullptr || capability == nullptr)
        {
            return nullptr;
        }

        wchar_t directory[MAX_PATH]{};
        wchar_t path[MAX_PATH]{};
        if (!CreatePlaceholderFile(
                directory,
                MAX_PATH,
                path,
                MAX_PATH) ||
            !SafeAddRefUnknown(original))
        {
            return nullptr;
        }
        if (!SafeAddRefUnknown(capability))
        {
            SafeReleaseUnknown(original);
            return nullptr;
        }

        auto bridge = new (std::nothrow) BridgedDataObject(
            original,
            capability,
            directory,
            path);
        if (bridge == nullptr)
        {
            SafeReleaseUnknown(capability);
            SafeReleaseUnknown(original);
        }
        return bridge;
    }

    HRESULT Materialize() noexcept
    {
        const LONG previous = InterlockedCompareExchange(
            &materializeState_,
            1,
            0);
        if (previous != 0)
        {
            return materializeResult_;
        }

        FORMATETC format{
            CF_HDROP,
            nullptr,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };
        STGMEDIUM medium{};
        HRESULT result = SafeGetData(original_, &format, &medium);
        if (SUCCEEDED(result) && medium.tymed == TYMED_HGLOBAL)
        {
            wchar_t sourcePath[32768]{};
            const UINT length = DragQueryFileW(
                static_cast<HDROP>(medium.hGlobal),
                0,
                sourcePath,
                static_cast<UINT>(sizeof(sourcePath) / sizeof(wchar_t)));
            if (length == 0 ||
                !CopyFileW(sourcePath, placeholderPath_, FALSE))
            {
                result = HRESULT_FROM_WIN32(GetLastError());
            }
        }
        else if (SUCCEEDED(result))
        {
            result = DV_E_TYMED;
        }

        if (medium.tymed != TYMED_NULL)
        {
            SafeReleaseStgMedium(&medium);
        }
        materializeResult_ = result;
        InterlockedExchange(&materializeState_, 2);

        char trace[96]{};
        sprintf_s(
            trace,
            "drag placeholder_materialize_%s hresult=0x%08lX",
            SUCCEEDED(result) ? "succeeded" : "failed",
            static_cast<unsigned long>(result));
        TraceOnce(0x40000, trace);
        return result;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** value) override
    {
        if (value == nullptr)
        {
            return E_POINTER;
        }
        if (iid == IID_IUnknown || iid == IID_IDataObject)
        {
            *value = static_cast<IDataObject*>(this);
        }
        else if (iid == IID_IDataObjectAsyncCapability)
        {
            *value = static_cast<IDataObjectAsyncCapability*>(this);
        }
        else
        {
            __try
            {
                return original_->QueryInterface(iid, value);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                *value = nullptr;
                return E_UNEXPECTED;
            }
        }
        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&references_));
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        const LONG references = InterlockedDecrement(&references_);
        if (references == 0)
        {
            delete this;
            return 0;
        }
        return static_cast<ULONG>(references);
    }

    HRESULT STDMETHODCALLTYPE GetData(
        FORMATETC* format,
        STGMEDIUM* medium) override
    {
        __try
        {
            if (format != nullptr &&
                medium != nullptr &&
                format->cfFormat == CF_HDROP &&
                format->lindex == -1 &&
                (format->tymed & TYMED_HGLOBAL) != 0)
            {
                return CreateFileDropMedium(placeholderPath_, medium);
            }
            return original_->GetData(format, medium);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            if (medium != nullptr)
            {
                ZeroMemory(medium, sizeof(*medium));
            }
            return E_UNEXPECTED;
        }
    }

    HRESULT STDMETHODCALLTYPE GetDataHere(
        FORMATETC* format,
        STGMEDIUM* medium) override
    {
        __try { return original_->GetDataHere(format, medium); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE QueryGetData(FORMATETC* format) override
    {
        __try
        {
            if (format != nullptr &&
                format->cfFormat == CF_HDROP &&
                format->lindex == -1 &&
                (format->tymed & TYMED_HGLOBAL) != 0)
            {
                return S_OK;
            }
            return original_->QueryGetData(format);
        }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE GetCanonicalFormatEtc(
        FORMATETC* format,
        FORMATETC* result) override
    {
        __try { return original_->GetCanonicalFormatEtc(format, result); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE SetData(
        FORMATETC* format,
        STGMEDIUM* medium,
        BOOL release) override
    {
        __try { return original_->SetData(format, medium, release); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE EnumFormatEtc(
        DWORD direction,
        IEnumFORMATETC** enumerator) override
    {
        __try { return original_->EnumFormatEtc(direction, enumerator); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE DAdvise(
        FORMATETC* format,
        DWORD flags,
        IAdviseSink* sink,
        DWORD* connection) override
    {
        __try { return original_->DAdvise(format, flags, sink, connection); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE DUnadvise(DWORD connection) override
    {
        __try { return original_->DUnadvise(connection); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE EnumDAdvise(IEnumSTATDATA** enumerator) override
    {
        __try { return original_->EnumDAdvise(enumerator); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE SetAsyncMode(BOOL mode) override
    {
        __try { return capability_->SetAsyncMode(mode); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE GetAsyncMode(BOOL* mode) override
    {
        __try { return capability_->GetAsyncMode(mode); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE StartOperation(IBindCtx* context) override
    {
        __try { return capability_->StartOperation(context); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE InOperation(BOOL* operation) override
    {
        __try { return capability_->InOperation(operation); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

    HRESULT STDMETHODCALLTYPE EndOperation(
        HRESULT result,
        IBindCtx* context,
        DWORD effect) override
    {
        __try { return capability_->EndOperation(result, context, effect); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

private:
    BridgedDataObject(
        IDataObject* original,
        IDataObjectAsyncCapability* capability,
        const wchar_t* directory,
        const wchar_t* path) noexcept
        : original_(original), capability_(capability)
    {
        wcscpy_s(placeholderDirectory_, directory);
        wcscpy_s(placeholderPath_, path);
    }

    ~BridgedDataObject()
    {
        SafeReleaseUnknown(capability_);
        SafeReleaseUnknown(original_);
    }

    volatile LONG references_ = 1;
    IDataObject* original_ = nullptr;
    IDataObjectAsyncCapability* capability_ = nullptr;
    wchar_t placeholderDirectory_[MAX_PATH]{};
    wchar_t placeholderPath_[MAX_PATH]{};
    volatile LONG materializeState_ = 0;
    HRESULT materializeResult_ = E_PENDING;
};

class BridgedDropSource final : public IDropSource
{
public:
    static BridgedDropSource* Create(
        IDropSource* original,
        BridgedDataObject* data) noexcept
    {
        if (original == nullptr || data == nullptr ||
            !SafeAddRefUnknown(original))
        {
            return nullptr;
        }
        if (!SafeAddRefUnknown(static_cast<IDataObject*>(data)))
        {
            SafeReleaseUnknown(original);
            return nullptr;
        }
        auto bridge = new (std::nothrow) BridgedDropSource(original, data);
        if (bridge == nullptr)
        {
            SafeReleaseUnknown(static_cast<IDataObject*>(data));
            SafeReleaseUnknown(original);
        }
        return bridge;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** value) override
    {
        if (value == nullptr)
        {
            return E_POINTER;
        }
        if (iid == IID_IUnknown || iid == IID_IDropSource)
        {
            *value = static_cast<IDropSource*>(this);
            AddRef();
            return S_OK;
        }
        __try { return original_->QueryInterface(iid, value); }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            *value = nullptr;
            return E_UNEXPECTED;
        }
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&references_));
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        const LONG references = InterlockedDecrement(&references_);
        if (references == 0)
        {
            delete this;
            return 0;
        }
        return static_cast<ULONG>(references);
    }

    HRESULT STDMETHODCALLTYPE QueryContinueDrag(
        BOOL escapePressed,
        DWORD keyState) override
    {
        HRESULT result = E_UNEXPECTED;
        __try
        {
            result = original_->QueryContinueDrag(escapePressed, keyState);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return E_UNEXPECTED;
        }
        if (result == DRAGDROP_S_DROP)
        {
            // Chromium clears its in-drag-loop guard in the original source
            // before returning DRAGDROP_S_DROP. Materialize now, before OLE
            // calls the target's Drop method.
            data_->Materialize();
        }
        return result;
    }

    HRESULT STDMETHODCALLTYPE GiveFeedback(DWORD effect) override
    {
        __try { return original_->GiveFeedback(effect); }
        __except (EXCEPTION_EXECUTE_HANDLER) { return E_UNEXPECTED; }
    }

private:
    BridgedDropSource(
        IDropSource* original,
        BridgedDataObject* data) noexcept
        : original_(original), data_(data) {}

    ~BridgedDropSource()
    {
        SafeReleaseUnknown(static_cast<IDataObject*>(data_));
        SafeReleaseUnknown(original_);
    }

    volatile LONG references_ = 1;
    IDropSource* original_ = nullptr;
    BridgedDataObject* data_ = nullptr;
};

DWORD WINAPI ExtractAsyncFileDrop(void* parameter) noexcept
{
    auto context = static_cast<AsyncExtractionContext*>(parameter);
    HRESULT initialized = E_UNEXPECTED;
    __try
    {
        initialized = CoInitializeEx(
            nullptr,
            COINIT_APARTMENTTHREADED);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        initialized = E_UNEXPECTED;
    }

    IDataObject* dataObject = nullptr;
    IDataObjectAsyncCapability* capability = nullptr;
    HRESULT dataResult = FAILED(initialized) ? initialized : E_FAIL;
    STGMEDIUM medium{};
    bool hasMedium = false;
    if (SUCCEEDED(initialized))
    {
        const HRESULT capabilityUnmarshal = SafeUnmarshalInterface(
            &context->capabilityStream,
            IID_IDataObjectAsyncCapability,
            reinterpret_cast<void**>(&capability));
        const bool workerOwnsOperation =
            SUCCEEDED(capabilityUnmarshal) &&
            capability != nullptr &&
            InterlockedCompareExchange(
                &context->operationOwner,
                kOperationOwnerWorker,
                kOperationOwnerPending) == kOperationOwnerPending;
        SetEvent(context->workerReady);

        HRESULT dataUnmarshal = E_ABORT;
        if (workerOwnsOperation)
        {
            dataUnmarshal = SafeUnmarshalInterface(
                &context->dataStream,
                IID_IDataObject,
                reinterpret_cast<void**>(&dataObject));
        }

        if (workerOwnsOperation &&
            (FAILED(dataUnmarshal) || dataObject == nullptr))
        {
            dataResult = dataUnmarshal;
            TraceOnce(0x1000, "drag data_object_unmarshal_failed");
        }

        if (FAILED(capabilityUnmarshal) || capability == nullptr)
        {
            TraceOnce(0x2000, "drag capability_unmarshal_failed");
        }
    }
    else
    {
        TraceOnce(0x4000, "drag worker_com_initialization_failed");
        SetEvent(context->workerReady);
    }

    WaitForSingleObject(context->dragCompleted, INFINITE);
    if (InterlockedCompareExchange(
            &context->operationOwner,
            kOperationOwnerWorker,
            kOperationOwnerWorker) == kOperationOwnerWorker &&
        dataObject != nullptr)
    {
        FORMATETC format{
            CF_HDROP,
            nullptr,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };
        constexpr int kMaximumAttempts = 201;
        int attempt = 0;
        for (; attempt < kMaximumAttempts; ++attempt)
        {
            ZeroMemory(&medium, sizeof(medium));
            dataResult = SafeGetData(dataObject, &format, &medium);
            if (SUCCEEDED(dataResult))
            {
                hasMedium = true;
                break;
            }

            const bool sourceStillFinishingDrag =
                dataResult == DV_E_FORMATETC ||
                dataResult == DATA_E_FORMATETC ||
                dataResult == E_PENDING ||
                dataResult == RPC_E_CALL_REJECTED ||
                dataResult == RPC_E_SERVERCALL_RETRYLATER;
            if (!sourceStillFinishingDrag ||
                attempt + 1 == kMaximumAttempts)
            {
                break;
            }
            Sleep(25);
        }

        char getDataTrace[128]{};
        sprintf_s(
            getDataTrace,
            "drag background_get_data_%s hresult=0x%08lX attempts=%d",
            hasMedium ? "succeeded" : "failed",
            static_cast<unsigned long>(dataResult),
            attempt + 1);
        TraceOnce(0x0800, getDataTrace);
    }
    if (hasMedium)
    {
        SafeReleaseStgMedium(&medium);
    }
    if (capability != nullptr &&
        InterlockedCompareExchange(
            &context->operationOwner,
            kOperationOwnerWorker,
            kOperationOwnerWorker) == kOperationOwnerWorker)
    {
        const HRESULT operationResult = FAILED(dataResult)
            ? dataResult
            : context->dragResult;
        SafeEndOperation(
            capability,
            operationResult,
            context->effect);
        TraceOnce(0x8000, "drag end_operation_called");
    }
    SafeReleaseUnknown(capability);
    SafeReleaseUnknown(dataObject);

    ReleaseMarshaledStream(context->dataStream);
    ReleaseMarshaledStream(context->capabilityStream);
    CloseHandle(context->workerReady);
    CloseHandle(context->dragCompleted);
    if (SUCCEEDED(initialized))
    {
        __try
        {
            CoUninitialize();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            // Keep all worker cleanup failures contained.
        }
    }
    delete context;
    return 0;
}

bool StartAsyncExtraction(
    IDataObject* dataObject,
    IDataObjectAsyncCapability* capability,
    AsyncExtractionContext** result) noexcept
{
    *result = nullptr;
    IStream* dataStream = nullptr;
    IStream* capabilityStream = nullptr;
    HRESULT marshalResult = SafeMarshalInterface(
        IID_IDataObject,
        dataObject,
        &dataStream);
    if (SUCCEEDED(marshalResult))
    {
        marshalResult = SafeMarshalInterface(
            IID_IDataObjectAsyncCapability,
            capability,
            &capabilityStream);
    }
    if (FAILED(marshalResult))
    {
        TraceOnce(0x10000, "drag interface_marshaling_failed");
        ReleaseMarshaledStream(dataStream);
        ReleaseMarshaledStream(capabilityStream);
        return false;
    }

    const HANDLE workerReady = CreateEventW(
        nullptr,
        TRUE,
        FALSE,
        nullptr);
    const HANDLE dragCompleted = CreateEventW(
        nullptr,
        TRUE,
        FALSE,
        nullptr);
    if (workerReady == nullptr || dragCompleted == nullptr)
    {
        if (workerReady != nullptr)
        {
            CloseHandle(workerReady);
        }
        if (dragCompleted != nullptr)
        {
            CloseHandle(dragCompleted);
        }
        ReleaseMarshaledStream(dataStream);
        ReleaseMarshaledStream(capabilityStream);
        return false;
    }

    auto context = new (std::nothrow) AsyncExtractionContext{
        dataStream,
        capabilityStream,
        workerReady,
        dragCompleted,
        kOperationOwnerPending,
        E_FAIL,
        DROPEFFECT_NONE
    };
    if (context == nullptr)
    {
        CloseHandle(workerReady);
        CloseHandle(dragCompleted);
        ReleaseMarshaledStream(dataStream);
        ReleaseMarshaledStream(capabilityStream);
        return false;
    }

    const HANDLE thread = CreateThread(
        nullptr,
        0,
        ExtractAsyncFileDrop,
        context,
        0,
        nullptr);
    if (thread == nullptr)
    {
        CloseHandle(workerReady);
        CloseHandle(dragCompleted);
        ReleaseMarshaledStream(dataStream);
        ReleaseMarshaledStream(capabilityStream);
        delete context;
        return false;
    }

    CloseHandle(thread);
    TraceOnce(0x20000, "drag background_extraction_started");
    *result = context;

    DWORD readyIndex = 0;
    HANDLE readyHandle = workerReady;
    CoWaitForMultipleHandles(
        COWAIT_DISPATCH_CALLS | COWAIT_DISPATCH_WINDOW_MESSAGES,
        2000,
        1,
        &readyHandle,
        &readyIndex);
    LONG owner = InterlockedCompareExchange(
        &context->operationOwner,
        kOperationOwnerCaller,
        kOperationOwnerPending);
    if (owner == kOperationOwnerPending)
    {
        owner = kOperationOwnerCaller;
    }
    return owner == kOperationOwnerWorker;
}

HRESULT WINAPI HookedDoDragDrop(
    IDataObject* dataObject,
    IDropSource* dropSource,
    DWORD allowedEffects,
    DWORD* effect) noexcept
{
    TraceOnce(0x0001, "drag do_drag_drop_entered");
    const auto original = g_originalDoDragDrop;
    if (original == nullptr)
    {
        return E_UNEXPECTED;
    }

    IDataObjectAsyncCapability* capability = nullptr;
    MaterializedFileDataObject* materializedData = nullptr;
    const bool ownsOperation = TryPrimeAsyncOperation(dataObject, &capability);
    if (ownsOperation)
    {
        STGMEDIUM materializedMedium{};
        const bool materialized = PreMaterializeFileDrop(
            dataObject,
            dropSource,
            &materializedMedium);
        if (materialized)
        {
            materializedData = MaterializedFileDataObject::Create(
                dataObject,
                materializedMedium.hGlobal);
            TraceOnce(
                0x2000000,
                materializedData == nullptr
                    ? "drag wrapper_creation_failed"
                    : "drag wrapper_created");
        }
        if (materializedMedium.tymed != TYMED_NULL)
        {
            SafeReleaseStgMedium(&materializedMedium);
        }
        CompleteAsyncOperation(
            capability,
            materialized ? S_OK : DV_E_FORMATETC,
            materialized ? DROPEFFECT_COPY : DROPEFFECT_NONE);
        capability = nullptr;
    }

    if (materializedData == nullptr)
    {
        return original(dataObject, dropSource, allowedEffects, effect);
    }

    const HRESULT result = original(
        static_cast<IDataObject*>(materializedData),
        dropSource,
        allowedEffects,
        effect);
    materializedData->Release();
    return result;
}

bool RvaIsValid(
    DWORD rva,
    SIZE_T length,
    DWORD imageSize) noexcept
{
    return rva < imageSize && length <= imageSize - rva;
}

bool MemoryRangeIsReadable(const void* address, SIZE_T length) noexcept
{
    MEMORY_BASIC_INFORMATION information{};
    if (VirtualQuery(
            address,
            &information,
            sizeof(information)) != sizeof(information) ||
        information.State != MEM_COMMIT ||
        (information.Protect & (PAGE_NOACCESS | PAGE_GUARD)) != 0)
    {
        return false;
    }

    const auto start = reinterpret_cast<std::uintptr_t>(address);
    const auto regionStart =
        reinterpret_cast<std::uintptr_t>(information.BaseAddress);
    const auto regionEnd = regionStart + information.RegionSize;
    return start >= regionStart &&
        length <= regionEnd - start;
}

bool RvaStringEquals(
    const std::uint8_t* base,
    DWORD rva,
    const char* expected,
    bool ignoreCase,
    DWORD imageSize) noexcept
{
    const SIZE_T expectedLength = std::strlen(expected);
    if (!RvaIsValid(rva, expectedLength + 1, imageSize))
    {
        return false;
    }

    const auto actual = reinterpret_cast<const char*>(base + rva);
    const int comparison = ignoreCase
        ? _strnicmp(actual, expected, expectedLength)
        : std::strncmp(actual, expected, expectedLength);
    return comparison == 0 && actual[expectedLength] == '\0';
}

bool PatchImportSlot(
    void** slot,
    void* resolvedOriginal = nullptr) noexcept
{
    DWORD previousProtection = 0;
    if (!VirtualProtect(
            slot,
            sizeof(void*),
            PAGE_READWRITE,
            &previousProtection))
    {
        g_patchStatus = 8;
        return false;
    }

    g_originalDoDragDrop = reinterpret_cast<DoDragDropFunction>(
        resolvedOriginal == nullptr ? *slot : resolvedOriginal);
    InterlockedExchangePointer(
        slot,
        reinterpret_cast<void*>(&HookedDoDragDrop));
    DWORD ignoredProtection = 0;
    if (!VirtualProtect(
            slot,
            sizeof(void*),
            previousProtection,
            &ignoredProtection))
    {
        InterlockedExchangePointer(
            slot,
            reinterpret_cast<void*>(g_originalDoDragDrop));
        VirtualProtect(
            slot,
            sizeof(void*),
            previousProtection,
            &ignoredProtection);
        g_originalDoDragDrop = nullptr;
        g_patchStatus = 9;
        return false;
    }
    FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
    if (g_originalDoDragDrop == nullptr)
    {
        g_patchStatus = 10;
        return false;
    }

    g_patchStatus = 0;
    return true;
}

bool PatchResolvedDoDragDropSlot(
    std::uint8_t* base,
    IMAGE_IMPORT_DESCRIPTOR* descriptors,
    SIZE_T descriptorLimit,
    DWORD imageSize) noexcept
{
    const HMODULE ole32 = GetModuleHandleW(L"ole32.dll");
    const auto doDragDrop = ole32 == nullptr
        ? nullptr
        : GetProcAddress(ole32, "DoDragDrop");
    if (doDragDrop == nullptr)
    {
        g_patchStatus = 15;
        return false;
    }

    auto descriptor = descriptors;
    for (SIZE_T descriptorIndex = 0;
         descriptorIndex < descriptorLimit && descriptor->Name != 0;
         ++descriptorIndex, ++descriptor)
    {
        if (descriptor->FirstThunk == 0 ||
            !RvaIsValid(
                descriptor->FirstThunk,
                sizeof(IMAGE_THUNK_DATA64),
                imageSize))
        {
            continue;
        }

        auto functions = reinterpret_cast<IMAGE_THUNK_DATA64*>(
            base + descriptor->FirstThunk);
        const SIZE_T functionLimit =
            (imageSize - descriptor->FirstThunk) /
            sizeof(IMAGE_THUNK_DATA64);
        for (SIZE_T thunkIndex = 0;
             thunkIndex < functionLimit && functions->u1.Function != 0;
             ++thunkIndex, ++functions)
        {
            if (reinterpret_cast<void*>(functions->u1.Function) ==
                reinterpret_cast<void*>(doDragDrop))
            {
                return PatchImportSlot(
                    reinterpret_cast<void**>(&functions->u1.Function));
            }
        }
    }

    g_patchStatus = 16;
    return false;
}

bool PatchKnownImportSlot(
    HMODULE module,
    DWORD slotRva,
    bool preserveExistingSlot) noexcept
{
    if (module == nullptr)
    {
        g_patchStatus = 17;
        return false;
    }

    const auto base = reinterpret_cast<std::uint8_t*>(module);
    const auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE ||
        dos->e_lfanew <= 0 ||
        dos->e_lfanew > 1024 * 1024)
    {
        g_patchStatus = 18;
        return false;
    }

    const auto nt = reinterpret_cast<IMAGE_NT_HEADERS64*>(
        base + static_cast<SIZE_T>(dos->e_lfanew));
    if (!MemoryRangeIsReadable(nt, sizeof(*nt)) ||
        nt->Signature != IMAGE_NT_SIGNATURE ||
        nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC ||
        !RvaIsValid(
            slotRva,
            sizeof(void*),
            nt->OptionalHeader.SizeOfImage))
    {
        g_patchStatus = 18;
        return false;
    }

    const HMODULE ole32 = GetModuleHandleW(L"ole32.dll");
    void* resolvedDoDragDrop = ole32 == nullptr
        ? nullptr
        : reinterpret_cast<void*>(
            GetProcAddress(ole32, "DoDragDrop"));
    if (resolvedDoDragDrop == nullptr)
    {
        g_patchStatus = 15;
        return false;
    }

    auto slot = reinterpret_cast<void**>(base + slotRva);
    return preserveExistingSlot
        ? PatchImportSlot(slot)
        : PatchImportSlot(slot, resolvedDoDragDrop);
}

bool PatchModuleImportUnsafe(HMODULE module) noexcept
{
    if (module == nullptr)
    {
        g_patchStatus = 1;
        return false;
    }

    const auto base = reinterpret_cast<std::uint8_t*>(module);
    const auto dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
    {
        g_patchStatus = 2;
        return false;
    }

    if (dosHeader->e_lfanew <= 0 ||
        dosHeader->e_lfanew > 1024 * 1024)
    {
        g_patchStatus = 3;
        return false;
    }

    const auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS64*>(
        base + static_cast<SIZE_T>(dosHeader->e_lfanew));
    if (!MemoryRangeIsReadable(ntHeaders, sizeof(*ntHeaders)) ||
        ntHeaders->Signature != IMAGE_NT_SIGNATURE ||
        ntHeaders->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC)
    {
        g_patchStatus = 4;
        return false;
    }

    const auto importDirectory =
        ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    const DWORD imageSize = ntHeaders->OptionalHeader.SizeOfImage;
    if (imageSize == 0 ||
        importDirectory.VirtualAddress == 0 ||
        !RvaIsValid(
            importDirectory.VirtualAddress,
            sizeof(IMAGE_IMPORT_DESCRIPTOR),
            imageSize))
    {
        g_patchStatus = 5;
        return false;
    }

    bool foundOleImport = false;
    bool foundDoDragDrop = false;
    auto descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
        base + importDirectory.VirtualAddress);
    const SIZE_T descriptorLimit =
        importDirectory.Size / sizeof(IMAGE_IMPORT_DESCRIPTOR);
    for (SIZE_T descriptorIndex = 0;
         descriptorIndex < descriptorLimit && descriptor->Name != 0;
         ++descriptorIndex, ++descriptor)
    {
        if (!RvaStringEquals(
                base,
                descriptor->Name,
                "ole32.dll",
                true,
                imageSize) ||
            descriptor->OriginalFirstThunk == 0)
        {
            continue;
        }
        foundOleImport = true;

        if (!RvaIsValid(
                descriptor->OriginalFirstThunk,
                sizeof(IMAGE_THUNK_DATA64),
                imageSize) ||
            !RvaIsValid(
                descriptor->FirstThunk,
                sizeof(IMAGE_THUNK_DATA64),
                imageSize))
        {
            g_patchStatus = 6;
            return false;
        }

        auto names = reinterpret_cast<IMAGE_THUNK_DATA64*>(
            base + descriptor->OriginalFirstThunk);
        auto functions = reinterpret_cast<IMAGE_THUNK_DATA64*>(
            base + descriptor->FirstThunk);
        const SIZE_T nameLimit =
            (imageSize - descriptor->OriginalFirstThunk) /
            sizeof(IMAGE_THUNK_DATA64);
        const SIZE_T functionLimit =
            (imageSize - descriptor->FirstThunk) /
            sizeof(IMAGE_THUNK_DATA64);
        const SIZE_T thunkLimit =
            nameLimit < functionLimit ? nameLimit : functionLimit;
        for (SIZE_T thunkIndex = 0;
             thunkIndex < thunkLimit && names->u1.AddressOfData != 0;
             ++thunkIndex, ++names, ++functions)
        {
            if (IMAGE_SNAP_BY_ORDINAL64(names->u1.Ordinal))
            {
                continue;
            }

            if (names->u1.AddressOfData > MAXDWORD)
            {
                g_patchStatus = 7;
                return false;
            }

            const DWORD importNameRva =
                static_cast<DWORD>(names->u1.AddressOfData) +
                static_cast<DWORD>(offsetof(IMAGE_IMPORT_BY_NAME, Name));
            if (!RvaStringEquals(
                    base,
                    importNameRva,
                    "DoDragDrop",
                    false,
                    imageSize))
            {
                continue;
            }
            foundDoDragDrop = true;

            return PatchImportSlot(
                reinterpret_cast<void**>(&functions->u1.Function));
        }
    }

    if (PatchResolvedDoDragDropSlot(
            base,
            reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
                base + importDirectory.VirtualAddress),
            descriptorLimit,
            imageSize))
    {
        return true;
    }

    if (g_patchStatus == 16)
    {
        g_patchStatus = !foundOleImport ? 11 : (!foundDoDragDrop ? 12 : 13);
    }
    return false;
}

bool PatchModuleImport(HMODULE module) noexcept
{
    __try
    {
        return PatchModuleImportUnsafe(module);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        g_patchStatus = 14;
        return false;
    }
}

void SignalBootstrapResult(bool succeeded, ULONGLONG nonce) noexcept
{
    wchar_t eventName[128]{};
    const wchar_t* suffix = succeeded ? L"Ready" : L"Failed";
    if (swprintf_s(
            eventName,
            L"Local\\PwaDrop.NativeHook.%s.%lu.%016llX",
            suffix,
            GetCurrentProcessId(),
            nonce) <= 0)
    {
        return;
    }

    const HANDLE event = OpenEventW(EVENT_MODIFY_STATE, FALSE, eventName);
    if (event != nullptr)
    {
        SetEvent(event);
        CloseHandle(event);
    }
}

}

extern "C" __declspec(dllexport)
DWORD WINAPI PwaDropBootstrap(void* parameter) noexcept
{
    auto request = static_cast<BootstrapRequest*>(parameter);
    ULONGLONG nonce = 0;
    __try
    {
        nonce = request == nullptr ? 0 : request->nonce;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        nonce = 0;
    }
    if (nonce == 0)
    {
        return 1;
    }

    g_enabledEvent = OpenEventW(SYNCHRONIZE, FALSE, kEnableEventName);
    TraceOnce(
        0x0400,
        g_enabledEvent == nullptr
            ? "bootstrap enabled_event_unavailable"
            : "bootstrap enabled_event_opened");

    HMODULE targetModule = GetModuleHandleW(L"msedge.dll");
    if (targetModule == nullptr)
    {
        targetModule = GetModuleHandleW(L"chrome.dll");
    }
    wchar_t executablePath[MAX_PATH]{};
    const bool isProbe =
        GetModuleFileNameW(nullptr, executablePath, MAX_PATH) > 0 &&
        _wcsicmp(
            wcsrchr(executablePath, L'\\') == nullptr
                ? executablePath
                : wcsrchr(executablePath, L'\\') + 1,
            L"PwaDrop.NativeProbe.exe") == 0;
    if (targetModule == nullptr)
    {
        targetModule = GetModuleHandleW(nullptr);
    }

    bool patched = PatchKnownImportSlot(
        targetModule,
        request->iatSlotRva,
        isProbe);
    DWORD patchStatus = g_patchStatus;
    if (!patched)
    {
        patched = PatchModuleImport(targetModule);
        patchStatus = g_patchStatus;
    }

    __try
    {
        request->patchStatus = patched ? 0 : patchStatus;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // The host can still use the failure event if its request buffer vanished.
    }
    SignalBootstrapResult(patched, nonce);
    if (!patched && g_module != nullptr)
    {
        if (g_enabledEvent != nullptr)
        {
            CloseHandle(g_enabledEvent);
            g_enabledEvent = nullptr;
        }
        FreeLibraryAndExitThread(g_module, 1);
    }
    return patched ? 0 : 1;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }

    return TRUE;
}
