# AccessibillityVR

Simulador VR de empatia/conscientização sobre **baixa visão (catarata)**. O jogador, sem deficiência visual, entra em uma cidade e precisa encontrar e apertar três botões na ordem correta enquanto enxerga o mundo embaçado e ofuscado — uma aproximação visual do que é viver com catarata.

O objetivo é **educacional**: dar ao usuário a sensação real (em primeira pessoa) do esforço extra que uma pessoa com baixa visão faz para tarefas do dia a dia, como atravessar uma rua e localizar elementos pequenos no ambiente.

> Projeto desenvolvido como demonstração técnica em Unity URP + XR Interaction Toolkit 3.x. Roda em PC (Linux/Windows/macOS) usando o XR Interaction Simulator para teste sem headset, e tem setup preservado para Meta Quest (Android).

---

## Sumário

- [Experiência](#experiência)
- [Controles](#controles)
- [Stack técnica](#stack-técnica)
- [Como rodar](#como-rodar)
- [Arquitetura](#arquitetura)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Limitações conhecidas](#limitações-conhecidas)
- [Documentação adicional](#documentação-adicional)

---

## Experiência

1. **Menu inicial** aparece à frente do jogador (nítido, sem catarata) com as opções *Começar Jogo* ou *Sair*.
2. Ao começar, o jogador é teleportado para **a frente da cabine telefônica** da cidade, virado para a rua.
3. O **filtro de catarata** é ativado: a cena fica embaçada (Depth of Field forte), com bloom ofuscante perto de fontes de luz, contraste e saturação reduzidos e uma vinheta sutil.
4. O jogador precisa **encontrar e apertar 3 PushButtons na ordem correta** (1 → 2 → 3). Cada botão tem um número amarelo pequeno acima dele, **propositalmente difícil de ler à distância** por causa do blur — força o jogador a se aproximar.
5. Carros passam pela rua. **Aproximar-se de um carro (~1,2 m)** dispara a tela *Você foi atropelado* — recomeça a cena.
6. Apertar os botões na ordem certa → **tela de vitória**.
7. Apertar fora de ordem → mensagem *Ordem errada! Recomeçando do botão 1*.

### HUD de acessibilidade
A tecla **M** (ou botão Y do controle esquerdo em VR) abre/fecha um **painel flutuante** que aparece à frente do jogador, **sempre nítido** (renderizado por uma overlay camera sem post-process). Contém:

- **Minimapa** mostrando o jogador (azul) e o próximo botão alvo (vermelho).
- **Slider de intensidade da catarata** (0 a 1) — permite o jogador comparar com e sem o efeito a qualquer momento.
- **Status da missão** (qual passo da sequência está esperando).

A ideia educacional: o HUD representa as *ajudas de acessibilidade* que uma pessoa com baixa visão usaria no mundo real (aplicativo de mapa, leitor de tela, etc.). O contraste entre olhar para o mundo borrado e olhar para o HUD nítido é o ponto pedagógico.

---

## Controles

### Editor (XR Interaction Simulator)
| Ação | Tecla / Botão |
|---|---|
| Andar | W / A / S / D |
| Olhar | Mouse |
| Clicar botão da cena | Clique esquerdo do mouse |
| Abrir/fechar HUD | **M** |
| Q / E (subir/descer) | **Desativados** intencionalmente |

### VR (Quest e similares)
| Ação | Controle |
|---|---|
| Andar | Stick analógico esquerdo |
| Virar | Stick analógico direito |
| Selecionar botão | Trigger |
| Abrir/fechar HUD | **Y** (controle esquerdo) |

---

## Stack técnica

- **Unity** 2022.3 LTS
- **Universal Render Pipeline (URP)** 14 — para o efeito de catarata via Volume System (Depth of Field, Bloom, Color Adjustments, Vignette)
- **XR Interaction Toolkit** 3.1.2 — interações VR, Near-Far Interactor, simulator
- **OpenXR Plugin** 1.14 — runtime VR (Android/Quest e Windows/macOS Standalone)
- **Input System** 1.14 — bindings de teclado, mouse e controles VR
- **TextMeshPro** 3.0.9 — UI

---

## Como rodar

### Pré-requisitos
- Unity Hub + Unity Editor **2022.3.62f1** (ou versão LTS próxima)
- ~10 GB livres

### Editor (sem headset)
1. Abra o projeto no Unity Hub.
2. Abra a cena `Assets/Scenes/SampleScene.unity`.
3. **Importante na primeira vez:** rode o menu `Tools/Accessibility/Fix URP PostProcessData`. Isso habilita o post-processing nos renderers URP — sem isso a catarata é invisível.
4. Aperte **Play**. A tela do menu inicial deve aparecer nítida; clique em *Começar Jogo*.
5. Use mouse + WASD para andar. Aperte **M** para abrir o HUD. Clique nos botões da cena na ordem 1 → 2 → 3.

### Linux específico
OpenXR não tem runtime para Linux x64. O projeto já vem com a inicialização automática do OpenXR **desativada para Standalone** (`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`), então o XR Interaction Simulator funciona sozinho sem erros.

### Build para Quest
1. Conecte o Quest via USB e habilite *Developer Mode*.
2. *File → Build Settings → Android* → Switch Platform.
3. *Build And Run*. O setup XR para Android (OpenXR + Oculus Loader) já está pronto.

---

## Arquitetura

Todo o comportamento da feature é construído procedualmente em runtime por um **único componente bootstrap** adicionado à raiz `_AccessibilityRoot` da cena. Não há prefabs nem wirings manuais no Editor — só adicionar o `AccessibilityBootstrap` em um GameObject vazio e ele monta o resto.

Decisão de design: priorizar **reprodutibilidade e velocidade de iteração** sobre flexibilidade de editor. O bootstrap encapsula toda a complexidade.

### Fluxo no Awake do bootstrap
```
Awake()
├─ EnsureLowVisionVolume        → URP Volume + LowVisionSettings
├─ EnableCameraPostProcessing   → renderPostProcessing=true em todas as câmeras
├─ DisableTemplateClutter       → desativa UI do VR Template, interactables demo, etc.
├─ DisableSimulatorYTranslate   → zera translateYSpeed (Q/E inertes)
├─ AddSceneColliders            → MeshCollider em casas, ruas, props que só tinham renderer
├─ ConstrainPlayer              → PlayerConstraints anexado no XR Origin (rig)
├─ RepositionPlayerNearBooth    → teleporta rig para 2 m da Telephone Booth
├─ SetupCars                    → CarHitDetector em cada CarMovement
├─ BuildHUD                     → Canvas world-space com minimapa, sliders, status
├─ SetupPushButtons             → BoxCollider + XRSimpleInteractable + ButtonMission
│                                 (+ label numérico amarelo bem pequeno acima)
├─ MissionManager               → singleton com lógica de sequência ordenada
├─ HUDToggle                    → tecla M / botão Y
├─ GameFlowUI                   → menus start/victory/game over
├─ MouseClickToButton           → fallback de clique no editor
└─ SetupHUDOverlay              → overlay camera UI-only sem post-process
```

### Componentes principais

| Script | Responsabilidade |
|---|---|
| `AccessibilityBootstrap` | Ponto único de entrada — constrói e wireia tudo |
| `LowVisionSettings` | Gera VolumeProfile em runtime; expõe `SetIntensity(0..1)` |
| `MinimapTracker` | Converte posição XZ do mundo para `anchoredPosition` no minimapa |
| `ButtonMission` | Self-subscribe ao `XRBaseInteractable.selectEntered`; campo `order` |
| `MissionManager` | Singleton com sequência esperada, eventos `OnStepAdvanced` e `OnMissionComplete` |
| `HUDToggle` | InputAction self-criada; posiciona HUD à frente da câmera ao abrir |
| `GameFlowUI` | Constrói menus inicial / vitória / game over; subscribe a `OnMissionComplete` e `OnPlayerHit` |
| `CarHitDetector` | Checa distância XZ entre carro e câmera; evento estático `OnPlayerHit` |
| `PlayerConstraints` | Wall collision via OverlapSphere; reverte rig em colisão *(parcial — ver limitações)* |
| `MouseClickToButton` | Fallback editor: raycast pelo cursor para disparar `ButtonMission.OnPressed` |
| `BillboardToCamera` | Mantém labels world-space sempre virados para o jogador |
| `AccessibilityEditorFix` | Menu *Tools/Accessibility/Fix URP PostProcessData* — patch dos URP renderers |

### Comunicação entre componentes
Eventos C# / `UnityEvent` evitam coupling forte:

- `MissionManager.OnStepAdvanced` (int) → `MinimapTracker` troca alvo
- `MissionManager.OnMissionComplete` → `GameFlowUI` mostra tela de vitória
- `CarHitDetector.OnPlayerHit` (estático) → `GameFlowUI` mostra tela de game over
- `Slider.onValueChanged` → `LowVisionSettings.SetIntensity`

### HUD nítido sobre mundo embaçado
Uma overlay camera (filha da Main Camera) renderiza apenas a layer `UI` com `renderPostProcessing = false`. A Main Camera exclui a layer `UI` do seu `cullingMask`. O bootstrap coloca o HUD e os menus na layer `UI`. Os números acima dos botões ficam na layer `Default` para serem propositalmente afetados pelo blur.

---

## Estrutura de pastas

```
Assets/
├─ Scenes/
│  └─ SampleScene.unity              # cena principal — adicionar AccessibilityBootstrap aqui
├─ Scripts/
│  └─ Accessibility/                 # toda a feature
│     ├─ AccessibilityBootstrap.cs
│     ├─ LowVisionSettings.cs
│     ├─ MinimapTracker.cs
│     ├─ ButtonMission.cs
│     ├─ MissionManager.cs
│     ├─ HUDToggle.cs
│     ├─ GameFlowUI.cs
│     ├─ CarHitDetector.cs
│     ├─ PlayerConstraints.cs
│     ├─ MouseClickToButton.cs
│     └─ BillboardToCamera.cs
├─ Editor/
│  └─ AccessibilityEditorFix.cs      # menu Tools/Accessibility/Fix URP PostProcessData
├─ Cenario, City 03, Samples, VRTemplateAssets, XR, XRI…   # assets do VR Template + cenário

docs/superpowers/
├─ specs/
│  └─ 2026-05-24-baixa-visao-hud-design.md   # spec de design
└─ plans/
   └─ 2026-05-24-baixa-visao-hud.md          # plano de implementação
```

---

## Limitações conhecidas

- **Wall collision do jogador é parcial.** O `PlayerConstraints` reverte a posição do rig se ele intercepta um collider, mas o XR Interaction Simulator pode mover a câmera de uma forma que ignora o constraint em alguns cenários. Funciona razoavelmente para o demo mas não é à prova de tudo.
- **Linux Standalone não roda OpenXR.** Por isso a inicialização automática está desativada na build target Standalone. O XR Simulator é a única forma de testar no editor Linux. Builds para Windows/macOS funcionam com OpenXR normalmente.
- **Não testado em headset real.** A arquitetura está preparada para Quest (XRI + OpenXR Android intactos), mas a feature foi validada apenas pelo XR Simulator em editor Linux.
- **Sem persistência.** Configurações do slider de blur, recorde, etc., não são salvos entre sessões.
- **Sem áudio espacial para navegação.** Uma pessoa com baixa visão real se apoiaria em pistas auditivas; isso ficou fora do escopo.

---

## Documentação adicional

- **Spec de design:** `docs/superpowers/specs/2026-05-24-baixa-visao-hud-design.md`
- **Plano de implementação:** `docs/superpowers/plans/2026-05-24-baixa-visao-hud.md`
- **PR principal:** [#1](https://github.com/FelipeTagliabues/AccessibillityVR/pull/1)

---

## Licença

Sem licença explícita. Os assets do XRI Starter Assets e VR Template seguem suas próprias licenças (Unity Companion License).
