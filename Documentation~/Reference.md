# 行为与 API 参考 / Behavior and API reference

[中文入门](../README.md) · [English quick start](../README_en.md)

## P1 精确组件 FEasyUIMask / Precision component

本分支未发布，包版本仍为 2.0.0。实现后已执行独立源码副本的编译、代表性 Player、精度/资源、迁移和精确预览检查；完整验收未结束，UPM 导入受环境故障阻塞。以下行为的实际通过范围以 [Release](Release.md) 为准，不扩大到未测平台和用例。矩形组件说明见下方 `FEasyUIRectMask`。

This unreleased implementation has source-copy compilation, representative Player, precision/resource, migration and preview results. Full acceptance is incomplete and UPM import is blocked. See [Release](Release.md) for verified scope. Version remains 2.0.0; rectangle behavior belongs to `FEasyUIRectMask`.

### 目标与形状 / Target and shape

- `targetUI` 必须是自身带有标准 `UnityEngine.UI.Image` 的 RectTransform，Image 类型必须为 Simple，且 Sprite 存在。不接受派生 Image、自定义材质或 IMeshModifier；Sliced、Filled、Tiled、RawImage、TMP、分离 Alpha、多目标及子节点组合不在 P1 范围内。
- 使用 Canvas 重建后的实际网格与 UV0，保留透视插值，支持旋转、镜像、非等比缩放、Preserve Aspect、Sprite Mesh、凹形和内部孔洞的提取设计。不可读源纹理通过 GPU 采样，无需读取源纹理 CPU 数据。
- 纹理采样 Alpha >= `alphaThreshold` 定义二值形状。遵循源纹理的采样设置；忽略 Image 颜色、顶点 Alpha 和 CanvasGroup Alpha。更换 Sprite/图集绑定后使用当次实际纹理和 UV。
- 目标祖先 Mask/RectMask2D 的可见裁剪不参与提取，不能称为完整屏幕最终可见形状。遮罩自身 Shader 保留 UGUI Stencil、矩形裁剪、softness 和 AlphaClip 路径，实际效果待 P1 验收。视觉 softness 不改变标准 UGUI 祖先射线过滤规则。
- 正向覆盖形状，反向覆盖外框内形状以外的部分；遮罩外框以外始终无显示和命中。颜色透明度与 `raycastTarget` 独立，洞口放行仅表示本遮罩不拦截，不保证下层收到事件。

Only one standard Simple Image is accepted. The actual rebuilt mesh, UV and sampled texture Alpha define the binary contour, including holes. Tint, vertex/CanvasGroup Alpha and target ancestor visibility clipping are ignored. Source Read/Write is unnecessary. Unsupported inputs invalidate rather than fall back to a rectangle. Representative own Stencil, nested Stencil, RectMask2D and softness paths passed P1 checks; this does not certify arbitrary nesting or custom masks.

### 公共 API / Public API

| 字段或属性 / Member | 默认或含义 / Default or meaning |
| --- | --- |
| `targetUI: RectTransform` | null；绑定目标自身 / target itself |
| `realTimeUpdate: bool` | false；启用时检查目标状态、投影及脏通知 / observe target changes |
| `invertMask: bool` | false；true 为外围覆盖 / cover the outside |
| `alphaThreshold: float` | 0.5；有效范围 [0.001, 1] / valid threshold range |
| `maxTextureSize: int` | 2048；有效上限钳制到 16～min(2048, 设备上限) / clamped size cap |
| `FailureReason: string` | 最近失败原因，只读 / last failure, read-only |
| `IsReady: bool` | 存在完整已提交快照 / a complete snapshot is committed |
| `IsPreparing: bool` | 有在途回读，可能同时已就绪 / a readback is in flight |
| `EffectiveTexelSize: Vector2` | 已提交纹素的屏幕像素尺寸，未就绪为零 / screen pixels per committed texel |

| 方法 / Method | 契约 / Contract |
| --- | --- |
| `Init()` | 幂等准备活动播放实例的私有状态，不激活或关闭宿主，不要求已有 Canvas / prepare an active playing instance without activation |
| `ConfigureResources(Material extraction, Material coverage)` | 分配正式包材质模板，撤销旧资源并请求重采样，不修改原材质 / configure templates and request resampling |
| `RefreshMask()` | 排队并重新采样同引用纹理，可激活自身，不激活父节点或启用组件 / queue a fresh sample, activating only this object if needed |
| `Close()` | 清空结果并停用整个宿主，保留全部配置 / clear state and deactivate the host, retaining configuration |

