using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using LightSide;
using Unity.Profiling;
using UnityEngine;

/// <summary>Standardized workloads and creation probes for directly integrated motion engines.</summary>
public sealed class MotionBenchmark : MonoBehaviour
{
    const string mainThreadMarker = "Main Thread";
    const string executionIsolation = "sharedProcess:freshContextPerAdapterWorkload;cooldownBeforeEachSupportedStep";
    const string executionOrder = "workloadMajorAlternatingAdapters:evenStepsForward;oddStepsReverse;emptyFrame,creation,singleTransformPosition,sharedTransformPosition,keyedManagedFloats,distinctTransformPositions,sequences";
    const string measurementPasses = "headline:mainThreadOnly;detail:adapterMarkersOnlyWhenAvailable;sameWarmedContext";
    const string validationBoundary = "sequenceBoundaryProbeBeforeMeasuredContext;outputDeltaAfterWarmupAndSettlingBeforeRecorders;semanticBetweenAndAfterPasses";
    const int recorderSettlingFrames = 5;
    const int sequenceProbeMaxSequences = 4;
    const float sequenceProbeTimeoutSeconds = 10f;
    const float from = 0f;
    const float to = 1f;
    static readonly MotionBenchmarkWorkload[] workloads =
    {
        MotionBenchmarkWorkload.EmptyFrame,
        MotionBenchmarkWorkload.Creation,
        MotionBenchmarkWorkload.SingleTransformPosition,
        MotionBenchmarkWorkload.SharedTransformPosition,
        MotionBenchmarkWorkload.KeyedManagedFloats,
        MotionBenchmarkWorkload.DistinctTransformPositions,
        MotionBenchmarkWorkload.Sequences
    };

    [SerializeField] Transform sharedTransform;
    [SerializeField] Transform distinctTransformPrefab;
    [SerializeField] Transform contextRoot;
    [SerializeReference] MotionBenchmarkAdapter[] adapters =
    {
        new LightSideMotionBenchmarkAdapter(),
        new PrimeTweenMotionBenchmarkAdapter(),
        new LitMotionMotionBenchmarkAdapter()
    };
    [SerializeField, Min(1)] int sharedMotionCount = 100_000;
    [SerializeField, Min(1)] int keyedFloatCount = 64_000;
    [SerializeField, Min(1)] int distinctTransformCount = 50_000;
    [SerializeField, Min(1)] int sequenceCount = 4_096;
    [SerializeField, Min(3)] int sequenceLength = 8;
    [SerializeField, Min(1f)] float steadyMotionDuration = 3_600f;
    [SerializeField, Min(1)] int warmupFrames = 60;
    [SerializeField, Min(1)] int measuredFrames = 120;
    [SerializeField, Min(1)] int creationBatchSize = 4_096;
    [SerializeField, Min(1)] int creationWarmupBatches = 3;
    [SerializeField, Min(1)] int creationSamples = 32;

    internal MotionBenchmarkData Results { get; private set; }

    /// <summary>Runs every configured adapter against the same targets, workloads, warmup, and sample counts.</summary>
    public IEnumerator RunBenchmarkCoroutine()
    {
        Results = new MotionBenchmarkData
        {
            config = new MotionBenchmarkConfigData
            {
                sharedMotionCount = sharedMotionCount,
                keyedFloatCount = keyedFloatCount,
                distinctTransformCount = distinctTransformCount,
                sequenceCount = sequenceCount,
                sequenceLength = sequenceLength,
                steadyMotionDurationSeconds = steadyMotionDuration,
                warmupFrames = warmupFrames,
                measuredFrames = measuredFrames,
                recorderSettlingFrames = recorderSettlingFrames,
                creationBatchSize = creationBatchSize,
                creationWarmupBatches = creationWarmupBatches,
                creationSamples = creationSamples,
                executionIsolation = executionIsolation,
                executionOrder = executionOrder,
                measurementPasses = measurementPasses,
                validationBoundary = validationBoundary,
                timeScale = Time.timeScale,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount
            }
        };

        ValidateConfiguration(out var adapterNames, out var markerNamesByAdapter);
        Results.config.adapterOrder = adapterNames;
        Results.config.workloadOrder = new string[workloads.Length];
        for (int workloadIndex = 0; workloadIndex < workloads.Length; workloadIndex++)
            Results.config.workloadOrder[workloadIndex] = WorkloadKey(workloads[workloadIndex]);
        for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
        {
            var adapter = adapters[adapterIndex];
            var engine = new MotionBenchmarkEngineData
            {
                adapterType = adapter.GetType().FullName,
                status = "measuring"
            };
            Results.engines.Add(adapterNames[adapterIndex], engine);
            try
            {
                engine.metadata = adapter.Metadata;
                ValidateMetadata(adapterNames[adapterIndex], engine.metadata);
            }
            catch (Exception exception)
            {
                engine.status = "failed";
                engine.statusReason = FailureReason(exception);
            }
        }

        bool completed = false;
        try
        {
            for (int workloadIndex = 0; workloadIndex < workloads.Length; workloadIndex++)
            {
                var workload = workloads[workloadIndex];
                for (int position = 0; position < adapters.Length; position++)
                {
                    int adapterIndex = (workloadIndex & 1) == 0
                        ? position
                        : adapters.Length - 1 - position;
                    var engine = Results.engines[adapterNames[adapterIndex]];
                    if (engine.status != "measuring") continue;

                    var adapter = adapters[adapterIndex];
                    var markerNames = markerNamesByAdapter[adapterIndex];
                    Exception failure = null;
                    var step = workload == MotionBenchmarkWorkload.Creation
                        ? RunCreationStep(adapter, engine)
                        : RunWorkloadStep(adapter, markerNames, engine, workload);
                    foreach (var frame in Flatten(step, exception => failure = exception))
                        yield return frame;
                    if (failure != null)
                    {
                        if (failure is BenchmarkCleanupException)
                            throw failure;
                        FailStep(engine, workload, markerNames, failure);
                    }
                }
            }
            completed = true;
        }
        finally
        {
            const string interrupted = "Motion benchmark run ended before this step completed.";
            for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
            {
                var engine = Results.engines[adapterNames[adapterIndex]];
                if (engine.status != "measuring") continue;
                FailPending(engine, markerNamesByAdapter[adapterIndex], interrupted, !completed);
                FinalizeStatus(engine);
            }
        }
    }

    IEnumerator RunWorkloadStep(MotionBenchmarkAdapter adapter, IReadOnlyList<string> markerNames,
        MotionBenchmarkEngineData engine, MotionBenchmarkWorkload workload)
    {
        var spec = CreateSpec(workload);
        var result = new MotionBenchmarkWorkloadData
        {
            status = "measuring",
            spec = spec,
            mainThread = MotionBenchmarkSeriesData.Create(mainThreadMarker, "milliseconds", measuredFrames,
                "headline")
        };
        engine.workloads.Add(WorkloadKey(workload), result);

        string reason = null;
        Exception unsupportedFailure = null;
        try
        {
            reason = adapter.UnsupportedReason(spec);
        }
        catch (Exception exception)
        {
            unsupportedFailure = exception;
        }
        if (unsupportedFailure != null)
        {
            Fail(result, unsupportedFailure, markerNames);
            yield break;
        }
        if (!string.IsNullOrEmpty(reason))
        {
            result.status = "unsupported";
            result.statusReason = reason;
            result.mainThread.status = "unsupported";
            result.mainThread.statusReason = reason;
            AddUnavailableMarkers(result, markerNames, reason);
            yield break;
        }

        Exception failure = null;
        foreach (var frame in Flatten(Cooldown(), exception => failure = exception))
            yield return frame;
        if (failure == null && workload == MotionBenchmarkWorkload.Sequences)
        {
            foreach (var frame in Flatten(RunSequenceSemanticProbe(adapter), exception => failure = exception))
                yield return frame;
            if (failure == null)
                foreach (var frame in Flatten(Cooldown(), exception => failure = exception))
                    yield return frame;
        }
        if (failure == null)
        {
            foreach (var frame in Flatten(RunWorkload(adapter, markerNames, spec, result),
                         exception => failure = exception))
                yield return frame;
        }
        if (failure != null)
        {
            if (failure is BenchmarkCleanupException)
                throw failure;
            Fail(result, failure, markerNames);
        }
    }

    IEnumerator RunCreationStep(MotionBenchmarkAdapter adapter, MotionBenchmarkEngineData engine)
    {
        var spec = CreateSpec(MotionBenchmarkWorkload.Creation);
        engine.creation = MotionBenchmarkCreationData.Create(spec, creationSamples);
        string reason = null;
        Exception unsupportedFailure = null;
        try
        {
            reason = adapter.UnsupportedReason(spec);
        }
        catch (Exception exception)
        {
            unsupportedFailure = exception;
        }
        if (unsupportedFailure != null)
        {
            Fail(engine.creation, unsupportedFailure);
            yield break;
        }
        if (!string.IsNullOrEmpty(reason))
        {
            Unsupported(engine.creation, reason);
            yield break;
        }

        Exception failure = null;
        foreach (var frame in Flatten(Cooldown(), exception => failure = exception))
            yield return frame;
        if (failure == null)
        {
            foreach (var frame in Flatten(RunCreation(adapter, spec, engine.creation),
                         exception => failure = exception))
                yield return frame;
        }
        if (failure != null)
        {
            if (failure is BenchmarkCleanupException)
                throw failure;
            Fail(engine.creation, failure);
        }
    }

