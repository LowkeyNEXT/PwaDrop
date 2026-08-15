#include <windows.h>
#include <ole2.h>
#include <ShlDisp.h>
#include <shellapi.h>
#include <shlobj.h>
#include <delayimp.h>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <iterator>

namespace
{
using ProbeDoDragDropFunction =
    HRESULT(WINAPI*)(IDataObject*, IDropSource*, DWORD, DWORD*);
ProbeDoDragDropFunction g_probeOriginalDoDragDrop = nullptr;
std::atomic<bool> g_originalDataAvailable{true};
std::atomic<bool> g_expectFileDrop{false};
std::atomic<bool> g_targetObservedFile{false};
std::atomic<bool> g_targetObservedSynchronousObject{false};
std::atomic<bool> g_targetQuerySucceeded{false};
std::atomic<bool> g_targetGetSucceeded{false};
std::atomic<bool> g_targetCountOne{false};
std::atomic<bool> g_targetPathRetrieved{false};
std::atomic<bool> g_targetPathExists{false};
std::atomic<bool> g_targetTaintQueryRejected{false};
std::atomic<bool> g_targetTaintGetRejected{false};
std::atomic<bool> g_targetEnumerationClean{false};

CLIPFORMAT RendererTaintFormat()
{
    return static_cast<CLIPFORMAT>(
        RegisterClipboardFormatW(L"chromium/x-renderer-taint"));
}

HRESULT WINAPI ForwardedDoDragDrop(
    IDataObject* dataObject,
    IDropSource* dropSource,
    DWORD allowedEffects,
    DWORD* effect)
{
    if (g_expectFileDrop.load())
    {
        // Model a source whose delayed renderer is no longer usable by the
        // destination. A correct bridge must retain the already materialized
        // CF_HDROP independently of the original object.
        g_originalDataAvailable.store(false);

        FORMATETC format{
            CF_HDROP,
            nullptr,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };
        STGMEDIUM medium{};
        const HRESULT queryResult = dataObject->QueryGetData(&format);
        const HRESULT dataResult = dataObject->GetData(&format, &medium);
        g_targetQuerySucceeded.store(SUCCEEDED(queryResult));
        g_targetGetSucceeded.store(SUCCEEDED(dataResult));
        bool hasOneExistingFile = false;
        if (SUCCEEDED(dataResult) && medium.tymed == TYMED_HGLOBAL)
        {
            wchar_t path[MAX_PATH]{};
            const bool countOne = DragQueryFileW(
                    static_cast<HDROP>(medium.hGlobal),
                    0xFFFFFFFF,
                    nullptr,
                    0) == 1;
            const bool pathRetrieved = DragQueryFileW(
                    static_cast<HDROP>(medium.hGlobal),
                    0,
                    path,
                    MAX_PATH) > 0;
            const bool pathExists = pathRetrieved &&
                GetFileAttributesW(path) != INVALID_FILE_ATTRIBUTES;
            g_targetCountOne.store(countOne);
            g_targetPathRetrieved.store(pathRetrieved);
            g_targetPathExists.store(pathExists);
            hasOneExistingFile = countOne && pathRetrieved && pathExists;
            ReleaseStgMedium(&medium);
        }
        g_targetObservedFile.store(
            SUCCEEDED(queryResult) && hasOneExistingFile);

        FORMATETC taintFormat{
            RendererTaintFormat(),
            nullptr,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };
        STGMEDIUM taintMedium{};
        const HRESULT taintQueryResult =
            dataObject->QueryGetData(&taintFormat);
        const HRESULT taintDataResult =
            dataObject->GetData(&taintFormat, &taintMedium);
        g_targetTaintQueryRejected.store(FAILED(taintQueryResult));
        g_targetTaintGetRejected.store(FAILED(taintDataResult));
        if (SUCCEEDED(taintDataResult))
        {
            ReleaseStgMedium(&taintMedium);
        }

        bool enumerationSucceeded = false;
        bool enumeratedFileDrop = false;
        bool enumeratedTaint = false;
        IEnumFORMATETC* formats = nullptr;
        if (SUCCEEDED(dataObject->EnumFormatEtc(DATADIR_GET, &formats)) &&
            formats != nullptr)
        {
            FORMATETC enumerated{};
            ULONG fetched = 0;
            HRESULT nextResult = S_OK;
            while ((nextResult = formats->Next(1, &enumerated, &fetched)) == S_OK)
            {
                enumeratedFileDrop =
                    enumeratedFileDrop || enumerated.cfFormat == CF_HDROP;
                enumeratedTaint =
                    enumeratedTaint ||
                    enumerated.cfFormat == taintFormat.cfFormat;
                if (enumerated.ptd != nullptr)
                {
                    CoTaskMemFree(enumerated.ptd);
                    enumerated.ptd = nullptr;
                }
            }
            enumerationSucceeded = nextResult == S_FALSE;
            formats->Release();
        }
        g_targetEnumerationClean.store(
            enumerationSucceeded && enumeratedFileDrop && !enumeratedTaint);

        IDataObjectAsyncCapability* asyncCapability = nullptr;
        const HRESULT asyncResult = dataObject->QueryInterface(
            IID_IDataObjectAsyncCapability,
            reinterpret_cast<void**>(&asyncCapability));
        g_targetObservedSynchronousObject.store(
            asyncResult == E_NOINTERFACE && asyncCapability == nullptr);
        if (asyncCapability != nullptr)
        {
            asyncCapability->Release();
        }
    }

    return g_probeOriginalDoDragDrop(
        dataObject,
        dropSource,
        allowedEffects,
        effect);
}

class ProbeDataObject final :
    public IDataObject,
    public IDataObjectAsyncCapability
{
public:
    explicit ProbeDataObject(bool supportsFileDrop = true)
        : supportsFileDrop_(supportsFileDrop),
          endedEvent_(CreateEventW(nullptr, TRUE, FALSE, nullptr))
    {
        if (supportsFileDrop_)
        {
            wchar_t temporaryDirectory[MAX_PATH]{};
            if (GetTempPathW(MAX_PATH, temporaryDirectory) > 0)
            {
                GetTempFileNameW(
                    temporaryDirectory,
                    L"PWD",
                    0,
                    filePath_);
            }
        }
        CoCreateFreeThreadedMarshaler(
            static_cast<IUnknown*>(static_cast<IDataObject*>(this)),
            &marshaler_);
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
        else if (iid == IID_IMarshal && marshaler_ != nullptr)
        {
            return marshaler_->QueryInterface(iid, value);
        }
        else
        {
            *value = nullptr;
            return E_NOINTERFACE;
        }

        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG value = --references_;
        if (value == 0)
        {
            delete this;
        }
        return value;
    }

    HRESULT STDMETHODCALLTYPE GetData(
        FORMATETC* format,
        STGMEDIUM* medium) override
    {
        if (format != nullptr &&
            format->cfFormat == RendererTaintFormat() &&
            medium != nullptr &&
            (format->tymed & TYMED_HGLOBAL) != 0)
        {
            const HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE, sizeof(wchar_t));
            if (memory == nullptr)
            {
                return E_OUTOFMEMORY;
            }
            medium->tymed = TYMED_HGLOBAL;
            medium->hGlobal = memory;
            medium->pUnkForRelease = nullptr;
            return S_OK;
        }
        if (format == nullptr ||
            medium == nullptr ||
            format->cfFormat != CF_HDROP ||
            (format->tymed & TYMED_HGLOBAL) == 0 ||
            !inOperation_.load() ||
            inDragLoop_.load() ||
            !g_originalDataAvailable.load() ||
            filePath_[0] == L'\0')
        {
            return DV_E_FORMATETC;
        }

        const SIZE_T pathCharacters = wcslen(filePath_) + 2;
        const SIZE_T bytes = sizeof(DROPFILES) +
            pathCharacters * sizeof(wchar_t);
        const HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE, bytes);
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
        ZeroMemory(drop, bytes);
        drop->pFiles = sizeof(DROPFILES);
        drop->fWide = TRUE;
        auto destination = reinterpret_cast<wchar_t*>(
            reinterpret_cast<BYTE*>(drop) + sizeof(DROPFILES));
        wcscpy_s(destination, pathCharacters, filePath_);
        GlobalUnlock(memory);

