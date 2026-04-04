// UniText GPU Upload — per-slice Texture2DArray upload without Apply()
//
// Architecture: C# collects all upload requests into a NativeArray, then issues
// a single IssuePluginEventAndData. The render thread callback receives a pointer
// to the batch header (count + array of requests) and processes them all at once.
// No shared mutable state — no race conditions.
//
// Platforms:
// - Windows: unitext_gpu.dll (D3D11 via UpdateSubresource, D3D12 via CopyTextureRegion)
// - Linux:   libunitext_gpu.so (OpenGL via glTexSubImage3D, Vulkan via vkCmdCopyBufferToImage)
// - macOS:   libunitext_gpu.dylib (Metal via replaceRegion — see unitext_gpu_metal.mm)
// - Android: libunitext_gpu.so (GLES3 via glTexSubImage3D, Vulkan via vkCmdCopyBufferToImage)
// - iOS:     libunitext_gpu.a (Metal — see unitext_gpu_metal.mm)
// - WebGL:   uses GpuUpload.jslib directly, this file is not compiled for WebGL

#include "unity_plugin_api/IUnityInterface.h"
#include "unity_plugin_api/IUnityGraphics.h"

#include <string.h> // memcpy

#ifdef _WIN32
#define UTEXPORT extern "C" __declspec(dllexport)
#else
#define UTEXPORT extern "C" __attribute__((visibility("default")))
#endif

#if defined(_WIN32) || defined(__ANDROID__) || defined(__linux__)
#define HAS_VULKAN_UPLOAD 1
#endif

// ============================================================================
// Upload Request — must match C# struct layout exactly (Sequential, Pack=1)
// ============================================================================

#pragma pack(push, 1)
struct GpuUploadRequest
{
    void* nativeTexPtr;     // 8 bytes (64-bit)
    void* pixelData;        // 8 bytes
    int width;
    int height;
    int sliceIndex;
    int mipLevel;
    int bytesPerPixel;
    int dstX;               // destination offset within slice (0 = whole-slice legacy)
    int dstY;
    int srcRowPitch;        // source stride in bytes (0 = tightly packed = width * bytesPerPixel)
};

struct GpuUploadBatch
{
    int count;
    int padding;            // align to 8 bytes
    // GpuUploadRequest requests[count] follows immediately
};
#pragma pack(pop)

// ============================================================================
// Forward declarations for platform-specific init/cleanup
// ============================================================================

#ifdef _WIN32
static void InitD3D12(IUnityInterfaces* interfaces);
static void ReleaseD3D11Context();
static void ReleaseD3D12Resources();
static void UploadD3D11(const GpuUploadRequest& r);
static void UploadD3D12Batch(const GpuUploadRequest* requests, int count);
#endif

#ifdef HAS_VULKAN_UPLOAD
static void InitVulkan(IUnityInterfaces* interfaces);
static void ReleaseVulkanResources();
static void UploadVulkanBatch(const GpuUploadRequest* requests, int count);
#endif

// ============================================================================
// Unity Plugin Lifecycle
// ============================================================================

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;

static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize)
    {
        auto renderer = s_Graphics->GetRenderer();
        (void)renderer;
#ifdef _WIN32
        if (renderer == kUnityGfxRendererD3D12)
            InitD3D12(s_UnityInterfaces);
#endif
#ifdef HAS_VULKAN_UPLOAD
        if (renderer == kUnityGfxRendererVulkan)
            InitVulkan(s_UnityInterfaces);
#endif
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
#ifdef _WIN32
        ReleaseD3D11Context();
        ReleaseD3D12Resources();
#endif
#ifdef HAS_VULKAN_UPLOAD
        ReleaseVulkanResources();
#endif
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();
    s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginUnload()
{
    if (s_Graphics)
        s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown);
    s_UnityInterfaces = nullptr;
    s_Graphics = nullptr;
}

UTEXPORT int ut_gpu_get_renderer()
{
    return s_Graphics ? (int)s_Graphics->GetRenderer() : -1;
}

// ============================================================================
// D3D11 (Windows)
// ============================================================================

#ifdef _WIN32
#include <d3d11.h>
#include "unity_plugin_api/IUnityGraphicsD3D11.h"

