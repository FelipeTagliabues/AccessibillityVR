using System.Collections.Generic;
using UnityEngine;

namespace Accessibility
{
    /// <summary>
    /// Impede o rig do player de atravessar paredes.
    /// Anexar no XR Origin (a raiz que contém Camera Offset, Main Camera e controllers).
    /// Quando detecta overlap do "corpo" do player com um collider não-self, reverte a posição
    /// do rig inteiro para o último estado válido. Movimenta o rig junto, sem descolar
    /// controles da câmera.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class PlayerConstraints : MonoBehaviour
    {
        [Tooltip("Raio do corpo do player (m).")]
        [SerializeField] private float bodyRadius = 0.35f;

        [Tooltip("Altura do centro do corpo do player (m acima do rig).")]
        [SerializeField] private float bodyCenterY = 1.0f;

        private Vector3 _lastValidPos;
        private readonly HashSet<Collider> _selfColliders = new HashSet<Collider>();

        void Start()
        {
            _lastValidPos = transform.position;
            CollectSelfColliders();
            Debug.Log($"[PlayerConstraints] Rig '{name}' ancorado. Self colliders ignorados: {_selfColliders.Count}.");
        }

        private void CollectSelfColliders()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) _selfColliders.Add(c);
            var bootstrap = FindObjectOfType<AccessibilityBootstrap>();
            if (bootstrap != null)
            {
                foreach (var c in bootstrap.GetComponentsInChildren<Collider>(true)) _selfColliders.Add(c);
            }
        }

        void LateUpdate()
        {
            var pos = transform.position;
            var checkPoint = pos + Vector3.up * bodyCenterY;

            var hits = Physics.OverlapSphere(checkPoint, bodyRadius, ~0, QueryTriggerInteraction.Ignore);
            Collider blocker = null;
            foreach (var h in hits)
            {
                if (!_selfColliders.Contains(h)) { blocker = h; break; }
            }

            if (blocker != null)
            {
                transform.position = _lastValidPos;
            }
            else
            {
                _lastValidPos = pos;
            }
        }
    }
}
