using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSide.CI
{
    internal sealed class ShaderCompileCheck
    {
        private static readonly string[] packageRoots =
        {
            "Packages/media.lightside.unitext",
            "Packages/media.lightside.unishapes",
            "Packages/media.lightside.unilottie"
        };

        [Test]
        public void AllShadersCompileWithoutWarningsOrErrors()
        {
            var expectedPipeline = GetCommandLineValue("-expectedRenderPipeline");
            Assert.IsNotEmpty(expectedPipeline, "-expectedRenderPipeline is required.");

            var compiler = new ShaderCompiler();
            var shaderPaths = FindShaderPaths();
            var diagnostics = new HashSet<string>(StringComparer.Ordinal);
            var urp = UrpContext.Create(expectedPipeline);
            var previousColorSpace = PlayerSettings.colorSpace;

            try
            {
                var renderingModes = urp == null ? new[] { "Built-in" } : urp.RenderingModes;
                Debug.Log("Shader compiler matrix: Unity " + Application.unityVersion
                    + ", pipeline modes: " + string.Join(", ", renderingModes)
                    + ", color spaces: Gamma, Linear"
                    + ", compiler platforms: " + string.Join(", ", compiler.Platforms)
                    + ", shaders: " + shaderPaths.Length + ".");

                foreach (var colorSpace in new[] { ColorSpace.Gamma, ColorSpace.Linear })
                {
                    PlayerSettings.colorSpace = colorSpace;
                    foreach (var renderingMode in renderingModes)
                    {
                        if (urp != null)
                            urp.SetRenderingMode(renderingMode);

                        var configuration = renderingMode + ", " + colorSpace;
                        Debug.Log("Compiling LightSide shaders for " + configuration + ".");
                        foreach (var shaderPath in shaderPaths)
                            compiler.Compile(shaderPath, configuration, diagnostics);
                    }
                }
            }
            finally
            {
                PlayerSettings.colorSpace = previousColorSpace;
                if (urp != null)
                    urp.Dispose();
            }

            foreach (var diagnostic in diagnostics)
                Debug.Log(diagnostic);

            Assert.IsEmpty(diagnostics,
                diagnostics.Count + " shader compiler warning(s) or error(s) were found. The complete list is printed above.");
        }

        private static string[] FindShaderPaths()
        {
            var shaderPaths = new List<string>();
            foreach (var packageRoot in packageRoots)
            {
                var packageShaders = AssetDatabase.FindAssets("t:Shader", new[] { packageRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Assert.IsNotEmpty(packageShaders, "No shaders were found under " + packageRoot + ".");
                shaderPaths.AddRange(packageShaders);
            }

            return shaderPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetCommandLineValue(string argument)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return null;
        }

        private sealed class ShaderCompiler
        {
            private const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            private readonly MethodInfo compile;
            private readonly MethodInfo fetchMessages;
            private readonly int platformMask;

            internal ShaderCompiler()
            {
                var shaderUtil = typeof(ShaderUtil);
                var availablePlatforms = shaderUtil.GetMethod(
                    "GetAvailableShaderCompilerPlatforms", staticFlags, null, Type.EmptyTypes, null);
                compile = shaderUtil.GetMethod(
                    "OpenCompiledShader", staticFlags, null,
                    new[] { typeof(Shader), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool) }, null);
                fetchMessages = shaderUtil.GetMethod(
                    "FetchCachedMessages", staticFlags, null, new[] { typeof(Shader) }, null);

                Assert.NotNull(availablePlatforms, "Unity's all-platform shader compiler API is unavailable.");
                Assert.NotNull(compile, "Unity's full-variant shader compiler API is unavailable.");
                Assert.NotNull(fetchMessages, "Unity's shader message cache API is unavailable.");

                platformMask = (int)availablePlatforms.Invoke(null, null);
                Assert.AreNotEqual(0, platformMask, "Unity reported no available shader compiler platforms.");
                Platforms = GetPlatformNames(platformMask);
            }

            internal string[] Platforms { get; private set; }

            internal void Compile(string shaderPath, string renderingMode, ISet<string> diagnostics)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                {
                    diagnostics.Add("[Error] [" + renderingMode + "] Failed to load shader: " + shaderPath);
                    return;
                }

                try
                {
                    ShaderUtil.ClearShaderMessages(shader);
                    compile.Invoke(null, new object[] { shader, 2, platformMask, true, false, true });
                    fetchMessages.Invoke(null, new object[] { shader });
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        diagnostics.Add(Format(shaderPath, renderingMode, message));
                }
                catch (Exception exception)
                {
                    diagnostics.Add("[Error] [" + renderingMode + "] " + shaderPath + " | "
                        + exception.GetBaseException());
                }
            }

            private static string[] GetPlatformNames(int availablePlatforms)
            {
                var names = new List<string>();
                for (var index = 0; index < 32; index++)
                {
                    if ((availablePlatforms & (1 << index)) != 0)
                        names.Add(((ShaderCompilerPlatform)index).ToString());
                }

                return names.ToArray();
            }

            private static string Format(string shaderPath, string renderingMode, ShaderMessage message)
            {
                var builder = new StringBuilder();
                builder.Append('[').Append(message.severity).Append("] [").Append(renderingMode).Append("] [")
                    .Append(message.platform).Append("] ").Append(shaderPath);
                if (!string.IsNullOrEmpty(message.file))
                    builder.Append(" | ").Append(message.file);
                if (message.line > 0)
                    builder.Append(':').Append(message.line);
                builder.Append(" | ").Append(message.message);
                if (!string.IsNullOrEmpty(message.messageDetails))
                    builder.AppendLine().Append(message.messageDetails);
                return builder.ToString();
            }
        }

        private sealed class UrpContext : IDisposable
        {
            private const string universalAssembly = "Unity.RenderPipelines.Universal.Runtime";
            private readonly ScriptableObject rendererData;
            private readonly RenderPipelineAsset pipelineAsset;
            private readonly PropertyInfo renderingMode;
            private readonly RenderPipelineAsset previousDefaultPipeline;
            private readonly RenderPipelineAsset previousQualityPipeline;
            private readonly IDictionary<string, object> modeValues;
            private readonly ShaderStrippingContext shaderStripping;

            private UrpContext(Type rendererDataType, Type pipelineAssetType)
            {
                previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
                previousQualityPipeline = QualitySettings.renderPipeline;
                rendererData = ScriptableObject.CreateInstance(rendererDataType);
                renderingMode = rendererDataType.GetProperty("renderingMode", BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(renderingMode, "UniversalRendererData.renderingMode is unavailable.");

                modeValues = Enum.GetValues(renderingMode.PropertyType)
                    .Cast<object>()
                    .OrderBy(Convert.ToInt32)
                    .ToDictionary(value => value.ToString(), value => value, StringComparer.Ordinal);
                Assert.IsNotEmpty(modeValues, "URP reported no rendering modes.");

                var create = pipelineAssetType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 1);
                Assert.NotNull(create, "UniversalRenderPipelineAsset.Create is unavailable.");
                pipelineAsset = create.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;
                Assert.NotNull(pipelineAsset, "UniversalRenderPipelineAsset.Create returned no pipeline asset.");
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;
                shaderStripping = ShaderStrippingContext.Create();
                RenderingModes = modeValues.Keys.ToArray();
            }

            internal string[] RenderingModes { get; private set; }

            internal static UrpContext Create(string expectedPipeline)
            {
                var rendererDataType = Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalRendererData, " + universalAssembly, false);
                var pipelineAssetType = Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " + universalAssembly, false);

                if (string.Equals(expectedPipeline, "builtin", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.IsNull(rendererDataType, "The Built-in fixture unexpectedly contains URP.");
                    Assert.IsNull(pipelineAssetType, "The Built-in fixture unexpectedly contains URP.");
                    return null;
                }

                Assert.AreEqual("urp", expectedPipeline.ToLowerInvariant(), "Unknown expected render pipeline.");
                Assert.NotNull(rendererDataType, "The URP fixture does not contain UniversalRendererData.");
                Assert.NotNull(pipelineAssetType, "The URP fixture does not contain UniversalRenderPipelineAsset.");
                return new UrpContext(rendererDataType, pipelineAssetType);
            }

            internal void SetRenderingMode(string mode)
            {
                renderingMode.SetValue(rendererData, modeValues[mode], null);
                EditorUtility.SetDirty(rendererData);

                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;

                Shader.DisableKeyword("_FORWARD_PLUS");
                Shader.DisableKeyword("_CLUSTER_LIGHT_LOOP");
                if (mode.EndsWith("Plus", StringComparison.Ordinal))
                    Shader.EnableKeyword(GetForwardPlusKeyword());
            }

            private static string GetForwardPlusKeyword()
            {
                var version = Application.unityVersion.Split('.');
                var major = int.Parse(version[0]);
                var minor = int.Parse(version[1]);
                return major > 6000 || major == 6000 && minor >= 1
                    ? "_CLUSTER_LIGHT_LOOP"
                    : "_FORWARD_PLUS";
            }

            public void Dispose()
            {
                Shader.DisableKeyword("_FORWARD_PLUS");
                Shader.DisableKeyword("_CLUSTER_LIGHT_LOOP");
                shaderStripping.Dispose();
                GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
                UnityEngine.Object.DestroyImmediate(pipelineAsset);
                UnityEngine.Object.DestroyImmediate(rendererData);
            }

            private sealed class ShaderStrippingContext : IDisposable
            {
                private static readonly string[] propertyNames =
                {
                    "stripUnusedVariants",
                    "stripUnusedPostProcessingVariants",
                    "stripScreenCoordOverrideVariants"
                };

                private readonly object settings;
                private readonly IDictionary<PropertyInfo, object> previousValues;

                private ShaderStrippingContext(object settings)
                {
                    this.settings = settings;
                    previousValues = new Dictionary<PropertyInfo, object>();

                    foreach (var propertyName in propertyNames)
                    {
                        var property = settings.GetType().GetProperty(
                            propertyName, BindingFlags.Instance | BindingFlags.Public);
                        if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
                            continue;

                        previousValues.Add(property, property.GetValue(settings, null));
                        property.SetValue(settings, false, null);
                    }

                    Assert.IsTrue(previousValues.Keys.Any(property => property.Name == "stripUnusedVariants"),
                        "URP's Strip Unused Variants setting is unavailable.");
                    var settingsAsset = settings as UnityEngine.Object;
                    if (settingsAsset != null)
                        EditorUtility.SetDirty(settingsAsset);
                    Debug.Log("URP shader variant stripping is disabled for compiler diagnostics.");
                }

                internal static ShaderStrippingContext Create()
                {
                    var globalSettingsType = Type.GetType(
                        "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, "
                        + universalAssembly, false);
                    Assert.NotNull(globalSettingsType, "UniversalRenderPipelineGlobalSettings is unavailable.");

                    var globalSettings = EnsureGlobalSettings(globalSettingsType);
                    var strippingSettingsType = Type.GetType(
                        "UnityEngine.Rendering.Universal.URPShaderStrippingSetting, "
                        + universalAssembly, false);
                    if (strippingSettingsType == null)
                        return new ShaderStrippingContext(globalSettings);

                    var getSettings = typeof(GraphicsSettings).GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .SingleOrDefault(method => method.Name == "GetRenderPipelineSettings"
                            && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
                    Assert.NotNull(getSettings, "GraphicsSettings.GetRenderPipelineSettings is unavailable.");
                    var settings = getSettings.MakeGenericMethod(strippingSettingsType).Invoke(null, null);
                    Assert.NotNull(settings, "URPShaderStrippingSetting is not registered in Graphics Settings.");
                    return new ShaderStrippingContext(settings);
                }

                private static object EnsureGlobalSettings(Type globalSettingsType)
                {
                    var instance = globalSettingsType.GetProperty(
                        "instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                    var globalSettings = instance == null ? null : instance.GetValue(null, null);
                    if (globalSettings != null)
                        return globalSettings;

                    var ensure = globalSettingsType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                        .Where(method => method.Name == "Ensure")
                        .SingleOrDefault(method => method.GetParameters().All(parameter => parameter.IsOptional));
                    Assert.NotNull(ensure, "UniversalRenderPipelineGlobalSettings.Ensure is unavailable.");
                    var parameters = ensure.GetParameters()
                        .Select(parameter => parameter.DefaultValue)
                        .ToArray();
                    globalSettings = ensure.Invoke(null, parameters);
                    Assert.NotNull(globalSettings, "URP Global Settings could not be created.");
                    return globalSettings;
                }

                public void Dispose()
                {
                    foreach (var previousValue in previousValues)
                        previousValue.Key.SetValue(settings, previousValue.Value, null);
                }
            }
        }
    }
}
