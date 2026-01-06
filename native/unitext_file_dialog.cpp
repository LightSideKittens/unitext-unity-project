// unitext_file_dialog.cpp — Native multi-file dialog for UniText Editor
// Windows: GetOpenFileNameW (comdlg32)
// macOS:   NSOpenPanel (AppKit) — compile with -x objective-c++
// Linux:   GTK3 via dlopen (no compile-time dependency)
//
// API:
//   char* unitext_open_files_dialog(title, filters, initialDir)
//     Returns heap-allocated UTF-8 paths, null-separated, double-null terminated.
//     Returns NULL if cancelled. Caller must free with unitext_free_dialog_result.
//
//   void unitext_free_dialog_result(char* result)
//
// filters format: comma-separated extensions, e.g. "ttf,otf,ttc"

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <commdlg.h>
#include <vector>
#endif

#ifdef __APPLE__
#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UTType.h>
#endif

#ifdef __linux__
#include <dlfcn.h>
#endif

#include <cstdlib>
#include <cstring>
#include <string>

#ifdef _WIN32
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

// ============================================================================
// Windows
// ============================================================================
#ifdef _WIN32

static std::string WideToUtf8(const wchar_t* wide, int len = -1)
{
    if (!wide) return {};
    if (len < 0) len = (int)wcslen(wide);
    if (len == 0) return {};
    int size = WideCharToMultiByte(CP_UTF8, 0, wide, len, nullptr, 0, nullptr, nullptr);
    std::string result(size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, wide, len, &result[0], size, nullptr, nullptr);
    return result;
}

static std::wstring Utf8ToWide(const char* utf8)
{
    if (!utf8) return {};
    int len = (int)strlen(utf8);
    if (len == 0) return {};
    int size = MultiByteToWideChar(CP_UTF8, 0, utf8, len, nullptr, 0);
    std::wstring result(size, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8, len, &result[0], size);
    return result;
}

static std::wstring BuildFilterW(const char* filters)
{
    std::wstring result = L"Font Files";
    result += L'\0';

    std::string f(filters ? filters : "");
    bool first = true;
    size_t pos = 0;
    while (pos < f.size())
    {
        size_t next = f.find(',', pos);
        if (next == std::string::npos) next = f.size();
        std::string ext(f, pos, next - pos);
        while (!ext.empty() && ext.front() == ' ') ext.erase(ext.begin());
        while (!ext.empty() && ext.back() == ' ') ext.pop_back();
        if (!ext.empty())
        {
            if (!first) result += L';';
            result += L"*.";
            result += Utf8ToWide(ext.c_str());
            first = false;
        }
        pos = next + 1;
    }
    result += L'\0';
    result += L'\0';
    return result;
}

EXPORT char* unitext_open_files_dialog(const char* title, const char* filters, const char* initialDir)
{
    const int BUF_SIZE = 65536;
    wchar_t* fileBuf = (wchar_t*)calloc(BUF_SIZE, sizeof(wchar_t));
    if (!fileBuf) return nullptr;

    std::wstring wTitle = Utf8ToWide(title ? title : "Select Files");
    std::wstring wFilter = BuildFilterW(filters);
    std::wstring wDir = initialDir ? Utf8ToWide(initialDir) : std::wstring();

    OPENFILENAMEW ofn = {};
    ofn.lStructSize = sizeof(OPENFILENAMEW);
    ofn.lpstrFilter = wFilter.c_str();
    ofn.lpstrFile = fileBuf;
    ofn.nMaxFile = BUF_SIZE;
    ofn.lpstrTitle = wTitle.c_str();
    ofn.lpstrInitialDir = wDir.empty() ? nullptr : wDir.c_str();
    ofn.Flags = OFN_ALLOWMULTISELECT | OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

    if (!GetOpenFileNameW(&ofn))
    {
        free(fileBuf);
        return nullptr;
    }

    // Single file: full path. Multiple: directory\0file1\0file2\0\0
    std::vector<std::wstring> parts;
    const wchar_t* p = fileBuf;
    while (*p)
    {
        parts.emplace_back(p);
        p += wcslen(p) + 1;
    }
    free(fileBuf);

    if (parts.empty()) return nullptr;

    std::string result;
    if (parts.size() == 1)
    {
        result = WideToUtf8(parts[0].c_str());
        result += '\0';
    }
    else
    {
        const std::wstring& dir = parts[0];
        for (size_t i = 1; i < parts.size(); i++)
        {
            std::wstring full = dir + L"\\" + parts[i];
            result += WideToUtf8(full.c_str());
            result += '\0';
        }
    }
    result += '\0';

    char* out = (char*)malloc(result.size());
    if (out) memcpy(out, result.data(), result.size());
    return out;
}

// ============================================================================
// macOS
// ============================================================================
#elif defined(__APPLE__)