static ID3D11DeviceContext* s_d3d11Ctx = nullptr;

static void ReleaseD3D11Context()
{
    if (s_d3d11Ctx) { s_d3d11Ctx->Release(); s_d3d11Ctx = nullptr; }
}

static void EnsureD3D11Context()
{
    if (s_d3d11Ctx) return;
    auto* d3d = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
    if (!d3d) return;
    ID3D11Device* device = d3d->GetDevice();
    if (device) device->GetImmediateContext(&s_d3d11Ctx);
}

static void UploadD3D11(const GpuUploadRequest& r)
{
    EnsureD3D11Context();
    if (!s_d3d11Ctx) return;

    auto* tex = static_cast<ID3D11Texture2D*>(r.nativeTexPtr);
    D3D11_TEXTURE2D_DESC desc;
    tex->GetDesc(&desc);

    UINT subresource = D3D11CalcSubresource((UINT)r.mipLevel, (UINT)r.sliceIndex, desc.MipLevels);
    UINT rowPitch = (UINT)(r.srcRowPitch > 0 ? r.srcRowPitch : r.width * r.bytesPerPixel);

    D3D11_BOX box;
    box.left = (UINT)r.dstX;
    box.top = (UINT)r.dstY;
    box.front = 0;
    box.right = (UINT)(r.dstX + r.width);
    box.bottom = (UINT)(r.dstY + r.height);
    box.back = 1;
    s_d3d11Ctx->UpdateSubresource(tex, subresource, &box, r.pixelData, rowPitch, 0);
}

// ============================================================================
// D3D12 (Windows)
// ============================================================================

#include "unity_plugin_api/IUnityGraphicsD3D12.h"

static IUnityGraphicsD3D12v7* s_D3D12 = nullptr;
static ID3D12Device* s_D3D12Device = nullptr;

static const int D3D12_RING = 3;
static ID3D12CommandAllocator* s_D3D12Alloc[D3D12_RING] = {};
static ID3D12GraphicsCommandList* s_D3D12CmdList = nullptr;
static ID3D12Resource* s_D3D12Upload[D3D12_RING] = {};
static size_t s_D3D12UploadCap[D3D12_RING] = {};
static UINT64 s_D3D12Fence[D3D12_RING] = {};
static int s_D3D12Frame = 0;

static void InitD3D12(IUnityInterfaces* interfaces)
{
    s_D3D12 = interfaces->Get<IUnityGraphicsD3D12v7>();
    if (!s_D3D12) return;
    s_D3D12Device = s_D3D12->GetDevice();
    if (!s_D3D12Device) { s_D3D12 = nullptr; return; }

    UnityD3D12PluginEventConfig config = {};
    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
    config.flags = kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
    config.ensureActiveRenderTextureIsBound = false;
    s_D3D12->ConfigureEvent(0, &config);

    for (int i = 0; i < D3D12_RING; i++)
    {
        HRESULT hr = s_D3D12Device->CreateCommandAllocator(
            D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&s_D3D12Alloc[i]));
        if (FAILED(hr)) { s_D3D12 = nullptr; return; }
    }

    HRESULT hr = s_D3D12Device->CreateCommandList(0,
        D3D12_COMMAND_LIST_TYPE_DIRECT, s_D3D12Alloc[0], nullptr,
        IID_PPV_ARGS(&s_D3D12CmdList));
    if (FAILED(hr)) { s_D3D12 = nullptr; return; }
    s_D3D12CmdList->Close();
}

static void ReleaseD3D12Resources()
{
    if (s_D3D12CmdList) { s_D3D12CmdList->Release(); s_D3D12CmdList = nullptr; }
    for (int i = 0; i < D3D12_RING; i++)
    {
        if (s_D3D12Upload[i]) { s_D3D12Upload[i]->Release(); s_D3D12Upload[i] = nullptr; }
        if (s_D3D12Alloc[i]) { s_D3D12Alloc[i]->Release(); s_D3D12Alloc[i] = nullptr; }
        s_D3D12UploadCap[i] = 0;
        s_D3D12Fence[i] = 0;
    }
    s_D3D12Device = nullptr;
    s_D3D12 = nullptr;
}

