#include <windows.h>
#include <tlhelp32.h>
#include <bcrypt.h>
#include <delayimp.h>
#include <softpub.h>
#include <wintrust.h>
#include <cstddef>
#include <cstring>
#include <string>
#include <vector>
#include <cwchar>

namespace
{
constexpr int kInvalidArguments = 2;
constexpr int kPolicyRejected = 3;
constexpr int kInjectionFailed = 4;
constexpr int kHookFailed = 5;
constexpr int kHookTimedOut = 6;
constexpr int kRandomFailed = 10;
constexpr int kAckEventFailed = 11;
constexpr int kRemotePathAllocationFailed = 12;
constexpr int kRemoteRequestAllocationFailed = 13;
constexpr int kRemoteWriteFailed = 14;
constexpr int kLoaderResolutionFailed = 15;
constexpr int kLoaderThreadFailed = 16;
constexpr int kLoaderThreadTimedOut = 17;
constexpr int kRemoteModuleMissing = 18;
constexpr int kBootstrapExportMissing = 19;
constexpr int kBootstrapThreadFailed = 20;
constexpr int kBootstrapThreadTimedOut = 21;
constexpr int kBootstrapReportedFailure = 22;
constexpr int kBootstrapAckTimedOut = 23;
constexpr int kImportSlotRvaMissing = 24;
constexpr int kHookPathRejected = 30;
constexpr int kTargetKindRejected = 31;
constexpr int kOpenProcessRejected = 32;
constexpr int kSessionRejected = 33;
constexpr int kUserRejected = 34;
constexpr int kElevationRejected = 35;
constexpr int kArchitectureRejected = 36;
constexpr int kStartTimeRejected = 37;
constexpr ULONGLONG kDateTimeFileTimeOffset = 504911232000000000ULL;

struct BootstrapRequest
{
    ULONGLONG nonce;
    DWORD patchStatus;
    DWORD iatSlotRva;
};

struct Handle
{
    HANDLE value = nullptr;
    Handle() = default;
    explicit Handle(HANDLE handle) : value(handle) {}
    ~Handle()
    {
        if (value != nullptr && value != INVALID_HANDLE_VALUE)
        {
            CloseHandle(value);
        }
    }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    operator HANDLE() const { return value; }
};

std::wstring FileName(const std::wstring& path)
{
    const auto separator = path.find_last_of(L"\\/");
    return separator == std::wstring::npos
        ? path
        : path.substr(separator + 1);
}

std::wstring FullPath(const wchar_t* path)
{
    const DWORD required = GetFullPathNameW(path, 0, nullptr, nullptr);
    if (required == 0)
    {
        return {};
    }

    std::wstring result(required, L'\0');
    const DWORD written = GetFullPathNameW(
        path,
        required,
        result.data(),
        nullptr);
    if (written == 0 || written >= required)
    {
        return {};
    }

    result.resize(written);
    return result;
}

std::wstring HostDirectory()
{
    std::vector<wchar_t> path(32768);
    const DWORD length = GetModuleFileNameW(
        nullptr,
        path.data(),
        static_cast<DWORD>(path.size()));
    if (length == 0 || length >= path.size())
    {
        return {};
    }

    std::wstring value(path.data(), length);
    const auto separator = value.find_last_of(L"\\/");
    return separator == std::wstring::npos
        ? std::wstring{}
        : value.substr(0, separator);
}

std::wstring ProcessImagePath(DWORD processId)
{
    Handle process(OpenProcess(
        PROCESS_QUERY_LIMITED_INFORMATION,
        FALSE,
        processId));
    if (process.value == nullptr)
    {
        return {};
    }

    std::vector<wchar_t> path(32768);
    DWORD length = static_cast<DWORD>(path.size());
    if (!QueryFullProcessImageNameW(
            process,
            0,
            path.data(),
            &length) ||
        length == 0)
    {
        return {};
    }

    return FullPath(std::wstring(path.data(), length).c_str());
}

bool IsUnderDirectory(
    const std::wstring& path,
    const std::wstring& directory)
{
    if (path.size() <= directory.size() ||
        _wcsnicmp(path.c_str(), directory.c_str(), directory.size()) != 0)
    {
        return false;
    }

    const wchar_t separator = path[directory.size()];
    return separator == L'\\' || separator == L'/';
}

bool HasTrustedSignature(const std::wstring& path)
{
    WINTRUST_FILE_INFO file{};
    file.cbStruct = sizeof(file);
    file.pcwszFilePath = path.c_str();

    WINTRUST_DATA trust{};
    trust.cbStruct = sizeof(trust);
    trust.dwUIChoice = WTD_UI_NONE;
    trust.fdwRevocationChecks = WTD_REVOKE_NONE;
    trust.dwUnionChoice = WTD_CHOICE_FILE;
    trust.pFile = &file;
    trust.dwStateAction = WTD_STATEACTION_VERIFY;
    trust.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL;
    trust.dwUIContext = WTD_UICONTEXT_EXECUTE;

    GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    const LONG status = WinVerifyTrust(nullptr, &action, &trust);
    trust.dwStateAction = WTD_STATEACTION_CLOSE;
    WinVerifyTrust(nullptr, &action, &trust);
    return status == ERROR_SUCCESS;
}

bool NameIsOneOf(
    const std::wstring& name,
    const wchar_t* const* allowed,
    size_t count)
{
    for (size_t index = 0; index < count; ++index)
    {
        if (_wcsicmp(name.c_str(), allowed[index]) == 0)
        {
            return true;
        }
    }

    return false;
}

struct WindowOwnerSearch
{
    DWORD processId;
    bool found;
};

BOOL CALLBACK FindVisibleTopLevelWindow(HWND window, LPARAM parameter)
{
    auto search = reinterpret_cast<WindowOwnerSearch*>(parameter);
    DWORD processId = 0;
    GetWindowThreadProcessId(window, &processId);
    if (processId == search->processId && IsWindowVisible(window))
    {
        search->found = true;
        return FALSE;
    }

    return TRUE;
}

bool OwnsVisibleTopLevelWindow(DWORD processId)
{
    WindowOwnerSearch search{processId, false};
    EnumWindows(
        FindVisibleTopLevelWindow,
        reinterpret_cast<LPARAM>(&search));
    return search.found;
}

bool EqualUser(HANDLE process)
{
    Handle currentToken;
    Handle targetToken;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &currentToken.value) ||
        !OpenProcessToken(process, TOKEN_QUERY, &targetToken.value))
    {
        return false;
    }

    DWORD currentSize = 0;
    DWORD targetSize = 0;
    GetTokenInformation(currentToken, TokenUser, nullptr, 0, &currentSize);
    GetTokenInformation(targetToken, TokenUser, nullptr, 0, &targetSize);
    if (currentSize == 0 || targetSize == 0)
    {
        return false;
    }

    std::vector<std::byte> currentBuffer(currentSize);
    std::vector<std::byte> targetBuffer(targetSize);
    if (!GetTokenInformation(
            currentToken,
            TokenUser,
            currentBuffer.data(),
            currentSize,
            &currentSize) ||
        !GetTokenInformation(
            targetToken,
            TokenUser,
            targetBuffer.data(),
            targetSize,
            &targetSize))
    {
        return false;
    }

    const auto currentUser =
        reinterpret_cast<TOKEN_USER*>(currentBuffer.data());
    const auto targetUser =
        reinterpret_cast<TOKEN_USER*>(targetBuffer.data());
    return EqualSid(currentUser->User.Sid, targetUser->User.Sid) != FALSE;
}

