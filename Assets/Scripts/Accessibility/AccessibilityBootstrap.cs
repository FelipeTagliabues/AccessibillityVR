using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Accessibility
{
    /// <summary>
    /// Bootstrap único da feature: constrói HUD, configura volume, wira PushButtons.
    /// Adicione este componente a um GameObject vazio na cena (ex.: "AccessibilityBootstrap")
    /// e tudo funciona em Play Mode, sem setup manual no Editor.
    /// </summary>
    public class AccessibilityBootstrap : MonoBehaviour
    {
        [Header("Bounds aproximados do cenário (XZ)")]
        [SerializeField] private Vector2 worldMin = new Vector2(-30f, -30f);
        [SerializeField] private Vector2 worldMax = new Vector2(30f, 30f);

        [Header("Missão")]
        [Tooltip("Tamanho da fonte do número em cada botão. Pequeno = difícil de ler com catarata.")]
        [SerializeField] private float buttonNumberFontSize = 14f;
        [Tooltip("Altura (m) acima do botão onde o número aparece.")]
        [SerializeField] private float buttonNumberHeight = 0.4f;
        [Tooltip("Tamanho do BoxCollider de cada PushButton (cubo).")]
        [SerializeField] private float buttonColliderSize = 1.5f;

        [Header("Áudio (opcional)")]
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip wrongClip;

        [Header("Player (auto se vazio: Camera.main)")]
        [SerializeField] private Transform playerOverride;

        void Awake()
        {
            EnsureLowVisionVolume();
            EnableCameraPostProcessing();
            AddSceneColliders();
            ConstrainPlayer();
            RepositionPlayerNearBooth();
            SetupCars();
            var hud = BuildHUD(out var statusText, out var blurSlider,
                out var minimapPanel, out var playerDot, out var targetDot, out var mapRect);

            var player = playerOverride != null ? playerOverride : (Camera.main != null ? Camera.main.transform : null);
            var pushButtons = SetupPushButtons();

            // MinimapTracker — aponta para o PRIMEIRO botão da sequência (order=1)
            var firstButton = pushButtons.Count > 0 ? pushButtons[0] : null;
            var tracker = minimapPanel.gameObject.AddComponent<MinimapTracker>();
            SetPrivate(tracker, "player", player);
            SetPrivate(tracker, "target", firstButton != null ? firstButton.transform : null);
            SetPrivate(tracker, "worldMin", worldMin);
            SetPrivate(tracker, "worldMax", worldMax);
            SetPrivate(tracker, "mapRect", mapRect);
            SetPrivate(tracker, "playerDot", playerDot);
            SetPrivate(tracker, "targetDot", targetDot);

            // MissionManager
            var mmGO = new GameObject("MissionManager");
            mmGO.transform.SetParent(transform);
            var mm = mmGO.AddComponent<MissionManager>();
            var src = mmGO.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            SetPrivate(mm, "statusText", statusText);
            SetPrivate(mm, "audioSource", src);
            SetPrivate(mm, "winClip", winClip);
            SetPrivate(mm, "wrongClip", wrongClip);

            // Blur slider wiring
            var lvSettings = FindObjectOfType<LowVisionSettings>();
            if (lvSettings != null && blurSlider != null)
            {
                blurSlider.onValueChanged.AddListener(lvSettings.SetIntensity);
            }

            // HUDToggle on this GameObject — self-cria sua InputAction interna
            var toggle = gameObject.AddComponent<HUDToggle>();
            SetPrivate(toggle, "hud", hud);

            // GameFlowUI — menu inicial + tela de vitória
            var flow = gameObject.AddComponent<GameFlowUI>();
            SetPrivate(flow, "lowVision", lvSettings);

            // Fallback de mouse para clicar nos botões em editor
            gameObject.AddComponent<MouseClickToButton>();

            // MinimapTracker segue o botão da ordem atual, conforme MissionManager avança.
            // Tracker já recalcula a cada frame, basta trocar a referência de target.
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.OnStepAdvanced += step =>
                {
                    if (step >= 1 && step <= pushButtons.Count)
                    {
                        SetPrivate(tracker, "target", pushButtons[step - 1].transform);
                    }
                };
            }

            Debug.Log($"[AccessibilityBootstrap] OK. PushButtons encontrados: {pushButtons.Count}.");
        }

        // ───────────────────────────── Low Vision Volume ─────────────────────────────

        private void EnsureLowVisionVolume()
        {
            var existing = FindObjectOfType<LowVisionSettings>();
            if (existing != null) return;

            var go = new GameObject("LowVisionVolume");
            go.transform.SetParent(transform);
            go.AddComponent<Volume>();
            go.AddComponent<LowVisionSettings>();
        }

        private void EnableCameraPostProcessing()
        {
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        // ───────────────────────────── Colliders no cenário ─────────────────────────────

        private void AddSceneColliders()
        {
            // Procura objetos com MeshRenderer mas sem Collider e adiciona MeshCollider.
            // Isso evita atravessar paredes/casas/calçadas que só tinham renderer.
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int added = 0;
            foreach (var root in roots)
            {
                // Pula o próprio _AccessibilityRoot, XR Origin, UI, etc. para não criar colliders em controles/HUD
                if (root == gameObject) continue;
                var nameLower = root.name.ToLowerInvariant();
                if (nameLower.Contains("xr ") || nameLower.Contains("accessibility") ||
                    nameLower.Contains("ui") || nameLower.Contains("event") ||
                    nameLower.Contains("simulator") || nameLower.Contains("teleport") ||
                    nameLower.Contains("interactable") || nameLower.Contains("gerenciador") ||
                    nameLower.Contains("spawn"))
                {
                    continue;
                }

                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (mf.GetComponent<Collider>() != null) continue;
                    if (mf.sharedMesh == null) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    added++;
                }
            }
            Debug.Log($"[AccessibilityBootstrap] {added} MeshCollider(s) adicionado(s) ao cenário.");
        }

        // ───────────────────────────── Player constraints ─────────────────────────────

        private void RepositionPlayerNearBooth()
        {
            var booth = GameObject.Find("Telephone Booth");
            var cam = Camera.main;
            if (booth == null || cam == null)
            {
                Debug.LogWarning("[AccessibilityBootstrap] Telephone Booth ou Camera.main não encontrados — não reposicionei o player.");
                return;
            }
            var rig = cam.transform.root;
            // 2m na frente da cabine (forward da booth aponta para fora da porta)
            var spawn = booth.transform.position + booth.transform.forward * 2f;
            spawn.y = rig.position.y;
            rig.position = spawn;
            // Olha para a cabine (vira-se 180° p/ ver a cena à frente)
            rig.LookAt(new Vector3(booth.transform.position.x, rig.position.y, booth.transform.position.z));
            rig.Rotate(0f, 180f, 0f);
            Debug.Log($"[AccessibilityBootstrap] Player reposicionado em {spawn} (perto da cabine).");
        }

        private void SetupCars()
        {
            int added = 0;
            foreach (var car in FindObjectsOfType<MonoBehaviour>())
            {
                // Pega scripts CarMovement (existente no projeto) como marcador de "isso é carro"
                if (car.GetType().Name != "CarMovement") continue;
                if (car.GetComponent<CarHitDetector>() != null) continue;
                car.gameObject.AddComponent<CarHitDetector>();
                added++;
            }
            Debug.Log($"[AccessibilityBootstrap] CarHitDetector adicionado em {added} carro(s).");
        }

        private void ConstrainPlayer()
        {
            // Anexa PlayerConstraints no XR Origin (root do rig) para que o rig inteiro
            // — câmera + controles + Camera Offset — seja revertido em colisão.
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[AccessibilityBootstrap] Camera.main não encontrada — PlayerConstraints não anexado.");
                return;
            }
            var rig = cam.transform.root;
            if (rig.GetComponent<PlayerConstraints>() == null)
            {
                rig.gameObject.AddComponent<PlayerConstraints>();
            }
            // Remove versão antiga na câmera se existir (cenário de hot-reload)
            var stale = cam.GetComponent<PlayerConstraints>();
            if (stale != null) Destroy(stale);
        }

        // ───────────────────────────── HUD UI ─────────────────────────────

        private GameObject BuildHUD(out TMP_Text statusText, out Slider blurSlider,
            out RectTransform minimapPanel, out RectTransform playerDot, out RectTransform targetDot, out RectTransform mapRect)
        {
            // Canvas world-space
            var hudGO = new GameObject("AccessibilityHUD");
            hudGO.transform.SetParent(transform);
            hudGO.transform.localPosition = new Vector3(0f, 1.5f, 0.6f);
            hudGO.transform.localScale = Vector3.one * 0.001f;

            var canvas = hudGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            hudGO.AddComponent<CanvasScaler>();
            hudGO.AddComponent<GraphicRaycaster>();
            // Tracked raycaster — necessário para sliders responderem ao raio do controle
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null) hudGO.AddComponent(trackedType);

            var rt = hudGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 500f);

            // Background
            var bg = CreateUIChild(hudGO.transform, "Background");
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.75f);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            // MinimapPanel
            minimapPanel = CreateUIChild(hudGO.transform, "MinimapPanel");
            minimapPanel.anchorMin = new Vector2(0.5f, 1f);
            minimapPanel.anchorMax = new Vector2(0.5f, 1f);
            minimapPanel.pivot = new Vector2(0.5f, 1f);
            minimapPanel.anchoredPosition = new Vector2(0f, -20f);
            minimapPanel.sizeDelta = new Vector2(280f, 280f);

            // MapBackground
            mapRect = CreateUIChild(minimapPanel, "MapBackground");
            var mapImg = mapRect.gameObject.AddComponent<Image>();
            mapImg.color = new Color(0.15f, 0.25f, 0.15f, 1f);
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;

            // Grid lines (visual placeholder simples)
            for (int i = 1; i < 4; i++)
            {
                float t = i / 4f;
                AddGridLine(mapRect, true, t);
                AddGridLine(mapRect, false, t);
            }

            // PlayerDot (azul)
            playerDot = CreateUIChild(mapRect, "PlayerDot");
            var pImg = playerDot.gameObject.AddComponent<Image>();
            pImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            playerDot.sizeDelta = new Vector2(16f, 16f);
            playerDot.anchorMin = playerDot.anchorMax = new Vector2(0.5f, 0.5f);

            // TargetDot (vermelho)
            targetDot = CreateUIChild(mapRect, "TargetDot");
            var tImg = targetDot.gameObject.AddComponent<Image>();
            tImg.color = new Color(1f, 0.3f, 0.3f, 1f);
            targetDot.sizeDelta = new Vector2(20f, 20f);
            targetDot.anchorMin = targetDot.anchorMax = new Vector2(0.5f, 0.5f);

            // StatusText
            var statusRT = CreateUIChild(hudGO.transform, "StatusText");
            statusText = statusRT.gameObject.AddComponent<TextMeshProUGUI>();
            statusText.text = "Procure o botão correto.";
            statusText.fontSize = 28f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
            statusRT.anchorMin = new Vector2(0f, 0.25f);
            statusRT.anchorMax = new Vector2(1f, 0.4f);
            statusRT.offsetMin = new Vector2(10f, 0f);
            statusRT.offsetMax = new Vector2(-10f, 0f);

            // BlurIntensitySlider
            var sliderLabelRT = CreateUIChild(hudGO.transform, "BlurLabel");
            var sliderLabel = sliderLabelRT.gameObject.AddComponent<TextMeshProUGUI>();
            sliderLabel.text = "Intensidade da catarata";
            sliderLabel.fontSize = 20f;
            sliderLabel.alignment = TextAlignmentOptions.Center;
            sliderLabel.color = Color.white;
            sliderLabelRT.anchorMin = new Vector2(0f, 0.15f);
            sliderLabelRT.anchorMax = new Vector2(1f, 0.22f);
            sliderLabelRT.offsetMin = sliderLabelRT.offsetMax = Vector2.zero;

            blurSlider = CreateSlider(hudGO.transform, "BlurIntensitySlider", out var sliderRT);
            sliderRT.anchorMin = new Vector2(0.1f, 0.05f);
            sliderRT.anchorMax = new Vector2(0.9f, 0.13f);
            sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;
            blurSlider.minValue = 0f;
            blurSlider.maxValue = 1f;
            blurSlider.value = 1f;
            blurSlider.wholeNumbers = false;

            // Rigidbody + Collider + LazyFollow — versão simplificada (sem grab) p/ hoje
            var rb = hudGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = hudGO.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.5f, 0.02f);

            hudGO.SetActive(false);
            return hudGO;
        }

        private static RectTransform CreateUIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void AddGridLine(RectTransform parent, bool horizontal, float t)
        {
            var rt = CreateUIChild(parent, "GridLine");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.3f, 0.45f, 0.3f, 0.6f);
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0f, t);
                rt.anchorMax = new Vector2(1f, t);
                rt.sizeDelta = new Vector2(0f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(t, 0f);
                rt.anchorMax = new Vector2(t, 1f);
                rt.sizeDelta = new Vector2(1f, 0f);
            }
        }

        private static Slider CreateSlider(Transform parent, string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            rt = go.GetComponent<RectTransform>();

            var bg = CreateUIChild(rt, "Background");
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            bg.anchorMin = new Vector2(0f, 0.4f);
            bg.anchorMax = new Vector2(1f, 0.6f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;

            var fillArea = CreateUIChild(rt, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.4f);
            fillArea.anchorMax = new Vector2(1f, 0.6f);
            fillArea.offsetMin = new Vector2(5f, 0f);
            fillArea.offsetMax = new Vector2(-5f, 0f);

            var fill = CreateUIChild(fillArea, "Fill");
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var handleArea = CreateUIChild(rt, "Handle Slide Area");
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            var handle = CreateUIChild(handleArea, "Handle");
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            handle.sizeDelta = new Vector2(20f, 0f);

            var slider = go.GetComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        // ───────────────────────────── PushButtons setup ─────────────────────────────

        private List<ButtonMission> SetupPushButtons()
        {
            var found = new List<ButtonMission>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var allButtons = new List<GameObject>();
            foreach (var root in roots)
            {
                CollectByName(root.transform, "PushButton", allButtons);
            }

            int order = 1;
            foreach (var go in allButtons)
            {
                if (go.GetComponent<ButtonMission>() != null) continue;

                // BoxCollider grande o suficiente p/ ser facilmente alvo de raio
                var col = go.GetComponent<BoxCollider>();
                if (col == null) col = go.AddComponent<BoxCollider>();
                col.size = Vector3.one * buttonColliderSize;
                col.center = Vector3.up * (buttonColliderSize * 0.5f);

                if (go.GetComponent<XRBaseInteractable>() == null)
                {
                    go.AddComponent<XRSimpleInteractable>();
                }

                var bm = go.AddComponent<ButtonMission>();
                bm.order = order;
                AddNumberLabel(go, order, buttonNumberFontSize, buttonNumberHeight);
                found.Add(bm);
                order++;
            }
            return found;
        }

        private static void AddNumberLabel(GameObject button, int order, float fontSize, float height)
        {
            var canvasGO = new GameObject($"_OrderLabel_{order}");
            canvasGO.transform.SetParent(button.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, height, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.005f;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);

            // Bilboard simples — sempre olha para a câmera
            canvasGO.AddComponent<BillboardToCamera>();

            var textGO = new GameObject("Number", typeof(RectTransform));
            textGO.transform.SetParent(canvasGO.transform, false);
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = order.ToString();
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.2f);
        }

        private static void CollectByName(Transform root, string contains, List<GameObject> result)
        {
            if (root.name.Contains(contains) && root.GetComponentInParent<ButtonMission>() == null)
            {
                // Pega o topo da subárvore PushButton, mas evita filhos profundos duplicados
                if (!result.Contains(root.gameObject)) result.Add(root.gameObject);
            }
            for (int i = 0; i < root.childCount; i++)
            {
                CollectByName(root.GetChild(i), contains, result);
            }
        }

        // ───────────────────────────── Reflection helpers ─────────────────────────────

        private static void SetPrivate(object obj, string fieldName, object value)
        {
            var f = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(obj, value);
            else Debug.LogWarning($"[Bootstrap] campo '{fieldName}' não encontrado em {obj.GetType().Name}");
        }
    }
}
