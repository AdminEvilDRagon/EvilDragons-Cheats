// DirectX 11 / 12 overlay: hooks the swap chain's Present, draws the ImGui menu on top of the game.
#include "ed_ui.h"
#include <d3d11.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <deque>
#include "MinHook.h"
#include "imgui.h"
#include "imgui_impl_win32.h"
#include "imgui_impl_dx12.h"
#include "imgui_impl_dx11.h"
#include "resource.h"

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND, UINT, WPARAM, LPARAM);

using PresentFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain3*, UINT, UINT);
using ResizeFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain3*, UINT, UINT, UINT, DXGI_FORMAT, UINT);
using ExecFn = void(STDMETHODCALLTYPE*)(ID3D12CommandQueue*, UINT, ID3D12CommandList* const*);

static PresentFn oPresent;
static ResizeFn oResize;
static ExecFn oExec;

static ID3D12CommandQueue* g_queue;
static ID3D12Device* g_dev;
static ID3D12DescriptorHeap* g_srvHeap;
static ID3D12DescriptorHeap* g_rtvHeap;
static ID3D12GraphicsCommandList* g_list;
static ID3D12Fence* g_fence;
static UINT64 g_fenceVal;
static HANDLE g_fenceEvent;
static HWND g_hwnd;
static WNDPROC g_oWndProc;
static bool g_ready, g_failed;
static ID3D12Resource* g_logo;
static D3D12_GPU_DESCRIPTOR_HANDLE g_logoGpu;

// DirectX 11 games
static bool g_dx11;
static ID3D11Device* g_dev11;
static ID3D11DeviceContext* g_ctx11;
static ID3D11RenderTargetView* g_rtv11;
static ID3D11ShaderResourceView* g_logo11;

struct Frame { ID3D12CommandAllocator* alloc = nullptr; ID3D12Resource* rt = nullptr; D3D12_CPU_DESCRIPTOR_HANDLE rtv{}; UINT64 fence = 0; };
static std::vector<Frame> g_frames;

// ---------------- input: window messages arrive on the game thread, ImGui runs on the render thread ----------------
struct Msg { UINT m; WPARAM w; LPARAM l; };
static std::mutex g_msgLock;
static std::deque<Msg> g_msgs;

static bool IsInputMsg(UINT m)
{
    return (m >= WM_MOUSEFIRST && m <= WM_MOUSELAST) || (m >= WM_KEYFIRST && m <= WM_KEYLAST) || m == WM_INPUT;
}

static LRESULT CALLBACK HookWndProc(HWND h, UINT m, WPARAM w, LPARAM l)
{
    if (g_menuOpen && IsInputMsg(m))
    {
        if (m != WM_INPUT)
        {
            std::lock_guard<std::mutex> lk(g_msgLock);
            if (g_msgs.size() < 512) g_msgs.push_back({ m, w, l });
        }
        // let key releases through so the game never thinks a key is stuck
        if (m != WM_KEYUP && m != WM_SYSKEYUP) return m == WM_INPUT ? DefWindowProcW(h, m, w, l) : 0;
    }
    return CallWindowProcW(g_oWndProc, h, m, w, l);
}

void* Render_LogoTexture() { if (g_dx11) return g_logo11; return g_logo ? (void*)g_logoGpu.ptr : nullptr; }

// ---------------- helpers ----------------
static void WaitFence(UINT64 v)
{
    if (v && g_fence->GetCompletedValue() < v)
    {
        g_fence->SetEventOnCompletion(v, g_fenceEvent);
        WaitForSingleObject(g_fenceEvent, 1000);
    }
}

static void ReleaseTargets()
{
    for (auto& f : g_frames) { WaitFence(f.fence); if (f.rt) { f.rt->Release(); f.rt = nullptr; } }
}

static void CreateTargets(IDXGISwapChain3* sc)
{
    UINT size = g_dev->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
    D3D12_CPU_DESCRIPTOR_HANDLE h = g_rtvHeap->GetCPUDescriptorHandleForHeapStart();
    for (UINT i = 0; i < g_frames.size(); i++)
    {
        g_frames[i].rtv = h;
        if (SUCCEEDED(sc->GetBuffer(i, IID_PPV_ARGS(&g_frames[i].rt))))
            g_dev->CreateRenderTargetView(g_frames[i].rt, nullptr, h);
        h.ptr += size;
    }
}