bool IsNotElevated(HANDLE process)
{
    Handle token;
    if (!OpenProcessToken(process, TOKEN_QUERY, &token.value))
    {
        return false;
    }

    TOKEN_ELEVATION elevation{};
    DWORD size = 0;
    return GetTokenInformation(
               token,
               TokenElevation,
               &elevation,
               sizeof(elevation),
               &size) &&
        elevation.TokenIsElevated == 0;
}

bool IsX64(HANDLE process)
{
    USHORT processMachine = IMAGE_FILE_MACHINE_UNKNOWN;
    USHORT nativeMachine = IMAGE_FILE_MACHINE_UNKNOWN;
    return IsWow64Process2(process, &processMachine, &nativeMachine) &&
        processMachine == IMAGE_FILE_MACHINE_UNKNOWN &&
        nativeMachine == IMAGE_FILE_MACHINE_AMD64;
}

bool HasExpectedStartTime(HANDLE process, ULONGLONG expectedDateTimeTicks)
{
    FILETIME created{};
    FILETIME exited{};
    FILETIME kernel{};
    FILETIME user{};
    if (!GetProcessTimes(process, &created, &exited, &kernel, &user))
    {
        return false;
    }

    ULARGE_INTEGER value{};
    value.LowPart = created.dwLowDateTime;
    value.HighPart = created.dwHighDateTime;
    return value.QuadPart + kDateTimeFileTimeOffset == expectedDateTimeTicks;
}

