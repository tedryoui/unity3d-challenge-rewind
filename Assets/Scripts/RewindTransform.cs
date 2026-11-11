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

        private Vector3    _initialPosition;
        private Vector3    _initialRotation;
        private Vector3    _initialScale;
        private Rigidbody  _rigidbody;

        public bool CanRewind => !_isRewindActive && HasMovedFromInitialPose();

        public CancellationToken LifetimeToken => destroyCancellationToken;
        
        private void Awake()
        {
            _isRecorderStopped = false;
            _isRewindActive    = false;
            _rigidbody         = GetComponent<Rigidbody>();
            
            _rewindBuffer      = new RingBufferRef<TransformRewindStamp>((uint)_trackAmount);
        }

        private void OnEnable()
        {
            RewindSystem.Register(this);
        }

        private void OnDisable()
        {
            RewindSystem.Unregister(this);
        }

        private void OnDestroy()
        {
            RewindSystem.Unregister(this);
            _rewindBuffer?.Dispose();
        }

        private void Start()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.eulerAngles;
            _initialScale    = transform.localScale;

            _monoProvider = new MonoRewindTransformProvider(transform, _rewindBuffer, _rewindSpeed);
            _jobProvider  = new JobRewindTransformProvider(transform, _rewindBuffer, _rewindSpeed);

            CreateStamp();
            
            _ = RunStampRecorder();
        }

        private bool HasMovedFromInitialPose()
        {
            if (_rewindBuffer.Count == 0)
                return false;

            float distSq = math.distancesq(transform.position, (float3)_initialPosition);
            if (distSq > 0.0001f)
                return true;

            if (Quaternion.Angle(transform.rotation, Quaternion.Euler(_initialRotation)) > 0.5f)
                return true;

            for (uint i = 0; i < _rewindBuffer.Count; i++)
            {
                if (math.distancesq(_rewindBuffer[i].position, (float3)_initialPosition) > 0.0001f)
                    return true;
            }

            return false;
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
                if (_isRecorderStopped || (_rigidbody != null && _rigidbody.isKinematic))
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

            CreateStamp();
            
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

