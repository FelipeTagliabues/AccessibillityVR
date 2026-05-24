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

        [Header("Sequência")]
        [Tooltip("Total de botões da missão (1..N).")]
        [SerializeField] private int totalSteps = 3;

        [Header("Mensagens")]
        [SerializeField] private string idleMessageTemplate = "Clique o botão {step} de {total}.";
        [SerializeField] private string wrongMessage = "Ordem errada! Recomeçando do botão 1.";
        [SerializeField] private string winMessage = "MISSÃO CUMPRIDA!";

        public event Action OnMissionComplete;
        public event Action<int> OnStepAdvanced; // próximo step esperado

        private int _expectedStep = 1;
        private bool _completed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start() => RefreshStatus();

        public int CurrentExpectedStep => _expectedStep;

        public void ReportPress(ButtonMission btn)
        {
            if (_completed) return;

            if (btn.order == _expectedStep)
            {
                _expectedStep++;
                if (_expectedStep > totalSteps)
                {
                    _completed = true;
                    SetStatus(winMessage);
                    Play(winClip);
                    OnMissionComplete?.Invoke();
                }
                else
                {
                    RefreshStatus();
                    OnStepAdvanced?.Invoke(_expectedStep);
                }
            }
            else
            {
                _expectedStep = 1;
                SetStatus(wrongMessage);
                Play(wrongClip);
                OnStepAdvanced?.Invoke(_expectedStep);
            }
        }

        private void RefreshStatus()
        {
            SetStatus(idleMessageTemplate.Replace("{step}", _expectedStep.ToString())
                                          .Replace("{total}", totalSteps.ToString()));
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