bool GetProcessEntry(DWORD processId, PROCESSENTRY32W* result)
{
    Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
    if (snapshot.value == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    if (!Process32FirstW(snapshot, &entry))
    {
        return false;
    }

    do
    {
        if (entry.th32ProcessID == processId)
        {
            *result = entry;
            return true;
        }
    }
    while (Process32NextW(snapshot, &entry));

    return false;
}

bool IsNativeProbeProcess(DWORD processId)
{
    const std::wstring expectedProbe = FullPath(
        (HostDirectory() + L"\\PwaDrop.NativeProbe.exe").c_str());
    const std::wstring actualProbe = ProcessImagePath(processId);
    return !expectedProbe.empty() &&
        !actualProbe.empty() &&
        _wcsicmp(expectedProbe.c_str(), actualProbe.c_str()) == 0;
}

bool IsAllowedRootProcess(DWORD processId)
{
    PROCESSENTRY32W target{};
    if (!GetProcessEntry(processId, &target))
    {
        return false;
    }

    const std::wstring targetName = target.szExeFile;
    if (_wcsicmp(targetName.c_str(), L"PwaDrop.NativeProbe.exe") == 0)
    {
        return IsNativeProbeProcess(processId);
    }

    const std::wstring targetPath = ProcessImagePath(processId);
    if (targetPath.empty() || !HasTrustedSignature(targetPath))
    {
        return false;
    }

    constexpr const wchar_t* directSources[] = {
        L"brave.exe",
        L"chrome.exe",
        L"chromium.exe",
        L"comet.exe",
        L"missive.exe",
        L"msedge.exe",
        L"opera.exe",
        L"slack.exe",
        L"superhuman.exe",
        L"vivaldi.exe"};
    const bool isKnownDirectSource = NameIsOneOf(
        targetName,
        directSources,
        _countof(directSources));
    const bool isWebView2 =
        _wcsicmp(targetName.c_str(), L"msedgewebview2.exe") == 0;
    const bool isDirectSource = isKnownDirectSource;
    if (!isDirectSource && !isWebView2)
    {
        return false;
    }

    PROCESSENTRY32W parent{};
    const bool hasParent = GetProcessEntry(
        target.th32ParentProcessID,
        &parent);
    if (hasParent && _wcsicmp(parent.szExeFile, target.szExeFile) == 0)
    {
        return false;
    }

    if (isDirectSource)
    {
        return OwnsVisibleTopLevelWindow(processId);
    }

    if (!hasParent)
    {
        return false;
    }

    DWORD ancestorId = target.th32ParentProcessID;
    for (int depth = 0; depth < 8 && ancestorId != 0; ++depth)
    {
        PROCESSENTRY32W ancestor{};
        if (!GetProcessEntry(ancestorId, &ancestor))
        {
            return false;
        }

        if (_wcsicmp(ancestor.szExeFile, L"olk.exe") == 0 ||
            _wcsicmp(
                ancestor.szExeFile,
                L"Microsoft.OutlookForWindows.exe") == 0 ||
            _wcsicmp(ancestor.szExeFile, L"ms-teams.exe") == 0 ||
            _wcsicmp(ancestor.szExeFile, L"msteams.exe") == 0)
        {
            return true;
        }

        ancestorId = ancestor.th32ParentProcessID;
    }

    return false;
}

bool IsModuleLoaded(DWORD processId, const std::wstring& expectedPath)
{
    Handle snapshot(CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
        processId));
    if (snapshot.value == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    MODULEENTRY32W module{};
    module.dwSize = sizeof(module);
    if (!Module32FirstW(snapshot, &module))
    {
        return false;
    }

    do
    {
        if (_wcsicmp(FullPath(module.szExePath).c_str(), expectedPath.c_str()) == 0 ||
            _wcsicmp(module.szModule, L"PwaDrop.Hook.dll") == 0)
        {
            return true;
        }
    }
    while (Module32NextW(snapshot, &module));

    return false;
}

void* RemoteModuleBase(DWORD processId, const std::wstring& expectedPath)
{
    Handle snapshot(CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
        processId));
    if (snapshot.value == INVALID_HANDLE_VALUE)
    {
        return nullptr;
    }

    MODULEENTRY32W module{};
    module.dwSize = sizeof(module);
    if (!Module32FirstW(snapshot, &module))
    {
        return nullptr;
    }

    do
    {
        if (_wcsicmp(
                FullPath(module.szExePath).c_str(),
                expectedPath.c_str()) == 0)
        {
            return module.modBaseAddr;
        }
    }
    while (Module32NextW(snapshot, &module));

    return nullptr;
}