    static IEnumerable<object> Flatten(IEnumerator routine, Action<Exception> failure)
    {
        var owned = new OwnedEnumerator(routine);
        Exception primary = null;
        bool cleanupFailure = false;
        try
        {
            while (true)
            {
                if (!owned.MoveNext(out var current, out primary, out var moveCleanupFailure))
                {
                    cleanupFailure |= moveCleanupFailure;
                    yield break;
                }
                yield return current;
            }
        }
        finally
        {
            primary = owned.Dispose(primary, ref cleanupFailure);
            if (primary != null)
                failure(primary);
            if (cleanupFailure && primary != null)
            {
                throw primary is BenchmarkCleanupException
                    ? primary
                    : new BenchmarkCleanupException("Benchmark coroutine cleanup failed.", primary);
            }
        }
    }

    IEnumerator RunSequenceSemanticProbe(MotionBenchmarkAdapter adapter)
    {
        int probeSequenceCount = Math.Min(sequenceCount, sequenceProbeMaxSequences);
        int probeMotionCount = checked(probeSequenceCount * sequenceLength);
        float probeDuration = Time.deltaTime * 30f;
        if (float.IsNaN(probeDuration) || float.IsInfinity(probeDuration) || probeDuration <= 0f)
            throw new InvalidOperationException("Motion sequence semantic probe requires a finite positive scaled frame delta.");
        var spec = new MotionBenchmarkSpec(
            MotionBenchmarkWorkload.Sequences,
            probeMotionCount,
            0,
            probeSequenceCount,
            sequenceLength,
            probeDuration,
            from,
            to,
            MotionBenchmarkEase.Linear,
            MotionBenchmarkClock.Scaled,
            MotionBenchmarkUpdatePhase.Update,
            1,
            MotionBenchmarkCycleMode.Restart,
            MotionBenchmarkTopology.SequentialChains,
            true);
        var managedValues = new float[probeMotionCount];
        var request = new MotionBenchmarkRequest(spec, sharedTransform, null, managedValues);
        MotionBenchmarkContext context = null;
        Exception failure = null;

        IEnumerator RunProbe()
        {
            context = adapter.Start(request)
                ?? throw new InvalidOperationException($"Motion adapter '{adapter.Name}' returned no sequence probe context.");
            context.CaptureOutputBaseline();
            context.Validate();
            if (ValidateSequenceOrder(managedValues, probeSequenceCount, sequenceLength, spec) != -1)
                throw new InvalidOperationException($"Motion adapter '{adapter.Name}' advanced the sequence probe before its first Update boundary.");

            double deadline = Time.realtimeSinceStartupAsDouble + sequenceProbeTimeoutSeconds;
            while (true)
            {
                yield return null;
                context.Validate();
                if (ValidateSequenceOrder(managedValues, probeSequenceCount, sequenceLength, spec) >= 2)
                    yield break;
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException($"Motion adapter '{adapter.Name}' did not cross two ordered sequence child boundaries within {sequenceProbeTimeoutSeconds:F0} seconds.");
            }
        }

        try
        {
            foreach (var frame in Flatten(RunProbe(), exception => failure = exception))
                yield return frame;
        }
        finally
        {
            Exception cleanupFailure = null;
            if (context != null)
                cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, context.Dispose);
            if (cleanupFailure != null)
            {
                var combined = failure == null
                    ? cleanupFailure
                    : new AggregateException(failure, cleanupFailure);
                throw new BenchmarkCleanupException("Motion sequence semantic probe cleanup failed.", combined);
            }
        }
        if (failure != null)
            throw failure;
    }

    static int ValidateSequenceOrder(float[] values, int sequenceCount, int sequenceLength,
        in MotionBenchmarkSpec spec)
    {
        int minimumProgress = int.MaxValue;
        for (int sequence = 0; sequence < sequenceCount; sequence++)
        {
            int progress = -1;
            bool suffixStarted = false;
            int offset = sequence * sequenceLength;
            for (int child = 0; child < sequenceLength; child++)
            {
                float value = values[offset + child];
                if (float.IsNaN(value) || value < spec.From - 0.0001f || value > spec.To + 0.0001f)
                    throw new InvalidOperationException($"Sequence probe {sequence} child {child} produced {value} outside [{spec.From}, {spec.To}].");
                bool atStart = Mathf.Approximately(value, spec.From);
                bool atEnd = Mathf.Approximately(value, spec.To);
                if (!suffixStarted && atEnd)
                {
                    progress = child;
                    continue;
                }
                if (!suffixStarted && !atStart)
                {
                    progress = child;
                    suffixStarted = true;
                    continue;
                }
                suffixStarted = true;
                if (!atStart)
                    throw new InvalidOperationException($"Sequence probe {sequence} advanced child {child} before every preceding child reached its end value.");
            }
            minimumProgress = Math.Min(minimumProgress, progress);
        }
        return minimumProgress;
    }

    IEnumerator RunWorkload(MotionBenchmarkAdapter adapter, IReadOnlyList<string> markerNames,
        MotionBenchmarkSpec spec, MotionBenchmarkWorkloadData result)
    {
        var originalPosition = sharedTransform.position;
        Transform[] distinctTargets = null;
        float[] managedValues = null;
        MotionBenchmarkContext context = null;
        RecorderCapture mainThread = null;
        RecorderCapture[] markers = null;
        Exception failure = null;

        IEnumerator Measure()
        {
            if (spec.Workload == MotionBenchmarkWorkload.DistinctTransformPositions)
                distinctTargets = CreateDistinctTargets();
            if (spec.Workload == MotionBenchmarkWorkload.KeyedManagedFloats)
                managedValues = new float[keyedFloatCount];
            else if (spec.Workload == MotionBenchmarkWorkload.Sequences)
                managedValues = new float[checked(sequenceCount * sequenceLength)];

            var request = new MotionBenchmarkRequest(spec, sharedTransform, distinctTargets, managedValues);
            context = adapter.Start(request)
                ?? throw new InvalidOperationException($"Motion adapter '{adapter.Name}' returned no context for {spec.Workload}.");
            context.CaptureOutputBaseline();

            for (int i = 0; i < warmupFrames; i++)
                yield return null;
            context.Validate();
            context.ValidateAdvancement();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (int i = 0; i < recorderSettlingFrames; i++)
                yield return null;

            mainThread = RecorderCapture.Required(ProfilerCategory.Internal, mainThreadMarker,
                measuredFrames + 2, result.mainThread);
            yield return null;
            mainThread.BeginSamples();

            for (int i = 0; i < measuredFrames; i++)
                yield return null;

            mainThread.Finish(measuredFrames);
            mainThread.Dispose();
            mainThread = null;
            try
            {
                context.Validate();
            }
            catch (Exception exception)
            {
                Invalidate(result.mainThread, FailureReason(exception));
                throw;
            }

            markers = CreateMarkerCaptures(markerNames, result);
            if (HasActiveCapture(markers))
            {
                yield return null;
                foreach (var marker in markers)
                    marker.BeginSamples();
                for (int i = 0; i < measuredFrames; i++)
                    yield return null;
                foreach (var marker in markers)
                    marker.Finish(measuredFrames);
            }
            try
            {
                context.Validate();
            }
            catch (Exception exception)
            {
                foreach (var marker in result.markers.Values)
                    if (marker.status == "measured" || marker.status == "partial")
                        Invalidate(marker, FailureReason(exception));
                throw;
            }
            result.status = "measured";
        }

        try
        {
            foreach (var frame in Flatten(Measure(), exception => failure = exception))
                yield return frame;
        }
        finally
        {
            Exception cleanupFailure = null;
            if (result.status == "measuring")
            {
                const string interrupted = "Measurement pass ended before the configured frame count.";
                if (mainThread != null)
                    cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, () => mainThread.Abort(interrupted));
                if (markers != null)
                    foreach (var marker in markers)
                        cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, () => marker.Abort(interrupted));
            }
            if (mainThread != null)
                cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, mainThread.Dispose);
            if (markers != null)
                foreach (var marker in markers)
                    cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, marker.Dispose);
            if (context != null)
                cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, context.Dispose);
            cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, () => sharedTransform.position = originalPosition);
            cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, () => DestroyTargets(distinctTargets));
            if (cleanupFailure != null)
            {
                var combined = failure == null
                    ? cleanupFailure
                    : new AggregateException(failure, cleanupFailure);
                throw new BenchmarkCleanupException("Motion workload cleanup failed.", combined);
            }
        }

        if (failure != null)
            throw failure;
        yield return null;
    }

    static bool HasActiveCapture(RecorderCapture[] captures)
    {
        foreach (var capture in captures)
            if (capture.Active)
                return true;
        return false;
    }

    IEnumerator RunCreation(MotionBenchmarkAdapter adapter, MotionBenchmarkSpec spec,
        MotionBenchmarkCreationData result)
    {
        var originalPosition = sharedTransform.position;
        var request = new MotionBenchmarkRequest(spec, sharedTransform, null, null);
        MotionBenchmarkCreationContext context = null;
        Exception failure = null;

        IEnumerator Measure()
        {
            context = adapter.PrepareCreation(request)
                ?? throw new InvalidOperationException($"Motion adapter '{adapter.Name}' returned no creation context.");
            _ = Stopwatch.GetTimestamp();
            _ = GC.GetAllocatedBytesForCurrentThread();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;

            var firstSample = CaptureCreationSample(context, spec.MotionCount);
            context.ValidateBatch();
            AddCreationSample(result.firstBatch, firstSample);
            MarkMeasured(result.firstBatch);
            ClearCreationBatch(context);
            sharedTransform.position = originalPosition;
            yield return null;

            for (int i = 0; i < creationWarmupBatches; i++)
            {
                context.CreateBatch();
                context.ValidateBatch();
                ClearCreationBatch(context);
                sharedTransform.position = originalPosition;
                yield return null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;

            for (int i = 0; i < creationSamples; i++)
            {
                var sample = CaptureCreationSample(context, spec.MotionCount);
                context.ValidateBatch();
                AddCreationSample(result.warmRecycled, sample);
                ClearCreationBatch(context);
                sharedTransform.position = originalPosition;
                yield return null;
            }

            result.status = "measured";
            MarkMeasured(result.warmRecycled);
        }

        try
        {
            foreach (var frame in Flatten(Measure(), exception => failure = exception))
                yield return frame;
        }
        finally
        {
            Exception cleanupFailure = null;
            if (context != null)
                cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, context.Dispose);
            cleanupFailure = BenchmarkCleanup.Capture(cleanupFailure, () => sharedTransform.position = originalPosition);
            if (cleanupFailure != null)
            {
                var combined = failure == null
                    ? cleanupFailure
                    : new AggregateException(failure, cleanupFailure);
                throw new BenchmarkCleanupException("Motion creation cleanup failed.", combined);
            }
        }
        if (failure != null)
            throw failure;
    }

    static void ClearCreationBatch(MotionBenchmarkCreationContext context)
    {
        try
        {
            context.ClearBatch();
        }
        catch (Exception exception)
        {
            throw new BenchmarkCleanupException("Motion creation batch cleanup failed.", exception);
        }
    }

    static (float time, float allocation) CaptureCreationSample(MotionBenchmarkCreationContext context,
        int batchSize)
    {
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        context.CreateBatch();
        long finished = Stopwatch.GetTimestamp();
        long allocationAfter = GC.GetAllocatedBytesForCurrentThread();

        return ((float)((finished - started) * 1_000_000.0 / Stopwatch.Frequency / batchSize),
            (float)((allocationAfter - allocationBefore) / (double)batchSize));
    }

    static void AddCreationSample(MotionBenchmarkCreationPassData pass,
        (float time, float allocation) sample)
    {
        pass.timePerCreation.samples.Add(sample.time);
        pass.gcBytesPerCreation.samples.Add(sample.allocation);
    }

    static void MarkMeasured(MotionBenchmarkCreationPassData pass)
    {
        pass.status = "measured";
        pass.timePerCreation.status = "measured";
        pass.gcBytesPerCreation.status = "measured";
    }

    static void Unsupported(MotionBenchmarkCreationData result, string reason)
    {
        result.status = "unsupported";
        result.statusReason = reason;
        Unsupported(result.firstBatch, reason);
        Unsupported(result.warmRecycled, reason);
    }

    static void Unsupported(MotionBenchmarkCreationPassData pass, string reason)
    {
        pass.status = "unsupported";
        pass.statusReason = reason;
        pass.timePerCreation.status = "unsupported";
        pass.timePerCreation.statusReason = reason;
        pass.gcBytesPerCreation.status = "unsupported";
        pass.gcBytesPerCreation.statusReason = reason;
    }

    static void Fail(MotionBenchmarkCreationData result, Exception exception)
        => Fail(result, FailureReason(exception));

    static void Fail(MotionBenchmarkCreationData result, string reason)
    {
        Fail(result.firstBatch, reason);
        Fail(result.warmRecycled, reason);
        bool hasData = result.firstBatch.status == "measured" || result.firstBatch.status == "partial" ||
                       result.warmRecycled.status == "measured" || result.warmRecycled.status == "partial";
        result.status = hasData ? "partial" : "failed";
        result.statusReason = reason;
    }

    static void Fail(MotionBenchmarkCreationPassData pass, string reason)
    {
        if (pass.status == "measured") return;
        bool hasSamples = pass.timePerCreation.samples.Count != 0 ||
                          pass.gcBytesPerCreation.samples.Count != 0;
        string status = hasSamples ? "partial" : "failed";
        pass.status = status;
        pass.statusReason = reason;
        pass.timePerCreation.status = status;
        pass.timePerCreation.statusReason = reason;
        pass.gcBytesPerCreation.status = status;
        pass.gcBytesPerCreation.statusReason = reason;
    }

    static void Fail(MotionBenchmarkWorkloadData result, Exception exception,
        IReadOnlyList<string> markerNames) => Fail(result, FailureReason(exception), markerNames);

    static void Fail(MotionBenchmarkWorkloadData result, string reason,
        IReadOnlyList<string> markerNames)
    {
        Fail(result.mainThread, reason);
        foreach (string markerName in markerNames)
        {
            if (!result.markers.TryGetValue(markerName, out var series))
            {
                series = MotionBenchmarkSeriesData.Create(markerName, "milliseconds", 0, "detail");
                result.markers.Add(markerName, series);
            }
            Fail(series, reason);
        }
        bool measured = result.mainThread.status == "measured" || result.mainThread.status == "partial";
        foreach (var series in result.markers.Values)
            measured |= series.status == "measured" || series.status == "partial";
        result.status = measured ? "partial" : "failed";
        result.statusReason = reason;
    }

    static void Fail(MotionBenchmarkSeriesData series, string reason)
    {
        if (series.status == "measured" || series.status == "partial") return;
        series.status = "failed";
        series.statusReason = reason;
    }

    static void Invalidate(MotionBenchmarkSeriesData series, string reason)
    {
        series.status = "failed";
        series.statusReason = reason;
    }

    void FailStep(MotionBenchmarkEngineData engine, MotionBenchmarkWorkload workload,
        IReadOnlyList<string> markerNames, Exception exception)
    {
        if (workload == MotionBenchmarkWorkload.Creation)
        {
            if (engine.creation == null)
                engine.creation = MotionBenchmarkCreationData.Create(CreateSpec(workload), creationSamples);
            Fail(engine.creation, exception);
            return;
        }

        string key = WorkloadKey(workload);
        if (!engine.workloads.TryGetValue(key, out var result))
        {
            result = new MotionBenchmarkWorkloadData
            {
                status = "measuring",
                spec = CreateSpec(workload),
                mainThread = MotionBenchmarkSeriesData.Create(mainThreadMarker, "milliseconds", 0, "headline")
            };
            engine.workloads.Add(key, result);
        }
        Fail(result, exception, markerNames);
    }

    void FailPending(MotionBenchmarkEngineData engine, IReadOnlyList<string> markerNames, string reason,
        bool includeMissing)
    {
        if (includeMissing && engine.creation == null)
            engine.creation = MotionBenchmarkCreationData.Create(CreateSpec(MotionBenchmarkWorkload.Creation),
                creationSamples);
        if (engine.creation != null && engine.creation.status == "measuring")
            Fail(engine.creation, reason);
        foreach (var workload in workloads)
        {
            if (workload == MotionBenchmarkWorkload.Creation) continue;
            string key = WorkloadKey(workload);
            if (!engine.workloads.TryGetValue(key, out var result))
            {
                if (!includeMissing) continue;
                result = new MotionBenchmarkWorkloadData
                {
                    status = "measuring",
                    spec = CreateSpec(workload),
                    mainThread = MotionBenchmarkSeriesData.Create(mainThreadMarker, "milliseconds", 0,
                        "headline")
                };
                engine.workloads.Add(key, result);
            }
            if (result.status == "measuring")
                Fail(result, reason, markerNames);
        }
    }

    static string FailureReason(Exception exception) =>
        $"{exception.GetType().FullName}: {exception.Message}";

    static void FinalizeStatus(MotionBenchmarkEngineData engine)
    {
        bool failed = engine.creation != null &&
                      (engine.creation.status == "failed" || engine.creation.status == "partial");
        bool measured = engine.creation != null &&
                        (engine.creation.status == "measured" || engine.creation.status == "partial");
        foreach (var workload in engine.workloads.Values)
        {
            failed |= workload.status == "failed" || workload.status == "partial";
            measured |= workload.status == "measured" || workload.status == "partial";
        }

        engine.status = failed ? (measured ? "partial" : "failed") : (measured ? "measured" : "unsupported");
        if (failed)
            engine.statusReason = measured
                ? "One or more workloads failed; successful workload results were retained."
                : "Every supported workload failed.";
    }

    RecorderCapture[] CreateMarkerCaptures(IReadOnlyList<string> markerNames,
        MotionBenchmarkWorkloadData result)
    {
        var captures = new RecorderCapture[markerNames.Count];
        for (int i = 0; i < markerNames.Count; i++)
        {
            string markerName = markerNames[i];
            var series = MotionBenchmarkSeriesData.Create(markerName, "milliseconds", measuredFrames, "detail");
            result.markers.Add(markerName, series);
#if ENABLE_PROFILER
            captures[i] = RecorderCapture.Optional(ProfilerCategory.Scripts, markerName,
                measuredFrames + 2, series);
#else
            captures[i] = RecorderCapture.Unavailable(series,
                "Exact motion markers are unavailable because profiler instrumentation is disabled in this player.");
#endif
        }
        return captures;
    }

    static void AddUnavailableMarkers(MotionBenchmarkWorkloadData result, IReadOnlyList<string> markerNames,
        string reason)
    {
        foreach (string markerName in markerNames)
            result.markers.Add(markerName, new MotionBenchmarkSeriesData
            {
                marker = markerName,
                unit = "milliseconds",
                measurementPass = "detail",
                status = "unsupported",
                statusReason = reason
            });
    }

    Transform[] CreateDistinctTargets()
    {
        var targets = new Transform[distinctTransformCount];
        int created = 0;
        try
        {
            while (created < targets.Length)
            {
                var target = Instantiate(distinctTransformPrefab, contextRoot, false);
                targets[created++] = target;
            }
            return targets;
        }
        catch
        {
            for (int i = 0; i < created; i++)
                if (targets[i] != null)
                    UnityEngine.Object.Destroy(targets[i].gameObject);
            throw;
        }
    }

    static void DestroyTargets(Transform[] targets)
    {
        if (targets == null) return;
        foreach (var target in targets)
            if (target != null)
                UnityEngine.Object.Destroy(target.gameObject);
    }

    IEnumerator Cooldown()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        for (int i = 0; i < 5; i++)
            yield return null;
    }

    int MotionCount(MotionBenchmarkWorkload workload) => workload switch
    {
        MotionBenchmarkWorkload.EmptyFrame => 0,
        MotionBenchmarkWorkload.SingleTransformPosition => 1,
        MotionBenchmarkWorkload.SharedTransformPosition => sharedMotionCount,
        MotionBenchmarkWorkload.KeyedManagedFloats => keyedFloatCount,
        MotionBenchmarkWorkload.DistinctTransformPositions => distinctTransformCount,
        MotionBenchmarkWorkload.Sequences => checked(sequenceCount * sequenceLength),
        MotionBenchmarkWorkload.Creation => creationBatchSize,
        _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
    };

    int TransformCount(MotionBenchmarkWorkload workload) => workload switch
    {
        MotionBenchmarkWorkload.EmptyFrame => 0,
        MotionBenchmarkWorkload.SingleTransformPosition => 1,
        MotionBenchmarkWorkload.SharedTransformPosition => 1,
        MotionBenchmarkWorkload.DistinctTransformPositions => distinctTransformCount,
        MotionBenchmarkWorkload.Creation => 1,
        _ => 0
    };

    static string WorkloadKey(MotionBenchmarkWorkload workload) => workload switch
    {
        MotionBenchmarkWorkload.EmptyFrame => "emptyFrame",
        MotionBenchmarkWorkload.SingleTransformPosition => "singleTransformPosition",
        MotionBenchmarkWorkload.SharedTransformPosition => "sharedTransformPosition",
        MotionBenchmarkWorkload.KeyedManagedFloats => "keyedManagedFloats",
        MotionBenchmarkWorkload.DistinctTransformPositions => "distinctTransformPositions",
        MotionBenchmarkWorkload.Sequences => "sequences",
        MotionBenchmarkWorkload.Creation => "creation",
        _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
    };

    MotionBenchmarkSpec CreateSpec(MotionBenchmarkWorkload workload) => new(
        workload,
        MotionCount(workload),
        TransformCount(workload),
        workload == MotionBenchmarkWorkload.Sequences ? sequenceCount : 0,
        workload == MotionBenchmarkWorkload.Sequences ? sequenceLength : 0,
        steadyMotionDuration,
        from,
        to,
        MotionBenchmarkEase.Linear,
        MotionBenchmarkClock.Scaled,
        MotionBenchmarkUpdatePhase.Update,
        1,
        MotionBenchmarkCycleMode.Restart,
        Topology(workload),
        true);

    static MotionBenchmarkTopology Topology(MotionBenchmarkWorkload workload) => workload switch
    {
        MotionBenchmarkWorkload.EmptyFrame => MotionBenchmarkTopology.Empty,
        MotionBenchmarkWorkload.SingleTransformPosition => MotionBenchmarkTopology.IndependentDistinctTargets,
        MotionBenchmarkWorkload.SharedTransformPosition => MotionBenchmarkTopology.IndependentSharedSink,
        MotionBenchmarkWorkload.KeyedManagedFloats => MotionBenchmarkTopology.IndependentKeyedSinks,
        MotionBenchmarkWorkload.DistinctTransformPositions => MotionBenchmarkTopology.IndependentDistinctTargets,
        MotionBenchmarkWorkload.Sequences => MotionBenchmarkTopology.SequentialChains,
        MotionBenchmarkWorkload.Creation => MotionBenchmarkTopology.IndependentSharedSink,
        _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
    };

    void ValidateConfiguration(out string[] adapterNames,
        out IReadOnlyList<string>[] markerNamesByAdapter)
    {
        if (sharedTransform == null)
            throw new InvalidOperationException("MotionBenchmark requires a shared transform.");
        if (distinctTransformPrefab == null)
            throw new InvalidOperationException("MotionBenchmark requires a distinct-transform prefab.");
        if (contextRoot == null)
            throw new InvalidOperationException("MotionBenchmark requires a context root.");
        if (!sharedTransform.gameObject.activeInHierarchy)
            throw new InvalidOperationException("MotionBenchmark shared transform must be active in the hierarchy.");
        if (!contextRoot.gameObject.activeInHierarchy)
            throw new InvalidOperationException("MotionBenchmark context root must be active in the hierarchy.");
        if (!distinctTransformPrefab.gameObject.activeSelf)
            throw new InvalidOperationException("MotionBenchmark distinct-transform prefab must be active.");
        if (distinctTransformPrefab == sharedTransform)
            throw new InvalidOperationException("MotionBenchmark shared transform and distinct-transform prefab must be different objects.");
        if (distinctTransformPrefab == contextRoot || contextRoot.IsChildOf(distinctTransformPrefab))
            throw new InvalidOperationException("MotionBenchmark distinct-transform prefab cannot contain the context root.");
        ValidateBareTransform(sharedTransform, "shared transform");
        ValidateBareTransform(distinctTransformPrefab, "distinct-transform prefab");
        ValidateBareTransform(contextRoot, "context root");
        if (adapters == null || adapters.Length == 0)
            throw new InvalidOperationException("MotionBenchmark requires at least one engine adapter.");
        if (sharedMotionCount <= 0 || keyedFloatCount <= 0 || distinctTransformCount <= 0 ||
            sequenceCount <= 0 || sequenceLength < 3 || creationBatchSize <= 0 ||
            creationWarmupBatches <= 0 || creationSamples <= 0 || warmupFrames <= 0 || measuredFrames <= 0)
            throw new InvalidOperationException("MotionBenchmark counts and sample sizes must be positive, and every sequence must contain at least three children.");
        if (float.IsNaN(steadyMotionDuration) || float.IsInfinity(steadyMotionDuration) || steadyMotionDuration <= 0f)
            throw new InvalidOperationException("MotionBenchmark duration must be finite and positive.");
        if (float.IsNaN(Time.timeScale) || float.IsInfinity(Time.timeScale) || Time.timeScale <= 0f)
            throw new InvalidOperationException("MotionBenchmark requires a finite positive Time.timeScale.");
        if ((double)steadyMotionDuration * sequenceLength > float.MaxValue)
            throw new InvalidOperationException("MotionBenchmark sequence duration exceeds the supported finite range.");
        if (sequenceCount > int.MaxValue / sequenceLength)
            throw new InvalidOperationException("MotionBenchmark sequence dimensions exceed the supported motion count.");
        if (sequenceCount * sequenceLength > int.MaxValue - sequenceCount)
            throw new InvalidOperationException("MotionBenchmark sequence children and roots exceed the supported total core count.");
        if (measuredFrames > int.MaxValue - 2)
            throw new InvalidOperationException("MotionBenchmark measured frame count exceeds ProfilerRecorder capacity.");

        adapterNames = new string[adapters.Length];
        markerNamesByAdapter = new IReadOnlyList<string>[adapters.Length];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
        {
            var adapter = adapters[adapterIndex];
            if (adapter == null)
                throw new InvalidOperationException("MotionBenchmark adapter slots cannot be null.");
            string adapterName = adapter.Name;
            if (string.IsNullOrWhiteSpace(adapterName))
                throw new InvalidOperationException($"Motion benchmark adapter '{adapter.GetType().FullName}' has no name.");
            if (!names.Add(adapterName))
                throw new InvalidOperationException($"Motion benchmark adapter name '{adapterName}' is duplicated.");
            adapterNames[adapterIndex] = adapterName;

            var sourceMarkers = adapter.ProfilerMarkers
                ?? throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' returned no marker collection.");
            int markerCount = sourceMarkers.Count;
            var markerNames = new string[markerCount];
            var uniqueMarkers = new HashSet<string>(StringComparer.Ordinal);
            for (int markerIndex = 0; markerIndex < markerCount; markerIndex++)
            {
                string markerName = sourceMarkers[markerIndex];
                if (string.IsNullOrWhiteSpace(markerName))
                    throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has an empty profiler marker.");
                if (!uniqueMarkers.Add(markerName))
                    throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' repeats profiler marker '{markerName}'.");
                markerNames[markerIndex] = markerName;
            }
            markerNamesByAdapter[adapterIndex] = markerNames;
        }
    }

    static void ValidateMetadata(string adapterName, in MotionBenchmarkAdapterMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.EngineVersion))
            throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has no engine version.");
        if (string.IsNullOrWhiteSpace(metadata.SourceRevision))
            throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has no source revision identity.");
        if (string.IsNullOrWhiteSpace(metadata.Integration))
            throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has no integration description.");
        if (string.IsNullOrWhiteSpace(metadata.CapacityPolicy))
            throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has no capacity policy.");
        if (string.IsNullOrWhiteSpace(metadata.HandleRetention))
            throw new InvalidOperationException($"Motion benchmark adapter '{adapterName}' has no handle-retention policy.");
    }

    static void ValidateBareTransform(Transform target, string role)
    {
        if (target.childCount != 0 || target.GetComponents<Component>().Length != 1)
            throw new InvalidOperationException($"MotionBenchmark {role} must contain only one Transform and no children.");
    }

    sealed class RecorderCapture : IDisposable
    {
        readonly MotionBenchmarkSeriesData series;
        readonly bool required;
        ProfilerRecorder recorder;
        bool active;
        bool sampling;
        bool finished;
        int firstSample;

        internal bool Active => active;

        RecorderCapture(MotionBenchmarkSeriesData series, bool required)
        {
            this.series = series;
            this.required = required;
        }

        internal static RecorderCapture Required(ProfilerCategory category, string marker, int capacity,
            MotionBenchmarkSeriesData series)
        {
            var capture = TryStart(category, marker, capacity, series, true);
            if (!capture.active)
                throw new NotSupportedException($"Required ProfilerRecorder '{marker}' is unavailable: {series.statusReason}");
            return capture;
        }

        internal static RecorderCapture Optional(ProfilerCategory category, string marker, int capacity,
            MotionBenchmarkSeriesData series) => TryStart(category, marker, capacity, series, false);

        internal static RecorderCapture Unavailable(MotionBenchmarkSeriesData series, string reason)
        {
            series.status = "unavailable";
            series.statusReason = reason;
            return new RecorderCapture(series, false);
        }

        internal void BeginSamples()
        {
            if (!active) return;
            firstSample = recorder.Count;
            sampling = true;
        }

        static RecorderCapture TryStart(ProfilerCategory category, string marker, int capacity,
            MotionBenchmarkSeriesData series, bool required)
        {
            var capture = new RecorderCapture(series, required);
            try
            {
                capture.recorder = ProfilerRecorder.StartNew(category, marker, capacity,
                    ProfilerRecorderOptions.Default |
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                    ProfilerRecorderOptions.SumAllSamplesInFrame);
                capture.active = capture.recorder.Valid;
                if (capture.active && capture.recorder.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds)
                {
                    var unit = capture.recorder.UnitType;
                    capture.recorder.Dispose();
                    capture.active = false;
                    series.status = "unavailable";
                    series.statusReason = $"ProfilerRecorder '{marker}' reports {unit} instead of nanoseconds.";
                    return capture;
                }
                if (capture.active)
                {
                    series.status = "measuring";
                }
                else
                {
                    capture.recorder.Dispose();
                    series.status = "unavailable";
                    series.statusReason = $"ProfilerRecorder '{marker}' is not exposed by this player.";
                }
            }
            catch (Exception exception)
            {
                capture.recorder.Dispose();
                capture.active = false;
                if (required)
                    throw new InvalidOperationException($"Required ProfilerRecorder '{marker}' could not start.", exception);
                series.status = "unavailable";
                series.statusReason = $"ProfilerRecorder '{marker}' could not start: {exception.GetType().Name}: {exception.Message}";
            }
            return capture;
        }

        internal void Finish(int expectedSamples)
        {
            if (!active) return;
            recorder.Stop();
            CopySamples();
            finished = true;

            if (series.samples.Count == expectedSamples)
            {
                series.status = "measured";
                return;
            }
            if (required)
                throw new InvalidOperationException($"Required ProfilerRecorder '{series.marker}' emitted {series.samples.Count} of {expectedSamples} frame samples.");
            series.status = "unavailable";
            series.statusReason = $"ProfilerRecorder '{series.marker}' emitted {series.samples.Count} of {expectedSamples} frame samples.";
        }

        internal void Abort(string reason)
        {
            if (!active || finished) return;
            recorder.Stop();
            if (sampling)
                CopySamples();
            finished = true;
            series.status = series.samples.Count == 0 ? "failed" : "partial";
            series.statusReason = reason;
        }

        void CopySamples()
        {
            series.samples.Clear();
            for (int i = firstSample; i < recorder.Count; i++)
            {
                var sample = recorder.GetSample(i);
                if (sample.Count > 0)
                    series.samples.Add(sample.Value * 0.000001f);
            }
        }

        public void Dispose()
        {
            if (!active) return;
            recorder.Dispose();
            active = false;
        }
    }
}

