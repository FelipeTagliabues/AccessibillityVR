using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Accessibility
{
    [DisallowMultipleComponent]
    public class ButtonMission : MonoBehaviour
    {
        [Tooltip("Ordem em que este botão deve ser clicado (1, 2, 3...).")]
        public int order = 1;

        void Start()
        {
            var interactable = GetComponent<XRBaseInteractable>();
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(_ =>
                {
                    Debug.Log($"[ButtonMission] '{name}' ordem={order} clicado.");
                    OnPressed();
                });
            }
            else
            {
                Debug.LogWarning($"[ButtonMission] '{name}' sem XRBaseInteractable — não vai receber press.");
            }
        }

        public void OnPressed()
        {
            if (MissionManager.Instance == null)
            {
                Debug.LogWarning("[ButtonMission] MissionManager não está na cena.");
                return;
            }
            MissionManager.Instance.ReportPress(this);
        }
    }
}
