#pragma once
#include "IUnityInterface.h"

#ifdef _WIN32
#include <d3d12.h>

typedef struct UnityGraphicsD3D12ResourceState UnityGraphicsD3D12ResourceState;
struct UnityGraphicsD3D12ResourceState
{
    ID3D12Resource*       resource;
    D3D12_RESOURCE_STATES expected;
    D3D12_RESOURCE_STATES current;
};

// IUnityGraphicsD3D12v2 — available since Unity 2017
UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D12v2)
{
    ID3D12Device* (UNITY_INTERFACE_API * GetDevice)();
    ID3D12Fence* (UNITY_INTERFACE_API * GetFrameFence)();
    UINT64(UNITY_INTERFACE_API * GetNextFrameFenceValue)();
    UINT64(UNITY_INTERFACE_API * ExecuteCommandList)(ID3D12GraphicsCommandList * commandList, int stateCount, UnityGraphicsD3D12ResourceState * states);
};
UNITY_REGISTER_INTERFACE_GUID(0xEC39D2F18446C745ULL, 0xB1A2626641D6B11FULL, IUnityGraphicsD3D12v2)

// IUnityGraphicsD3D12v7 — available since Unity 2019.3
// Adds ConfigureEvent for proper plugin event synchronization on D3D12.

struct UnityGraphicsD3D12RecordingState
{
    ID3D12GraphicsCommandList* commandList;
};

enum UnityD3D12GraphicsQueueAccess
{
    kUnityD3D12GraphicsQueueAccess_DontCare,
    kUnityD3D12GraphicsQueueAccess_Allow,
};

enum UnityD3D12EventConfigFlagBits
{
    kUnityD3D12EventConfigFlag_EnsurePreviousFrameSubmission = (1 << 0),
    kUnityD3D12EventConfigFlag_FlushCommandBuffers = (1 << 1),
    kUnityD3D12EventConfigFlag_SyncWorkerThreads = (1 << 2),
    kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState = (1 << 3),
};

struct UnityD3D12PluginEventConfig
{
    UnityD3D12GraphicsQueueAccess graphicsQueueAccess;
    UINT32 flags;
    bool ensureActiveRenderTextureIsBound;
};

struct RenderSurfaceBase;
typedef struct RenderSurfaceBase* UnityRenderBuffer;

UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D12v7)
{
    ID3D12Device* (UNITY_INTERFACE_API * GetDevice)();
    void* (UNITY_INTERFACE_API * GetSwapChain)();
    UINT32(UNITY_INTERFACE_API * GetSyncInterval)();
    UINT(UNITY_INTERFACE_API * GetPresentFlags)();
    ID3D12Fence* (UNITY_INTERFACE_API * GetFrameFence)();
    UINT64(UNITY_INTERFACE_API * GetNextFrameFenceValue)();
    UINT64(UNITY_INTERFACE_API * ExecuteCommandList)(ID3D12GraphicsCommandList * commandList, int stateCount, UnityGraphicsD3D12ResourceState * states);
    void(UNITY_INTERFACE_API * SetPhysicalVideoMemoryControlValues)(const void * memInfo);
    ID3D12CommandQueue* (UNITY_INTERFACE_API * GetCommandQueue)();
    void* (UNITY_INTERFACE_API * TextureFromRenderBuffer)(UnityRenderBuffer rb);
    void* (UNITY_INTERFACE_API * TextureFromNativeTexture)(void * texture);
    void(UNITY_INTERFACE_API * ConfigureEvent)(int eventID, const UnityD3D12PluginEventConfig * pluginEventConfig);
    bool(UNITY_INTERFACE_API * CommandRecordingState)(UnityGraphicsD3D12RecordingState * outCommandRecordingState);
};
UNITY_REGISTER_INTERFACE_GUID(0x4624B0DA41B64AACULL, 0x915AABCB9BC3F0D3ULL, IUnityGraphicsD3D12v7)

#endif // _WIN32
