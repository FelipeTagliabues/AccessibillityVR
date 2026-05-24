# Design — Simulação de Baixa Visão (Catarata) + HUD de Acessibilidade

**Data:** 2026-05-24
**Projeto:** AccessibillityVR
**Abordagem aprovada:** A (leve, Quest-friendly, sem shader custom)

## 1. Objetivo

Permitir que um jogador sem deficiência visual experimente, em VR, a sensação de ter **catarata** (visão embaçada, perda de contraste, ofuscamento) enquanto cumpre a missão de **achar e apertar um botão específico** entre três PushButtons existentes na cena `SampleScene`. Um HUD invocável por botão do controle oferece minimapa e ajustes de acessibilidade, representando as "ajudas" que uma pessoa real com baixa visão poderia usar.

Propósito pedagógico: **empatia/conscientização**, não acessibilidade real para usuários com deficiência.

## 2. Escopo

**Inclui:**
- Efeito visual de catarata via URP Volume (sem shader custom).
- Slider para diminuir intensidade do efeito (comparação "com/sem aids").
- HUD flutuante invocado por botão Y do controle esquerdo, com minimapa e status da missão.
- Missão: 1 PushButton-alvo (configurável no Inspector) entre os 3 existentes.
- Compatibilidade com Quest standalone (Android) e PC VR (Standalone).

**Não inclui (YAGNI):**
- Outros tipos de baixa visão (glaucoma, DMRI).
- Áudio espacial direcional / TTS / leitor de tela.
- Persistência de configurações entre sessões.
- Localização (textos em pt-BR fixo).
- Tutorial inicial "sem catarata".

## 3. Arquitetura

Quatro subsistemas independentes, comunicando via `UnityEvent` para evitar coupling forte:

```
┌─────────────────────┐    ┌─────────────────────┐
│ LowVisionVolume     │    │ MissionManager      │
│ URP Volume + slider │    │ alvo, vitória       │
└─────────┬───────────┘    └─────────┬───────────┘
          │ controla                 │ notifica
          │                          │
┌─────────▼───────────┐    ┌─────────▼───────────┐
│ AccessibilityHUD    │◄───┤ ButtonMission       │
│ Canvas + minimapa   │    │ em cada PushButton  │
└─────────────────────┘    └─────────────────────┘
          ▲
          │ toggle
          │
┌─────────┴───────────┐
│ HUDToggle           │
│ InputActionReference│
└─────────────────────┘
```

## 4. Componentes

### 4.1 LowVisionSettings (`Assets/Scripts/Accessibility/LowVisionSettings.cs`)
- MonoBehaviour anexado ao GameObject "LowVisionVolume".
- Referencia `Volume` (URP) e `VolumeProfile` `LowVisionProfile.asset`.
- API pública: `void SetIntensity(float t)` onde `t ∈ [0,1]`.
  - `t = 0` → efeito desligado (lerp dos parâmetros para valores neutros).
  - `t = 1` → efeito máximo (valores definidos no profile).
- Internamente faz lerp em: `DepthOfField.aperture`, `Bloom.intensity`, `ColorAdjustments.contrast`, `ColorAdjustments.saturation`, `Vignette.intensity`.

### 4.2 LowVisionProfile (`Assets/Settings/LowVisionProfile.asset`)
URP Volume Profile com:
- **Depth of Field**: modo Bokeh, Focus Distance 0.3m, Aperture 32 (máximo), Focal Length 50mm.
- **Bloom**: Intensity 1.5, Threshold 0.8, Scatter 0.7.
- **Color Adjustments**: Contrast −25, Saturation −15.
- **Vignette**: Intensity 0.2, Smoothness 0.4.

Override de todos os parâmetros checkado para serem manipuláveis em runtime.

### 4.3 AccessibilityHUD (`Assets/Prefabs/AccessibilityHUD.prefab`)
Prefab com Canvas world-space (~30 cm de largura), reusando padrão do `Spatial Panel Manipulator` existente:
- Componentes raiz: `Canvas` (Render Mode World Space), `CanvasScaler`, `GraphicRaycaster`, `TrackedDeviceGraphicRaycaster`, `XRGrabInteractable`, `LazyFollow`, `Rigidbody` (isKinematic), `BoxCollider`.
- Hierarquia visual:
  - `Background` (Image, painel semitransparente)
  - `MinimapPanel`
    - `MapBackground` (`RawImage` apontando para `Assets/Media/minimap.png`)
    - `PlayerDot` (Image azul, ~12px)
    - `TargetDot` (Image vermelha; pulsação opcional via coroutine que lerpa `localScale` entre 1.0 e 1.3, ciclo de 1s)
  - `StatusText` (TMP, exibe "Procure o botão • Distância: X m" — texto grande para acessibilidade)
  - `SettingsPanel`
    - `BlurIntensitySlider` (0–1, default 1)
    - `FontSizeSlider` (0.5–2x, default 1)
    - `ContrastToggle` (toggle alto contraste)

