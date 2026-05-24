# Baixa Visão (Catarata) + HUD — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar um efeito visual de catarata (URP Volume) à `SampleScene`, um HUD flutuante invocável (minimapa + sliders de acessibilidade) e uma missão de "achar o botão correto" entre os 3 PushButtons existentes — tudo funcional em uma sessão.

**Architecture:** 5 scripts MonoBehaviour isolados (`LowVisionSettings`, `MinimapTracker`, `ButtonMission`, `MissionManager`, `HUDToggle`) comunicando por `UnityEvent` + um singleton-leve. Efeito visual via URP Volume Profile, sem shader custom. Minimapa como `RawImage` estático com pontos posicionados por math puro de bounds.

**Tech Stack:** Unity URP 14, XR Interaction Toolkit 3.1.2, OpenXR, Input System 1.14, TMP. Setup via Unity MCP server (já conectado) onde possível; passos manuais marcados claramente.

**Nota sobre TDD:** Scripts Unity dependem de `UnityEngine` e não rodam fora do Editor. O projeto não tem test framework configurado, e configurá-lo (asmdef + Test Runner) adicionaria overhead incompatível com a meta "hoje". Testes formais são substituídos pelo **smoke test manual final (Task 14)** que valida o golden path do spec. A única math pura (`MinimapTracker.WorldToMapPosition`) é mantida estática e os exemplos de cálculo aparecem inline no código para conferência mental.

**Pré-requisitos:**
- Unity Editor aberto com a `SampleScene` carregada.
- MCP-Unity server rodando (já configurado em `.mcp.json`).

---

## File Structure

```
Assets/
├─ Scripts/
│  └─ Accessibility/           [novo]
│     ├─ LowVisionSettings.cs
│     ├─ MinimapTracker.cs
│     ├─ ButtonMission.cs
│     ├─ MissionManager.cs
│     └─ HUDToggle.cs
├─ Prefabs/                     [novo]
│  └─ AccessibilityHUD.prefab
├─ Settings/
│  └─ LowVisionProfile.asset    [novo]
└─ Media/
   └─ minimap.png               [novo]

docs/
└─ superpowers/
   ├─ specs/
   │  └─ 2026-05-24-baixa-visao-hud-design.md   [já existe]
   └─ plans/
      └─ 2026-05-24-baixa-visao-hud.md          [este arquivo]
```

---

### Task 1: Criar estrutura de pastas e LowVisionSettings.cs

**Files:**
- Create: `Assets/Scripts/Accessibility/LowVisionSettings.cs`

- [ ] **Step 1: Criar o script**

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Accessibility
{
    [RequireComponent(typeof(Volume))]
    public class LowVisionSettings : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float intensity = 1f;

        [Header("Valores máximos (quando intensity = 1)")]
        [SerializeField] private float maxAperture = 32f;
        [SerializeField] private float maxBloomIntensity = 1.5f;
        [SerializeField] private float minContrast = -25f;
        [SerializeField] private float minSaturation = -15f;
        [SerializeField] private float maxVignette = 0.2f;

        private Volume _volume;
        private DepthOfField _dof;
        private Bloom _bloom;
        private ColorAdjustments _color;
        private Vignette _vignette;

        void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.profile.TryGet(out _dof);
            _volume.profile.TryGet(out _bloom);
            _volume.profile.TryGet(out _color);
            _volume.profile.TryGet(out _vignette);
            Apply();
        }

        public void SetIntensity(float t)
        {
            intensity = Mathf.Clamp01(t);
            Apply();
        }

        private void Apply()
        {
            if (_dof != null) _dof.aperture.value = Mathf.Lerp(16f, maxAperture, intensity);
            if (_bloom != null) _bloom.intensity.value = Mathf.Lerp(0f, maxBloomIntensity, intensity);
            if (_color != null)
            {
                _color.contrast.value = Mathf.Lerp(0f, minContrast, intensity);
                _color.saturation.value = Mathf.Lerp(0f, minSaturation, intensity);
            }
            if (_vignette != null) _vignette.intensity.value = Mathf.Lerp(0f, maxVignette, intensity);
        }

        void OnValidate()
        {
            if (Application.isPlaying && _volume != null) Apply();
        }
    }
}
```

- [ ] **Step 2: Disparar recompile no Unity**

Chamar `mcp__mcp-unity__recompile_scripts`. Esperado: `success: true`, sem erros no console.

- [ ] **Step 3: Verificar console Unity**

Chamar `mcp__mcp-unity__get_console_logs` com `logType: "Error"`. Esperado: nenhum erro relacionado a `LowVisionSettings`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Accessibility/LowVisionSettings.cs Assets/Scripts/Accessibility.meta 2>/dev/null
git add Assets/Scripts/Accessibility/
git commit -m "feat(a11y): add LowVisionSettings controlling URP Volume intensity"
```