static void LoadLogo()
{
    HRSRC res = FindResourceW(g_self, MAKEINTRESOURCEW(IDR_LOGO), MAKEINTRESOURCEW(10));
    if (!res) return;
    const BYTE* px = (const BYTE*)LockResource(LoadResource(g_self, res));
    const UINT W = 96, H = 96;
    if (!px || SizeofResource(g_self, res) < W * H * 4) return;

    D3D12_HEAP_PROPERTIES hp{ D3D12_HEAP_TYPE_DEFAULT };
    D3D12_RESOURCE_DESC rd{};
    rd.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D; rd.Width = W; rd.Height = H; rd.DepthOrArraySize = 1;
    rd.MipLevels = 1; rd.Format = DXGI_FORMAT_R8G8B8A8_UNORM; rd.SampleDesc.Count = 1;
    if (FAILED(g_dev->CreateCommittedResource(&hp, D3D12_HEAP_FLAG_NONE, &rd, D3D12_RESOURCE_STATE_COPY_DEST, nullptr, IID_PPV_ARGS(&g_logo)))) return;

    UINT pitch = (W * 4 + D3D12_TEXTURE_DATA_PITCH_ALIGNMENT - 1) & ~(D3D12_TEXTURE_DATA_PITCH_ALIGNMENT - 1);
    D3D12_HEAP_PROPERTIES up{ D3D12_HEAP_TYPE_UPLOAD };
    D3D12_RESOURCE_DESC bd{};
    bd.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER; bd.Width = pitch * H; bd.Height = 1; bd.DepthOrArraySize = 1;
    bd.MipLevels = 1; bd.SampleDesc.Count = 1; bd.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
    ID3D12Resource* upload = nullptr;
    if (FAILED(g_dev->CreateCommittedResource(&up, D3D12_HEAP_FLAG_NONE, &bd, D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(&upload)))) { g_logo->Release(); g_logo = nullptr; return; }
    BYTE* map = nullptr;
    upload->Map(0, nullptr, (void**)&map);
    for (UINT y = 0; y < H; y++) memcpy(map + y * pitch, px + y * W * 4, W * 4);
    upload->Unmap(0, nullptr);

    auto& f = g_frames[0];
    f.alloc->Reset();
    g_list->Reset(f.alloc, nullptr);
    D3D12_TEXTURE_COPY_LOCATION dst{ g_logo, D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX }; dst.SubresourceIndex = 0;
    D3D12_TEXTURE_COPY_LOCATION src{ upload, D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT };
    src.PlacedFootprint.Footprint = { DXGI_FORMAT_R8G8B8A8_UNORM, W, H, 1, pitch };
    g_list->CopyTextureRegion(&dst, 0, 0, 0, &src, nullptr);
    D3D12_RESOURCE_BARRIER b{}; b.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    b.Transition = { g_logo, D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES, D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE };
    g_list->ResourceBarrier(1, &b);
    g_list->Close();
    ID3D12CommandList* lists[] = { g_list };
    oExec(g_queue, 1, lists);
    g_queue->Signal(g_fence, ++g_fenceVal);
    WaitFence(g_fenceVal);
    upload->Release();

    UINT inc = g_dev->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
    D3D12_CPU_DESCRIPTOR_HANDLE cpu = g_srvHeap->GetCPUDescriptorHandleForHeapStart(); cpu.ptr += inc;
    g_logoGpu = g_srvHeap->GetGPUDescriptorHandleForHeapStart(); g_logoGpu.ptr += inc;
    D3D12_SHADER_RESOURCE_VIEW_DESC sd{};
    sd.Format = DXGI_FORMAT_R8G8B8A8_UNORM; sd.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
    sd.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING; sd.Texture2D.MipLevels = 1;
    g_dev->CreateShaderResourceView(g_logo, &sd, cpu);
}

