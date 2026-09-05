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
        /// <summary>Packages that must ship shaders — Core owns the surface family every other package renders through.</summary>
        private static readonly string[] requiredShaderRoots =
        {
            "Packages/media.lightside.core"
        };

        /// <summary>Packages scanned for shaders of their own; each may legitimately ship none.</summary>
        private static readonly string[] optionalShaderRoots =
        {
            "Packages/media.lightside.unitext",
            "Packages/media.lightside.unishapes",
            "Packages/media.lightside.unilottie"
        };

        /// <summary>
        /// Compiles every LightSide shader across the requested color spaces and compiler platforms, failing
        /// on any compiler warning or error. The timeout must stay under the CI job's own, so an overrun
        /// surfaces as a test failure instead of a killed runner, and above the ~90 minutes a cold URP leg
        /// spends compiling.
        /// </summary>
        [Test, Timeout(6600000)]
        public void AllShadersCompileWithoutWarningsOrErrors()
        {
            var expectedPipeline = RequireExpectedPipeline();
            HdrpFixture.EnsureImported(expectedPipeline);

            var compiler = new ShaderCompiler(SplitCommandLineList("-shaderPlatforms"));
            var shaderPaths = FindShaderPaths(expectedPipeline);
            var diagnostics = new HashSet<string>(StringComparer.Ordinal);
            var urp = UrpContext.Create(expectedPipeline);
            var previousColorSpace = PlayerSettings.colorSpace;
            var colorSpaces = RequestedColorSpaces(expectedPipeline);

            try
            {
                Debug.Log("Shader compiler matrix: Unity " + Application.unityVersion
                    + ", pipeline: " + expectedPipeline
                    + ", color spaces: " + string.Join(", ", colorSpaces)
                    + ", compiler platforms: " + string.Join(", ", compiler.Platforms)
                    + ", shaders: " + shaderPaths.Length + ".");

                foreach (var colorSpace in colorSpaces)
                {
                    PlayerSettings.colorSpace = colorSpace;
                    var configuration = expectedPipeline + ", " + colorSpace;
                    Debug.Log("Compiling LightSide shaders for " + configuration + ".");
                    foreach (var shaderPath in shaderPaths)
                        compiler.Compile(shaderPath, configuration, diagnostics);
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

        /// <summary>
        /// A shader can compile cleanly and still render magenta: when no SubShader matches the active
        /// pipeline, or the matched one only carries legacy LightMode passes an SRP draws with the error
        /// shader, the failure produces zero compiler messages. This asserts, per shader, that the
        /// SubShader the fixture's pipeline would select exists and every one of its passes is drawable there.
        /// </summary>
        [Test, Timeout(600000)]
        public void ActivePipelineSelectsDrawableSubShaders()
        {
            var expectedPipeline = RequireExpectedPipeline();
            HdrpFixture.EnsureImported(expectedPipeline);

            var pipelineTag = ExpectedPipelineTag(expectedPipeline);
            var builtin = pipelineTag.Length == 0;
            var renderPipelineTag = new ShaderTagId("RenderPipeline");
            var lightModeTag = new ShaderTagId("LightMode");
            var failures = new List<string>();

            foreach (var shaderPath in FindShaderPaths(expectedPipeline))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                {
                    failures.Add(shaderPath + " | failed to load.");
                    continue;
                }

                var selected = -1;
                for (var index = 0; index < shader.subshaderCount; index++)
                {
                    var tag = shader.FindSubshaderTagValue(index, renderPipelineTag).name;
                    if (!string.IsNullOrEmpty(tag) && !string.Equals(tag, pipelineTag, StringComparison.Ordinal))
                        continue;
                    selected = index;
                    break;
                }

                if (selected < 0)
                {
                    failures.Add(shaderPath + " | no SubShader is selectable for '" + expectedPipeline
                        + "' (every SubShader is tagged for another pipeline) — it renders magenta there.");
                    continue;
                }

                if (builtin)
                    continue;

                var passCount = shader.GetPassCountInSubshader(selected);
                for (var pass = 0; pass < passCount; pass++)
                {
                    var lightMode = shader.FindPassTagValue(selected, pass, lightModeTag).name;
                    if (Array.IndexOf(legacyOnlyLightModes, lightMode) < 0)
                        continue;
                    failures.Add(shaderPath + " | SubShader " + selected + " pass " + pass
                        + " has legacy LightMode '" + lightMode + "', which '" + expectedPipeline
                        + "' draws with the error shader — it renders magenta there.");
                }
            }

            foreach (var failure in failures)
                Debug.Log("[SubShader selection] " + failure);

            Assert.IsEmpty(failures,
                failures.Count + " shader(s) are not drawable under the '" + expectedPipeline
                + "' pipeline. The complete list is printed above.");
        }

        /// <summary>LightMode tags only the Built-in pipeline draws; SRPs render such passes with the magenta error shader.</summary>
        private static readonly string[] legacyOnlyLightModes =
        {
            "Always", "ForwardBase", "ForwardAdd", "PrepassBase", "PrepassFinal",
            "Vertex", "VertexLMRGBM", "VertexLM"
        };

        private static string RequireExpectedPipeline()
        {
            var expectedPipeline = GetCommandLineValue("-expectedRenderPipeline");
            Assert.IsNotEmpty(expectedPipeline, "-expectedRenderPipeline is required.");
            return expectedPipeline;
        }

        private static bool IsHdrp(string expectedPipeline)
            => string.Equals(expectedPipeline, "hdrp", StringComparison.OrdinalIgnoreCase);

        private static string ExpectedPipelineTag(string expectedPipeline)
        {
            if (string.Equals(expectedPipeline, "builtin", StringComparison.OrdinalIgnoreCase))
                return "";
            if (string.Equals(expectedPipeline, "urp", StringComparison.OrdinalIgnoreCase))
                return "UniversalPipeline";
            if (IsHdrp(expectedPipeline))
                return "HDRenderPipeline";
            Assert.Fail("Unknown expected render pipeline: " + expectedPipeline);
            return null;
        }

        private static string[] FindShaderPaths(string expectedPipeline)
        {
            var shaderPaths = new List<string>();

            foreach (var packageRoot in requiredShaderRoots)
            {
                var packageShaders = FindShadersUnder(packageRoot);
                Assert.IsNotEmpty(packageShaders, "No shaders were found under " + packageRoot + ".");
                shaderPaths.AddRange(packageShaders);
            }

            foreach (var packageRoot in optionalShaderRoots)
                shaderPaths.AddRange(FindShadersUnder(packageRoot));

            if (IsHdrp(expectedPipeline))
            {
                var hdrpShaders = FindShadersUnder(HdrpFixture.TargetFolder);
                Assert.IsNotEmpty(hdrpShaders, "No HDRP shader assets were found under " + HdrpFixture.TargetFolder + ".");
                shaderPaths.AddRange(hdrpShaders);
            }

            return shaderPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] FindShadersUnder(string packageRoot)
            => AssetDatabase.FindAssets("t:Shader", new[] { packageRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <summary>
        /// Mirrors the package's own HDRP delivery: the graphs live in Core's hidden HdrpAssets~ folder
        /// (a .shadergraph in the always-imported tree breaks Built-in-only imports) and are copied
        /// into Assets before use — here explicitly, because the fixture has no active HDRP asset
        /// to trigger the editor's automatic copy.
        /// </summary>
        private static class HdrpFixture
        {
            internal const string TargetFolder = "Assets/LightSide/HDRP";
            private const string SourceFolder = "Packages/media.lightside.core/HdrpAssets~";

            internal static void EnsureImported(string expectedPipeline)
            {
                if (!IsHdrp(expectedPipeline)) return;

                var source = FileUtil.GetPhysicalPath(SourceFolder);
                Assert.IsTrue(System.IO.Directory.Exists(source),
                    "HDRP shader sources are missing from the package: " + SourceFolder);

                System.IO.Directory.CreateDirectory(TargetFolder);
                var copied = false;
                foreach (var file in System.IO.Directory.GetFiles(source))
                {
                    var target = System.IO.Path.Combine(TargetFolder, System.IO.Path.GetFileName(file));
                    if (System.IO.File.Exists(target)) continue;
                    System.IO.File.Copy(file, target);
                    copied = true;
                }

                if (copied)
                    AssetDatabase.ImportAsset(TargetFolder, ImportAssetOptions.ImportRecursive);
            }
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

        private static string[] SplitCommandLineList(string argument)
        {
            var value = GetCommandLineValue(argument);
            return string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToArray();
        }

        /// <summary>
        /// The color spaces to sweep, from <c>-shaderColorSpaces</c>; Gamma and Linear when it is absent,
        /// Linear alone under HDRP. Gamma and Linear compile different variants, so a run that names only
        /// one leaves the other unchecked — the caller decides which legs carry that cost.
        /// </summary>
        private static ColorSpace[] RequestedColorSpaces(string expectedPipeline)
        {
            // HDRP is Linear-only; a Gamma sweep there would compile a configuration the pipeline forbids.
            var hdrp = IsHdrp(expectedPipeline);
            var requested = SplitCommandLineList("-shaderColorSpaces");
            if (requested.Length == 0)
                return hdrp ? new[] { ColorSpace.Linear } : new[] { ColorSpace.Gamma, ColorSpace.Linear };

            var colorSpaces = new List<ColorSpace>();
            foreach (var name in requested)
            {
                ColorSpace colorSpace;
                Assert.IsTrue(Enum.TryParse(name, true, out colorSpace)
                    && (colorSpace == ColorSpace.Gamma || colorSpace == ColorSpace.Linear),
                    "-shaderColorSpaces names an unknown color space: " + name + ".");
                Assert.IsFalse(hdrp && colorSpace == ColorSpace.Gamma,
                    "HDRP is Linear-only; -shaderColorSpaces must not ask an HDRP fixture for Gamma.");
                colorSpaces.Add(colorSpace);
            }

            return colorSpaces.Distinct().ToArray();
        }

        private sealed class ShaderCompiler
        {
            /// <summary>
            /// The <c>mode</c> OpenCompiledShader takes, indexing ShaderInspectorPlatformsPopup's platform
            /// modes: 0 current device, 1 current build platform, 2 all platforms, 3 custom. Only 3 reads
            /// <c>customPlatformsMask</c> — under 2 the mask is ignored and every available platform compiles.
            /// </summary>
            private const int customPlatformsMode = 3;

            private const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            private readonly MethodInfo compile;
            private readonly MethodInfo fetchMessages;
            private readonly int platformMask;

            internal ShaderCompiler(string[] requestedPlatforms)
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

                var available = (int)availablePlatforms.Invoke(null, null);
                Assert.AreNotEqual(0, available, "Unity reported no available shader compiler platforms.");
                platformMask = Restrict(available, requestedPlatforms);
                Platforms = GetPlatformNames(platformMask);
            }

            /// <summary>
            /// Narrows the editor's available platforms to the requested names; an empty list or the single
            /// name <c>all</c> keeps every one. A name this editor cannot compile fails the run — quietly
            /// compiling fewer platforms would report a green check for coverage that never ran.
            /// </summary>
            private static int Restrict(int available, string[] requested)
            {
                if (requested.Length == 0
                    || requested.Length == 1 && string.Equals(requested[0], "all", StringComparison.OrdinalIgnoreCase))
                    return available;

                var mask = 0;
                foreach (var name in requested)
                {
                    ShaderCompilerPlatform platform;
                    Assert.IsTrue(Enum.TryParse(name, true, out platform)
                        && Enum.IsDefined(typeof(ShaderCompilerPlatform), platform),
                        "-shaderPlatforms names an unknown shader compiler platform: " + name + ".");

                    var bit = 1 << (int)platform;
                    Assert.AreNotEqual(0, available & bit,
                        "This editor cannot compile for " + platform + "; it offers "
                        + string.Join(", ", GetPlatformNames(available)) + ".");
                    mask |= bit;
                }

                return mask;
            }

            internal string[] Platforms { get; private set; }

            internal void Compile(string shaderPath, string configuration, ISet<string> diagnostics)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                {
                    diagnostics.Add("[Error] [" + configuration + "] Failed to load shader: " + shaderPath);
                    return;
                }

                try
                {
                    ShaderUtil.ClearShaderMessages(shader);
                    compile.Invoke(null, new object[] { shader, customPlatformsMode, platformMask, true, false, true });
                    fetchMessages.Invoke(null, new object[] { shader });
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    {
                        if ((platformMask & (1 << (int)message.platform)) == 0)
                            diagnostics.Add("[Error] [" + configuration + "] The compiler platform restriction did "
                                + "not take effect: " + message.platform + " compiled although it was not requested, "
                                + "so the matrix logged above is narrower than the sweep that ran.");
                        diagnostics.Add(Format(shaderPath, configuration, message));
                    }
                }
                catch (Exception exception)
                {
                    diagnostics.Add("[Error] [" + configuration + "] " + shaderPath + " | "
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

            private static string Format(string shaderPath, string configuration, ShaderMessage message)
            {
                var builder = new StringBuilder();
                builder.Append('[').Append(message.severity).Append("] [").Append(configuration).Append("] [")
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
            private readonly RenderPipelineAsset previousDefaultPipeline;
            private readonly RenderPipelineAsset previousQualityPipeline;
            private readonly ShaderStrippingContext shaderStripping;

            private UrpContext(Type rendererDataType, Type pipelineAssetType)
            {
                previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
                previousQualityPipeline = QualitySettings.renderPipeline;
                rendererData = ScriptableObject.CreateInstance(rendererDataType);

                var create = pipelineAssetType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 1);
                Assert.NotNull(create, "UniversalRenderPipelineAsset.Create is unavailable.");
                pipelineAsset = create.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;
                Assert.NotNull(pipelineAsset, "UniversalRenderPipelineAsset.Create returned no pipeline asset.");
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;
                shaderStripping = ShaderStrippingContext.Create();
            }

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

                if (IsHdrp(expectedPipeline))
                {
                    Assert.IsNull(rendererDataType, "The HDRP fixture unexpectedly contains URP.");
                    var hdrpAssetType = Type.GetType(
                        "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, "
                        + "Unity.RenderPipelines.HighDefinition.Runtime", false);
                    Assert.NotNull(hdrpAssetType, "The HDRP fixture does not contain HDRenderPipelineAsset.");
                    return null;
                }

                Assert.AreEqual("urp", expectedPipeline.ToLowerInvariant(), "Unknown expected render pipeline.");
                Assert.NotNull(rendererDataType, "The URP fixture does not contain UniversalRendererData.");
                Assert.NotNull(pipelineAssetType, "The URP fixture does not contain UniversalRenderPipelineAsset.");
                return new UrpContext(rendererDataType, pipelineAssetType);
            }

            public void Dispose()
            {
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