Estado inicial: `SetActive(false)`. Posição inicial: spawn ~50 cm à frente do XR Origin quando ativado pela primeira vez (LazyFollow ajusta).

### 4.4 MinimapTracker (`Assets/Scripts/Accessibility/MinimapTracker.cs`)
- MonoBehaviour anexado a `MinimapPanel`.
- Campos serializados:
  - `Transform player` (atribuir XR Origin main camera).
  - `Transform target` (atribuir o PushButton-alvo).
  - `Vector2 worldMin` e `Vector2 worldMax` (XZ; bounds do cenário, atribuídos manualmente).
  - `RectTransform playerDot`, `RectTransform targetDot`.
  - `RectTransform mapRect` (o `MapBackground`).
- `Update()`: converte `player.position.xz` e `target.position.xz` em `Vector2` normalizado nos bounds → `anchoredPosition` dentro de `mapRect`. Target dot tem posição fixa após primeiro frame.
- Não precisa de render texture nem segunda câmera.

### 4.5 ButtonMission (`Assets/Scripts/Accessibility/ButtonMission.cs`)
- MonoBehaviour anexado a cada um dos 3 PushButtons existentes (`PushButton`, `PushButton (1)`, `PushButton (2)` ou equivalentes).
- Campo `bool isTarget` (default false).
- Detecta press do PushButton existente (escuta o `UnityEvent` já presente no prefab; o setup manual liga o evento de press ao método `OnPressed()`).
- Em `OnPressed()`: chama `MissionManager.Instance.ReportPress(this)`.

### 4.6 MissionManager (`Assets/Scripts/Accessibility/MissionManager.cs`)
- MonoBehaviour singleton-leve na cena (campo `public static MissionManager Instance`).
- Campos serializados: referência ao `AccessibilityHUD` (para atualizar StatusText), `AudioClip winClip`, `AudioClip wrongClip`, `AudioSource source`.
- API:
  - `void ReportPress(ButtonMission btn)`:
    - Se `btn.isTarget` → toca `winClip`, atualiza StatusText para "MISSÃO CUMPRIDA!".
    - Caso contrário → toca `wrongClip`, atualiza StatusText para "Não é esse botão. Tente outro.".
- Expõe `event Action OnMissionComplete` para extensibilidade futura.

### 4.7 HUDToggle (`Assets/Scripts/Accessibility/HUDToggle.cs`)
- MonoBehaviour anexado ao XR Origin (ou ao Left Controller).
- Campo serializado `InputActionReference toggleAction` (default: bind `<XRController>{LeftHand}/secondaryButton` = botão Y nos controles Meta).
- Em `OnEnable`: subscribe `toggleAction.action.performed += OnToggle`.
- `OnToggle()`: `hud.SetActive(!hud.activeSelf)`.
- Campo serializado `GameObject hud` (referência ao prefab instanciado na cena).

## 5. Fluxo de dados

```
Início da cena
  ├─ LowVisionVolume.SetIntensity(1.0) ── catarata ligada full
  ├─ AccessibilityHUD instanciado, SetActive(false)
  ├─ MissionManager.Awake() ── seta Instance
  └─ MinimapTracker.Update() roda mesmo com HUD off (custo desprezível) ou ativa só quando HUD on

Player aperta Y
  └─ HUDToggle.OnToggle() ── hud.SetActive(true) ── LazyFollow posiciona

Player move pela cena
  └─ MinimapTracker.Update() ── playerDot.anchoredPosition atualizado

Player aperta PushButton qualquer
  ├─ PushButton.UnityEvent → ButtonMission.OnPressed()
  ├─ MissionManager.ReportPress(btn)
  │   ├─ btn.isTarget == true → winClip + UI "MISSÃO CUMPRIDA"
  │   └─ btn.isTarget == false → wrongClip + UI "Não é esse"
  └─ (opcional futuro) reset/fim de cena

Player move BlurIntensitySlider
  └─ Slider.onValueChanged → LowVisionSettings.SetIntensity(value)
```