static void InitImGuiCommon()
{
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = nullptr;
    io.LogFilename = nullptr;
    io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;

    char fonts[MAX_PATH];
    GetWindowsDirectoryA(fonts, MAX_PATH);
    std::string bold = std::string(fonts) + "\\Fonts\\segoeuib.ttf";
    std::string semi = std::string(fonts) + "\\Fonts\\seguisb.ttf";
    ImFontConfig cfg; cfg.OversampleH = 2; cfg.OversampleV = 2;
    const ImWchar* ranges = io.Fonts->GetGlyphRangesDefault();
    if (GetFileAttributesA(semi.c_str()) != INVALID_FILE_ATTRIBUTES) io.Fonts->AddFontFromFileTTF(semi.c_str(), 18.0f, &cfg, ranges);
    else io.Fonts->AddFontDefault();
    if (GetFileAttributesA(bold.c_str()) != INVALID_FILE_ATTRIBUTES) io.Fonts->AddFontFromFileTTF(bold.c_str(), 24.0f, &cfg, ranges);

    ImGui_ImplWin32_Init(g_hwnd);
}

static bool InitOverlay(IDXGISwapChain3* sc)
{
    if (FAILED(sc->GetDevice(IID_PPV_ARGS(&g_dev)))) return false;
    DXGI_SWAP_CHAIN_DESC desc{};
    sc->GetDesc(&desc);
    g_hwnd = desc.OutputWindow;
    g_frames.resize(desc.BufferCount);

    D3D12_DESCRIPTOR_HEAP_DESC sh{ D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV, 2, D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE };
    D3D12_DESCRIPTOR_HEAP_DESC rh{ D3D12_DESCRIPTOR_HEAP_TYPE_RTV, desc.BufferCount, D3D12_DESCRIPTOR_HEAP_FLAG_NONE };
    if (FAILED(g_dev->CreateDescriptorHeap(&sh, IID_PPV_ARGS(&g_srvHeap)))) return false;
    if (FAILED(g_dev->CreateDescriptorHeap(&rh, IID_PPV_ARGS(&g_rtvHeap)))) return false;
    for (auto& f : g_frames)
        if (FAILED(g_dev->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&f.alloc)))) return false;
    if (FAILED(g_dev->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, g_frames[0].alloc, nullptr, IID_PPV_ARGS(&g_list)))) return false;
    g_list->Close();
    if (FAILED(g_dev->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&g_fence)))) return false;
    g_fenceEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    CreateTargets(sc);

    InitImGuiCommon();
    ImGui_ImplDX12_Init(g_dev, (int)desc.BufferCount, desc.BufferDesc.Format, g_srvHeap,
        g_srvHeap->GetCPUDescriptorHandleForHeapStart(), g_srvHeap->GetGPUDescriptorHandleForHeapStart());
    ImGui_ImplDX12_CreateDeviceObjects();
    LoadLogo();

    g_oWndProc = (WNDPROC)SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, (LONG_PTR)HookWndProc);
    Log("overlay ready (%u buffers, format %d)", desc.BufferCount, (int)desc.BufferDesc.Format);
    return true;
}

static void RenderFrame(IDXGISwapChain3* sc)
{
    {
        std::lock_guard<std::mutex> lk(g_msgLock);
        for (auto& m : g_msgs) ImGui_ImplWin32_WndProcHandler(g_hwnd, m.m, m.w, m.l);
        g_msgs.clear();
    }
    ImGui_ImplDX12_NewFrame();
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();
    Menu_HandleHotkeys();
    Menu_Draw();
    ImGui::Render();
    ImDrawData* dd = ImGui::GetDrawData();
    if (!dd || dd->CmdListsCount == 0) return;

    UINT idx = sc->GetCurrentBackBufferIndex();
    if (idx >= g_frames.size() || !g_frames[idx].rt) return;
    Frame& f = g_frames[idx];
    WaitFence(f.fence);
    f.alloc->Reset();
    g_list->Reset(f.alloc, nullptr);

    D3D12_RESOURCE_BARRIER b{}; b.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    b.Transition = { f.rt, D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES, D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_RENDER_TARGET };
    g_list->ResourceBarrier(1, &b);
    g_list->OMSetRenderTargets(1, &f.rtv, FALSE, nullptr);
    g_list->SetDescriptorHeaps(1, &g_srvHeap);
    ImGui_ImplDX12_RenderDrawData(dd, g_list);
    std::swap(b.Transition.StateBefore, b.Transition.StateAfter);
    g_list->ResourceBarrier(1, &b);
    g_list->Close();

    ID3D12CommandList* lists[] = { g_list };
    oExec(g_queue, 1, lists);
    g_queue->Signal(g_fence, ++g_fenceVal);
    f.fence = g_fenceVal;
}

