using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
    public struct TransformRewindStamp
    {
        public float3   position;
        public float3   rotation;
        public float3   scale;
    }

    public class RewindTransform : MonoBehaviour, IRewind
    {
        [Space(18)]
        [SerializeField] private UnityEvent _onStart;
        [SerializeField] private UnityEvent _onComplete;
        
        [SerializeField] private float _trackDelay  = 1.0f;
        [SerializeField] private int   _trackAmount = 15;
        [SerializeField] private float _rewindSpeed = 1.0f;

        private RingBufferRef<TransformRewindStamp> _rewindBuffer;
        private MonoRewindTransformProvider         _monoProvider;
        private JobRewindTransformProvider          _jobProvider;

        private bool _isRecorderStopped;
        private bool _isRewindActive;

        public bool CanRewind => _rewindBuffer.Count > 2 && !_isRewindActive;

        public CancellationToken LifetimeToken => destroyCancellationToken;
        
        private void Awake()
        {
            _isRecorderStopped = false;
            _isRewindActive    = false;
            
            _rewindBuffer      = new RingBufferRef<TransformRewindStamp>((uint)_trackAmount);
        }

        private void Start()
        {
            _monoProvider = new MonoRewindTransformProvider(transform, _rewindBuffer, _rewindSpeed);
            _jobProvider  = new JobRewindTransformProvider(transform, _rewindBuffer, _rewindSpeed);
            
            _ = RunStampRecorder();
        }

        private async Awaitable RunStampRecorder()
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                Application.exitCancellationToken,
                destroyCancellationToken
            );
            var timer = 0.0f;

            while (!cts.IsCancellationRequested)
            {
                if (_isRecorderStopped)
                {
                    timer = 0.0f;
                    await Awaitable.NextFrameAsync(cts.Token);
                    continue;
                }
                
                timer += Time.deltaTime;

                if (timer >= _trackDelay)
                {
                    timer = 0.0f;
                    CreateStamp();
                }
                
                await Awaitable.NextFrameAsync(cts.Token);
            }
        }

        public void OnRewindStarted()
        {
            _isRecorderStopped = true;
            _isRewindActive    = true;
            
            _onStart.Invoke();
        }

        public void OnRewindCompleted()
        {
            _isRecorderStopped = false;
            _isRewindActive    = false;
            
            _onComplete?.Invoke();
            
            Clear();
        }

        public Awaitable RewindMono(CancellationToken token) => _monoProvider.Rewind(token);

        public JobRewindEntry CreateJobEntry() => _jobProvider.CreateEntry();

        public void Clear()
        {
            _rewindBuffer.Clear();

            CreateStamp();
        }

        private void CreateStamp()
        {
            var stamp = new TransformRewindStamp
            {
                position = transform.position,
                rotation = transform.eulerAngles,
                scale    = transform.localScale
            };
            
            _rewindBuffer.Append(stamp);
        }
    }
}

