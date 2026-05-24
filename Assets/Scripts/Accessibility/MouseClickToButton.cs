using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Accessibility
{
    /// <summary>
    /// Fallback de teste: clique esquerdo no Game window dispara ButtonMission direto.
    /// Existe porque a interação via XR Simulator em editor pode ser difícil de calibrar.
    /// Não interfere em VR real (o NearFarInteractor + XRSimpleInteractable cuidam disso).
    /// </summary>
    public class MouseClickToButton : MonoBehaviour
    {
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private bool logHits = true;

        void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            // Se o cursor está sobre UI, deixa a UI lidar (botões de menu)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = Camera.main;
            if (cam == null) return;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = cam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out var hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                var btn = hit.collider.GetComponentInParent<ButtonMission>();
                if (btn != null)
                {
                    if (logHits) Debug.Log($"[MouseClickToButton] '{hit.collider.name}' → order={btn.order}.");
                    btn.OnPressed();
                }
                else if (logHits)
                {
                    Debug.Log($"[MouseClickToButton] Raio bateu em '{hit.collider.name}' mas não é botão.");
                }
            }
        }
    }
}