没有 `areaSelectMode`、`targetOffset`、`targetSize` 或 `p0*` 字段。不保留实验开关、局部对照探针和同名矩形别名。

### 提交、失效与刷新 / Commit and refresh

首份异步回读完成前不显示、不命中。GPU RT、CPU RGBA32 数据、模式、外框和投影一起提交。普通目标变化等待期间保留上一份完整结果；连续变化按生成顺序完成，不因每帧都有新变换而永远丢弃全部结果。换目标/身份、关闭、失效、销毁或重载使过期回调失去提交资格，过期错误也不能覆盖新状态。

宿主 RectTransform、外框、根 Canvas、观察摄像机/viewport/投影/裁剪范围等结构变化触发旧结果清空并重新准备，不继续保留映射失效的快照。代码在结构回调、LateUpdate、Canvas 绘制前和精确射线查询时检查宿主；代表性在途宿主/摄像机变化已通过同帧失效与恢复后画面对照，任意脚本/布局时序尚未穷尽，**当前支持声明限于稳定宿主和观察投影**。

缺少目标、Sprite、正式资源、不支持配置、无效变换或投影：显示和点击均为空，保留宿主与配置，通过 `FailureReason` 和 Inspector/运行时警告暴露错误。禁用目标、全透明图和完全越界是有效空形状：正向空、反向整个外框。无效配置不会反向覆盖整个外框。

显式刷新与实时变化请求在 LateUpdate 合并，Canvas 更新期间新增请求保留。实时关闭后不轮询目标层级；字段赋值和纹理内容变化须显式刷新，宿主结构检查仍运行。实时开启时比较必要的实际状态值，订阅网格/材质脏通知与图集注册；同步纹理 updateCount，但外部原生纹理写入等无法检测的内容变更仍应主动刷新。可恢复的资源/网格/回读失败在实时开启时限频重试，修复无效配置后按变化或显式刷新恢复。

First readiness is empty. Rendering and CPU hits commit together; pending target changes retain the previous complete snapshot. Epochs reject stale identity/lifecycle/error callbacks. Host mapping changes invalidate immediately at the implemented observation points. Representative in-flight host/camera mutations passed same-frame invalidation and recovered rendering checks. Arbitrary script/layout ordering is unverified, so the declared scope still requires a stable host/observer. Disabled, transparent and offscreen targets are valid empty; invalid input clears both outputs even when inverted. Explicit refresh always resamples, including unchanged texture references. Real-time mode combines exact state comparison with dirty/atlas notifications; real-time off does not poll the target hierarchy.

### 资源、精度与延迟 / Resources, precision and delay

包内 `PrecisionAlphaExtraction.mat`、`PrecisionCoverage.mat` 序列化引用 Shader；脚本 importer 的 defaultReferences 为新增编辑器组件赋值。Inspector 的资源恢复按钮通过序列化事务修改引用。运行时创建组件必须自行持有序列化资源引用并初始化，例如：

```csharp
[SerializeField] private Material extraction; // PrecisionAlphaExtraction.mat
[SerializeField] private Material coverage;   // PrecisionCoverage.mat

void Configure(F_EasyUIMask.FEasyUIMask mask, RectTransform image)
{
    mask.ConfigureResources(extraction, coverage);
    mask.targetUI = image;
    mask.invertMask = true;
    mask.RefreshMask();
}
```

不依赖 Shader.Find、Resources 隐式加载或 P0 工具菜单。每实例只修改自己生成的 Material、Mesh、RT 和字节数组，不改目标层级、共享材质或原图。关闭立即撤销提交资格，在途提取资源保留到 GPU 回调结束；域重载/退出可等待该实例 GPU 队列排空。普通刷新和每次点击均不等待 GPU。

屏幕区域 RT 使用线性 RGBA32、Point/Clamp、无 mipmap/MSAA。未降采样时边界对齐屏幕像素网格；尺寸触顶后共同调整两轴纹素步长、区域、尺寸和保护边。CPU 仅查询同提交纹素的 R 通道，边界受离散采样限制，不承诺无限精度。继续使用 RGBA32，尚未引入未经回归的 R8/单通道优化；2048² 双槽 GPU/CPU 数据合计约 64 MiB，不含其他开销。本次 P1 回读中位数约 16.4 ms、最大约 104.76 ms；不承诺固定帧数或零延迟。

