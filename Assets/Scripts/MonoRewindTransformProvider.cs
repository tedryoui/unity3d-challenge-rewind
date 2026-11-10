using System;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class MonoRewindTransformProvider : RewindTransformProvider
    {
        private readonly struct Pose
        {
            public readonly Vector3    position;
            public readonly Quaternion rotation;
            public readonly Vector3    scale;

            public Pose(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                this.position = position;
                this.rotation = rotation;
                this.scale    = scale;
            }
        }

        private readonly Transform                        _target;
        private          RingBufferRef<TransformRewindStamp> _ringBuffer;

        private readonly float _rewindSpeed;

        public MonoRewindTransformProvider(
            Transform target, 
            RingBufferRef<TransformRewindStamp> ringBuffer, 
            float rewindSpeed)
        {
            _target      = target;
            _ringBuffer  = ringBuffer;
            _rewindSpeed = rewindSpeed;
        }

        public override async Awaitable Rewind(CancellationToken cts)
        {
            int count = (int)_ringBuffer.Count;

            if (count == 0)
                return;

            float duration = Mathf.Max(0.01f, GetRewindOverallDistance(_ringBuffer) / Mathf.Max(0.01f, _rewindSpeed));
            float elapsed  = 0.0f;

            try
            {
                while (elapsed < duration)
                {
                    cts.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;

                    ApplyFrames(Mathf.Clamp01(elapsed / duration), false, count);

                    await Awaitable.NextFrameAsync(cts);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            ApplyFrames(1.0f, false, count);
        }

        public float GetRewindOverallDistance(RingBufferRef<TransformRewindStamp> ringBuffer)
        {
            uint  count    = ringBuffer.Count;
            float distance = 0.0f;

            for (uint index = 1; index < count; index++)
            {
                distance += math.distance(ringBuffer[index - 1].position, ringBuffer[index].position);
            }

            return distance;
        }

#region Helpers

        private Pose GetFrame(int rewindIndex, int count)
        {
            ref readonly var stamp = ref _ringBuffer[(uint)(count - 1 - rewindIndex)];

            return new Pose(stamp.position, Quaternion.Euler(stamp.rotation), stamp.scale);
        }

        private static Pose Lerp(Pose a, Pose b, float t)
        {
            return new Pose(
                Vector3.Lerp(a.position, b.position, t),
                Quaternion.Slerp(a.rotation, b.rotation, t),
                Vector3.Lerp(a.scale, b.scale, t)
            );
        }

        private Pose EvaluateFrames(float t, int count)
        {
            if (count == 0)
                return default;

            if (count == 1)
                return GetFrame(0, count);

            if (count == 2)
                return Lerp(GetFrame(0, count), GetFrame(1, count), t);

            GetSegmentIndices(t, count, out int index, out float segT);

            Pose f1 = GetFrame(index, count);
            Pose f2 = GetFrame(index + 1, count);

            Pose p0 = index > 0         ? GetFrame(index - 1, count) : default;
            Pose p3 = index + 2 < count ? GetFrame(index + 2, count) : default;

            bool hasP0 = index > 0;
            bool hasP3 = index + 2 < count;

            return new Pose(
                EvaluatePosition(segT, hasP0, p0, f1, f2, hasP3, p3),
                EvaluateRotation(segT, hasP0, p0, f1, f2, hasP3, p3),
                EvaluateScale(segT, hasP0, p0, f1, f2, hasP3, p3)
            );
        }

        private static Vector3 EvaluatePosition(float segT, bool hasP0, Pose p0, Pose f1, Pose f2, bool hasP3, Pose p3)
        {
            Vector3 a = hasP0 ? p0.position : f1.position - (f2.position - f1.position);
            Vector3 d = hasP3 ? p3.position : f2.position + (f2.position - f1.position);
            return CatmullRom3D(segT, a, f1.position, f2.position, d);
        }

        private static Vector3 EvaluateScale(float segT, bool hasP0, Pose p0, Pose f1, Pose f2, bool hasP3, Pose p3)
        {
            Vector3 a = hasP0 ? p0.scale : f1.scale - (f2.scale - f1.scale);
            Vector3 d = hasP3 ? p3.scale : f2.scale + (f2.scale - f1.scale);
            return Vector3.Max(CatmullRom3D(segT, a, f1.scale, f2.scale, d), Vector3.zero);
        }

        private static Quaternion EvaluateRotation(float segT, bool hasP0, Pose p0, Pose f1, Pose f2, bool hasP3, Pose p3)
        {
            Quaternion q0 = hasP0 ? p0.rotation : f1.rotation;
            Quaternion q3 = hasP3 ? p3.rotation : f2.rotation;

            q0 = AlignHemisphere(f1.rotation, q0);
            Quaternion q2 = AlignHemisphere(f1.rotation, f2.rotation);
            q3 = AlignHemisphere(q2, q3);

            return NormalizeQuaternion(new Quaternion(
                CatmullRom1D(segT, q0.x, f1.rotation.x, q2.x, q3.x),
                CatmullRom1D(segT, q0.y, f1.rotation.y, q2.y, q3.y),
                CatmullRom1D(segT, q0.z, f1.rotation.z, q2.z, q3.z),
                CatmullRom1D(segT, q0.w, f1.rotation.w, q2.w, q3.w)
            ));
        }

        private void ApplyFrames(float t, bool useLocal, int count)
        {
            Pose frame = EvaluateFrames(t, count);

            if (useLocal)
            {
                _target.localPosition = frame.position;
                _target.localRotation = frame.rotation;
                _target.localScale    = frame.scale;
            }
            else
            {
                _target.position   = frame.position;
                _target.rotation   = frame.rotation;
                _target.localScale = frame.scale;
            }
        }

        private static void GetSegmentIndices(float t, int count, out int index, out float segmentT)
        {
            t = Mathf.Clamp01(t);
            int   numSegments = count - 1;
            float scaledT     = t * numSegments;
            index = Mathf.FloorToInt(scaledT);

            if (index >= numSegments)
            {
                index   = numSegments - 1;
                scaledT = numSegments;
            }

            segmentT = scaledT - index;
        }

        private static Vector3 CatmullRom3D(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static float CatmullRom1D(float t, float p0, float p1, float p2, float p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Quaternion AlignHemisphere(Quaternion reference, Quaternion target)
        {
            return Quaternion.Dot(reference, target) < 0f
                ? new Quaternion(-target.x, -target.y, -target.z, -target.w)
                : target;
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float dot = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (dot < 1e-6f) return Quaternion.identity;

            float invMag = 1.0f / Mathf.Sqrt(dot);
            return new Quaternion(q.x * invMag, q.y * invMag, q.z * invMag, q.w * invMag);
        }

#endregion
    }
}
