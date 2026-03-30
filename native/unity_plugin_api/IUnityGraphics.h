// Unity Native Plugin API copyright © 2015 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.

#pragma once
#include "IUnityInterface.h"

typedef enum UnityGfxRenderer
{
    kUnityGfxRendererD3D11             =  2,
    kUnityGfxRendererNull              =  4,
    kUnityGfxRendererOpenGLES30        = 11,
    kUnityGfxRendererPS4               = 13,
    kUnityGfxRendererXboxOne           = 14,
    kUnityGfxRendererMetal             = 16,
    kUnityGfxRendererOpenGLCore        = 17,
    kUnityGfxRendererD3D12             = 18,
    kUnityGfxRendererVulkan            = 21,
    kUnityGfxRendererNvn               = 22,
    kUnityGfxRendererXboxOneD3D12      = 23,
    kUnityGfxRendererGameCoreXboxOne   = 24,
    kUnityGfxRendererGameCoreXboxSeries  = 25,
    kUnityGfxRendererPS5               = 26,
    kUnityGfxRendererPS5NGGC           = 27
} UnityGfxRenderer;

typedef enum UnityGfxDeviceEventType
{
    kUnityGfxDeviceEventInitialize     = 0,
    kUnityGfxDeviceEventShutdown       = 1,
    kUnityGfxDeviceEventBeforeReset    = 2,
    kUnityGfxDeviceEventAfterReset     = 3,
} UnityGfxDeviceEventType;

typedef void (UNITY_INTERFACE_API * IUnityGraphicsDeviceEventCallback)(UnityGfxDeviceEventType eventType);

UNITY_DECLARE_INTERFACE(IUnityGraphics)
{
    UnityGfxRenderer(UNITY_INTERFACE_API * GetRenderer)();
    void(UNITY_INTERFACE_API * RegisterDeviceEventCallback)(IUnityGraphicsDeviceEventCallback callback);
    void(UNITY_INTERFACE_API * UnregisterDeviceEventCallback)(IUnityGraphicsDeviceEventCallback callback);
    int(UNITY_INTERFACE_API * ReserveEventIDRange)(int count);
};
UNITY_REGISTER_INTERFACE_GUID(0x7CBA0A9CA4DDB544ULL, 0x8C5AD4926EB17B11ULL, IUnityGraphics)

typedef void (UNITY_INTERFACE_API * UnityRenderingEvent)(int eventId);
typedef void (UNITY_INTERFACE_API * UnityRenderingEventAndData)(int eventId, void* data);
