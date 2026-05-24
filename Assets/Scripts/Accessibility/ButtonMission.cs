using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Accessibility
{
    [DisallowMultipleComponent]
    public class ButtonMission : MonoBehaviour
    {
        [Tooltip("Marque true em apenas UM dos PushButtons da cena.")]
        public bool isTarget = false;

        void Start()
        {
            var interactable = GetComponent<XRBaseInteractable>();
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(_ => OnPressed());
            }
            else
            {
                Debug.LogWarning($"[ButtonMission] '{name}' sem XRBaseInteractable — adicione um para receber press.");
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
