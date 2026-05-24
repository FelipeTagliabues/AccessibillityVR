using System;
using TMPro;
using UnityEngine;

namespace Accessibility
{
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;

        [Header("Áudio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip wrongClip;

        [Header("Mensagens")]
        [SerializeField] private string idleMessage = "Procure o botão correto.";
        [SerializeField] private string wrongMessage = "Não é esse botão. Tente outro.";
        [SerializeField] private string winMessage = "MISSÃO CUMPRIDA!";

        public event Action OnMissionComplete;

        private bool _completed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            SetStatus(idleMessage);
        }

        public void ReportPress(ButtonMission btn)
        {
            if (_completed) return;

            if (btn.isTarget)
            {
                _completed = true;
                SetStatus(winMessage);
                Play(winClip);
                OnMissionComplete?.Invoke();
            }
            else
            {
                SetStatus(wrongMessage);
                Play(wrongClip);
            }
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
