using UnityEngine;

namespace DefaultNamespace
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidBodyHelper : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;

        private int _defaultLayer;

        private int IgnoreCollisionLayerMask => LayerMask.NameToLayer("Ignore Collision");

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            
            _defaultLayer = _rigidbody.gameObject.layer;
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
            
            _rigidbody.isKinematic      = false;
            _rigidbody.gameObject.layer = _defaultLayer;
            _rigidbody.WakeUp();
        }
    }
}