static NSArray* ParseFilterExtensions(const char* filters)
{
    if (!filters) return nil;
    NSString* filterStr = [NSString stringWithUTF8String:filters];
    NSArray* exts = [filterStr componentsSeparatedByString:@","];
    NSMutableArray* trimmed = [NSMutableArray array];
    for (NSString* ext in exts)
    {
        NSString* t = [ext stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
        if (t.length > 0) [trimmed addObject:t];
    }
    return trimmed.count > 0 ? trimmed : nil;
}

static void ApplyFileTypeFilter(NSOpenPanel* panel, NSArray* extensions)
{
    if (!extensions) return;

    if (@available(macOS 12.0, *))
    {
        NSMutableArray* types = [NSMutableArray array];
        for (NSString* ext in extensions)
        {
            UTType* type = [UTType typeWithFilenameExtension:ext];
            if (type) [types addObject:type];
        }
        if ([types count] > 0)
            [panel setAllowedContentTypes:types];
    }
    else
    {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        [panel setAllowedFileTypes:extensions];
#pragma clang diagnostic pop
    }
}

EXPORT char* unitext_open_files_dialog(const char* title, const char* filters, const char* initialDir)
{
    @autoreleasepool
    {
        NSOpenPanel* panel = [NSOpenPanel openPanel];
        [panel setCanChooseFiles:YES];
        [panel setCanChooseDirectories:NO];
        [panel setAllowsMultipleSelection:YES];

        if (title)
            [panel setTitle:[NSString stringWithUTF8String:title]];

        if (initialDir)
            [panel setDirectoryURL:[NSURL fileURLWithPath:[NSString stringWithUTF8String:initialDir]]];

        ApplyFileTypeFilter(panel, ParseFilterExtensions(filters));

        if ([panel runModal] != NSModalResponseOK)
            return nullptr;

        NSArray<NSURL*>* urls = [panel URLs];
        if (urls.count == 0)
            return nullptr;

        std::string result;
        for (NSURL* url in urls)
        {
            const char* path = [[url path] UTF8String];
            if (path)
            {
                result += path;
                result += '\0';
            }
        }
        result += '\0';

        char* out = (char*)malloc(result.size());
        if (out) memcpy(out, result.data(), result.size());
        return out;
    }
}

// ============================================================================
// Linux (GTK3 via dlopen — no compile-time dependency)
// ============================================================================
#elif defined(__linux__)

typedef int  (*fn_gtk_init_check)(int*, char***);
typedef void*(*fn_gtk_file_chooser_dialog_new)(const char*, void*, int, const char*, ...);
typedef void (*fn_gtk_file_chooser_set_select_multiple)(void*, int);
typedef void*(*fn_gtk_file_filter_new)();
typedef void (*fn_gtk_file_filter_add_pattern)(void*, const char*);
typedef void (*fn_gtk_file_filter_set_name)(void*, const char*);
typedef void (*fn_gtk_file_chooser_add_filter)(void*, void*);
typedef void (*fn_gtk_file_chooser_set_current_folder)(void*, const char*);
typedef int  (*fn_gtk_dialog_run)(void*);
typedef void*(*fn_gtk_file_chooser_get_filenames)(void*);
typedef void (*fn_gtk_widget_destroy)(void*);
typedef int  (*fn_gtk_events_pending)();
typedef void (*fn_gtk_main_iteration)();
typedef void (*fn_g_free)(void*);
typedef void (*fn_g_slist_free)(void*);

struct GSList_
{
    void* data;
    GSList_* next;
};

// Loaded once, kept for process lifetime — gtk_init_check registers global state
// that becomes invalid if the library is unloaded.
static void* s_gtk  = nullptr;
static void* s_glib = nullptr;
static bool  s_gtk_loaded = false;

static fn_gtk_file_chooser_dialog_new  s_dialog_new;
static fn_gtk_file_chooser_set_select_multiple s_set_multi;
static fn_gtk_file_filter_new          s_filter_new;
static fn_gtk_file_filter_add_pattern  s_filter_add_pattern;
static fn_gtk_file_filter_set_name     s_filter_set_name;
static fn_gtk_file_chooser_add_filter  s_chooser_add_filter;
static fn_gtk_file_chooser_set_current_folder s_set_folder;
static fn_gtk_dialog_run               s_dialog_run;
static fn_gtk_file_chooser_get_filenames s_get_filenames;
static fn_gtk_widget_destroy           s_widget_destroy;
static fn_gtk_events_pending           s_events_pending;
static fn_gtk_main_iteration           s_main_iteration;
static fn_g_free                       s_g_free;
static fn_g_slist_free                 s_g_slist_free;

static bool EnsureGtkLoaded()
{
    if (s_gtk_loaded) return s_dialog_run != nullptr;

    s_gtk_loaded = true;
    s_gtk = dlopen("libgtk-3.so.0", RTLD_LAZY);
    if (!s_gtk) return false;

    s_glib = dlopen("libglib-2.0.so.0", RTLD_LAZY);

    auto init_check = (fn_gtk_init_check)dlsym(s_gtk, "gtk_init_check");
    s_dialog_new         = (fn_gtk_file_chooser_dialog_new)dlsym(s_gtk, "gtk_file_chooser_dialog_new");
    s_set_multi          = (fn_gtk_file_chooser_set_select_multiple)dlsym(s_gtk, "gtk_file_chooser_set_select_multiple");
    s_filter_new         = (fn_gtk_file_filter_new)dlsym(s_gtk, "gtk_file_filter_new");
    s_filter_add_pattern = (fn_gtk_file_filter_add_pattern)dlsym(s_gtk, "gtk_file_filter_add_pattern");
    s_filter_set_name    = (fn_gtk_file_filter_set_name)dlsym(s_gtk, "gtk_file_filter_set_name");
    s_chooser_add_filter = (fn_gtk_file_chooser_add_filter)dlsym(s_gtk, "gtk_file_chooser_add_filter");
    s_set_folder         = (fn_gtk_file_chooser_set_current_folder)dlsym(s_gtk, "gtk_file_chooser_set_current_folder");
    s_dialog_run         = (fn_gtk_dialog_run)dlsym(s_gtk, "gtk_dialog_run");
    s_get_filenames      = (fn_gtk_file_chooser_get_filenames)dlsym(s_gtk, "gtk_file_chooser_get_filenames");
    s_widget_destroy     = (fn_gtk_widget_destroy)dlsym(s_gtk, "gtk_widget_destroy");
    s_events_pending     = (fn_gtk_events_pending)dlsym(s_gtk, "gtk_events_pending");
    s_main_iteration     = (fn_gtk_main_iteration)dlsym(s_gtk, "gtk_main_iteration");
    s_g_free             = s_glib ? (fn_g_free)dlsym(s_glib, "g_free") : nullptr;
    s_g_slist_free       = s_glib ? (fn_g_slist_free)dlsym(s_glib, "g_slist_free") : nullptr;

    if (!init_check || !s_dialog_new || !s_dialog_run || !s_get_filenames || !s_widget_destroy)
        return false;

    init_check(nullptr, nullptr);
    return true;
}

EXPORT char* unitext_open_files_dialog(const char* title, const char* filters, const char* initialDir)
{
    if (!EnsureGtkLoaded()) return nullptr;

    // GTK_FILE_CHOOSER_ACTION_OPEN=0, GTK_RESPONSE_ACCEPT=-3, GTK_RESPONSE_CANCEL=-6
    void* dialog = s_dialog_new(
        title ? title : "Select Files", nullptr, 0,
        "_Cancel", -6, "_Open", -3, nullptr);

    if (!dialog) return nullptr;

    if (s_set_multi) s_set_multi(dialog, 1);

    if (initialDir && *initialDir && s_set_folder)
        s_set_folder(dialog, initialDir);

    if (filters && *filters && s_filter_new && s_filter_add_pattern && s_filter_set_name && s_chooser_add_filter)
    {
        void* filter = s_filter_new();
        s_filter_set_name(filter, "Font Files");

        std::string f(filters);
        size_t pos = 0;
        while (pos < f.size())
        {
            size_t next = f.find(',', pos);
            if (next == std::string::npos) next = f.size();
            std::string ext(f, pos, next - pos);
            while (!ext.empty() && ext.front() == ' ') ext.erase(ext.begin());
            while (!ext.empty() && ext.back() == ' ') ext.pop_back();
            if (!ext.empty())
                s_filter_add_pattern(filter, ("*." + ext).c_str());
            pos = next + 1;
        }
        s_chooser_add_filter(dialog, filter);
    }

    char* out = nullptr;

    if (s_dialog_run(dialog) == -3) // GTK_RESPONSE_ACCEPT
    {
        GSList_* list = (GSList_*)s_get_filenames(dialog);
        if (list)
        {
            std::string result;
            for (GSList_* node = list; node; node = node->next)
            {
                const char* path = (const char*)node->data;
                if (path)
                {
                    result += path;
                    result += '\0';
                    if (s_g_free) s_g_free(node->data);
                }
            }
            result += '\0';

            out = (char*)malloc(result.size());
            if (out) memcpy(out, result.data(), result.size());

            if (s_g_slist_free) s_g_slist_free(list);
        }
    }

    s_widget_destroy(dialog);
    if (s_events_pending && s_main_iteration)
        while (s_events_pending()) s_main_iteration();

    return out;
}

#endif // platform

// ============================================================================
// Common
// ============================================================================

EXPORT void unitext_free_dialog_result(char* result)
{
    free(result);
}