/// <summary>Identifies a standardized benchmark workload supplied to an engine adapter.</summary>
public enum MotionBenchmarkWorkload
{
    /// <summary>No live motion; establishes the scene and player frame baseline.</summary>
    EmptyFrame,
    /// <summary>One position motion writing one transform.</summary>
    SingleTransformPosition,
    /// <summary>Many position motions composited onto one transform.</summary>
    SharedTransformPosition,
    /// <summary>Keyed float motions writing distinct slots in one managed array.</summary>
    KeyedManagedFloats,
    /// <summary>One position motion for every distinct transform.</summary>
    DistinctTransformPositions,
    /// <summary>Repeated timelines containing multiple managed float motions.</summary>
    Sequences,
    /// <summary>Batches used only to measure creation time and managed allocation.</summary>
    Creation
}

/// <summary>Curve shape every engine must represent identically.</summary>
public enum MotionBenchmarkEase
{
    /// <summary>Constant-rate interpolation from the authored start to end value.</summary>
    Linear
}

/// <summary>Time domain that advances the workload.</summary>
public enum MotionBenchmarkClock
{
    /// <summary>Unity game time affected by <see cref="Time.timeScale"/>.</summary>
    Scaled
}

/// <summary>Unity player phase in which the workload advances.</summary>
public enum MotionBenchmarkUpdatePhase
{
    /// <summary>The script Update phase.</summary>
    Update
}

