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

#endif // _WIN32