---

### Task 2: MinimapTracker.cs

**Files:**
- Create: `Assets/Scripts/Accessibility/MinimapTracker.cs`

- [ ] **Step 1: Criar o script**

```csharp
using UnityEngine;

namespace Accessibility
{
    public class MinimapTracker : MonoBehaviour
    {
        [Header("Referências de mundo")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform target;

        [Header("Bounds do cenário (mundo XZ)")]
        [SerializeField] private Vector2 worldMin = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 worldMax = new Vector2(20f, 20f);

        [Header("Referências de UI")]
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform playerDot;
        [SerializeField] private RectTransform targetDot;

        void Start()
        {
            if (target != null && targetDot != null)
            {
                targetDot.anchoredPosition = WorldToMapPosition(
                    new Vector2(target.position.x, target.position.z),
                    worldMin, worldMax, mapRect.rect.size);
            }
        }

        void Update()
        {
            if (player == null || playerDot == null || mapRect == null) return;
            playerDot.anchoredPosition = WorldToMapPosition(
                new Vector2(player.position.x, player.position.z),
                worldMin, worldMax, mapRect.rect.size);
        }

        // Math pura. Exemplo: worldPos=(0,0), min=(-20,-20), max=(20,20), mapSize=(300,300)
        // → normalized=(0.5, 0.5) → centered=(0, 0). Centro do mapa, correto.
        public static Vector2 WorldToMapPosition(Vector2 worldXZ, Vector2 worldMin, Vector2 worldMax, Vector2 mapSize)
        {
            Vector2 range = worldMax - worldMin;
            if (Mathf.Approximately(range.x, 0f) || Mathf.Approximately(range.y, 0f))
                return Vector2.zero;
            Vector2 normalized = new Vector2(
                (worldXZ.x - worldMin.x) / range.x,
                (worldXZ.y - worldMin.y) / range.y);
            return new Vector2(
                (normalized.x - 0.5f) * mapSize.x,
                (normalized.y - 0.5f) * mapSize.y);
        }
    }
}
```

- [ ] **Step 2: Recompile + verificar console**

`mcp__mcp-unity__recompile_scripts` → `mcp__mcp-unity__get_console_logs` (errors only).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Accessibility/MinimapTracker.cs
git commit -m "feat(a11y): add MinimapTracker with world-to-map math"
```

---

### Task 3: ButtonMission.cs

**Files:**
- Create: `Assets/Scripts/Accessibility/ButtonMission.cs`

- [ ] **Step 1: Criar o script**

```csharp
using UnityEngine;
using UnityEngine.Events;

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
```

- [ ] **Step 2: Recompile + verificar console**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Accessibility/ButtonMission.cs
git commit -m "feat(a11y): add ButtonMission to flag target PushButton"
```

---

### Task 4: MissionManager.cs

**Files:**
- Create: `Assets/Scripts/Accessibility/MissionManager.cs`

- [ ] **Step 1: Criar o script**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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
```

- [ ] **Step 2: Recompile + verificar console**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Accessibility/MissionManager.cs
git commit -m "feat(a11y): add MissionManager singleton for win/wrong feedback"
```

---

### Task 5: HUDToggle.cs

**Files:**
- Create: `Assets/Scripts/Accessibility/HUDToggle.cs`

- [ ] **Step 1: Criar o script**

```csharp
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
```