/// <summary>Behavior after a workload cycle completes.</summary>
public enum MotionBenchmarkCycleMode
{
    /// <summary>Returns to the authored start before another cycle.</summary>
    Restart
}

/// <summary>Ownership and sink arrangement an adapter must reproduce.</summary>
public enum MotionBenchmarkTopology
{
    /// <summary>No motions and no sinks.</summary>
    Empty,
    /// <summary>Independent motions competing for one sink.</summary>
    IndependentSharedSink,
    /// <summary>Independent motions writing keyed slots in one container.</summary>
    IndependentKeyedSinks,
    /// <summary>Independent motions writing separate transform targets.</summary>
    IndependentDistinctTargets,
    /// <summary>Independent timelines whose children run one after another.</summary>
    SequentialChains
}

/// <summary>Complete immutable semantics and dimensions of one comparable workload.</summary>
public readonly struct MotionBenchmarkSpec
{
    internal MotionBenchmarkSpec(MotionBenchmarkWorkload workload, int motionCount, int transformCount,
        int sequenceCount, int sequenceLength, float durationSeconds, float from, float to,
        MotionBenchmarkEase ease, MotionBenchmarkClock clock, MotionBenchmarkUpdatePhase updatePhase,
        int cycles, MotionBenchmarkCycleMode cycleMode, MotionBenchmarkTopology topology, bool essential)
    {
        Workload = workload;
        MotionCount = motionCount;
        TransformCount = transformCount;
        SequenceCount = sequenceCount;
        SequenceLength = sequenceLength;
        DurationSeconds = durationSeconds;
        From = from;
        To = to;
        Ease = ease;
        Clock = clock;
        UpdatePhase = updatePhase;
        Cycles = cycles;
        CycleMode = cycleMode;
        Topology = topology;
        Essential = essential;
    }

    /// <summary>The operation shape the adapter must create.</summary>
    public MotionBenchmarkWorkload Workload { get; }

    /// <summary>Number of requested independent motions, sequence children, or creations in a batch.</summary>
    public int MotionCount { get; }

    /// <summary>Number of motions owned as children by sequence roots.</summary>
    public int ChildMotionCount => Workload == MotionBenchmarkWorkload.Sequences ? MotionCount : 0;

    /// <summary>Number of live roots that own sequence children.</summary>
    public int SequenceRootCount => Workload == MotionBenchmarkWorkload.Sequences ? SequenceCount : 0;

    /// <summary>Total live MotionCore population created by the workload or creation batch.</summary>
    public int TotalLiveCoreCount => checked(MotionCount + SequenceRootCount);

    /// <summary>Number of transform sinks written by the workload.</summary>
    public int TransformCount { get; }

    /// <summary>Number of independent timelines in the sequence workload.</summary>
    public int SequenceCount { get; }

    /// <summary>Number of children chained into every sequence.</summary>
    public int SequenceLength { get; }

    /// <summary>Finite seconds occupied by every motion or sequence child.</summary>
    public float DurationSeconds { get; }

    /// <summary>Scalar start value, mapped componentwise for position workloads.</summary>
    public float From { get; }

    /// <summary>Scalar end value, mapped componentwise for position workloads.</summary>
    public float To { get; }

    /// <summary>Curve shape applied to every motion.</summary>
    public MotionBenchmarkEase Ease { get; }

    /// <summary>Time domain that advances every root.</summary>
    public MotionBenchmarkClock Clock { get; }

    /// <summary>Unity player phase that advances every root.</summary>
    public MotionBenchmarkUpdatePhase UpdatePhase { get; }

    /// <summary>Finite number of passes over each motion or sequence.</summary>
    public int Cycles { get; }

    /// <summary>Behavior between cycles.</summary>
    public MotionBenchmarkCycleMode CycleMode { get; }

    /// <summary>Ownership and sink arrangement the adapter must reproduce.</summary>
    public MotionBenchmarkTopology Topology { get; }

    /// <summary>Whether accessibility policy must leave the workload running.</summary>
    public bool Essential { get; }
}

