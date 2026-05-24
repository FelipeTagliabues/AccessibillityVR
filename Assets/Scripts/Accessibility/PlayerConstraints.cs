using System.Collections.Generic;
using UnityEngine;

namespace Accessibility
{
    /// <summary>
    /// Trava a altura do player e impede atravessar paredes.
    /// Anexar na Main Camera (HMD). Roda em LateUpdate para vir DEPOIS do simulator.
    /// </summary>
    public class PlayerConstraints : MonoBehaviour
    {
        [Tooltip("Altura fixa do 'olho' (mundo Y). Impede Q/E voar/cair.")]
        [SerializeField] private float eyeWorldY = 1.7f;

        [Tooltip("Raio do corpo do player (m).")]
        [SerializeField] private float bodyRadius = 0.3f;

        [Tooltip("Layers consideradas obstáculo. -1 = tudo.")]
        [SerializeField] private LayerMask wallMask = ~0;

        private Vector3 _lastValidPos;
        private readonly HashSet<Collider> _selfColliders = new HashSet<Collider>();

        void Start()
        {
            // Lock altura inicial
            var p = transform.position;
            p.y = eyeWorldY;
            transform.position = p;
            _lastValidPos = p;

            // Coleta colliders do rig (XR Origin) para ignorar em testes de overlap
            var root = transform.root;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                _selfColliders.Add(c);
            }
            // Também ignora colliders de tudo que esteja sob o _AccessibilityRoot (HUD, menus)
            var bootstrap = FindObjectOfType<AccessibilityBootstrap>();
            if (bootstrap != null)
            {
                foreach (var c in bootstrap.GetComponentsInChildren<Collider>(true))
                {
                    _selfColliders.Add(c);
                }
            }
        }

        void LateUpdate()
        {
            // 1. Lock Y — Q/E não levam mais para void/infinito
            var pos = transform.position;
            pos.y = eyeWorldY;

            // 2. Detecta overlap com obstáculos (não-self)
            var hits = Physics.OverlapSphere(pos, bodyRadius, wallMask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            foreach (var h in hits)
            {
                if (!_selfColliders.Contains(h))
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
            {
                transform.position = _lastValidPos;
            }
            else
            {
                transform.position = pos;
                _lastValidPos = pos;
            }
        }
    }
}