## 6. Setup manual no Editor (passo a passo)

Lista para o usuário executar após criação dos scripts e prefab:

1. Criar GameObject vazio "LowVisionVolume" na raiz da cena.
   - Adicionar componente `Volume` (URP), atribuir `LowVisionProfile`.
   - Adicionar componente `LowVisionSettings`, atribuir referência ao Volume e VolumeProfile.
2. Adicionar GameObject vazio "MissionManager" na raiz da cena.
   - Anexar script `MissionManager`.
   - Atribuir referências de áudio (clips em `Assets/Media/`) e AudioSource.
3. Em cada PushButton (`Cenario/.../PushButton`, etc):
   - Adicionar componente `ButtonMission`.
   - Marcar `isTarget = true` em **um único** deles.
   - Wiring do `UnityEvent` do PushButton existente para chamar `ButtonMission.OnPressed`.
4. Arrastar prefab `AccessibilityHUD` para a cena (filho do XR Origin para herdar transform inicial).
   - Atribuir referências no `MinimapTracker` (player = XR Origin/Camera, target = PushButton-alvo).
   - Atribuir bounds XZ do cenário (medir nos limites visíveis do `Cenario`).
5. No XR Origin, adicionar componente `HUDToggle`.
   - Atribuir `toggleAction` (criar InputActionReference apontando para botão Y se ainda não existir nos XRI Inputs).
   - Atribuir `hud` = instância de AccessibilityHUD da cena.
6. Tirar screenshot top-down do cenário (do Editor, Scene View em Top, ortográfica, enquadrar o `Cenario`), salvar como `Assets/Media/minimap.png`.
   - Configurar a textura como Sprite (2D and UI), sem mipmaps, filter Bilinear.
   - Atribuir ao `RawImage` MapBackground.

## 7. Critérios de aceite (golden path)

Em ordem de execução manual:

1. ✅ Cena entra em Play Mode sem erros no Console.
2. ✅ Visão do player aparece imediatamente borrada/ofuscada (catarata ligada).
3. ✅ Apertar Y no controle esquerdo (ou tecla equivalente no XR Interaction Simulator) faz o HUD aparecer à frente do player.
4. ✅ Apertar Y novamente esconde o HUD.
5. ✅ Movendo o player pelo cenário, o ponto azul do minimapa se move em correspondência (sem inversão de eixo).
6. ✅ O ponto vermelho do minimapa está fixo na posição correta do botão-alvo.
7. ✅ Mover o `BlurIntensitySlider` para 0 → cena fica nítida; para 1 → volta o efeito máximo.
8. ✅ Caminhar até um PushButton-distrator e apertar → som de erro, texto "Não é esse botão".
9. ✅ Caminhar até o PushButton-alvo e apertar → som de vitória, texto "MISSÃO CUMPRIDA!".

## 8. Riscos e mitigações

| Risco | Mitigação |
|-------|-----------|
| Minimapa misaligned por bounds errados | Worldbounds editáveis no Inspector + testagem visual rápida |
| DoF do URP não funciona em Single-Pass Instanced no Quest | Testar cedo; fallback para Color Adjustments + Vignette mais agressivos |
| Botão Y não bindado nos XRI Inputs default | Criar InputAction manualmente no setup; documentado no passo 5 |
| PushButton existente não tem UnityEvent acessível | Inspeção via MCP antes; se necessário, listener de XRBaseInteractable.selectEntered |
| Performance no Quest com DoF + Bloom | Profile no headset; se cair de 72fps, reduzir DoF para alta qualidade desligada |

## 9. Estrutura de arquivos resultante

```
Assets/
├─ Scripts/
│  └─ Accessibility/
│     ├─ LowVisionSettings.cs   [novo]
│     ├─ MinimapTracker.cs       [novo]
│     ├─ ButtonMission.cs         [novo]
│     ├─ MissionManager.cs        [novo]
│     └─ HUDToggle.cs             [novo]
├─ Prefabs/
│  └─ AccessibilityHUD.prefab   [novo]
├─ Settings/
│  └─ LowVisionProfile.asset    [novo]
└─ Media/
   └─ minimap.png                [novo]
```

## 10. Próximo passo

Após aprovação desta spec, transitar para `superpowers:writing-plans` para gerar o plano de implementação detalhado por etapa.