/// <summary>Immutable engine identity and preparation policy stored with every result.</summary>
public readonly struct MotionBenchmarkAdapterMetadata
{
    /// <summary>Creates the provenance record an adapter guarantees for its engine integration.</summary>
    public MotionBenchmarkAdapterMetadata(string engineVersion, string sourceRevision, string integration,
        string capacityPolicy, string handleRetention)
    {
        EngineVersion = engineVersion;
        SourceRevision = sourceRevision;
        Integration = integration;
        CapacityPolicy = capacityPolicy;
        HandleRetention = handleRetention;
    }

    /// <summary>Engine package or assembly version.</summary>
    public string EngineVersion { get; }

    /// <summary>Commit, package lock identity, or explicit reference to the enclosing run revision.</summary>
    public string SourceRevision { get; }

    /// <summary>How the harness calls the engine without measured-path discovery.</summary>
    public string Integration { get; }

    /// <summary>Capacity reservation and pool preparation applied before measurement.</summary>
    public string CapacityPolicy { get; }

    /// <summary>How every returned handle is consumed during creation measurement.</summary>
    public string HandleRetention { get; }
}

/// <summary>Immutable specification and prepared targets for one adapter invocation.</summary>
public readonly struct MotionBenchmarkRequest
{
    internal MotionBenchmarkRequest(in MotionBenchmarkSpec spec, Transform sharedTransform,
        Transform[] distinctTransforms, float[] managedValues)
    {
        Spec = spec;
        SharedTransform = sharedTransform;
        DistinctTransforms = distinctTransforms;
        ManagedValues = managedValues;
    }

    /// <summary>Complete semantics and dimensions the adapter must reproduce.</summary>
    public MotionBenchmarkSpec Spec { get; }

    /// <summary>The single position target shared by the shared-transform and creation cases.</summary>
    public Transform SharedTransform { get; }

    /// <summary>Unique position targets for the distinct-transform case.</summary>
    public IReadOnlyList<Transform> DistinctTransforms { get; }

    /// <summary>Mutable keyed destinations for managed-float and sequence cases.</summary>
    public float[] ManagedValues { get; }
}

