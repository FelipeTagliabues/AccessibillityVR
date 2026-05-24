using System;
using UnityEngine;

namespace Accessibility
{
    /// <summary>
    /// Detecta proximidade do carro com o player; quando bate, dispara evento global.
    /// </summary>
    public class CarHitDetector : MonoBehaviour
    {
        public static event Action OnPlayerHit;

        [SerializeField] private float hitRadius = 1.2f;
        [Tooltip("Cooldown em segundos depois de um hit (evita disparos múltiplos por frame).")]
        [SerializeField] private float cooldown = 2f;

        private Transform _player;
        private float _lastHitTime = -100f;

        void Start()
        {
            if (Camera.main != null) _player = Camera.main.transform;
        }

        void Update()
        {
            if (_player == null)
            {
                if (Camera.main != null) _player = Camera.main.transform;
                return;
            }
            if (Time.time - _lastHitTime < cooldown) return;

            float dx = transform.position.x - _player.position.x;
            float dz = transform.position.z - _player.position.z;
            if (dx * dx + dz * dz <= hitRadius * hitRadius)
            {
                _lastHitTime = Time.time;
                Debug.Log($"[CarHitDetector] Player atropelado por '{name}'.");
                OnPlayerHit?.Invoke();
            }
        }
    }
}