static void EnsureD3D12UploadBuffer(int slot, size_t requiredSize)
{
    if (s_D3D12Upload[slot] && s_D3D12UploadCap[slot] >= requiredSize) return;
    if (s_D3D12Upload[slot]) { s_D3D12Upload[slot]->Release(); s_D3D12Upload[slot] = nullptr; }

    D3D12_HEAP_PROPERTIES heapProps = {};
    heapProps.Type = D3D12_HEAP_TYPE_UPLOAD;

    D3D12_RESOURCE_DESC desc = {};
    desc.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
    desc.Width = requiredSize;
    desc.Height = 1;
    desc.DepthOrArraySize = 1;
    desc.MipLevels = 1;
    desc.Format = DXGI_FORMAT_UNKNOWN;
    desc.SampleDesc.Count = 1;
    desc.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;

    s_D3D12Device->CreateCommittedResource(
        &heapProps, D3D12_HEAP_FLAG_NONE,
        &desc, D3D12_RESOURCE_STATE_GENERIC_READ,
        nullptr, IID_PPV_ARGS(&s_D3D12Upload[slot]));
    s_D3D12UploadCap[slot] = requiredSize;
}

static void UploadD3D12Batch(const GpuUploadRequest* requests, int count)
{
    if (!s_D3D12 || !s_D3D12Device || count == 0) return;

    int slot = s_D3D12Frame % D3D12_RING;

    // Wait for this slot's previous GPU work to finish
    if (s_D3D12Fence[slot] > 0)
    {
        ID3D12Fence* fence = s_D3D12->GetFrameFence();
        if (fence && fence->GetCompletedValue() < s_D3D12Fence[slot])
        {
            HANDLE event = CreateEvent(nullptr, FALSE, FALSE, nullptr);
            if (event)
            {
                fence->SetEventOnCompletion(s_D3D12Fence[slot], event);
                WaitForSingleObject(event, 5000);
                CloseHandle(event);
            }
        }
    }

    // D3D12_TEXTURE_DATA_PITCH_ALIGNMENT = 256, PLACEMENT_ALIGNMENT = 512
    const UINT PITCH_ALIGN = 256;
    const size_t PLACE_ALIGN = 512;

    // Calculate total upload buffer size and per-request info
    struct ReqInfo { size_t offset; UINT rowPitch; };
    ReqInfo infos[64];
    size_t totalSize = 0;
    int n = count < 64 ? count : 64;

    for (int i = 0; i < n; i++)
    {
        UINT rowPitch = ((requests[i].width * requests[i].bytesPerPixel) + PITCH_ALIGN - 1) & ~(PITCH_ALIGN - 1);
        size_t reqSize = (size_t)rowPitch * requests[i].height;
        infos[i] = { totalSize, rowPitch };
        totalSize += (reqSize + PLACE_ALIGN - 1) & ~(PLACE_ALIGN - 1);
    }

    EnsureD3D12UploadBuffer(slot, totalSize);
    if (!s_D3D12Upload[slot]) return;

    void* mapped = nullptr;
    s_D3D12Upload[slot]->Map(0, nullptr, &mapped);
    for (int i = 0; i < n; i++)
    {
        int srcRow = requests[i].width * requests[i].bytesPerPixel;
        int srcStride = requests[i].srcRowPitch > 0 ? requests[i].srcRowPitch : srcRow;
        for (int y = 0; y < requests[i].height; y++)
        {
            memcpy((char*)mapped + infos[i].offset + (size_t)y * infos[i].rowPitch,
                   (char*)requests[i].pixelData + (size_t)y * srcStride,
                   srcRow);
        }
    }
    D3D12_RANGE written = { 0, totalSize };
    s_D3D12Upload[slot]->Unmap(0, &written);

    // Record copy commands
    s_D3D12Alloc[slot]->Reset();
    s_D3D12CmdList->Reset(s_D3D12Alloc[slot], nullptr);

    // Track unique textures for resource state declarations
    void* uniqueRes[64] = {};
    int numUnique = 0;

    for (int i = 0; i < n; i++)
    {
        auto* resource = static_cast<ID3D12Resource*>(requests[i].nativeTexPtr);
        D3D12_RESOURCE_DESC texDesc = resource->GetDesc();
        UINT sub = requests[i].mipLevel + requests[i].sliceIndex * texDesc.MipLevels;

        // Barrier: COMMON -> COPY_DEST
        D3D12_RESOURCE_BARRIER barrier = {};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition.pResource = resource;
        barrier.Transition.Subresource = sub;
        barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_COMMON;
        barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_COPY_DEST;
        s_D3D12CmdList->ResourceBarrier(1, &barrier);

        // Source: upload buffer
        D3D12_TEXTURE_COPY_LOCATION src = {};
        src.pResource = s_D3D12Upload[slot];
        src.Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;
        src.PlacedFootprint.Offset = infos[i].offset;
        src.PlacedFootprint.Footprint.Format = texDesc.Format;
        src.PlacedFootprint.Footprint.Width = requests[i].width;
        src.PlacedFootprint.Footprint.Height = requests[i].height;
        src.PlacedFootprint.Footprint.Depth = 1;
        src.PlacedFootprint.Footprint.RowPitch = infos[i].rowPitch;

        // Dest: texture subresource
        D3D12_TEXTURE_COPY_LOCATION dst = {};
        dst.pResource = resource;
        dst.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
        dst.SubresourceIndex = sub;

        s_D3D12CmdList->CopyTextureRegion(&dst, (UINT)requests[i].dstX, (UINT)requests[i].dstY, 0, &src, nullptr);

        // Barrier: COPY_DEST -> COMMON
        barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_COPY_DEST;
        barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_COMMON;
        s_D3D12CmdList->ResourceBarrier(1, &barrier);

        // Collect unique resources
        bool found = false;
        for (int j = 0; j < numUnique; j++)
            if (uniqueRes[j] == resource) { found = true; break; }
        if (!found && numUnique < 64)
            uniqueRes[numUnique++] = resource;
    }

    s_D3D12CmdList->Close();

    // Tell Unity what resource states we expect/leave
    UnityGraphicsD3D12ResourceState states[64];
    for (int i = 0; i < numUnique; i++)
    {
        states[i].resource = static_cast<ID3D12Resource*>(uniqueRes[i]);
        states[i].expected = D3D12_RESOURCE_STATE_COMMON;
        states[i].current = D3D12_RESOURCE_STATE_COMMON;
    }

    s_D3D12Fence[slot] = s_D3D12->ExecuteCommandList(s_D3D12CmdList, numUnique, states);
    s_D3D12Frame++;
}