/// <summary>
/// Direct integration point for a motion engine. Implementations create their native motions before sampling;
/// no discovery, reflection, or delegate translation occurs in the measured frame path.
/// </summary>
[Serializable]
public abstract class MotionBenchmarkAdapter
{
    /// <summary>Stable result key used to compare this adapter across runs.</summary>
    public abstract string Name { get; }

    /// <summary>Engine identity and preparation policies serialized with every result.</summary>
    public abstract MotionBenchmarkAdapterMetadata Metadata { get; }

    /// <summary>Exact elapsed-time markers sampled in a detail pass separate from the headline frame pass.</summary>
    public virtual IReadOnlyList<string> ProfilerMarkers => Array.Empty<string>();

    /// <summary>Returns a precise reason when the adapter cannot represent a workload, or null when it can.</summary>
    public virtual string UnsupportedReason(in MotionBenchmarkSpec spec) => null;

    /// <summary>Creates and starts the complete requested workload.</summary>
    public abstract MotionBenchmarkContext Start(in MotionBenchmarkRequest request);

    /// <summary>Prepares reusable storage for warmed creation batches without creating measured motions yet.</summary>
    public abstract MotionBenchmarkCreationContext PrepareCreation(in MotionBenchmarkRequest request);
}

/// <summary>Owns one live workload and must synchronously stop it when disposed.</summary>
public abstract class MotionBenchmarkContext : IDisposable
{
    /// <summary>Verifies live count, topology, and current outputs without running inside a sampled frame.</summary>
    public abstract void Validate();

    /// <summary>Captures workload outputs immediately before warmup for later advancement validation.</summary>
    public abstract void CaptureOutputBaseline();

    /// <summary>Verifies that every non-empty workload produced an observable output change during warmup.</summary>
    public abstract void ValidateAdvancement();

    /// <summary>Stops every motion and releases adapter-owned workload state.</summary>
    public abstract void Dispose();
}

/// <summary>Owns reusable state for isolated creation batches.</summary>
public abstract class MotionBenchmarkCreationContext : IDisposable
{
    /// <summary>Creates exactly the requested batch without performing cleanup.</summary>
    public abstract void CreateBatch();

    /// <summary>Verifies that the complete requested batch is live without altering it.</summary>
    public abstract void ValidateBatch();

    /// <summary>Synchronously stops and releases the current batch while retaining reusable harness storage.</summary>
    public abstract void ClearBatch();

    /// <summary>Releases any live batch and all adapter-owned creation state.</summary>
    public abstract void Dispose();
}

internal static class MotionBenchmarkValidation
{
    internal static void RequireRange(Vector3 value, in MotionBenchmarkSpec spec, string engine,
        string role = "position", int index = -1)
    {
        RequireRange(value.x, spec, engine, $"{role} x", index);
        RequireRange(value.y, spec, engine, $"{role} y", index);
        RequireRange(value.z, spec, engine, $"{role} z", index);
    }

    internal static void RequireRange(float value, in MotionBenchmarkSpec spec, string engine,
        string role, int index = -1)
    {
        float min = Mathf.Min(spec.From, spec.To);
        float max = Mathf.Max(spec.From, spec.To);
        if (float.IsNaN(value) || float.IsInfinity(value) || value < min - 0.0001f || value > max + 0.0001f)
            throw new InvalidOperationException(index < 0
                ? $"{engine} {role} produced {value} outside [{min}, {max}]."
                : $"{engine} {role} {index} produced {value} outside [{min}, {max}].");
    }

    internal static void RequireEqual(Vector3 actual, Vector3 expected, string engine,
        string role, int index = -1)
    {
        if (!IsFinite(actual) || !IsFinite(expected) ||
            (actual - expected).sqrMagnitude > 0.00000001f)
            throw new InvalidOperationException(index < 0
                ? $"{engine} {role} wrote {actual} instead of {expected}."
                : $"{engine} {role} {index} wrote {actual} instead of {expected}.");
    }

    internal static void RequireEqual(float actual, float expected, string engine,
        string role, int index = -1)
    {
        if (!Mathf.Approximately(actual, expected))
            throw new InvalidOperationException(index < 0
                ? $"{engine} {role} was {actual} instead of {expected}."
                : $"{engine} {role} {index} was {actual} instead of {expected}.");
    }

    internal static void RequireEqual(double actual, double expected, string engine,
        string role, int index = -1)
    {
        double tolerance = Math.Max(0.000001d, Math.Abs(expected) * 0.000001d);
        if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException(index < 0
                ? $"{engine} {role} was {actual} instead of {expected}."
                : $"{engine} {role} {index} was {actual} instead of {expected}.");
    }

    internal static void RequireChanged(Vector3 actual, Vector3 initial, string engine,
        string role, int index = -1)
    {
        if ((actual - initial).sqrMagnitude == 0f)
            throw new InvalidOperationException(index < 0
                ? $"{engine} {role} did not advance during warmup."
                : $"{engine} {role} {index} did not advance during warmup.");
    }

    internal static void RequireChanged(float actual, float initial, string engine,
        string role, int index)
    {
        if (actual == initial)
            throw new InvalidOperationException($"{engine} {role} {index} did not advance during warmup.");
    }

    static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}

internal sealed class MotionBenchmarkOutputProbe
{
    readonly MotionBenchmarkRequest request;
    readonly string engine;
    readonly Vector3 emptyPosition;
    Vector3 sharedBaseline;
    Vector3[] distinctBaselines;
    float[] managedBaselines;
    bool baselineCaptured;

    internal MotionBenchmarkOutputProbe(in MotionBenchmarkRequest request, string engine)
    {
        this.request = request;
        this.engine = engine;
        emptyPosition = request.SharedTransform.position;
    }