// ---------------- DirectX 11 ----------------
static const BYTE* LogoPixels()
{
    HRSRC res = FindResourceW(g_self, MAKEINTRESOURCEW(IDR_LOGO), MAKEINTRESOURCEW(10));
    if (!res || SizeofResource(g_self, res) < 96 * 96 * 4) return nullptr;
    return (const BYTE*)LockResource(LoadResource(g_self, res));
}

static void CreateTarget11(IDXGISwapChain3* sc)
{
    ID3D11Texture2D* bb = nullptr;
    if (SUCCEEDED(sc->GetBuffer(0, IID_PPV_ARGS(&bb))))
    {
        g_dev11->CreateRenderTargetView(bb, nullptr, &g_rtv11);
        bb->Release();
    }
}

static bool InitOverlay11(IDXGISwapChain3* sc)
{
    g_ctx11 = nullptr;
    g_dev11->GetImmediateContext(&g_ctx11);
    DXGI_SWAP_CHAIN_DESC desc{};
    sc->GetDesc(&desc);
    g_hwnd = desc.OutputWindow;
    CreateTarget11(sc);
    InitImGuiCommon();
    ImGui_ImplDX11_Init(g_dev11, g_ctx11);
    if (const BYTE* px = LogoPixels())
    {
        D3D11_TEXTURE2D_DESC td{};
        td.Width = 96; td.Height = 96; td.MipLevels = 1; td.ArraySize = 1; td.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        td.SampleDesc.Count = 1; td.Usage = D3D11_USAGE_DEFAULT; td.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA init{ px, 96 * 4, 0 };
        ID3D11Texture2D* tex = nullptr;
        if (SUCCEEDED(g_dev11->CreateTexture2D(&td, &init, &tex)))
        {
            g_dev11->CreateShaderResourceView(tex, nullptr, &g_logo11);
            tex->Release();
        }
    }
    g_oWndProc = (WNDPROC)SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, (LONG_PTR)HookWndProc);
    Log("overlay ready (dx11, format %d)", (int)desc.BufferDesc.Format);
    return true;
}

static bool BuildFrame()
{
    {
        std::lock_guard<std::mutex> lk(g_msgLock);
        for (auto& m : g_msgs) ImGui_ImplWin32_WndProcHandler(g_hwnd, m.m, m.w, m.l);
        g_msgs.clear();
    }
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();
    Menu_HandleHotkeys();
    Menu_Draw();
    ImGui::Render();
    ImDrawData* dd = ImGui::GetDrawData();
    return dd && dd->CmdListsCount > 0;
}

static void RenderFrame11(IDXGISwapChain3* sc)
{
    if (!g_rtv11) CreateTarget11(sc);
    ImGui_ImplDX11_NewFrame();
    if (!BuildFrame() || !g_rtv11) return;
    g_ctx11->OMSetRenderTargets(1, &g_rtv11, nullptr);
    ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
}