- [ ] **Step 2: Recompile + verificar console**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Accessibility/HUDToggle.cs
git commit -m "feat(a11y): add HUDToggle bound to InputAction"
```

---

### Task 6: Criar LowVisionProfile (Volume Profile asset)

**Files:**
- Create: `Assets/Settings/LowVisionProfile.asset`

- [ ] **Step 1: Pedir ao usuário para criar o profile no Editor**

Mensagem a exibir ao usuário:

> No Unity Editor: clique direito em `Assets/Settings/` → Create → Volume Profile → renomear para `LowVisionProfile`. Confirme antes de prosseguir.

Aguardar confirmação textual do usuário.

- [ ] **Step 2: Configurar overrides via MCP**

Após confirmação, usar `mcp__mcp-unity__batch_execute` (se possível) ou instruir o usuário a clicar **Add Override** e adicionar:
- `Post-processing/Depth of Field` — Mode: Bokeh, Focus Distance: 0.3, Aperture: 32, Focal Length: 50
- `Post-processing/Bloom` — Intensity: 1.5, Threshold: 0.8, Scatter: 0.7
- `Post-processing/Color Adjustments` — Contrast: -25, Saturation: -15
- `Post-processing/Vignette` — Intensity: 0.2, Smoothness: 0.4

Todos os campos com checkbox **on** para serem overrides ativos.

- [ ] **Step 3: Commit**

```bash
git add Assets/Settings/LowVisionProfile.asset Assets/Settings/LowVisionProfile.asset.meta
git commit -m "feat(a11y): add LowVisionProfile URP Volume profile"
```

---

### Task 7: Adicionar LowVisionVolume GameObject na cena

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

- [ ] **Step 1: Verificar que a SampleScene está carregada**

Chamar `mcp__mcp-unity__get_scene_info`. Esperado: `Active Scene: SampleScene`.

- [ ] **Step 2: Criar GameObject "LowVisionVolume" via menu**

Chamar `mcp__mcp-unity__execute_menu_item` com `menuPath: "GameObject/Volume/Global Volume"`.

- [ ] **Step 3: Renomear o GameObject**

Usar `mcp__mcp-unity__update_gameobject` (consultar schema) ou pedir ao usuário renomear para `LowVisionVolume` no Hierarchy.

- [ ] **Step 4: Atribuir o profile criado**

Pedir ao usuário no Editor: selecionar `LowVisionVolume`, no componente `Volume`, arrastar `LowVisionProfile.asset` para o campo `Profile`.

- [ ] **Step 5: Adicionar componente LowVisionSettings**

```
mcp__mcp-unity__update_gameobject:
  idOrName: "LowVisionVolume"
  componentsToAdd: ["Accessibility.LowVisionSettings"]
```

(Se o tool não aceitar `componentsToAdd`, pedir ao usuário fazer Add Component → LowVisionSettings.)

- [ ] **Step 6: Validar que catarata está visível**

Pedir ao usuário: entrar em Play Mode brevemente. Esperado: cena fica embaçada e com bloom forte. Sair do Play Mode.

- [ ] **Step 7: Salvar cena**

`mcp__mcp-unity__save_scene` (default scene).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat(a11y): wire LowVisionVolume into SampleScene"
```

---

### Task 8: Capturar minimap PNG (top-down)

**Files:**
- Create: `Assets/Media/minimap.png`

- [ ] **Step 1: Instruir usuário a capturar a textura**

Mensagem ao usuário:

> Na Scene View do Unity:
> 1. Mude para projeção ortográfica (gizmo do canto superior direito → clique em "Persp" para alternar para "Iso").
> 2. Selecione o GameObject `Cenario` → tecle `F` (frame).
> 3. Mude para vista Top (gizmo verde Y).
> 4. Ajuste o zoom para enquadrar todo o cenário com folga.
> 5. Tire um screenshot da Scene View (Window → General → Game ou print da tela) — só preciso de uma imagem retangular top-down.
> 6. Salve como `Assets/Media/minimap.png` (criar pasta `Assets/Media/` se não existir).
> 7. No Inspector da textura: Texture Type = "Sprite (2D and UI)", Mip Maps Generated = off, Filter Mode = Bilinear. Apply.
> 8. Confirme quando estiver pronto.

Aguardar confirmação. (Alternativa: usar `mcp__chrome-devtools__take_screenshot` se quiser automatizar a captura via Game View, mas requer Game View configurada com câmera top-down — fora do escopo "hoje".)

- [ ] **Step 2: Commit**

```bash
git add Assets/Media/minimap.png Assets/Media/minimap.png.meta
git commit -m "feat(a11y): add top-down minimap texture"
```

---