        medium->tymed = TYMED_HGLOBAL;
        medium->hGlobal = memory;
        medium->pUnkForRelease = nullptr;
        ++getDataCount_;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetDataHere(FORMATETC*, STGMEDIUM*) override
    {
        return E_NOTIMPL;
    }
    HRESULT STDMETHODCALLTYPE QueryGetData(FORMATETC* format) override
    {
        if (format == nullptr || (format->tymed & TYMED_HGLOBAL) == 0)
        {
            return DV_E_FORMATETC;
        }
        return (supportsFileDrop_ && format->cfFormat == CF_HDROP) ||
                format->cfFormat == RendererTaintFormat()
            ? S_OK : DV_E_FORMATETC;
    }
    HRESULT STDMETHODCALLTYPE GetCanonicalFormatEtc(FORMATETC*, FORMATETC*) override
    {
        return E_NOTIMPL;
    }
    HRESULT STDMETHODCALLTYPE SetData(FORMATETC*, STGMEDIUM*, BOOL) override
    {
        return E_NOTIMPL;
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
        if (direction != DATADIR_GET)
        {
            return E_NOTIMPL;
        }
        FORMATETC formats[] = {
            {CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL},
            {RendererTaintFormat(), nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL}
        };
        return SHCreateStdEnumFmtEtc(
            static_cast<UINT>(std::size(formats)),
            formats,
            enumerator);
    }
    HRESULT STDMETHODCALLTYPE DAdvise(
        FORMATETC*,
        DWORD,
        IAdviseSink*,
        DWORD*) override
    {
        return OLE_E_ADVISENOTSUPPORTED;
    }
    HRESULT STDMETHODCALLTYPE DUnadvise(DWORD) override
    {
        return OLE_E_ADVISENOTSUPPORTED;
    }
    HRESULT STDMETHODCALLTYPE EnumDAdvise(IEnumSTATDATA**) override
    {
        return OLE_E_ADVISENOTSUPPORTED;
    }

