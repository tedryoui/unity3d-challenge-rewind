using UnityEngine;

namespace DefaultNamespace
{
    public class PhysicsCallbackEvents : MonoBehaviour
    {
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryWakeOther(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryWakeOther(collision.collider);
        }

        private void TryWakeOther(Collider col)
        {
            if (col == null || col.attachedRigidbody == null)
                return;

            if (col.attachedRigidbody.TryGetComponent<RigidBodyHelper>(out var helper) && helper.IsStaticUntilHit)
            {
                Vector3 dir = (col.bounds.center - transform.position);
                dir.y = 0.15f;
                if (dir.sqrMagnitude < 0.001f)
                    dir = transform.forward + Vector3.up * 0.15f;
                else
                    dir.Normalize();

                helper.ActivateAndAddForce(dir * 5.0f, ForceMode.VelocityChange);
            }
        }
    }
}