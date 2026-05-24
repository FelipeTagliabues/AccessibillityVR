using UnityEngine;

namespace Accessibility
{
    public class ButtonMission : MonoBehaviour
    {
        [Tooltip("Marque true em apenas UM dos PushButtons da cena.")]
        public bool isTarget = false;

        [Tooltip("Chamar manualmente no UnityEvent OnPress do XRPushButton (ou similar).")]
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
