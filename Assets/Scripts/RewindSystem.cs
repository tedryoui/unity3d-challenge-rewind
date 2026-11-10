using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Collections;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

namespace DefaultNamespace
{
    public enum RewindMode
    {
        Mono,
        Job
    }

    [AutoStaticsCleanup]
    public static partial class RewindSystem
    {
        private static HashSet<IRewind> _rewindInstances;
        private static bool             _isRewinding;

        [OnEnteringPlayMode]
        public static void OnCreate()
        {
            _rewindInstances = new HashSet<IRewind>();
            _isRewinding     = false;

            CreateRewindDebugger();

            Core.TrackOnObjectInstantiate("RewindInitializer", InjectRewindObject);
        }

        private static void CreateRewindDebugger()
        {
            var obj = new GameObject("RewindDebugger");
            Object.DontDestroyOnLoad(obj);

            obj.AddComponent<RewindDebugger>();
        }

        private static void InjectRewindObject(Object unityObject)
        {
            if (unityObject is GameObject unityGameObject &&
                unityGameObject.TryGetComponent(out IRewind rewind))
                _rewindInstances.Add(rewind);
        }

        private static bool IsAlive(IRewind rewind)
            => rewind != null && !(rewind is Object unityObject && unityObject == null);

        public static void Rewind(RewindMode mode)
        {
            if (_isRewinding)
                return;

            _ = RewindAsync(mode);
        }

        private static async Awaitable RewindAsync(RewindMode mode)
        {
            _rewindInstances.RemoveWhere(r => !IsAlive(r));

            var targets = new List<IRewind>();

            foreach (var rewind in _rewindInstances)
            {
                if (rewind.CanRewind)
                    targets.Add(rewind);
            }

            if (targets.Count == 0)
                return;

            _isRewinding = true;

            foreach (var target in targets)
                target.OnRewindStarted();

            try
            {
                switch (mode)
                {
                    case RewindMode.Mono: await RewindMono(targets); break;
                    case RewindMode.Job:  await RewindJob(targets);  break;
                    default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }
            finally
            {
                foreach (var target in targets)
                {
                    if (IsAlive(target))
                        target.OnRewindCompleted();
                }

                _isRewinding = false;
            }
        }

        private static async Awaitable RewindMono(List<IRewind> targets)
        {
            var linked = new List<CancellationTokenSource>(targets.Count);
            var tasks  = new List<Awaitable>(targets.Count);

            try
            {
                foreach (var target in targets)
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(
                        Application.exitCancellationToken, target.LifetimeToken);

                    linked.Add(cts);
                    tasks.Add(target.RewindMono(cts.Token));
                }

                foreach (var task in tasks)
                    await task;
            }
            finally
            {
                foreach (var cts in linked)
                    cts.Dispose();
            }
        }

        private static async Awaitable RewindJob(List<IRewind> targets)
        {
            var active = new List<JobRewindEntry>(targets.Count);

            foreach (var target in targets)
            {
                var entry = target.CreateJobEntry();
                entry.token = target.LifetimeToken;
                active.Add(entry);
            }

            var access = new TransformAccessArray(GetTransforms(active));

            try
            {
                while (active.Count > 0 && !Application.exitCancellationToken.IsCancellationRequested)
                {
                    bool changed = false;
                    for (int i = active.Count - 1; i >= 0; i--)
                    {
                        if (active[i].target != null && !active[i].token.IsCancellationRequested)
                            continue;

                        active.RemoveAt(i);
                        changed = true;
                    }

                    if (active.Count == 0)
                        break;

                    if (changed)
                        access.SetTransforms(GetTransforms(active));

                    RunJob(active, access);

                    changed = false;
                    for (int i = active.Count - 1; i >= 0; i--)
                    {
                        if (active[i].elapsed < active[i].duration)
                            continue;

                        active.RemoveAt(i);
                        changed = true;
                    }

                    if (changed && active.Count > 0)
                        access.SetTransforms(GetTransforms(active));

                    await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                access.Dispose();
            }
        }

        private static Transform[] GetTransforms(List<JobRewindEntry> entries)
        {
            var result = new Transform[entries.Count];

            for (int i = 0; i < result.Length; i++)
                result[i] = entries[i].target;

            return result;
        }

        private static void RunJob(List<JobRewindEntry> entries, TransformAccessArray access)
        {
            int n     = entries.Count;
            int total = 0;

            for (int i = 0; i < n; i++)
                total += entries[i].frames.Length;

            var jobEntries = new NativeArray<RewindJobEntry>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var jobFrames  = new NativeArray<TransformRewindStamp>(total, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            int offset = 0;
            for (int i = 0; i < n; i++)
            {
                var e = entries[i];
                e.elapsed += Time.deltaTime;

                jobEntries[i] = new RewindJobEntry
                {
                    offset = offset,
                    count  = e.frames.Length,
                    t      = Mathf.Clamp01(e.elapsed / e.duration)
                };

                NativeArray<TransformRewindStamp>.Copy(e.frames, 0, jobFrames, offset, e.frames.Length);
                offset += e.frames.Length;
            }

            new RewindTransformJob { entries = jobEntries, frames = jobFrames }.Schedule(access).Complete();

            jobEntries.Dispose();
            jobFrames.Dispose();
        }

        public static void Clear()
        {
            foreach (var rewindInstance in _rewindInstances)
            {
                if (IsAlive(rewindInstance))
                    rewindInstance.Clear();
            }
        }
    }
}