### Task 9: Construir AccessibilityHUD na cena (instância, depois virar prefab)

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`
- Create (eventualmente): `Assets/Prefabs/AccessibilityHUD.prefab`

- [ ] **Step 1: Criar Canvas world-space "AccessibilityHUD"**

Via MCP, executar menu `GameObject/UI/Canvas`, depois renomear para `AccessibilityHUD`. No componente `Canvas`:
- Render Mode: World Space
- Width: 300, Height: 300, Scale: 0.001 (resultando em ~30cm visíveis)

Adicionar componente `TrackedDeviceGraphicRaycaster`.

- [ ] **Step 2: Adicionar filhos visuais**

Pedir ao usuário no Editor (mais rápido que MCP para UI nested):

> Como filhos do `AccessibilityHUD`, crie via menu UI:
> - `Image` nomeado `Background` (fundo escuro semitransparente, esticado a tudo).
> - GameObject vazio `MinimapPanel` (RectTransform 200x200, anchor center).
>   - `RawImage` filho `MapBackground` (atribuir textura `minimap.png`).
>   - `Image` filho `PlayerDot` (cor azul, tamanho 12x12).
>   - `Image` filho `TargetDot` (cor vermelha, tamanho 12x12).
> - `TextMeshPro - Text (UI)` `StatusText` abaixo do mapa (fonte 24, cor branca).
> - GameObject vazio `SettingsPanel`:
>   - `Slider` `BlurIntensitySlider` (min 0, max 1, value 1, whole numbers off).
>   - `Slider` `FontSizeSlider` (min 0.5, max 2, value 1).
>   - `Toggle` `ContrastToggle`.
>
> Confirme quando terminar.

- [ ] **Step 3: Adicionar componente `MinimapTracker` ao MinimapPanel**

Via MCP (`mcp__mcp-unity__update_gameobject` com componente `Accessibility.MinimapTracker`) ou Editor.

- [ ] **Step 4: Configurar bounds aproximados do cenário**

Inspecionar via MCP os limites XZ do `Cenario`:

```
mcp__mcp-unity__get_gameobject:
  idOrName: "Cenario"
  includeComponentProperties: true
  maxDepth: 0
```

Estimar bounds manualmente baseado em filhos visíveis (carros, casas). Default sugerido: `worldMin = (-30, -30)`, `worldMax = (30, 30)`. Ajustar após smoke test se mapa parecer descalibrado.

- [ ] **Step 5: Conectar referências do MinimapTracker no Inspector**

Mensagem ao usuário:

> Selecione `MinimapPanel`. No componente `MinimapTracker`:
> - Player: arraste a `Main Camera` do XR Origin
> - Target: arraste o PushButton que será o alvo (deixe vazio por enquanto; setamos na Task 11)
> - mapRect: arraste o `MapBackground`
> - playerDot: arraste `PlayerDot`
> - targetDot: arraste `TargetDot`

- [ ] **Step 6: Tornar o HUD interativo (XRGrab + LazyFollow)**

Mensagem:

> Selecione `AccessibilityHUD`. Adicione componentes:
> - `Rigidbody` (isKinematic = true, useGravity = false)
> - `BoxCollider` (size cobrindo o painel ~0.3 x 0.3 x 0.02)
> - `XR Grab Interactable`
> - `Lazy Follow` (target = Main Camera do XR Origin, offset Z = 0.5, smoothing 0.1)

- [ ] **Step 7: Desativar o HUD por padrão**

```
mcp__mcp-unity__update_gameobject:
  idOrName: "AccessibilityHUD"
  activeSelf: false
```

- [ ] **Step 8: Conectar slider de blur ao LowVisionSettings**

Mensagem:

> No `BlurIntensitySlider`, onValueChanged (Dynamic float):
> - Object: `LowVisionVolume`
> - Function: `LowVisionSettings.SetIntensity`

- [ ] **Step 9: Salvar como prefab e cena**

Arrastar `AccessibilityHUD` do Hierarchy para `Assets/Prefabs/AccessibilityHUD.prefab`. Salvar cena.

- [ ] **Step 10: Commit**

```bash
git add Assets/Prefabs/ Assets/Scenes/SampleScene.unity
git commit -m "feat(a11y): build AccessibilityHUD with minimap and sliders"
```

---

### Task 10: Adicionar ButtonMission em cada PushButton e marcar o alvo

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

- [ ] **Step 1: Localizar os 3 PushButtons**

Via MCP:

```
mcp__mcp-unity__get_gameobject:
  idOrName: "PushButton"
  includeComponentProperties: false
  maxDepth: 1