精确预览仅对当前单选的活动编辑对象创建独立临时资源，包含 Prefab Stage；不激活禁用对象或跨 Stage 引用，不创建矩形替代预览。预览会话归属于遮罩对象，Inspector 临时销毁或重新登记不清空已有快照、不强制重采样。编辑对象以根 Canvas 的像素矩形（Overlay）或观察摄像机视口作为尺寸依据，避免窗口/菜单上下文中的全局 Screen 尺寸变化误使结果失效；真实宿主和投影变化仍触发检查。取消选择、Stage 变化、禁用、销毁、重载及退出清理。OnValidate 只提交线程安全请求，主线程队列再执行预览与 Graphic 失效操作。临时资源不序列化到场景/Prefab。

Runtime-created masks must receive serialized package material references through `ConfigureResources`. Instance resources survive in-flight GPU work until completion. The linear RGBA32 screen-region mask uses Point/Clamp sampling, a guard border and a shared downsampling pitch. Two maximum-size GPU/CPU slots cost about 64 MiB. CPU hits read committed bytes only. This P1 run measured a median near 16.4 ms and maximum near 104.76 ms, without a fixed-frame guarantee. A single selected active edit/Prefab object owns its temporary GPU preview across Inspector replacements without forcing resampling. Edit-mode dimensions come from the root Canvas pixel rect or observer viewport, while actual host/projection changes still invalidate. Selection/stage/lifecycle transitions clean resources up.

### P1 组件迁移 / Component migration

旧 `Runtime/FEasyUIMask.cs` 的 GUID 随文件移动到 `FEasyUIRectMask.cs`，原矩形字段名、类型、枚举值和 helper owner 字段名保留，owner 的类型改为矩形组件。新精确组件使用与旧矩形不同的脚本 GUID，不按旧场景实验字段自动转换。从 v2.0.0 导入的场景、Prefab、Variant、对象引用、owner、UnityEvent 和显式迁移类型后的消费脚本夹具已通过 32 项检查，原始 RuntimeOnly UnityEvent 在真实 Play Mode 下另通过 9 项调用检查，不能替代任意业务工程迁移审计。

```csharp
// Old rectangle consumer -> P1 rectangle consumer
[SerializeField] private F_EasyUIMask.FEasyUIRectMask rectangle;
void ShowRectangle(RectTransform target)
{
    rectangle.areaSelectMode = F_EasyUIMask.FEasyUIRectMask.AreaSelectMode.UITarget;
    rectangle.targetUI = target;
    rectangle.RefreshMask();
}
```

审计 `FEasyUIMask` 字段、方法参数、GetComponent/泛型参数、继承、反射字符串和程序集限定类型名。共用的 targetUI/RefreshMask 等调用仍可能编译，但会指向不同形状语义。必须按用途选择组件，不宣称无损源码兼容。P0 场景随旧 GUID 成为矩形组件，需要精确功能时在测试副本中显式添加新组件并迁移目标/阈值/质量及资源，不批量改写业务资产。

The original script GUID moves with the rectangle component. Audit fields, arguments, GetComponent/generics, inheritance and reflection names. Shared member names may still compile while changing semantics. Representative v2.0.0 scene/Prefab/variant/reference/UnityEvent/owner fixtures passed after explicitly migrating consumer types; arbitrary business consumers still require an audit. P0 scenes become rectangle instances through their old script reference; precision requires explicit new components in test copies.

## 矩形组件 FEasyUIRectMask：区域与交互规则

目标原始矩形为 T，本体当前 RectTransform 外框为 O，绘制和命中共用交集 I = O ∩ T；裁剪结果不覆盖原始 T。

| 状态 | 正向覆盖/拦截 | 反向覆盖/拦截 |
| --- | --- | --- |
| 普通交集 | I | O 中除去 I 的部分 |
| 无交集或 Manual 零尺寸 | 空 | 整个 O |
| 目标覆盖整个外框 | 整个 O | 空 |
| 无效投影、缺少配置或无效外框 | 空 | 空 |