    internal void CaptureBaseline(int motionCount)
    {
        switch (request.Spec.Workload)
        {
            case MotionBenchmarkWorkload.EmptyFrame:
            case MotionBenchmarkWorkload.SingleTransformPosition:
            case MotionBenchmarkWorkload.SharedTransformPosition:
                sharedBaseline = request.SharedTransform.position;
                break;
            case MotionBenchmarkWorkload.KeyedManagedFloats:
            case MotionBenchmarkWorkload.Sequences:
                managedBaselines = new float[request.ManagedValues.Length];
                Array.Copy(request.ManagedValues, managedBaselines, managedBaselines.Length);
                break;
            case MotionBenchmarkWorkload.DistinctTransformPositions:
                distinctBaselines = new Vector3[motionCount];
                for (int i = 0; i < motionCount; i++)
                    distinctBaselines[i] = request.DistinctTransforms[i].position;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Spec.Workload, null);
        }
        baselineCaptured = true;
    }

    internal void ValidateCurrent(int motionCount)
    {
        switch (request.Spec.Workload)
        {
            case MotionBenchmarkWorkload.EmptyFrame:
                MotionBenchmarkValidation.RequireEqual(request.SharedTransform.position, emptyPosition,
                    engine, "empty-frame shared target");
                return;
            case MotionBenchmarkWorkload.SingleTransformPosition:
            case MotionBenchmarkWorkload.SharedTransformPosition:
                MotionBenchmarkValidation.RequireRange(request.SharedTransform.position,
                    request.Spec, engine);
                return;
            case MotionBenchmarkWorkload.KeyedManagedFloats:
            case MotionBenchmarkWorkload.Sequences:
                for (int i = 0; i < motionCount; i++)
                    MotionBenchmarkValidation.RequireRange(request.ManagedValues[i], request.Spec,
                        engine, "managed value", i);
                return;
            case MotionBenchmarkWorkload.DistinctTransformPositions:
                for (int i = 0; i < motionCount; i++)
                    MotionBenchmarkValidation.RequireRange(request.DistinctTransforms[i].position,
                        request.Spec, engine, "position", i);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Spec.Workload, null);
        }
    }

    internal void ValidateAdvancement(int motionCount, int sequenceCount)
    {
        if (!baselineCaptured)
            throw new InvalidOperationException($"{engine} output baseline was not captured before warmup.");
        switch (request.Spec.Workload)
        {
            case MotionBenchmarkWorkload.EmptyFrame:
                return;
            case MotionBenchmarkWorkload.SingleTransformPosition:
            case MotionBenchmarkWorkload.SharedTransformPosition:
                MotionBenchmarkValidation.RequireChanged(request.SharedTransform.position, sharedBaseline,
                    engine, "shared-transform output");
                return;
            case MotionBenchmarkWorkload.KeyedManagedFloats:
                for (int i = 0; i < motionCount; i++)
                    MotionBenchmarkValidation.RequireChanged(request.ManagedValues[i], managedBaselines[i],
                        engine, "managed value", i);
                return;
            case MotionBenchmarkWorkload.DistinctTransformPositions:
                for (int i = 0; i < motionCount; i++)
                    MotionBenchmarkValidation.RequireChanged(request.DistinctTransforms[i].position,
                        distinctBaselines[i], engine, "distinct-transform output", i);
                return;
            case MotionBenchmarkWorkload.Sequences:
                for (int i = 0; i < sequenceCount; i++)
                {
                    int activeChild = i * request.Spec.SequenceLength;
                    MotionBenchmarkValidation.RequireChanged(request.ManagedValues[activeChild],
                        managedBaselines[activeChild], engine, "sequence active output", i);
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Spec.Workload, null);
        }
    }
}

/// <summary>Direct adapter for the LightSide motion engine.</summary>
[Serializable]
public sealed class LightSideMotionBenchmarkAdapter : MotionBenchmarkAdapter
{
    static readonly string[] markers =
    {
        "LightSide.Motion.Total",
        "LightSide.Motion.Advance",
        "LightSide.Motion.Write"
    };

    /// <inheritdoc/>
    public override string Name => "lightSide";

    /// <inheritdoc/>
    public override MotionBenchmarkAdapterMetadata Metadata => new(
        typeof(Motion).Assembly.GetName().Version?.ToString() ?? "unknown",
        "run.meta.commit",
        "directPublicApi",
        "engineDefault",
        "preallocatedMotionHandleArray");

    /// <inheritdoc/>
    public override IReadOnlyList<string> ProfilerMarkers => markers;

    /// <inheritdoc/>
    public override string UnsupportedReason(in MotionBenchmarkSpec spec)
    {
        if (spec.Ease != MotionBenchmarkEase.Linear) return $"Unsupported ease: {spec.Ease}.";
        if (spec.Clock != MotionBenchmarkClock.Scaled) return $"Unsupported clock: {spec.Clock}.";
        if (spec.UpdatePhase != MotionBenchmarkUpdatePhase.Update)
            return $"Unsupported update phase: {spec.UpdatePhase}.";
        if (spec.Cycles != 1) return $"Unsupported cycle count: {spec.Cycles}.";
        if (spec.CycleMode != MotionBenchmarkCycleMode.Restart)
            return $"Unsupported cycle mode: {spec.CycleMode}.";
        return null;
    }

