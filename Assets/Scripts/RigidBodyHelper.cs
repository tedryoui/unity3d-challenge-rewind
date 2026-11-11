using UnityEngine;

namespace DefaultNamespace
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidBodyHelper : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private bool      _startStatic = true;

        private int  _defaultLayer;
        private bool _isActivated;

        public bool IsStaticUntilHit => _startStatic && !_isActivated;

        private int IgnoreCollisionLayerMask => LayerMask.NameToLayer("Ignore Collision");

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            
            _defaultLayer = _rigidbody.gameObject.layer;

            if (_startStatic)
            {
                _isActivated           = false;
                _rigidbody.isKinematic = true;
                _rigidbody.Sleep();
            }
            else
            {
                _isActivated = true;
            }
        }

        public void ActivateAndAddForce(Vector3 force, ForceMode mode = ForceMode.VelocityChange)
        {
            if (_rigidbody is null)
                return;

            if (!_isActivated)
                ActivateChainReaction();

            _rigidbody.AddForce(force, mode);
        }

        public void ActivateChainReaction()
        {
            if (_rigidbody is null || _isActivated)
                return;

            _isActivated           = true;
            _rigidbody.isKinematic = false;
            _rigidbody.WakeUp();

            // Wake up any stacked or touching objects so piles/tables collapse naturally
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col == null || !col.enabled) continue;
                var b = col.bounds;
                var hits = Physics.OverlapBox(b.center + Vector3.up * 0.08f, b.extents + Vector3.one * 0.08f, Quaternion.identity);
                foreach (var hit in hits)
                {
                    if (hit.attachedRigidbody == null || hit.attachedRigidbody == _rigidbody)
                        continue;

                    if (hit.attachedRigidbody.TryGetComponent<RigidBodyHelper>(out var neighbor) && neighbor.IsStaticUntilHit)
                        neighbor.ActivateChainReaction();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // When this active dynamic object hits another object that is still static-until-hit, wake it up and transfer impulse!
            if (!_isActivated || _rigidbody == null || _rigidbody.isKinematic)
                return;

            var otherRb = collision.rigidbody;
            if (otherRb != null && otherRb.TryGetComponent<RigidBodyHelper>(out var otherHelper) && otherHelper.IsStaticUntilHit)
            {
                Vector3 impactVel = collision.relativeVelocity;
                if (impactVel.sqrMagnitude > 0.1f)
                {
                    otherHelper.ActivateAndAddForce(-impactVel * 0.65f, ForceMode.VelocityChange);
                }
                else
                {
                    otherHelper.ActivateChainReaction();
                }
            }
        }

        public void ResetToStaticIfConfigured()
        {
            if (_rigidbody is null)
                return;

            _rigidbody.gameObject.layer = _defaultLayer;

            if (_startStatic)
            {
                _isActivated           = false;
                _rigidbody.isKinematic = true;
                _rigidbody.Sleep();
            }
            else
            {
                _isActivated                = true;
                _rigidbody.isKinematic      = false;
                _rigidbody.linearVelocity   = Vector3.zero;
                _rigidbody.angularVelocity  = Vector3.zero;
                _rigidbody.WakeUp();
            }
        }

        public void ForceDynamic()
        {
            _startStatic = false;
            _isActivated = true;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.WakeUp();
            }
        }

        public void Disable()
        {
            if (_rigidbody is null)
            {
                Debug.Log("RigidBodyHelper: No rigidbody attached to this game object.", gameObject);
                return;
            }
            
            _rigidbody.isKinematic      = true;
            _rigidbody.gameObject.layer = IgnoreCollisionLayerMask;
            _rigidbody.Sleep();
        }

        public void Enable()
        {
            if (_rigidbody is null)
            {
                Debug.Log("RigidBodyHelper: No rigidbody attached to this game object.", gameObject);
                return;
            }
            
            ResetToStaticIfConfigured();
        }
    }
}