以上拦截以 `raycastTarget=true` 为前提。目标边界归入交集，反向洞口边界放行；外框外始终不拦截。关闭 `raycastTarget` 只使本 Graphic 不作为射线目标，不保证其他 UI 或子节点也能穿透。透明度与射线开关独立。

Manual 的负尺寸逐轴钳制为零，不回写公开字段；零尺寸是有效空目标。NaN、Infinity 和溢出使状态失效。UITarget 零面积或不可逆变换视为无效，区别于 Manual 有效空目标。

UITarget 将目标自身世界四角经两端根 Canvas 投影，得到遮罩局部轴对齐包围矩形。旋转、镜像和透视可能产生矩形空白区；不按透明像素、子节点或上级 Mask 的可见部分收缩。

支持同显示器的不同父节点、Canvas、CanvasScaler 和摄像机 viewport。World Space 仅按明确指定的观察摄像机对齐；不支持跨显示器、RenderTexture、立体摄像机、多观察视角同时对齐或跨近远裁剪面的目标。不承诺识别所有自定义渲染合成拓扑。Manual 本地计算不要求摄像机。

## 公共字段与方法

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `areaSelectMode` | `AreaSelectMode` | `Manual` 或 `UITarget` |
| `realTimeUpdate` | `bool` | 激活且启用时逐帧检查变化 |
| `invertMask` | `bool` | false 正向，true 反向 |
| `targetOffset` | `Vector2` | 相对遮罩 Pivot 的目标中心 |
| `targetSize` | `Vector2` | 遮罩本地单位的宽高 |
| `targetUI` | `RectTransform` | 跟随目标 |

| 方法 | 契约 |
| --- | --- |
| `Init()` | 幂等准备运行时资源，不显示或关闭，不要求已有 Canvas；通常无需主动调用 |
| `RefreshMask()` | 提交当前区域和模式，可激活自身，但不激活父节点或启用组件；强制请求顶点重建 |
| `Close()` | 失效并停用整个 GameObject，影响同物体组件和子层级；重复调用安全，不清空配置 |

实时关闭时，直接赋值字段保留上一份已提交区域与模式，直到显式刷新或结构变化触发新刷新。实时开启时仅在自定义状态变化后置脏。实际显示在正常 Canvas 重建阶段生效；不承诺函数返回前完成渲染或复杂自动布局同帧零延迟。依赖自动布局的调用方应在布局完成后刷新。

失败检测只失效本组件显示和命中，不关闭宿主。实时开启时持续尝试恢复；关闭时一次请求无论成败都会消费，等待显式刷新或启用、父节点、Canvas 结构变化的新请求。主动 Close 后不会自行重新激活。

## 编辑器与辅助对象

Inspector、手柄、重新选择及 Undo/Redo 共用主线程去重刷新队列。只有单个活动选择处于 Manual / Edit Area 时才注册编辑输入；Escape 退出，Alt/View 保持导航，暂不支持多对象编辑。

`Refresh Preview` 不激活对象或启用组件。编辑模式及 Prefab Stage 仅更新非序列化缓存，不创建辅助对象、不修改持久资产；跨 Stage 目标拒绝预览。播放场景实例走不激活宿主的运行时入口。编辑模式实时检查仅适用于当前选中遮罩；关闭实时更新后，移动目标或摄像机需手动刷新预览。不进行全场景自动预览。

运行时 `TargetArea` 由序列化 owner 标记识别，域重载后按归属恢复。同名用户对象不认领、不迁移、不删除。辅助对象被移走、添加子节点或额外组件时保留并提示；仅清理直接属于本遮罩且无用户内容的区域。显示和点击不依赖辅助对象。

## 迁移

- Awake / Init 不再自动隐藏宿主；依赖初始隐藏时将 GameObject 设为 inactive。
- 非中心 Pivot 的 Manual 偏移改为相对 Pivot。要保留旧运行时中心，使用 `newOffset = oldOffset + mask.rectTransform.rect.center`；不自动批量迁移。
- 实时关闭时，赋值后调用 RefreshMask 提交完整区域和模式。失败不再关闭整个宿主。


---

## Rectangle component FEasyUIRectMask: geometry and interaction

Let T be the original target rectangle, O the current mask RectTransform, and I = O ∩ T. Rendering and hit testing share I without overwriting T.

