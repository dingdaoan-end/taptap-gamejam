using UnityEngine;

namespace TapTapGameJam.Graybox
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float followSpeed = 12f;

        public void SetTarget(Transform value)
        {
            target = value;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            Vector3 position = target.position;
            transform.position = new Vector3(position.x, position.y, -10f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 position = new Vector3(target.position.x, target.position.y, -10f);
            float weight = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, position, weight);
        }
    }
}
