using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DefaultNamespace
{
    public sealed class JobRewindEntry
    {
        public Transform              target;
        public TransformRewindStamp[] frames;
        public float                  duration;
        public float                  elapsed;
        public CancellationToken      token;
    }

    public class JobRewindTransformProvider
    {
        private readonly Transform                           _target;
        private readonly RingBufferRef<TransformRewindStamp> _ringBuffer;
        private readonly float                               _rewindSpeed;

        public JobRewindTransformProvider(
            Transform target,
            RingBufferRef<TransformRewindStamp> ringBuffer,
            float rewindSpeed)
        {
            _target      = target;
            _ringBuffer  = ringBuffer;
            _rewindSpeed = rewindSpeed;
        }

        public JobRewindEntry CreateEntry()
        {
            int count = (int)_ringBuffer.Count;

            var   frames   = new TransformRewindStamp[count];
            float distance = 0.0f;

            for (int i = 0; i < count; i++)
            {
                frames[i] = _ringBuffer[(uint)(count - 1 - i)];

                if (i > 0)
                    distance += math.distance(frames[i - 1].position, frames[i].position);
            }

            return new JobRewindEntry
            {
                target   = _target,
                frames   = frames,
                duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, _rewindSpeed)),
                elapsed  = 0.0f
            };
        }
    }

    public struct RewindJobEntry
    {
        public int   offset;
        public int   count;
        public float t;
    }

    [BurstCompile]
    public struct RewindTransformJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<RewindJobEntry>       entries;
        [ReadOnly] public NativeArray<TransformRewindStamp> frames;

        public void Execute(int index, TransformAccess transform)
        {
            RewindJobEntry e = entries[index];
            Pose           p = Evaluate(e.t, e.offset, e.count);

            transform.SetPositionAndRotation(p.position, p.rotation);
            transform.localScale = p.scale;
        }

        private struct Pose
        {
            public float3     position;
            public quaternion rotation;
            public float3     scale;
        }

        private Pose GetFrame(int offset, int index)
        {
            TransformRewindStamp s = frames[offset + index];

            return new Pose
            {
                position = s.position,
                rotation = quaternion.EulerZXY(math.radians(s.rotation)),
                scale    = s.scale
            };
        }

        private Pose Evaluate(float t, int offset, int count)
        {
            if (count == 1)
                return GetFrame(offset, 0);

            if (count == 2)
            {
                Pose a = GetFrame(offset, 0);
                Pose b = GetFrame(offset, 1);

                return new Pose
                {
                    position = math.lerp(a.position, b.position, t),
                    rotation = math.slerp(a.rotation, b.rotation, t),
                    scale    = math.lerp(a.scale, b.scale, t)
                };
            }

            int   numSegments = count - 1;
            float scaled      = math.clamp(t, 0f, 1f) * numSegments;
            int   index       = (int)math.floor(scaled);

            if (index >= numSegments)
            {
                index  = numSegments - 1;
                scaled = numSegments;
            }

            float segT = scaled - index;

            Pose f1 = GetFrame(offset, index);
            Pose f2 = GetFrame(offset, index + 1);

            bool hasP0 = index > 0;
            bool hasP3 = index + 2 < count;

            Pose p0 = hasP0 ? GetFrame(offset, index - 1) : default;
            Pose p3 = hasP3 ? GetFrame(offset, index + 2) : default;

            float3 pa = hasP0 ? p0.position : f1.position - (f2.position - f1.position);
            float3 pd = hasP3 ? p3.position : f2.position + (f2.position - f1.position);

            float3 sa = hasP0 ? p0.scale : f1.scale - (f2.scale - f1.scale);
            float3 sd = hasP3 ? p3.scale : f2.scale + (f2.scale - f1.scale);

            float4 r1 = f1.rotation.value;
            float4 r0 = hasP0 ? p0.rotation.value : r1;
            float4 r2 = f2.rotation.value;
            float4 r3 = hasP3 ? p3.rotation.value : r2;

            r0 = Align(r1, r0);
            r2 = Align(r1, r2);
            r3 = Align(r2, r3);

            float4 q = CatmullRom(segT, r0, r1, r2, r3);
            float  m = math.dot(q, q);

            return new Pose
            {
                position = CatmullRom(segT, pa, f1.position, f2.position, pd),
                rotation = m < 1e-6f ? quaternion.identity : new quaternion(q * math.rsqrt(m)),
                scale    = math.max(CatmullRom(segT, sa, f1.scale, f2.scale, sd), float3.zero)
            };
        }

        private static float4 Align(float4 reference, float4 target)
            => math.dot(reference, target) < 0f ? -target : target;

        private static float3 CatmullRom(float t, float3 p0, float3 p1, float3 p2, float3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float4 CatmullRom(float t, float4 p0, float4 p1, float4 p2, float4 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