| State | Forward coverage / blocking | Inverted coverage / blocking |
| --- | --- | --- |
| Ordinary intersection | I | O excluding I |
| No intersection or zero-size Manual target | Empty | All O |
| Target covers the outer rect | All O | Empty |
| Invalid projection, missing configuration or unusable outer rect | Empty | Empty |

Blocking assumes `raycastTarget=true`. Target boundaries belong to the intersection and pass through in inverted mode. Points outside O never hit. Disabling `raycastTarget` removes this Graphic as a target without guaranteeing that other UI or descendants pass through. Alpha and raycast participation are independent.

Manual negative size components clamp to zero without rewriting fields; zero is a valid empty target. NaN, Infinity and overflow invalidate the state. A zero-area or non-invertible UITarget is invalid, unlike a valid empty Manual target.

UITarget projects its own four world corners through the two root Canvas configurations into a mask-local axis-aligned bounding rectangle. Rotation, mirroring and perspective can leave rectangular empty corners; transparency, children and ancestor Mask visibility do not shrink the target.

Different parents, canvases, CanvasScalers and camera viewports on the same display are supported. World Space uses the explicitly chosen observer. Cross-display projection, RenderTexture output, stereo cameras, simultaneous multi-view alignment and targets crossing camera clipping planes are unsupported. Not every custom rendering/compositing topology can be detected. Manual local calculations do not require a camera.

## Public fields and methods

| Field | Type | Meaning |
| --- | --- | --- |
| `areaSelectMode` | `AreaSelectMode` | `Manual` or `UITarget` |
| `realTimeUpdate` | `bool` | Check changes each frame while active and enabled |
| `invertMask` | `bool` | false for forward, true for inverted |
| `targetOffset` | `Vector2` | Target center relative to the mask pivot |
| `targetSize` | `Vector2` | Width and height in mask-local UI units |
| `targetUI` | `RectTransform` | Followed target |

| Method | Contract |
| --- | --- |
| `Init()` | Idempotently prepares runtime resources without showing or hiding; a Canvas is not required yet; normally optional |
| `RefreshMask()` | Commits current bounds and mode, may activate this GameObject, never activates its parent or enables the component, and forces a vertex rebuild request |
| `Close()` | Invalidates and deactivates the whole GameObject, affecting its components and descendants; repeatable without clearing configuration |

With real-time updates disabled, assignments retain the previous complete state until an explicit refresh or structural event requests another refresh. Real-time updates dirty vertices only when custom state changes. Rendering takes effect during normal Canvas rebuilding, not synchronously before the call returns. Complex layout has no same-frame zero-delay guarantee; refresh after layout completes when needed.

Detected failure clears only this component's rendering and hit state, leaving the host active. Real-time mode keeps attempting recovery. Otherwise each request is consumed regardless of success and waits for an explicit refresh or another enable/parent/Canvas hierarchy event. A deliberately closed object never reactivates itself.

## Editor and helpers

Inspector, handles, reselection and Undo/Redo share a coalesced main-thread queue. Only a single active selection in Manual / Edit Area captures editing input. Escape exits; Alt/View navigation remains available. Multi-object area editing is not supported.

`Refresh Preview` never activates objects or enables components. Edit Mode and Prefab Stage update only nonserialized caches, without creating helpers or modifying persistent assets; cross-stage targets are rejected. Playing scene instances use the runtime path without activation. Edit-mode real-time checking applies only to the selected mask. With it disabled, refresh manually after moving a target or camera. There is no automatic whole-scene preview.

Runtime `TargetArea` helpers use serialized owner markers for recovery after domain reloads. Same-name user objects are never claimed, migrated or deleted. Moved helpers or those containing user children or extra components are preserved with a warning. Cleanup touches only directly owned areas without user content. Rendering and hit testing do not depend on helpers.

## Migration

- Awake / Init no longer hide the host automatically. Initialize the GameObject as inactive when it must start hidden.
- Manual offsets now use the mask pivot. Preserve an old non-center-pivot runtime center with `newOffset = oldOffset + mask.rectTransform.rect.center`. No bulk migration occurs.
- With real-time updates disabled, call RefreshMask after assignments to commit complete bounds and mode. Configuration failure no longer deactivates the host.