```

Listar instâncias `PushButton`, `PushButton (1)`, `PushButton (2)` na hierarquia. Anotar paths.

- [ ] **Step 2: Adicionar componente `ButtonMission` em cada um**

Para cada PushButton (3 chamadas):

```
mcp__mcp-unity__update_gameobject:
  idOrName: "<path do PushButton>"
  componentsToAdd: ["Accessibility.ButtonMission"]
```

(Se o tool não aceitar, instruir o usuário a Add Component em cada um.)

- [ ] **Step 3: Marcar UM como target**

Decidir qual PushButton será o alvo (sugestão: o mais distante do spawn do player, para dar valor à missão). No Inspector, `isTarget = true` apenas nele.

- [ ] **Step 4: Wiring do PushButton press → ButtonMission.OnPressed**

Inspecionar o prefab PushButton (XRPushButton ou interactable similar) e encontrar o `UnityEvent` de press (provavelmente `OnPress` / `OnSelectEntered`).

Mensagem ao usuário:

> Em cada um dos 3 PushButtons:
> 1. Procure o componente `XRPushButton` (ou o que tiver `OnPress`/`onPress`).
> 2. Em `OnPress` (UnityEvent), adicione um listener:
>    - Object: o próprio PushButton
>    - Function: `ButtonMission.OnPressed` (Dynamic ou Static, sem parâmetros)
> Confirme quando terminar os três.

- [ ] **Step 5: Salvar cena**

`mcp__mcp-unity__save_scene`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat(a11y): mark target PushButton and wire press events"
```

---

### Task 11: Atribuir target no MinimapTracker + adicionar MissionManager

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

- [ ] **Step 1: Conectar Target do MinimapTracker**

Mensagem:

> Selecione `MinimapPanel` no Hierarchy. No `MinimapTracker`, arraste o PushButton que você marcou como `isTarget = true` para o campo `Target`. Confirme.

- [ ] **Step 2: Criar GameObject "MissionManager" na raiz**

Via MCP: `mcp__mcp-unity__execute_menu_item` com `menuPath: "GameObject/Create Empty"`, depois `update_gameobject` para renomear para `MissionManager`.

Adicionar componente `Accessibility.MissionManager`.

- [ ] **Step 3: Adicionar AudioSource ao MissionManager**

Mensagem ao usuário (ou via MCP `componentsToAdd: ["AudioSource"]`):

> No `MissionManager`, adicione um `AudioSource` (PlayOnAwake: off, SpatialBlend: 0).

- [ ] **Step 4: Conectar referências do MissionManager**

Mensagem:

> No componente `MissionManager`:
> - statusText: arraste o `StatusText` do AccessibilityHUD
> - audioSource: o próprio AudioSource criado acima
> - winClip: escolha um som agradável (ex.: usar `Assets/CarHorn.mp3` por enquanto como placeholder, ou outro clip)
> - wrongClip: outro som curto (pode reusar mesmo)

- [ ] **Step 5: Salvar e commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat(a11y): wire MissionManager and minimap target"
```

---

### Task 12: Adicionar HUDToggle ao XR Origin

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

- [ ] **Step 1: Localizar XR Origin**

```
mcp__mcp-unity__get_gameobject:
  idOrName: "Complete XR Origin Set Up Variant"
  maxDepth: 0