#endif // _WIN32

// ============================================================================
// OpenGL / OpenGL ES
// ============================================================================

#if defined(__ANDROID__)
#include <GLES3/gl3.h>
#define HAS_GL_UPLOAD 1
#elif defined(__linux__) && !defined(__ANDROID__) && __has_include(<GL/gl.h>)
#include <GL/gl.h>
#include <GL/glext.h>
#define HAS_GL_UPLOAD 1
#endif

#ifdef HAS_GL_UPLOAD
static void UploadOpenGL(const GpuUploadRequest& r)
{
    unsigned int texId = (unsigned int)(uintptr_t)r.nativeTexPtr;
    glBindTexture(GL_TEXTURE_2D_ARRAY, texId);

    GLenum format, type;
    if (r.bytesPerPixel == 4) { format = GL_RGBA; type = GL_UNSIGNED_BYTE; }
    else if (r.bytesPerPixel == 2) { format = GL_RED; type = GL_HALF_FLOAT; }
    else if (r.bytesPerPixel == 8) { format = GL_RGBA; type = GL_HALF_FLOAT; }
    else return;

    if (r.srcRowPitch > 0)
        glPixelStorei(GL_UNPACK_ROW_LENGTH, r.srcRowPitch / r.bytesPerPixel);

    glTexSubImage3D(GL_TEXTURE_2D_ARRAY, r.mipLevel,
        r.dstX, r.dstY, r.sliceIndex, r.width, r.height, 1,
        format, type, r.pixelData);

    if (r.srcRowPitch > 0)
        glPixelStorei(GL_UNPACK_ROW_LENGTH, 0);
}
#endif

// ============================================================================
// Vulkan (Android + Linux)
// ============================================================================

#ifdef HAS_VULKAN_UPLOAD