    HRESULT STDMETHODCALLTYPE SetAsyncMode(BOOL asyncMode) override
    {
        asyncMode_.store(asyncMode);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetAsyncMode(BOOL* asyncMode) override
    {
        *asyncMode = asyncMode_.load();
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE StartOperation(IBindCtx*) override
    {
        ++startCount_;
        inOperation_.store(TRUE);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE InOperation(BOOL* inOperation) override
    {
        *inOperation = inOperation_.load();
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE EndOperation(
        HRESULT result,
        IBindCtx*,
        DWORD effect) override
    {
        endResult_.store(result);
        endEffect_.store(effect);
        ++endCount_;
        inOperation_.store(FALSE);
        SetEvent(endedEvent_);
        return S_OK;
    }

    int StartCount() const { return startCount_.load(); }
    int EndCount() const { return endCount_.load(); }
    int GetDataCount() const { return getDataCount_.load(); }
    HRESULT EndResult() const { return endResult_.load(); }
    DWORD EndEffect() const { return endEffect_.load(); }
    void SetInDragLoop(bool value) { inDragLoop_.store(value); }
    bool WaitForEnd()
    {
        DWORD index = 0;
        return SUCCEEDED(CoWaitForMultipleHandles(
            COWAIT_DISPATCH_CALLS | COWAIT_DISPATCH_WINDOW_MESSAGES,
            2000,
            1,
            &endedEvent_,
            &index));
    }

private:
    ~ProbeDataObject()
    {
        if (marshaler_ != nullptr)
        {
            marshaler_->Release();
        }
        if (filePath_[0] != L'\0')
        {
            DeleteFileW(filePath_);
        }
        CloseHandle(endedEvent_);
    }
    std::atomic<ULONG> references_{1};
    bool supportsFileDrop_ = true;
    wchar_t filePath_[MAX_PATH]{};
    std::atomic<BOOL> asyncMode_{TRUE};
    std::atomic<BOOL> inOperation_{FALSE};
    std::atomic<bool> inDragLoop_{false};
    std::atomic<int> startCount_{0};
    std::atomic<int> endCount_{0};
    std::atomic<int> getDataCount_{0};
    std::atomic<HRESULT> endResult_{E_PENDING};
    std::atomic<DWORD> endEffect_{MAXDWORD};
    HANDLE endedEvent_ = nullptr;
    IUnknown* marshaler_ = nullptr;
};

class CancelDropSource final : public IDropSource
{
public:
    explicit CancelDropSource(ProbeDataObject* dataObject)
        : dataObject_(dataObject) {}

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
        *value = nullptr;
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override { return --references_; }
    HRESULT STDMETHODCALLTYPE QueryContinueDrag(BOOL, DWORD) override
    {
        dataObject_->SetInDragLoop(false);
        return DRAGDROP_S_DROP;
    }
    HRESULT STDMETHODCALLTYPE GiveFeedback(DWORD) override
    {
        return DRAGDROP_S_USEDEFAULTCURSORS;
    }

private:
    std::atomic<ULONG> references_{1};
    ProbeDataObject* dataObject_ = nullptr;
};

DWORD WINAPI WakeDragLoop(void* parameter)
{
    const DWORD threadId = static_cast<DWORD>(
        reinterpret_cast<ULONG_PTR>(parameter));
    Sleep(300);
    PostThreadMessageW(threadId, WM_MOUSEMOVE, 0, 0);
    return 0;
}

bool ReplaceSlot(
    std::uint8_t* base,
    char* moduleName,
    IMAGE_THUNK_DATA64* imports,
    IMAGE_THUNK_DATA64* functions,
    bool hideImportName)
{
    bool replacedSlot = false;
    while (imports->u1.AddressOfData != 0)
    {
        if (!IMAGE_SNAP_BY_ORDINAL64(imports->u1.Ordinal))
        {
            const auto import = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                base + imports->u1.AddressOfData);
            if (std::strcmp(
                    reinterpret_cast<const char*>(import->Name),
                    "DoDragDrop") == 0)
            {
                DWORD slotProtection = 0;
                auto slot = reinterpret_cast<void**>(&functions->u1.Function);
                if (!VirtualProtect(
                        slot,
                        sizeof(void*),
                        PAGE_READWRITE,
                        &slotProtection))
                {
                    return false;
                }
                const HMODULE ole32 = GetModuleHandleW(L"ole32.dll");
                g_probeOriginalDoDragDrop = ole32 == nullptr
                    ? nullptr
                    : reinterpret_cast<ProbeDoDragDropFunction>(
                        GetProcAddress(ole32, "DoDragDrop"));
                if (g_probeOriginalDoDragDrop == nullptr)
                {
                    return false;
                }
                InterlockedExchangePointer(
                    slot,
                    reinterpret_cast<void*>(&ForwardedDoDragDrop));
                DWORD ignoredSlotProtection = 0;
                if (!VirtualProtect(
                        slot,
                        sizeof(void*),
                        slotProtection,
                        &ignoredSlotProtection))
                {
                    return false;
                }
                replacedSlot = true;
                break;
            }
        }
        ++imports;
        ++functions;
    }
    if (!replacedSlot)
    {
        return false;
    }
    if (!hideImportName)
    {
        return true;
    }

    DWORD previousProtection = 0;
    if (!VirtualProtect(
            moduleName,
            1,
            PAGE_READWRITE,
            &previousProtection))
    {
        return false;
    }
    moduleName[0] = 'x';
    DWORD ignoredProtection = 0;
    return VirtualProtect(
               moduleName,
               1,
               previousProtection,
               &ignoredProtection) != FALSE;
}

bool PrepareOleImport(bool hideImportName)
{
    const auto base = reinterpret_cast<std::uint8_t*>(
        GetModuleHandleW(nullptr));
    const auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE)
    {
        return false;
    }

    const auto nt = reinterpret_cast<IMAGE_NT_HEADERS64*>(
        base + dos->e_lfanew);
    const auto directory =
        nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    auto descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
        base + directory.VirtualAddress);
    while (descriptor->Name != 0)
    {
        auto name = reinterpret_cast<char*>(base + descriptor->Name);
        if (_stricmp(name, "ole32.dll") == 0)
        {
            return ReplaceSlot(
                base,
                name,
                reinterpret_cast<IMAGE_THUNK_DATA64*>(
                    base + descriptor->OriginalFirstThunk),
                reinterpret_cast<IMAGE_THUNK_DATA64*>(
                    base + descriptor->FirstThunk),
                hideImportName);
        }
        ++descriptor;
    }

    const auto delayDirectory =
        nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT];
    auto delayDescriptor = reinterpret_cast<ImgDelayDescr*>(
        base + delayDirectory.VirtualAddress);
    while (delayDescriptor->rvaDLLName != 0)
    {
        auto name = reinterpret_cast<char*>(
            base + delayDescriptor->rvaDLLName);
        if (_stricmp(name, "ole32.dll") == 0)
        {
            return ReplaceSlot(
                base,
                name,
                reinterpret_cast<IMAGE_THUNK_DATA64*>(
                    base + delayDescriptor->rvaINT),
                reinterpret_cast<IMAGE_THUNK_DATA64*>(
                    base + delayDescriptor->rvaIAT),
                hideImportName);
        }
        ++delayDescriptor;
    }

