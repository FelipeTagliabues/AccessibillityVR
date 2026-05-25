using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Accessibility
{
    /// <summary>
    /// Constrói e gerencia o Menu Inicial e a Tela de Vitória.
    /// Menu inicial pausa a missão (catarata = 0); Começar libera a missão (catarata = 1).
    /// Vitória dispara quando MissionManager.OnMissionComplete sobe.
    /// </summary>
    public class GameFlowUI : MonoBehaviour
    {
        [SerializeField] private LowVisionSettings lowVision;
        [SerializeField] private float spawnDistance = 0.8f;

        private GameObject _startMenu;
        private GameObject _victoryScreen;
        private GameObject _gameOverScreen;

        void Start()
        {
            _startMenu = BuildMenu("StartMenu",
                title: "AccessibillityVR",
                subtitle: "Experimente o mundo com baixa visão.\nClique os botões na ordem correta.",
                primaryLabel: "Começar Jogo",
                primaryAction: StartGame,
                secondaryLabel: "Sair",
                secondaryAction: QuitGame);

            _victoryScreen = BuildMenu("VictoryScreen",
                title: "MISSÃO CUMPRIDA!",
                subtitle: "Você encontrou todos os botões na ordem certa.\nObrigado por experimentar a baixa visão.",
                primaryLabel: "Jogar Novamente",
                primaryAction: Replay,
                secondaryLabel: "Sair",
                secondaryAction: QuitGame);
            _victoryScreen.SetActive(false);

            _gameOverScreen = BuildMenu("GameOverScreen",
                title: "VOCÊ FOI ATROPELADO",
                subtitle: "Atravessar a rua sem enxergar bem é perigoso.\nTente de novo, com mais cuidado.",
                primaryLabel: "Tentar de Novo",
                primaryAction: Replay,
                secondaryLabel: "Sair",
                secondaryAction: QuitGame);
            _gameOverScreen.SetActive(false);

            if (lowVision != null) lowVision.SetIntensity(0f); // sem catarata durante o menu
            PositionInFrontOfCamera(_startMenu);

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionComplete += OnMissionComplete;
            }
            CarHitDetector.OnPlayerHit += OnPlayerHit;
        }

        void OnDestroy()
        {
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnMissionComplete -= OnMissionComplete;
            }
            CarHitDetector.OnPlayerHit -= OnPlayerHit;
        }

        private void OnPlayerHit()
        {
            if (_gameOverScreen == null || _gameOverScreen.activeSelf) return;
            if (lowVision != null) lowVision.SetIntensity(0f);
            _gameOverScreen.SetActive(true);
            PositionInFrontOfCamera(_gameOverScreen);
        }

        private void StartGame()
        {
            _startMenu.SetActive(false);
            if (lowVision != null) lowVision.SetIntensity(1f);
        }

        private void OnMissionComplete()
        {
            if (lowVision != null) lowVision.SetIntensity(0f); // limpa para ver a vitória
            _victoryScreen.SetActive(true);
            PositionInFrontOfCamera(_victoryScreen);
        }

        private void Replay()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────────────────────── construção da UI ─────────────────────────────

        private GameObject BuildMenu(string name, string title, string subtitle,
            string primaryLabel, Action primaryAction,
            string secondaryLabel, Action secondaryAction)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.001f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null) go.AddComponent(trackedType);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 550f);

            // Background
            var bg = NewChild(go.transform, "Background");
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.9f);
            FillParent(bg);

            // Title
            var titleRT = NewChild(go.transform, "Title");
            var titleText = titleRT.gameObject.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 64;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleRT.anchorMin = new Vector2(0f, 0.7f);
            titleRT.anchorMax = new Vector2(1f, 0.95f);
            titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

            // Subtitle
            var subRT = NewChild(go.transform, "Subtitle");
            var subText = subRT.gameObject.AddComponent<TextMeshProUGUI>();
            subText.text = subtitle;
            subText.fontSize = 28;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.85f, 0.85f, 0.85f);
            subRT.anchorMin = new Vector2(0.05f, 0.45f);
            subRT.anchorMax = new Vector2(0.95f, 0.7f);
            subRT.offsetMin = subRT.offsetMax = Vector2.zero;

            // Primary button
            BuildButton(go.transform, primaryLabel, primaryAction,
                anchorMin: new Vector2(0.1f, 0.22f), anchorMax: new Vector2(0.9f, 0.36f),
                color: new Color(0.2f, 0.6f, 1f));

            // Secondary button
            BuildButton(go.transform, secondaryLabel, secondaryAction,
                anchorMin: new Vector2(0.1f, 0.06f), anchorMax: new Vector2(0.9f, 0.2f),
                color: new Color(0.5f, 0.5f, 0.5f));

            // Collider para interactor de VR encostar
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.7f, 0.55f, 0.02f);
            col.isTrigger = true;

            return go;
        }

        private void BuildButton(Transform parent, string label, Action onClick,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var btnGO = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = btnGO.GetComponent<Image>();
            img.color = color;

            var btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var labelGO = NewChild(rt, "Label");
            var labelText = labelGO.gameObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 32;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            FillParent(labelGO);
        }

        private static RectTransform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void PositionInFrontOfCamera(GameObject menu)
        {
            var cam = Camera.main;
            if (cam == null) return;
            menu.transform.position = cam.transform.position + cam.transform.forward * spawnDistance;
            menu.transform.rotation = Quaternion.LookRotation(menu.transform.position - cam.transform.position, Vector3.up);
        }
    }
}