std::wstring RemoteModulePath(DWORD processId, const wchar_t* moduleName)
{
    Handle snapshot(CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
        processId));
    if (snapshot.value == INVALID_HANDLE_VALUE)
    {
        return {};
    }

    MODULEENTRY32W module{};
    module.dwSize = sizeof(module);
    if (!Module32FirstW(snapshot, &module))
    {
        return {};
    }

    do
    {
        if (_wcsicmp(module.szModule, moduleName) == 0)
        {
            return FullPath(module.szExePath);
        }
    }
    while (Module32NextW(snapshot, &module));

    return {};
}

DWORD FindDoDragDropIatRva(const std::wstring& modulePath)
{
    const HMODULE module = LoadLibraryExW(
        modulePath.c_str(),
        nullptr,
        DONT_RESOLVE_DLL_REFERENCES);
    if (module == nullptr)
    {
        return 0;
    }

    DWORD result = 0;
    __try
    {
        const auto base = reinterpret_cast<const std::byte*>(module);
        const auto dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
        if (dos->e_magic == IMAGE_DOS_SIGNATURE &&
            dos->e_lfanew > 0 &&
            dos->e_lfanew <= 1024 * 1024)
        {
            const auto nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(
                base + dos->e_lfanew);
            if (nt->Signature == IMAGE_NT_SIGNATURE &&
                nt->OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC)
            {
                const DWORD imageSize = nt->OptionalHeader.SizeOfImage;
                const auto directory =
                    nt->OptionalHeader.DataDirectory[
                        IMAGE_DIRECTORY_ENTRY_IMPORT];
                if (directory.VirtualAddress < imageSize &&
                    directory.Size >= sizeof(IMAGE_IMPORT_DESCRIPTOR))
                {
                    auto descriptor =
                        reinterpret_cast<const IMAGE_IMPORT_DESCRIPTOR*>(
                            base + directory.VirtualAddress);
                    const SIZE_T descriptorLimit =
                        directory.Size / sizeof(IMAGE_IMPORT_DESCRIPTOR);
                    for (SIZE_T descriptorIndex = 0;
                         descriptorIndex < descriptorLimit &&
                             descriptor->Name != 0 &&
                             result == 0;
                         ++descriptorIndex, ++descriptor)
                    {
                        if (descriptor->Name >= imageSize ||
                            descriptor->OriginalFirstThunk == 0 ||
                            descriptor->OriginalFirstThunk >= imageSize ||
                            descriptor->FirstThunk == 0 ||
                            descriptor->FirstThunk >= imageSize)
                        {
                            continue;
                        }

                        const auto importedModule =
                            reinterpret_cast<const char*>(
                                base + descriptor->Name);
                        if (_stricmp(importedModule, "ole32.dll") != 0)
                        {
                            continue;
                        }

                        auto names =
                            reinterpret_cast<const IMAGE_THUNK_DATA64*>(
                                base + descriptor->OriginalFirstThunk);
                        const SIZE_T thunkLimit =
                            (imageSize - descriptor->OriginalFirstThunk) /
                            sizeof(IMAGE_THUNK_DATA64);
                        for (SIZE_T thunkIndex = 0;
                             thunkIndex < thunkLimit &&
                                 names->u1.AddressOfData != 0;
                             ++thunkIndex, ++names)
                        {
                            if (IMAGE_SNAP_BY_ORDINAL64(names->u1.Ordinal) ||
                                names->u1.AddressOfData >= imageSize)
                            {
                                continue;
                            }

                            const auto import =
                                reinterpret_cast<const IMAGE_IMPORT_BY_NAME*>(
                                    base + names->u1.AddressOfData);
                            if (std::strcmp(
                                    reinterpret_cast<const char*>(
                                        import->Name),
                                    "DoDragDrop") == 0)
                            {
                                const ULONGLONG slotRva =
                                    descriptor->FirstThunk +
                                    thunkIndex * sizeof(IMAGE_THUNK_DATA64);
                                if (slotRva <= MAXDWORD &&
                                    slotRva + sizeof(void*) <= imageSize)
                                {
                                    result = static_cast<DWORD>(slotRva);
                                }
                                break;
                            }
                        }
                    }
                }

                if (result == 0)
                {
                    const auto delayDirectory =
                        nt->OptionalHeader.DataDirectory[
                            IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT];
                    if (delayDirectory.VirtualAddress < imageSize &&
                        delayDirectory.Size >= sizeof(ImgDelayDescr))
                    {
                        auto descriptor =
                            reinterpret_cast<const ImgDelayDescr*>(
                                base + delayDirectory.VirtualAddress);
                        const SIZE_T descriptorLimit =
                            delayDirectory.Size / sizeof(ImgDelayDescr);
                        for (SIZE_T descriptorIndex = 0;
                             descriptorIndex < descriptorLimit &&
                                 descriptor->rvaDLLName != 0 &&
                                 result == 0;
                             ++descriptorIndex, ++descriptor)
                        {
                            if ((descriptor->grAttrs & dlattrRva) == 0 ||
                                descriptor->rvaDLLName >= imageSize ||
                                descriptor->rvaINT >= imageSize ||
                                descriptor->rvaIAT >= imageSize)
                            {
                                continue;
                            }

                            const auto importedModule =
                                reinterpret_cast<const char*>(
                                    base + descriptor->rvaDLLName);
                            if (_stricmp(importedModule, "ole32.dll") != 0)
                            {
                                continue;
                            }

                            auto names =
                                reinterpret_cast<const IMAGE_THUNK_DATA64*>(
                                    base + descriptor->rvaINT);
                            const SIZE_T thunkLimit =
                                (imageSize - descriptor->rvaINT) /
                                sizeof(IMAGE_THUNK_DATA64);
                            for (SIZE_T thunkIndex = 0;
                                 thunkIndex < thunkLimit &&
                                     names->u1.AddressOfData != 0;
                                 ++thunkIndex, ++names)
                            {
                                if (IMAGE_SNAP_BY_ORDINAL64(
                                        names->u1.Ordinal) ||
                                    names->u1.AddressOfData >= imageSize)
                                {
                                    continue;
                                }

                                const auto import =
                                    reinterpret_cast<
                                        const IMAGE_IMPORT_BY_NAME*>(
                                        base +
                                        names->u1.AddressOfData);
                                if (std::strcmp(
                                        reinterpret_cast<const char*>(
                                            import->Name),
                                        "DoDragDrop") == 0)
                                {
                                    const ULONGLONG slotRva =
                                        descriptor->rvaIAT +
                                        thunkIndex *
                                            sizeof(IMAGE_THUNK_DATA64);
                                    if (slotRva <= MAXDWORD &&
                                        slotRva + sizeof(void*) <=
                                            imageSize)
                                    {
                                        result =
                                            static_cast<DWORD>(slotRva);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        result = 0;
    }

    FreeLibrary(module);
    return result;
}

SIZE_T ExportOffset(
    const std::wstring& modulePath,
    const char* exportName)
{
    const HMODULE module = LoadLibraryExW(
        modulePath.c_str(),
        nullptr,
        DONT_RESOLVE_DLL_REFERENCES);
    if (module == nullptr)
    {
        return 0;
    }

    const auto procedure = reinterpret_cast<const std::byte*>(
        GetProcAddress(module, exportName));
    const SIZE_T offset = procedure == nullptr
        ? 0
        : static_cast<SIZE_T>(
            procedure - reinterpret_cast<const std::byte*>(module));
    FreeLibrary(module);
    return offset;
}

void* RemoteProcedureAddress(DWORD processId, const wchar_t* moduleName, const char* procedure)
{
    const HMODULE requestedModule = GetModuleHandleW(moduleName);
    if (requestedModule == nullptr)
    {
        return nullptr;
    }

    const auto localProcedure = reinterpret_cast<const std::byte*>(
        GetProcAddress(requestedModule, procedure));
    if (localProcedure == nullptr)
    {
        return nullptr;
    }

    MEMORY_BASIC_INFORMATION procedureMemory{};
    if (VirtualQuery(
            localProcedure,
            &procedureMemory,
            sizeof(procedureMemory)) != sizeof(procedureMemory) ||
        procedureMemory.AllocationBase == nullptr)
    {
        return nullptr;
    }

    const auto owningModule = static_cast<HMODULE>(
        procedureMemory.AllocationBase);
    std::vector<wchar_t> owningPath(32768);
    const DWORD owningPathLength = GetModuleFileNameW(
        owningModule,
        owningPath.data(),
        static_cast<DWORD>(owningPath.size()));
    if (owningPathLength == 0 || owningPathLength >= owningPath.size())
    {
        return nullptr;
    }
    const std::wstring owningName = FileName(
        std::wstring(owningPath.data(), owningPathLength));

    Handle snapshot(CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
        processId));
    if (snapshot.value == INVALID_HANDLE_VALUE)
    {
        return nullptr;
    }

    MODULEENTRY32W module{};
    module.dwSize = sizeof(module);
    if (!Module32FirstW(snapshot, &module))
    {
        return nullptr;
    }

    do
    {
        if (_wcsicmp(module.szModule, owningName.c_str()) == 0)
        {
            const auto offset =
                localProcedure -
                reinterpret_cast<const std::byte*>(owningModule);
            return reinterpret_cast<std::byte*>(module.modBaseAddr) + offset;
        }
    }
    while (Module32NextW(snapshot, &module));

    return nullptr;
}

int Inject(
    HANDLE process,
    DWORD processId,
    const std::wstring& hookPath)
{
    std::wstring importModulePath = RemoteModulePath(
        processId,
        L"msedge.dll");
    if (importModulePath.empty())
    {
        importModulePath = RemoteModulePath(processId, L"chrome.dll");
    }
    if (importModulePath.empty())
    {
        importModulePath = ProcessImagePath(processId);
    }
    const DWORD iatSlotRva = FindDoDragDropIatRva(importModulePath);
    if (iatSlotRva == 0)
    {
        return kImportSlotRvaMissing;
    }

    ULONGLONG nonce = 0;
    if (BCryptGenRandom(
            nullptr,
            reinterpret_cast<PUCHAR>(&nonce),
            sizeof(nonce),
            BCRYPT_USE_SYSTEM_PREFERRED_RNG) != 0 ||
        nonce == 0)
    {
        return kRandomFailed;
    }

    wchar_t readyName[128]{};
    wchar_t failedName[128]{};
    swprintf_s(
        readyName,
        L"Local\\PwaDrop.NativeHook.Ready.%lu.%016llX",
        processId,
        nonce);
    swprintf_s(
        failedName,
        L"Local\\PwaDrop.NativeHook.Failed.%lu.%016llX",
        processId,
        nonce);

    Handle ready(CreateEventW(nullptr, TRUE, FALSE, readyName));
    const DWORD readyError = GetLastError();
    Handle failed(CreateEventW(nullptr, TRUE, FALSE, failedName));
    const DWORD failedError = GetLastError();
    if (ready.value == nullptr ||
        failed.value == nullptr ||
        readyError == ERROR_ALREADY_EXISTS ||
        failedError == ERROR_ALREADY_EXISTS)
    {
        return kAckEventFailed;
    }

    const SIZE_T pathBytes = (hookPath.size() + 1) * sizeof(wchar_t);
    void* remotePath = VirtualAllocEx(
        process,
        nullptr,
        pathBytes,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_READWRITE);
    if (remotePath == nullptr)
    {
        return kRemotePathAllocationFailed;
    }

    void* remoteRequest = VirtualAllocEx(
        process,
        nullptr,
        sizeof(BootstrapRequest),
        MEM_COMMIT | MEM_RESERVE,
        PAGE_READWRITE);
    if (remoteRequest == nullptr)
    {
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        return kRemoteRequestAllocationFailed;
    }

    int result = kRemoteWriteFailed;
    const BootstrapRequest request{ nonce, 0, iatSlotRva };
    if (WriteProcessMemory(
            process,
            remotePath,
            hookPath.c_str(),
            pathBytes,
            nullptr) &&
        WriteProcessMemory(
            process,
            remoteRequest,
            &request,
            sizeof(request),
            nullptr))
    {
        const auto loadLibrary = reinterpret_cast<LPTHREAD_START_ROUTINE>(
            RemoteProcedureAddress(processId, L"kernel32.dll", "LoadLibraryW"));
        Handle thread;
        if (loadLibrary != nullptr)
        {
            thread.value = CreateRemoteThread(
                process,
                nullptr,
                0,
                loadLibrary,
                remotePath,
                0,
                nullptr);
        }
        if (loadLibrary == nullptr)
        {
            result = kLoaderResolutionFailed;
        }
        else if (thread.value == nullptr)
        {
            result = kLoaderThreadFailed;
        }
        else if (WaitForSingleObject(thread, 5000) != WAIT_OBJECT_0)
        {
            result = kLoaderThreadTimedOut;
        }
        else
        {
            const auto remoteModule = reinterpret_cast<std::byte*>(
                RemoteModuleBase(processId, hookPath));
            const SIZE_T bootstrapOffset = ExportOffset(
                hookPath,
                "PwaDropBootstrap");
            Handle bootstrapThread;
            if (remoteModule == nullptr)
            {
                result = kRemoteModuleMissing;
            }
            else if (bootstrapOffset == 0)
            {
                result = kBootstrapExportMissing;
            }
            else
            {
                bootstrapThread.value = CreateRemoteThread(
                    process,
                    nullptr,
                    0,
                    reinterpret_cast<LPTHREAD_START_ROUTINE>(
                        remoteModule + bootstrapOffset),
                    remoteRequest,
                    0,
                    nullptr);
            }

            if (remoteModule != nullptr &&
                bootstrapOffset != 0 &&
                bootstrapThread.value == nullptr)
            {
                result = kBootstrapThreadFailed;
            }
            else if (bootstrapThread.value != nullptr &&
                WaitForSingleObject(bootstrapThread, 5000) != WAIT_OBJECT_0)
            {
                result = kBootstrapThreadTimedOut;
            }
            else if (bootstrapThread.value != nullptr)
            {
                HANDLE events[] = { ready, failed };
                const DWORD waitResult = WaitForMultipleObjects(
                    2,
                    events,
                    FALSE,
                    3000);
                if (waitResult == WAIT_OBJECT_0)
                {
                    result = 0;
                }
                else if (waitResult == WAIT_OBJECT_0 + 1)
                {
                    BootstrapRequest completedRequest{};
                    if (ReadProcessMemory(
                            process,
                            remoteRequest,
                            &completedRequest,
                            sizeof(completedRequest),
                            nullptr) &&
                        completedRequest.patchStatus != 0)
                    {
                        result = 40 +
                            static_cast<int>(completedRequest.patchStatus);
                    }
                    else
                    {
                        result = kBootstrapReportedFailure;
                    }
                }
                else
                {
                    result = kBootstrapAckTimedOut;
                }
            }
        }
    }

    VirtualFreeEx(process, remoteRequest, 0, MEM_RELEASE);
    VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
    return result;
}
}

int wmain(int argc, wchar_t** argv)
{
    if (argc != 4)
    {
        return kInvalidArguments;
    }

    wchar_t* end = nullptr;
    const unsigned long parsedProcessId = wcstoul(argv[1], &end, 10);
    if (parsedProcessId == 0 || end == argv[1] || *end != L'\0')
    {
        return kInvalidArguments;
    }

    end = nullptr;
    const ULONGLONG expectedTicks = _wcstoui64(argv[2], &end, 10);
    if (expectedTicks == 0 || end == argv[2] || *end != L'\0')
    {
        return kInvalidArguments;
    }

    const DWORD processId = static_cast<DWORD>(parsedProcessId);
    const std::wstring hookPath = FullPath(argv[3]);
    const std::wstring allowedPath = FullPath(
        (HostDirectory() + L"\\PwaDrop.Hook.dll").c_str());
    if (hookPath.empty() ||
        _wcsicmp(hookPath.c_str(), allowedPath.c_str()) != 0 ||
        GetFileAttributesW(hookPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return kHookPathRejected;
    }
    if (!IsAllowedRootProcess(processId))
    {
        return kTargetKindRejected;
    }

    Handle process(OpenProcess(
        PROCESS_CREATE_THREAD |
            PROCESS_QUERY_INFORMATION |
            PROCESS_VM_OPERATION |
            PROCESS_VM_WRITE |
            PROCESS_VM_READ |
            SYNCHRONIZE,
        FALSE,
        processId));
    DWORD targetSession = 0;
    DWORD currentSession = 0;
    if (process.value == nullptr)
    {
        return kOpenProcessRejected;
    }
    if (!ProcessIdToSessionId(processId, &targetSession) ||
        !ProcessIdToSessionId(GetCurrentProcessId(), &currentSession) ||
        targetSession != currentSession)
    {
        return kSessionRejected;
    }
    if (!EqualUser(process))
    {
        return kUserRejected;
    }
    if (!IsNotElevated(process) && !IsNativeProbeProcess(processId))
    {
        return kElevationRejected;
    }
    if (!IsX64(process))
    {
        return kArchitectureRejected;
    }
    if (!HasExpectedStartTime(process, expectedTicks))
    {
        return kStartTimeRejected;
    }

    if (IsModuleLoaded(processId, hookPath))
    {
        return 0;
    }

    return Inject(process, processId, hookPath);
}