    return false;
}
}

int wmain(int argc, wchar_t** argv)
{
    if (argc > 2)
    {
        return 9;
    }

    const HRESULT initialized = OleInitialize(nullptr);
    if (FAILED(initialized))
    {
        return 10;
    }

    const bool hideImportName =
        argc == 2 &&
        _wcsicmp(argv[1], L"--hide-ole-import-name") == 0;
    const bool noFileDrop =
        argc == 2 &&
        _wcsicmp(argv[1], L"--no-cf-hdrop") == 0;
    HMODULE webViewImage = nullptr;
    if (argc == 2 && !hideImportName && !noFileDrop)
    {
        webViewImage = LoadLibraryExW(
            argv[1],
            nullptr,
            DONT_RESOLVE_DLL_REFERENCES);
        if (webViewImage == nullptr)
        {
            OleUninitialize();
            return 13;
        }
    }
    if (webViewImage == nullptr && !PrepareOleImport(hideImportName))
    {
        OleUninitialize();
        return 14;
    }

    const HANDLE enabled = CreateEventW(
        nullptr,
        TRUE,
        TRUE,
        L"Local\\PwaDrop.DragBridge.Enabled.v7");
    if (enabled == nullptr)
    {
        if (webViewImage != nullptr)
        {
            FreeLibrary(webViewImage);
        }
        OleUninitialize();
        return 11;
    }

    Sleep(3000);
    if (webViewImage != nullptr)
    {
        Sleep(500);
        CloseHandle(enabled);
        FreeLibrary(webViewImage);
        OleUninitialize();
        return 0;
    }

    auto* dataObject = new ProbeDataObject(!noFileDrop);
    CancelDropSource dropSource(dataObject);
    DWORD effect = DROPEFFECT_NONE;
    const HANDLE wakeThread = CreateThread(
        nullptr,
        0,
        WakeDragLoop,
        reinterpret_cast<void*>(
            static_cast<ULONG_PTR>(GetCurrentThreadId())),
        0,
        nullptr);
    dataObject->SetInDragLoop(true);
    g_originalDataAvailable.store(true);
    g_expectFileDrop.store(!noFileDrop);
    g_targetObservedFile.store(false);
    g_targetObservedSynchronousObject.store(false);
    g_targetQuerySucceeded.store(false);
    g_targetGetSucceeded.store(false);
    g_targetCountOne.store(false);
    g_targetPathRetrieved.store(false);
    g_targetPathExists.store(false);
    g_targetTaintQueryRejected.store(false);
    g_targetTaintGetRejected.store(false);
    g_targetEnumerationClean.store(false);
    const HRESULT dragResult = DoDragDrop(
        dataObject,
        &dropSource,
        DROPEFFECT_COPY,
        &effect);
    const bool workerCompleted = noFileDrop
        ? false
        : dataObject->WaitForEnd();
    if (wakeThread != nullptr)
    {
        WaitForSingleObject(wakeThread, 1000);
        CloseHandle(wakeThread);
    }
    const bool dragCompleted =
        dragResult == DRAGDROP_S_CANCEL ||
        dragResult == DRAGDROP_S_DROP;
    const bool passed = noFileDrop
        ? dragCompleted &&
            dataObject->StartCount() == 0 &&
            dataObject->EndCount() == 0 &&
            dataObject->GetDataCount() == 0
        : dragCompleted &&
            workerCompleted &&
            dataObject->StartCount() == 1 &&
            dataObject->EndCount() == 1 &&
            dataObject->GetDataCount() == 1 &&
            dataObject->EndResult() == S_OK &&
            dataObject->EndEffect() == DROPEFFECT_COPY &&
            g_targetObservedFile.load() &&
            g_targetObservedSynchronousObject.load() &&
            g_targetTaintQueryRejected.load() &&
            g_targetTaintGetRejected.load() &&
            g_targetEnumerationClean.load();

    int exitCode = 0;
    if (!passed)
    {
        exitCode = !dragCompleted ? 40
            : !workerCompleted ? 41
            : dataObject->StartCount() != 1 ? 42
            : dataObject->EndCount() != 1 ? 43
            : dataObject->GetDataCount() != 1 ? 44
            : dataObject->EndResult() != S_OK ? 45
            : dataObject->EndEffect() != DROPEFFECT_COPY ? 46
            : !g_targetQuerySucceeded.load() ? 47
            : !g_targetGetSucceeded.load() ? 48
            : !g_targetCountOne.load() ? 49
            : !g_targetPathRetrieved.load() ? 50
            : !g_targetPathExists.load() ? 51
            : !g_targetObservedFile.load() ? 52
            : !g_targetObservedSynchronousObject.load() ? 53
            : !g_targetTaintQueryRejected.load() ? 54
            : !g_targetTaintGetRejected.load() ? 55
            : !g_targetEnumerationClean.load() ? 56
            : 57;
    }

    dataObject->Release();
    CloseHandle(enabled);
    OleUninitialize();
    return exitCode;
}