    /// <inheritdoc/>
    public override MotionBenchmarkContext Start(in MotionBenchmarkRequest request)
    {
        var timing = CreateTiming(request.Spec);
        return request.Spec.Workload switch
        {
            MotionBenchmarkWorkload.EmptyFrame => LightSideContext.ForMotions(request),
            MotionBenchmarkWorkload.SingleTransformPosition => StartShared(request, timing),
            MotionBenchmarkWorkload.SharedTransformPosition => StartShared(request, timing),
            MotionBenchmarkWorkload.KeyedManagedFloats => StartKeyed(request, timing),
            MotionBenchmarkWorkload.DistinctTransformPositions => StartDistinct(request, timing),
            MotionBenchmarkWorkload.Sequences => StartSequences(request, timing),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Spec.Workload, null)
        };
    }

    /// <inheritdoc/>
    public override MotionBenchmarkCreationContext PrepareCreation(in MotionBenchmarkRequest request)
    {
        if (request.Spec.Workload != MotionBenchmarkWorkload.Creation)
            throw new ArgumentException("A creation context requires the creation workload.", nameof(request));
        return new LightSideCreationContext(request.SharedTransform, request.Spec,
            CreateTiming(request.Spec));
    }

    static MotionTiming CreateTiming(in MotionBenchmarkSpec spec)
    {
        var timing = MotionTiming.Of(spec.DurationSeconds, EasingType.Linear);
        timing.Clock = PlaybackClock.Scaled;
        timing.UpdatePhase = MotionUpdatePhase.Update;
        timing.Cycles = spec.Cycles;
        timing.CycleMode = MotionCycle.Restart;
        timing.Essential = spec.Essential;
        return timing;
    }

    static MotionBenchmarkContext StartShared(in MotionBenchmarkRequest request, in MotionTiming timing)
    {
        var context = LightSideContext.ForMotions(request);
        var from = Vector3.one * request.Spec.From;
        var to = Vector3.one * request.Spec.To;
        try
        {
            for (int i = 0; i < request.Spec.MotionCount; i++)
                context.Add(Motion.Drive(request.SharedTransform, MotionChannel.Position,
                    from, to, timing));
            return context;
        }
        catch (Exception exception)
        {
            DisposeAfterStartFailure(context, exception);
            throw;
        }
    }

    static MotionBenchmarkContext StartKeyed(in MotionBenchmarkRequest request, in MotionTiming timing)
    {
        var context = LightSideContext.ForMotions(request);
        try
        {
            for (int i = 0; i < request.Spec.MotionCount; i++)
                context.Add(Motion.To(request.ManagedValues, i, request.Spec.From, request.Spec.To, timing,
                    static (values, key, value) => values[key] = value));
            return context;
        }
        catch (Exception exception)
        {
            DisposeAfterStartFailure(context, exception);
            throw;
        }
    }

    static MotionBenchmarkContext StartDistinct(in MotionBenchmarkRequest request, in MotionTiming timing)
    {
        var context = LightSideContext.ForMotions(request);
        var from = Vector3.one * request.Spec.From;
        var to = Vector3.one * request.Spec.To;
        try
        {
            for (int i = 0; i < request.Spec.MotionCount; i++)
                context.Add(Motion.Drive(request.DistinctTransforms[i], MotionChannel.Position,
                    from, to, timing));
            return context;
        }
        catch (Exception exception)
        {
            DisposeAfterStartFailure(context, exception);
            throw;
        }
    }

    static MotionBenchmarkContext StartSequences(in MotionBenchmarkRequest request, in MotionTiming timing)
    {
        var context = LightSideContext.ForSequences(request);
        try
        {
            int key = 0;
            for (int i = 0; i < request.Spec.SequenceCount; i++)
            {
                var sequence = MotionSequence.Create();
                context.Add(sequence);
                for (int child = 0; child < request.Spec.SequenceLength; child++, key++)
                {
                    var motion = Motion.To(request.ManagedValues, key, request.Spec.From, request.Spec.To, timing,
                        static (values, index, value) => values[index] = value);
                    context.Add(motion);
                    sequence.Chain(motion);
                }
            }
            return context;
        }
        catch (Exception exception)
        {
            DisposeAfterStartFailure(context, exception);
            throw;
        }
    }

    static void DisposeAfterStartFailure(MotionBenchmarkContext context, Exception primary)
    {
        try
        {
            context.Dispose();
        }
        catch (Exception cleanup)
        {
            throw new BenchmarkCleanupException("Motion adapter setup cleanup failed.",
                new AggregateException(primary, cleanup));
        }
    }

    sealed class LightSideContext : MotionBenchmarkContext
    {
        readonly MotionBenchmarkRequest request;
        readonly Motion[] motions;
        readonly MotionSequence[] sequences;
        readonly MotionBenchmarkOutputProbe output;
        int motionCount;
        int sequenceCount;

        LightSideContext(in MotionBenchmarkRequest request, Motion[] motions, MotionSequence[] sequences)
        {
            this.request = request;
            this.motions = motions;
            this.sequences = sequences;
            output = new MotionBenchmarkOutputProbe(request, "LightSide");
        }

        internal static LightSideContext ForMotions(in MotionBenchmarkRequest request) =>
            new(request, new Motion[request.Spec.MotionCount], null);

        internal static LightSideContext ForSequences(in MotionBenchmarkRequest request) =>
            new(request, new Motion[request.Spec.MotionCount], new MotionSequence[request.Spec.SequenceCount]);

        internal void Add(Motion motion) => motions[motionCount++] = motion;

        internal void Add(MotionSequence sequence) => sequences[sequenceCount++] = sequence;

        public override void CaptureOutputBaseline() => output.CaptureBaseline(motionCount);

        public override void Validate()
        {
            if (motionCount != request.Spec.MotionCount)
                throw new InvalidOperationException($"LightSide created {motionCount} of {request.Spec.MotionCount} requested motions.");
            if (sequenceCount != request.Spec.SequenceCount)
                throw new InvalidOperationException($"LightSide created {sequenceCount} of {request.Spec.SequenceCount} requested sequences.");

            for (int i = 0; i < motionCount; i++)
                if (!motions[i].IsAlive)
                    throw new InvalidOperationException($"LightSide motion {i} ended before the validation boundary.");
            for (int i = 0; i < sequenceCount; i++)
                if (!sequences[i].IsAlive)
                    throw new InvalidOperationException($"LightSide sequence {i} ended before the validation boundary.");

            output.ValidateCurrent(motionCount);
            switch (request.Spec.Workload)
            {
                case MotionBenchmarkWorkload.EmptyFrame:
                    break;
                case MotionBenchmarkWorkload.SingleTransformPosition:
                case MotionBenchmarkWorkload.SharedTransformPosition:
                    ValidateShared();
                    break;
                case MotionBenchmarkWorkload.KeyedManagedFloats:
                    ValidateManaged();
                    break;
                case MotionBenchmarkWorkload.DistinctTransformPositions:
                    ValidateDistinct();
                    break;
                case MotionBenchmarkWorkload.Sequences:
                    ValidateSequences();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.Spec.Workload, null);
            }
        }

        public override void ValidateAdvancement() =>
            output.ValidateAdvancement(motionCount, sequenceCount);

        void ValidateShared()
        {
            var actual = request.SharedTransform.position;
            for (int i = 0; i < motionCount; i++)
                MotionBenchmarkValidation.RequireEqual(motions[i].Value<Vector3>(), actual,
                    "LightSide", "shared-transform motion", i);
        }

        void ValidateManaged()
        {
            for (int i = 0; i < motionCount; i++)
            {
                float actual = request.ManagedValues[i];
                MotionBenchmarkValidation.RequireEqual(motions[i].Value<float>(), actual,
                    "LightSide", "managed motion", i);
            }
        }

        void ValidateDistinct()
        {
            for (int i = 0; i < motionCount; i++)
            {
                var actual = request.DistinctTransforms[i].position;
                MotionBenchmarkValidation.RequireEqual(motions[i].Value<Vector3>(), actual,
                    "LightSide", "distinct-transform motion", i);
            }
        }

        void ValidateSequences()
        {
            float expectedDuration = request.Spec.DurationSeconds * request.Spec.SequenceLength;
            for (int i = 0; i < sequenceCount; i++)
            {
                MotionBenchmarkValidation.RequireEqual(sequences[i].ContentDuration, expectedDuration,
                    "LightSide", "sequence content duration", i);
                MotionBenchmarkValidation.RequireEqual(sequences[i].Duration, expectedDuration,
                    "LightSide", "sequence total duration", i);
            }
            ValidateManaged();
        }

        public override void Dispose()
        {
            Exception failure = null;
            if (sequences != null)
            {
                for (int i = sequenceCount - 1; i >= 0; i--)
                {
                    var sequence = sequences[i];
                    sequences[i] = null;
                    failure = BenchmarkCleanup.Capture(failure, sequence.Stop);
                }
            }
            for (int i = motionCount - 1; i >= 0; i--)
            {
                var motion = motions[i];
                motions[i] = default;
                failure = BenchmarkCleanup.Capture(failure, motion.Stop);
            }
            motionCount = 0;
            sequenceCount = 0;
            if (failure != null)
                throw failure;
        }
    }

    sealed class LightSideCreationContext : MotionBenchmarkCreationContext
    {
        readonly Transform target;
        readonly Motion[] motions;
        readonly MotionTiming timing;
        readonly MotionBenchmarkSpec spec;
        int count;

        internal LightSideCreationContext(Transform target, in MotionBenchmarkSpec spec, in MotionTiming timing)
        {
            this.target = target;
            motions = new Motion[spec.MotionCount];
            this.timing = timing;
            this.spec = spec;
        }

        public override void CreateBatch()
        {
            if (count != 0)
                throw new InvalidOperationException("The previous creation batch is still live.");
            try
            {
                for (; count < motions.Length; count++)
                    motions[count] = Motion.Drive(target, MotionChannel.Position,
                        Vector3.one * spec.From, Vector3.one * spec.To, timing);
            }
            catch (Exception primary)
            {
                try
                {
                    ClearBatch();
                }
                catch (Exception cleanup)
                {
                    throw new BenchmarkCleanupException("LightSide creation setup cleanup failed.",
                        new AggregateException(primary, cleanup));
                }
                throw;
            }
        }

        public override void ValidateBatch()
        {
            if (count != spec.MotionCount)
                throw new InvalidOperationException($"LightSide created {count} of {spec.MotionCount} requested motions.");
            for (int i = 0; i < count; i++)
                if (!motions[i].IsAlive)
                    throw new InvalidOperationException($"LightSide creation motion {i} ended before cleanup.");
        }

        public override void ClearBatch()
        {
            Exception failure = null;
            for (int i = count - 1; i >= 0; i--)
            {
                var motion = motions[i];
                motions[i] = default;
                failure = BenchmarkCleanup.Capture(failure, motion.Stop);
            }
            count = 0;
            if (failure != null)
                throw failure;
        }

        public override void Dispose() => ClearBatch();
    }
}

internal sealed class MotionBenchmarkData
{
    internal MotionBenchmarkConfigData config;
    internal readonly Dictionary<string, MotionBenchmarkEngineData> engines = new();
}

internal sealed class MotionBenchmarkConfigData
{
    internal int sharedMotionCount;
    internal int keyedFloatCount;
    internal int distinctTransformCount;
    internal int sequenceCount;
    internal int sequenceLength;
    internal float steadyMotionDurationSeconds;
    internal int warmupFrames;
    internal int measuredFrames;
    internal int recorderSettlingFrames;
    internal int creationBatchSize;
    internal int creationWarmupBatches;
    internal int creationSamples;
    internal string executionIsolation;
    internal string executionOrder;
    internal string[] adapterOrder;
    internal string[] workloadOrder;
    internal string measurementPasses;
    internal string validationBoundary;
    internal float timeScale;
    internal int targetFrameRate;
    internal int vSyncCount;
}

internal sealed class MotionBenchmarkEngineData
{
    internal string status;
    internal string statusReason;
    internal string adapterType;
    internal MotionBenchmarkAdapterMetadata metadata;
    internal readonly Dictionary<string, MotionBenchmarkWorkloadData> workloads = new();
    internal MotionBenchmarkCreationData creation;
}

internal sealed class MotionBenchmarkWorkloadData
{
    internal string status;
    internal string statusReason;
    internal MotionBenchmarkSpec spec;
    internal MotionBenchmarkSeriesData mainThread;
    internal readonly Dictionary<string, MotionBenchmarkSeriesData> markers = new();
}

internal sealed class MotionBenchmarkCreationData
{
    internal string status;
    internal string statusReason;
    internal MotionBenchmarkSpec spec;
    internal string firstBatchScope;
    internal string warmRecycledScope;
    internal MotionBenchmarkCreationPassData firstBatch;
    internal MotionBenchmarkCreationPassData warmRecycled;

    internal static MotionBenchmarkCreationData Create(in MotionBenchmarkSpec spec, int warmSampleCount) => new()
    {
        status = "measuring",
        spec = spec,
        firstBatchScope = "firstActualBatchInAdapterSessionNotProcessCold",
        warmRecycledScope = "samePreparedContextAfterFirstBatchAndConfiguredWarmup",
        firstBatch = MotionBenchmarkCreationPassData.Create(1, "creationFirstBatch"),
        warmRecycled = MotionBenchmarkCreationPassData.Create(warmSampleCount, "creationWarmRecycled")
    };
}

internal sealed class MotionBenchmarkCreationPassData
{
    internal string status;
    internal string statusReason;
    internal MotionBenchmarkSeriesData timePerCreation;
    internal MotionBenchmarkSeriesData gcBytesPerCreation;

    internal static MotionBenchmarkCreationPassData Create(int capacity, string measurementPass) => new()
    {
        status = "measuring",
        timePerCreation = MotionBenchmarkSeriesData.Create("creation", "microsecondsPerCreation", capacity,
            measurementPass),
        gcBytesPerCreation = MotionBenchmarkSeriesData.Create("gcAllocation", "bytesPerCreation", capacity,
            measurementPass)
    };
}

internal sealed class MotionBenchmarkSeriesData
{
    internal string marker;
    internal string unit;
    internal string measurementPass;
    internal string status;
    internal string statusReason;
    internal List<float> samples = new();

    internal static MotionBenchmarkSeriesData Create(string marker, string unit, int capacity,
        string measurementPass) => new()
    {
        marker = marker,
        unit = unit,
        measurementPass = measurementPass,
        samples = new List<float>(capacity)
    };
}
