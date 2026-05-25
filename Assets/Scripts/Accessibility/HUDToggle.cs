using UnityEngine;
using UnityEngine.InputSystem;

namespace Accessibility
{
    public class HUDToggle : MonoBehaviour
    {
        [Tooltip("GameObject do HUD a ser ligado/desligado.")]
        [SerializeField] private GameObject hud;

        [Tooltip("Bindings do Input System que disparam o toggle. Default: M no teclado + Y no controle esquerdo.")]
        [SerializeField] private string[] bindingPaths = new[]
        {
            "<Keyboard>/m",
            "<XRController>{LeftHand}/secondaryButton"
        };

        [Tooltip("Estado inicial do HUD.")]
        [SerializeField] private bool startVisible = false;

        [Tooltip("Distância à frente da câmera ao abrir o HUD.")]
        [SerializeField] private float spawnDistance = 0.6f;

        private InputAction _action;

        void OnEnable()
        {
            if (hud != null) hud.SetActive(startVisible);

            _action = new InputAction("ToggleHUD", InputActionType.Button);
            foreach (var p in bindingPaths) _action.AddBinding(p);
            _action.performed += OnToggle;
            _action.Enable();
        }

        void OnDisable()
        {
            if (_action != null)
            {
                _action.performed -= OnToggle;
                _action.Disable();
                _action.Dispose();
                _action = null;
            }
        }

        private void OnToggle(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[HUDToggle] Toggle disparado via {ctx.control?.path}. HUD presente={hud != null}.");
            if (hud == null) return;
            bool willBeActive = !hud.activeSelf;
            hud.SetActive(willBeActive);
            if (willBeActive) PositionInFrontOfCamera();
        }

        private void PositionInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var camTf = cam.transform;
            hud.transform.position = camTf.position + camTf.forward * spawnDistance;
            hud.transform.rotation = Quaternion.LookRotation(hud.transform.position - camTf.position, Vector3.up);
        }
    }
}
