using UnityEngine;

namespace Accessibility
{
    /// <summary>
    /// Mantém o GameObject sempre virado para a Main Camera (rotação Y).
    /// </summary>
    public class BillboardToCamera : MonoBehaviour
    {
        private Transform _cam;

        void LateUpdate()
        {
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            if (_cam == null) return;
            var dir = _cam.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        }
    }
}