```

- [ ] **Step 2: Adicionar componente HUDToggle**

Via MCP `update_gameobject` com `componentsToAdd: ["Accessibility.HUDToggle"]`, ou via Editor.

- [ ] **Step 3: Criar/escolher InputActionReference para botão Y esquerdo**

Mensagem ao usuário:

> 1. Encontre o asset `XRI Default Input Actions` (ou similar) em `Assets/Samples/XR Interaction Toolkit/.../Starter Assets/`.
> 2. Duplique-o ou edite: dentro do Action Map `XRI LeftHand`, crie uma nova Action chamada `ToggleHUD`, Action Type: Button.
> 3. Adicione um Binding: Path = `<XRController>{LeftHand}/secondaryButton`.
> 4. No `HUDToggle` do XR Origin:
>    - toggleAction: arraste a `ToggleHUD` action recém-criada (como InputActionReference)
>    - hud: arraste o `AccessibilityHUD` da cena
>    - startVisible: false
> Confirme quando pronto.

- [ ] **Step 4: Salvar e commit**

```bash
git add Assets/Scenes/SampleScene.unity Assets/Samples/
git commit -m "feat(a11y): wire HUDToggle to left controller Y button"
```

---

### Task 13: Smoke test — golden path

**Files:** nenhum

- [ ] **Step 1: Entrar em Play Mode**

Via MCP (`mcp__mcp-unity__execute_menu_item` com `menuPath: "Edit/Play"`) ou usuário aperta Play.

- [ ] **Step 2: Verificar console limpo**

`mcp__mcp-unity__get_console_logs` com `logType: "Error"`. Esperado: sem erros.

- [ ] **Step 3: Checklist visual (usuário com headset OU usando XR Interaction Simulator presente na cena)**

Mensagem ao usuário com cada item para confirmar OK/FAIL:

> 1. Cena entra em Play sem erro no console.
> 2. A visão fica imediatamente embaçada e ofuscante (catarata ativa).
> 3. Apertar **Y** no controle esquerdo (ou tecla mapeada no simulator) faz o HUD aparecer na frente.
> 4. Apertar **Y** de novo esconde o HUD.
> 5. Movendo o player, o ponto azul do minimapa se move correspondentemente.
> 6. O ponto vermelho está fixo no botão-alvo.
> 7. Mover o `BlurIntensitySlider` para 0 → cena fica nítida; mover para 1 → volta o efeito.
> 8. Apertar um PushButton-distrator → som de erro + texto "Não é esse botão. Tente outro."
> 9. Apertar o PushButton-alvo → som de vitória + texto "MISSÃO CUMPRIDA!".

- [ ] **Step 4: Tratar falhas**

Para cada item FAIL, abrir um sub-task de bugfix usando `superpowers:systematic-debugging`. Documentar fix no commit. Repetir Step 1-3 até golden path passar.

- [ ] **Step 5: Sair do Play Mode e commit final**

```bash
git add -u
git commit -m "fix(a11y): smoke test adjustments" --allow-empty
```

(Commit vazio se não houve ajustes — apenas marco temporal.)

---

### Task 14: Atualizar `MEMORY.md` do auto-memory com referências do projeto (opcional, não bloqueante)

**Files:**
- Modify: `/home/murilolima/.claude/projects/-home-murilolima-projetos-AccessibillityVR/memory/MEMORY.md`

- [ ] **Step 1: Salvar memória de referência sobre estrutura do projeto**

Criar memory file `accessibility_project.md` (type: project) descrevendo: cena `SampleScene`, scripts em `Assets/Scripts/Accessibility/`, condição simulada (catarata), missão (achar 1 de 3 PushButtons). Linkar para spec e plan.

(Não bloqueante para o smoke test passar.)

---

## Self-Review

**Spec coverage:**
- §2 Escopo "URP Volume sem shader" → Task 1 (script) + Task 6 (profile) + Task 7 (scene) ✓
- §2 "HUD invocável por botão Y" → Task 5 + Task 12 ✓
- §2 "Missão 1-de-3 PushButtons" → Task 3 + Task 10 ✓
- §4.1-4.7 todos componentes → Tasks 1-5 (scripts) + Tasks 7,9,10,11,12 (wiring) ✓
- §6 setup manual → coberto distribuído em Tasks 6-12 ✓
- §7 critérios de aceite → Task 13 mapeia 1:1 ✓
- §8 riscos (DoF Single-Pass, bounds errados, botão Y não bindado) → endereçados nas tarefas relevantes (Task 7 Step 6 valida visualmente; Task 9 Step 4 com bounds default ajustável; Task 12 Step 3 cria action manualmente) ✓

**Placeholder scan:** Nenhum TBD/TODO/"add appropriate handling". Mensagens ao usuário são completas. Código completo em cada step.

**Type consistency:** `MissionManager.Instance`, `ButtonMission.isTarget`, `MissionManager.ReportPress(ButtonMission)`, `LowVisionSettings.SetIntensity(float)` — consistentes entre Tasks 3, 4, 9 Step 8.

**Risco residual:** O tool `mcp__mcp-unity__update_gameobject` pode não suportar `componentsToAdd` — verifico no schema na hora; se não, fallback é instruir o usuário (todas as etapas marcadas "via MCP" têm fallback manual implícito). Documentado neste self-review.
