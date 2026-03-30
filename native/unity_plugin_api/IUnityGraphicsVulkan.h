#pragma once
#include "IUnityInterface.h"
#include "IUnityGraphics.h"

#ifndef VK_NO_PROTOTYPES
#define VK_NO_PROTOTYPES
#endif

#ifndef UNITY_VULKAN_HEADER
#define UNITY_VULKAN_HEADER <vulkan/vulkan.h>
#endif

#include UNITY_VULKAN_HEADER

struct UnityVulkanInstance
{
    VkPipelineCache pipelineCache;
    VkInstance instance;
    VkPhysicalDevice physicalDevice;
    VkDevice device;
    VkQueue graphicsQueue;
    PFN_vkGetInstanceProcAddr getInstanceProcAddr;
    unsigned int queueFamilyIndex;
    void* reserved[8];
};

struct UnityVulkanMemory
{
    VkDeviceMemory memory;
    VkDeviceSize offset;
    VkDeviceSize size;
    void* mapped;
    VkMemoryPropertyFlags flags;
    unsigned int memoryTypeIndex;
    void* reserved[4];
};

enum UnityVulkanResourceAccessMode
{
    kUnityVulkanResourceAccess_ObserveOnly,
    kUnityVulkanResourceAccess_PipelineBarrier,
    kUnityVulkanResourceAccess_Recreate,
};

struct UnityVulkanImage
{
    UnityVulkanMemory memory;
    VkImage image;
    VkImageLayout layout;
    VkImageAspectFlags aspect;
    VkImageUsageFlags usage;
    VkFormat format;
    VkExtent3D extent;
    VkImageTiling tiling;
    VkImageType type;
    VkSampleCountFlagBits samples;
    int layers;
    int mipCount;
    void* reserved[4];
};

struct UnityVulkanBuffer
{
    UnityVulkanMemory memory;
    VkBuffer buffer;
    size_t sizeInBytes;
    VkBufferUsageFlags usage;
    void* reserved[4];
};

struct UnityVulkanRecordingState
{
    VkCommandBuffer commandBuffer;
    VkCommandBufferLevel commandBufferLevel;
    VkRenderPass renderPass;
    VkFramebuffer framebuffer;
    int subPassIndex;
    unsigned long long currentFrameNumber;
    unsigned long long safeFrameNumber;
    void* reserved[4];
};

enum UnityVulkanEventRenderPassPreCondition
{
    kUnityVulkanRenderPass_DontCare,
    kUnityVulkanRenderPass_EnsureInside,
    kUnityVulkanRenderPass_EnsureOutside
};

enum UnityVulkanGraphicsQueueAccess
{
    kUnityVulkanGraphicsQueueAccess_DontCare,
    kUnityVulkanGraphicsQueueAccess_Allow,
};

enum UnityVulkanEventConfigFlagBits
{
    kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission = (1 << 0),
    kUnityVulkanEventConfigFlag_FlushCommandBuffers = (1 << 1),
    kUnityVulkanEventConfigFlag_SyncWorkerThreads = (1 << 2),
    kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState = (1 << 3),
};

struct UnityVulkanPluginEventConfig
{
    UnityVulkanEventRenderPassPreCondition renderPassPrecondition;
    UnityVulkanGraphicsQueueAccess graphicsQueueAccess;
    uint32_t flags;
};

const VkImageSubresource* const UnityVulkanWholeImage = NULL;

typedef PFN_vkGetInstanceProcAddr(UNITY_INTERFACE_API * UnityVulkanInitCallback)(PFN_vkGetInstanceProcAddr getInstanceProcAddr, void* userdata);

enum UnityVulkanSwapchainMode
{
    kUnityVulkanSwapchainMode_Default,
    kUnityVulkanSwapchainMode_Offscreen
};

struct UnityVulkanSwapchainConfiguration
{
    UnityVulkanSwapchainMode mode;
};

UNITY_DECLARE_INTERFACE(IUnityGraphicsVulkan)
{
    bool(UNITY_INTERFACE_API * InterceptInitialization)(UnityVulkanInitCallback func, void* userdata);
    PFN_vkVoidFunction(UNITY_INTERFACE_API * InterceptVulkanAPI)(const char* name, PFN_vkVoidFunction func);
    void(UNITY_INTERFACE_API * ConfigureEvent)(int eventID, const UnityVulkanPluginEventConfig * pluginEventConfig);
    UnityVulkanInstance(UNITY_INTERFACE_API * Instance)();
    bool(UNITY_INTERFACE_API * CommandRecordingState)(UnityVulkanRecordingState * outCommandRecordingState, UnityVulkanGraphicsQueueAccess queueAccess);
    bool(UNITY_INTERFACE_API * AccessTexture)(void* nativeTexture, const VkImageSubresource * subResource, VkImageLayout layout,
        VkPipelineStageFlags pipelineStageFlags, VkAccessFlags accessFlags, UnityVulkanResourceAccessMode accessMode, UnityVulkanImage * outImage);
    bool(UNITY_INTERFACE_API * AccessRenderBufferTexture)(UnityRenderBuffer nativeRenderBuffer, const VkImageSubresource * subResource, VkImageLayout layout,
        VkPipelineStageFlags pipelineStageFlags, VkAccessFlags accessFlags, UnityVulkanResourceAccessMode accessMode, UnityVulkanImage * outImage);
    bool(UNITY_INTERFACE_API * AccessRenderBufferResolveTexture)(UnityRenderBuffer nativeRenderBuffer, const VkImageSubresource * subResource, VkImageLayout layout,
        VkPipelineStageFlags pipelineStageFlags, VkAccessFlags accessFlags, UnityVulkanResourceAccessMode accessMode, UnityVulkanImage * outImage);
    bool(UNITY_INTERFACE_API * AccessBuffer)(void* nativeBuffer, VkPipelineStageFlags pipelineStageFlags, VkAccessFlags accessFlags, UnityVulkanResourceAccessMode accessMode, UnityVulkanBuffer * outBuffer);
    void(UNITY_INTERFACE_API * EnsureOutsideRenderPass)();
    void(UNITY_INTERFACE_API * EnsureInsideRenderPass)();
    void(UNITY_INTERFACE_API * AccessQueue)(UnityRenderingEventAndData, int eventId, void* userData, bool flush);
    bool(UNITY_INTERFACE_API * ConfigureSwapchain)(const UnityVulkanSwapchainConfiguration * swapChainConfig);
};
UNITY_REGISTER_INTERFACE_GUID(0x95355348d4ef4e11ULL, 0x9789313dfcffcc87ULL, IUnityGraphicsVulkan)