#include "unity_plugin_api/IUnityGraphicsVulkan.h"

static IUnityGraphicsVulkan* s_Vulkan = nullptr;
static VkDevice s_VkDevice = VK_NULL_HANDLE;
static VkPhysicalDevice s_VkPhysDevice = VK_NULL_HANDLE;

// Dynamically loaded Vulkan functions
static PFN_vkCreateBuffer                    s_vkCreateBuffer;
static PFN_vkGetBufferMemoryRequirements     s_vkGetBufferMemoryRequirements;
static PFN_vkAllocateMemory                  s_vkAllocateMemory;
static PFN_vkBindBufferMemory               s_vkBindBufferMemory;
static PFN_vkMapMemory                       s_vkMapMemory;
static PFN_vkDestroyBuffer                   s_vkDestroyBuffer;
static PFN_vkFreeMemory                      s_vkFreeMemory;
static PFN_vkCmdCopyBufferToImage           s_vkCmdCopyBufferToImage;
static PFN_vkGetPhysicalDeviceMemoryProperties s_vkGetPhysicalDeviceMemoryProperties;
static PFN_vkFlushMappedMemoryRanges         s_vkFlushMappedMemoryRanges;

static VkPhysicalDeviceMemoryProperties s_VkMemProps = {};

// Triple-buffered staging to avoid GPU/CPU conflicts
static const int VK_RING = 3;
struct VkStagingSlot
{
    VkBuffer buffer;
    VkDeviceMemory memory;
    void* mapped;
    size_t capacity;
    bool coherent;
};
static VkStagingSlot s_VkStaging[VK_RING] = {};
static int s_VkFrame = 0;

#define VK_LOAD(inst, fn) s_##fn = (PFN_##fn)(inst).getInstanceProcAddr((inst).instance, #fn)

static void InitVulkan(IUnityInterfaces* interfaces)
{
    s_Vulkan = interfaces->Get<IUnityGraphicsVulkan>();
    if (!s_Vulkan) return;

    auto inst = s_Vulkan->Instance();
    s_VkDevice = inst.device;
    s_VkPhysDevice = inst.physicalDevice;
    if (!s_VkDevice) { s_Vulkan = nullptr; return; }

    VK_LOAD(inst, vkCreateBuffer);
    VK_LOAD(inst, vkGetBufferMemoryRequirements);
    VK_LOAD(inst, vkAllocateMemory);
    VK_LOAD(inst, vkBindBufferMemory);
    VK_LOAD(inst, vkMapMemory);
    VK_LOAD(inst, vkDestroyBuffer);
    VK_LOAD(inst, vkFreeMemory);
    VK_LOAD(inst, vkCmdCopyBufferToImage);
    VK_LOAD(inst, vkGetPhysicalDeviceMemoryProperties);
    VK_LOAD(inst, vkFlushMappedMemoryRanges);

    if (!s_vkCreateBuffer || !s_vkCmdCopyBufferToImage)
    {
        s_Vulkan = nullptr;
        return;
    }

    s_vkGetPhysicalDeviceMemoryProperties(s_VkPhysDevice, &s_VkMemProps);

    // Configure our batch event to run outside render pass
    UnityVulkanPluginEventConfig config = {};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
    config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission
                 | kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
    s_Vulkan->ConfigureEvent(0, &config);
}

static void ReleaseVulkanResources()
{
    if (s_VkDevice)
    {
        for (int i = 0; i < VK_RING; i++)
        {
            if (s_VkStaging[i].buffer)
            {
                s_vkDestroyBuffer(s_VkDevice, s_VkStaging[i].buffer, nullptr);
                s_vkFreeMemory(s_VkDevice, s_VkStaging[i].memory, nullptr);
            }
            s_VkStaging[i] = {};
        }
    }
    s_VkDevice = VK_NULL_HANDLE;
    s_VkPhysDevice = VK_NULL_HANDLE;
    s_Vulkan = nullptr;
}

static uint32_t FindVkMemoryType(uint32_t typeBits, VkMemoryPropertyFlags desired)
{
    for (uint32_t i = 0; i < s_VkMemProps.memoryTypeCount; i++)
        if ((typeBits & (1u << i)) && (s_VkMemProps.memoryTypes[i].propertyFlags & desired) == desired)
            return i;
    return ~0u;
}

