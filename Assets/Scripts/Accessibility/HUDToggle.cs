using UnityEngine;
using UnityEngine.InputSystem;

namespace Accessibility
{
    public class HUDToggle : MonoBehaviour
    {
        [Tooltip("InputAction de botão (ex.: Y do controle esquerdo / secondaryButton).")]
        [SerializeField] private InputActionReference toggleAction;

        [Tooltip("GameObject do HUD a ser ligado/desligado.")]
        [SerializeField] private GameObject hud;

        [Tooltip("Estado inicial do HUD.")]
        [SerializeField] private bool startVisible = false;

        void OnEnable()
        {
            if (hud != null) hud.SetActive(startVisible);
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.performed += OnToggle;
                toggleAction.action.Enable();
            }
        }

        void OnDisable()
        {
            if (toggleAction != null && toggleAction.action != null)
                toggleAction.action.performed -= OnToggle;
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (hud != null) hud.SetActive(!hud.activeSelf);
        }
    }
}
