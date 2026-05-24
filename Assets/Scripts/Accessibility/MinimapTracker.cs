using UnityEngine;

namespace Accessibility
{
    public class MinimapTracker : MonoBehaviour
    {
        [Header("Referências de mundo")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform target;

        [Header("Bounds do cenário (mundo XZ)")]
        [SerializeField] private Vector2 worldMin = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 worldMax = new Vector2(20f, 20f);

        [Header("Referências de UI")]
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform playerDot;
        [SerializeField] private RectTransform targetDot;

        void Start()
        {
            if (target != null && targetDot != null && mapRect != null)
            {
                targetDot.anchoredPosition = WorldToMapPosition(
                    new Vector2(target.position.x, target.position.z),
                    worldMin, worldMax, mapRect.rect.size);
            }
        }

        void Update()
        {
            if (player == null || playerDot == null || mapRect == null) return;
            playerDot.anchoredPosition = WorldToMapPosition(
                new Vector2(player.position.x, player.position.z),
                worldMin, worldMax, mapRect.rect.size);
        }

        public static Vector2 WorldToMapPosition(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax, Vector2 mapSize)
        {
            Vector2 range = worldMax - worldMin;
            if (Mathf.Approximately(range.x, 0f) || Mathf.Approximately(range.y, 0f))
                return Vector2.zero;
            Vector2 normalized = new Vector2(
                (worldXZ.x - worldMin.x) / range.x,
                (worldXZ.y - worldMin.y) / range.y);
            return new Vector2(
                (normalized.x - 0.5f) * mapSize.x,
                (normalized.y - 0.5f) * mapSize.y);
        }
    }
}