static bool EnsureVkStagingBuffer(int slot, size_t requiredSize)
{
    if (s_VkStaging[slot].capacity >= requiredSize) return true;

    // Destroy old
    if (s_VkStaging[slot].buffer)
    {
        s_vkDestroyBuffer(s_VkDevice, s_VkStaging[slot].buffer, nullptr);
        s_vkFreeMemory(s_VkDevice, s_VkStaging[slot].memory, nullptr);
        s_VkStaging[slot] = {};
    }

    VkBufferCreateInfo bufCI = {};
    bufCI.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
    bufCI.size = requiredSize;
    bufCI.usage = VK_BUFFER_USAGE_TRANSFER_SRC_BIT;
    bufCI.sharingMode = VK_SHARING_MODE_EXCLUSIVE;

    if (s_vkCreateBuffer(s_VkDevice, &bufCI, nullptr, &s_VkStaging[slot].buffer) != VK_SUCCESS)
        return false;

    VkMemoryRequirements memReqs;
    s_vkGetBufferMemoryRequirements(s_VkDevice, s_VkStaging[slot].buffer, &memReqs);

    // Prefer HOST_VISIBLE + HOST_COHERENT, fall back to just HOST_VISIBLE
    bool coherent = true;
    uint32_t memType = FindVkMemoryType(memReqs.memoryTypeBits,
        VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    if (memType == ~0u)
    {
        memType = FindVkMemoryType(memReqs.memoryTypeBits, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT);
        coherent = false;
    }
    if (memType == ~0u) return false;

    VkMemoryAllocateInfo allocInfo = {};
    allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
    allocInfo.allocationSize = memReqs.size;
    allocInfo.memoryTypeIndex = memType;

    if (s_vkAllocateMemory(s_VkDevice, &allocInfo, nullptr, &s_VkStaging[slot].memory) != VK_SUCCESS)
        return false;

    s_vkBindBufferMemory(s_VkDevice, s_VkStaging[slot].buffer, s_VkStaging[slot].memory, 0);
    s_vkMapMemory(s_VkDevice, s_VkStaging[slot].memory, 0, requiredSize, 0, &s_VkStaging[slot].mapped);
    s_VkStaging[slot].capacity = requiredSize;
    s_VkStaging[slot].coherent = coherent;
    return true;
}

static void UploadVulkanBatch(const GpuUploadRequest* requests, int count)
{
    if (!s_Vulkan || count == 0) return;

    int slot = s_VkFrame % VK_RING;
    s_VkFrame++;

    // Calculate total staging size
    size_t totalSize = 0;
    for (int i = 0; i < count; i++)
        totalSize += (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;

    if (!EnsureVkStagingBuffer(slot, totalSize)) return;

    size_t offset = 0;
    for (int i = 0; i < count; i++)
    {
        int srcRow = requests[i].width * requests[i].bytesPerPixel;
        int srcStride = requests[i].srcRowPitch > 0 ? requests[i].srcRowPitch : srcRow;
        if (srcStride == srcRow)
        {
            size_t sz = (size_t)srcRow * requests[i].height;
            memcpy((char*)s_VkStaging[slot].mapped + offset, requests[i].pixelData, sz);
            offset += sz;
        }
        else
        {
            for (int y = 0; y < requests[i].height; y++)
            {
                memcpy((char*)s_VkStaging[slot].mapped + offset + (size_t)y * srcRow,
                       (char*)requests[i].pixelData + (size_t)y * srcStride,
                       srcRow);
            }
            offset += (size_t)srcRow * requests[i].height;
        }
    }

    // Flush if not coherent
    if (!s_VkStaging[slot].coherent && s_vkFlushMappedMemoryRanges)
    {
        VkMappedMemoryRange range = {};
        range.sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
        range.memory = s_VkStaging[slot].memory;
        range.offset = 0;
        range.size = VK_WHOLE_SIZE;
        s_vkFlushMappedMemoryRanges(s_VkDevice, 1, &range);
    }

    // Record copy commands
    offset = 0;
    for (int i = 0; i < count; i++)
    {
        // AccessTexture handles layout transition + pipeline barrier
        UnityVulkanImage image;
        if (!s_Vulkan->AccessTexture(
            requests[i].nativeTexPtr, UnityVulkanWholeImage,
            VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            VK_PIPELINE_STAGE_TRANSFER_BIT,
            VK_ACCESS_TRANSFER_WRITE_BIT,
            kUnityVulkanResourceAccess_PipelineBarrier,
            &image))
        {
            size_t sz = (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;
            offset += sz;
            continue;
        }

        // Must re-query after AccessTexture (it may record commands)
        UnityVulkanRecordingState state;
        if (!s_Vulkan->CommandRecordingState(&state, kUnityVulkanGraphicsQueueAccess_DontCare))
        {
            size_t sz = (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;
            offset += sz;
            continue;
        }

        VkBufferImageCopy region = {};
        region.bufferOffset = offset;
        region.bufferRowLength = 0;     // tightly packed
        region.bufferImageHeight = 0;   // tightly packed
        region.imageSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
        region.imageSubresource.mipLevel = requests[i].mipLevel;
        region.imageSubresource.baseArrayLayer = requests[i].sliceIndex;
        region.imageSubresource.layerCount = 1;
        region.imageOffset = { requests[i].dstX, requests[i].dstY, 0 };
        region.imageExtent = { (uint32_t)requests[i].width, (uint32_t)requests[i].height, 1 };

        s_vkCmdCopyBufferToImage(state.commandBuffer,
            s_VkStaging[slot].buffer, image.image,
            VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &region);

        size_t sz = (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;
        offset += sz;
    }
}

#endif // HAS_VULKAN_UPLOAD

// ============================================================================
// Metal (implemented in unitext_gpu_metal.mm)
// ============================================================================

#if defined(__APPLE__)
extern "C" void UploadMetal(const GpuUploadRequest& req);
#endif

// ============================================================================
// Dispatch — processes entire batch from IssuePluginEventAndData
// ============================================================================

static void UNITY_INTERFACE_API OnGpuUploadBatchEvent(int eventId, void* data)
{
    if (!s_Graphics || !data) return;

    auto* batch = static_cast<const GpuUploadBatch*>(data);
    auto* requests = reinterpret_cast<const GpuUploadRequest*>(batch + 1);
    int count = batch->count;
    if (count <= 0) return;

    auto renderer = s_Graphics->GetRenderer();

    switch (renderer)
    {
#ifdef _WIN32
    case kUnityGfxRendererD3D11:
        for (int i = 0; i < count; i++)
        {
            auto& r = requests[i];
            if (!r.nativeTexPtr || !r.pixelData) continue;
            UploadD3D11(r);
        }
        break;

    case kUnityGfxRendererD3D12:
        UploadD3D12Batch(requests, count);
        break;
#endif

#ifdef HAS_GL_UPLOAD
    case kUnityGfxRendererOpenGLCore:
    case kUnityGfxRendererOpenGLES30:
        for (int i = 0; i < count; i++)
        {
            auto& r = requests[i];
            if (!r.nativeTexPtr || !r.pixelData) continue;
            UploadOpenGL(r);
        }
        break;
#endif

#ifdef HAS_VULKAN_UPLOAD
    case kUnityGfxRendererVulkan:
        UploadVulkanBatch(requests, count);
        break;
#endif

#if defined(__APPLE__)
    case kUnityGfxRendererMetal:
        for (int i = 0; i < count; i++)
        {
            auto& r = requests[i];
            if (!r.nativeTexPtr || !r.pixelData) continue;
            UploadMetal(r);
        }
        break;
#endif

    default:
        break;
    }
}

UTEXPORT UnityRenderingEventAndData ut_gpu_get_upload_batch_event()
{
    if (!s_Graphics) return nullptr;
    auto renderer = s_Graphics->GetRenderer();
#ifdef _WIN32
    if (renderer == kUnityGfxRendererD3D12 && !s_D3D12) return nullptr;
#endif
#ifdef HAS_VULKAN_UPLOAD
    if (renderer == kUnityGfxRendererVulkan && !s_Vulkan) return nullptr;
#endif
    return OnGpuUploadBatchEvent;
}
