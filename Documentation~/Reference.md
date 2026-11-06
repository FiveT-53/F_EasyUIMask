# 行为与 API 参考 / Behavior and API reference

[中文入门](../README.md) · [English quick start](../README_en.md)

## P0 精确遮罩实验 / Precision experiment

仅本开发分支提供技术验证入口；包版本仍为 2.0.0，以下配置不是稳定公共 API。`p0PrecisionExperiment=false` 保留原矩形路径。启用时要求 `UITarget`、单个标准 Image（Simple；Sliced/Filled 仅架构探针）、Sprite 和两个显式引用的实验材质。通过 `Tools/F_Easy UI Mask/P0/Configure Selected Mask` 在使用工程内创建材质资产并分配，随场景引用进入 Player；不依赖运行时 Shader.Find。

`p0AlphaThreshold` 默认 0.5，取纹理 Alpha >= 阈值，忽略目标 Image/顶点/CanvasGroup 透明度。GPU 从 CanvasRenderer 的实际网格/UV 提取二值蒙版，支持不可读源图；同一 RT 的异步回读数据用于 CPU 点击。首份回读前无显示/命中，等待期间保留上份完整状态；无效配置清空，不关闭宿主。禁用目标、全透明、Filled=0 或完全越界是有效空，反向覆盖整个外框。射线参与与遮罩颜色 Alpha 独立。

`RefreshMask()` 在此实验中排队，于 LateUpdate 合并、更新 Canvas 后取网格；刷新期间请求保留。实时关闭后普通变化等待显式刷新或结构请求。正常刷新不同步回读，每次点击只查已提交字节；关闭/销毁后旧回调不能复活结果。域重载和进程退出清理时可等待该实例最后一次 GPU 请求。

默认屏幕投影局部 RT，线性 RGBA32、Point/Clamp、无 mipmap/MSAA、一圈有效纹素保护，上限 `p0MaxTextureSize`（最大 2048）。未降采样时，区域边界对齐屏幕像素网格，一个 RT 纹素对应一个屏幕像素；超过尺寸上限时，两轴采用共同的有效纹素尺寸，区域、保护和 RT 尺寸一起计算。误差仍受纹素大小限制；`p0TargetLocalProbe` 是以目标局部单位栅格化的对照探针，不保证屏幕等精度。`p0*` 非序列化计数/耗时字段仅为诊断。最多保留活动、在途/备用两份常规 RT 与对应 CPU 数据；改变尺寸可能重新分配。

编辑模式与 Prefab Stage 不创建实验 GPU 预览，仍走原矩形预览。暂不支持 Tiled、分离 Alpha、自定义材质/网格效果、目标父级裁剪的可见轮廓、多目标、子节点组合或边距/羽化；不扩大原摄像机投影支持范围。目标父级可见裁剪未参与提取，不能将提取结果解释为裁剪后的完整最终形状。临时改变遮罩自身外框/投影时，UGUI 射线粗筛仍使用实时 RectTransform；等待期间的粗筛边界须进一步验证。

This branch-only probe is opt-in and has no stable precision API. Configure material assets through the P0 editor menu and use one standard Image in UITarget mode. Texture Alpha at or above the threshold defines the shape; target tint and CanvasGroup Alpha are ignored. Rendering and CPU hits commit together after asynchronous GPU readback. First readiness is empty; pending work keeps the previous snapshot. Invalid input clears both outputs without closing the host; a disabled/transparent/zero-fill/offscreen target is valid empty. Click queries do not read back the GPU.

The screen-region RGBA32 mask uses Point/Clamp sampling with a texel guard and a 2048 cap. Without downsampling, region bounds align to the screen pixel grid with one RT texel per screen pixel. At the size cap, both axes share a texel pitch, and bounds, guards and dimensions are calculated together; precision remains limited by the effective texel size. A target-local comparison probe and diagnostic counters are experimental. No GPU preview is allocated in Edit Mode or Prefab Stage. Tiled, split Alpha, custom materials/effects, target ancestor visibility clipping, multiple targets and edge effects remain outside scope. Ancestor clipping on the target is not reconstructed. Existing projection restrictions still apply. UGUI's live RectTransform raycast broad phase during pending outer/projection changes requires further validation.

## 区域与交互规则

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

## Geometry and interaction

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