// ---------------- hooks ----------------
static HRESULT STDMETHODCALLTYPE HkPresent(IDXGISwapChain3* sc, UINT sync, UINT flags)
{
    // DirectX 11 game?
    if (!g_ready && !g_failed && !g_dx11)
    {
        ID3D11Device* d11 = nullptr;
        if (SUCCEEDED(sc->GetDevice(IID_PPV_ARGS(&d11))) && d11)
        {
            g_dx11 = true;
            g_dev11 = d11;
            g_ready = InitOverlay11(sc);
            if (!g_ready) { g_failed = true; Log("overlay init failed (dx11)"); }
        }
    }
    if (g_dx11)
    {
        if (g_ready && !g_failed) RenderFrame11(sc);
        return oPresent(sc, sync, flags);
    }
    if (g_queue && !g_failed)
    {
        if (!g_ready)
        {
            g_ready = InitOverlay(sc);
            if (!g_ready) { g_failed = true; Log("overlay init failed"); }
        }
        if (g_ready)
        {
            if (!g_frames.empty() && !g_frames[0].rt) CreateTargets(sc);
            RenderFrame(sc);
        }
    }
    return oPresent(sc, sync, flags);
}

static HRESULT STDMETHODCALLTYPE HkResize(IDXGISwapChain3* sc, UINT count, UINT w, UINT h, DXGI_FORMAT fmt, UINT flags)
{
    if (g_dx11)
    {
        if (g_rtv11) { g_rtv11->Release(); g_rtv11 = nullptr; }
        return oResize(sc, count, w, h, fmt, flags);
    }
    if (g_ready) ReleaseTargets();
    HRESULT hr = oResize(sc, count, w, h, fmt, flags);
    if (g_ready && SUCCEEDED(hr) && count != 0 && count != g_frames.size())
    {
        Log("buffer count changed (%u -> %u), overlay off", (UINT)g_frames.size(), count);
        g_failed = true;
    }
    return hr;
}

static void STDMETHODCALLTYPE HkExec(ID3D12CommandQueue* q, UINT n, ID3D12CommandList* const* lists)
{
    if (!g_queue && q->GetDesc().Type == D3D12_COMMAND_LIST_TYPE_DIRECT) g_queue = q;
    oExec(q, n, lists);
}

bool Render_Hook()
{
    // A throwaway device + swap chain, only to read the vtable addresses.
    WNDCLASSEXW wc{ sizeof(wc), CS_HREDRAW | CS_VREDRAW, DefWindowProcW, 0, 0, GetModuleHandleW(nullptr) };
    wc.lpszClassName = L"EvilDragonDummy";
    RegisterClassExW(&wc);
    HWND wnd = CreateWindowW(wc.lpszClassName, L"", WS_OVERLAPPEDWINDOW, 0, 0, 100, 100, nullptr, nullptr, wc.hInstance, nullptr);

    bool ok = false;
    ID3D12Device* dev = nullptr; ID3D12CommandQueue* q = nullptr; IDXGIFactory4* fac = nullptr; IDXGISwapChain1* sc = nullptr;
    if (SUCCEEDED(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&dev))))
    {
        D3D12_COMMAND_QUEUE_DESC qd{ D3D12_COMMAND_LIST_TYPE_DIRECT };
        if (SUCCEEDED(dev->CreateCommandQueue(&qd, IID_PPV_ARGS(&q))) && SUCCEEDED(CreateDXGIFactory1(IID_PPV_ARGS(&fac))))
        {
            DXGI_SWAP_CHAIN_DESC1 sd{};
            sd.Width = 100; sd.Height = 100; sd.Format = DXGI_FORMAT_R8G8B8A8_UNORM; sd.SampleDesc.Count = 1;
            sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT; sd.BufferCount = 2; sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
            if (SUCCEEDED(fac->CreateSwapChainForHwnd(q, wnd, &sd, nullptr, nullptr, &sc)))
            {
                void** scv = *(void***)sc;
                void** qv = *(void***)q;
                MH_CreateHook(scv[8], (void*)HkPresent, (void**)&oPresent);
                MH_CreateHook(scv[13], (void*)HkResize, (void**)&oResize);
                MH_CreateHook(qv[10], (void*)HkExec, (void**)&oExec);
                ok = MH_EnableHook(MH_ALL_HOOKS) == MH_OK;
            }
        }
    }
    if (sc) sc->Release(); if (fac) fac->Release(); if (q) q->Release(); if (dev) dev->Release();
    DestroyWindow(wnd);
    UnregisterClassW(wc.lpszClassName, wc.hInstance);
    Log(ok ? "dx12 hooks installed" : "dx12 hooks FAILED");
    return ok;
}
