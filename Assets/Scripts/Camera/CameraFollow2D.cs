using UnityEngine;

namespace Snow2.Camera
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 0f, -10f);
        [Range(0f, 1f)]
        public float SmoothFactor = 0.15f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (Target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    return;
                }
                Target = player.transform;
            }

            var desired = Target.position + Offset;
            desired.z = Offset.z;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, Mathf.Max(0.0001f, SmoothFactor));
        }
    }
}

