# F_Easy UI Mask

[English](README_en.md)

绘制矩形覆盖层或镂空引导层，支持手动区域和目标 UI 跟随。组件继承 `MaskableGraphic`，可以受上级遮罩裁剪；**不会自动裁剪自己的子 UI**。

## 环境与验证状态

- 最低兼容目标：Unity 2019.4+ / 团结引擎 1.8.4+，最低版本尚未实际导入验证。
- 依赖 `com.unity.ugui`，清单声明版本 `1.0.0`；两个插件程序集显式引用 `UnityEngine.UI`。
- 已通过 Unity 2022.3.62f3 程序集的 Runtime、Editor 与 Player 条件源码编译检查，以及隔离状态、矩形网格和归属测试。
- 本机 Editor 许可证不可用，干净导入、Scene 交互、原生生命周期、Prefab 保存和 Player 构建/运行仍未验收；源码检查不替代引擎验证。
- 本次新增 `Runtime/FEasyUIMaskTargetArea.cs` 的引擎 `.meta` 尚待对应引擎生成并提交。现有 GUID 保持不变，完整引擎分支和安装包验收尚未完成。

## 安装与分支

在 `窗口 > 包管理器 > + > 从 git URL 添加包` 中选择对应分支：

| 引擎 | 分支 | Git URL |
| --- | --- | --- |
| Unity | `unity` | `https://github.com/FiveT-53/F_EasyUIMask.git#unity` |
| 团结引擎 | `tuanjie` | `https://github.com/FiveT-53/F_EasyUIMask.git#tuanjie` |

本仓库两个引擎分支维护各自的元数据，应保留对应分支 GUID。`main` 只维护通用源码，不带 `.meta`；开发时先在 main 提交，再同步源码到引擎分支，由对应引擎生成新增资源的元数据，不重建现有资源 GUID。

手动安装时，先通过包管理器安装 Unity UI，再把对应分支复制到工程 `Assets` 下。UPM 安装会按清单请求 UI 依赖。上述安装路径仍需在可用引擎中完成干净导入验证。

已有 `v1.0.0-unity` / `v1.0.0-tuanjie` 标签属于旧版本，不包含本次 Unreleased 修复；本轮未创建新标签或发布版本。

## 快速开始

1. 在 Canvas 下新建 UI GameObject，添加 `F_Easy UI Mask`，设置外框 RectTransform、颜色和 `raycastTarget`。
2. Manual：配置相对遮罩 Pivot 的 `targetOffset` 与本地 `targetSize`；在 Scene 中点击 `Edit Area` 调整。
3. UITarget：设置 `targetUI`。两端 Camera / World Space 根 Canvas 都必须设置 `worldCamera`；Overlay 使用空摄像机。
4. `invertMask=false` 覆盖交集内部；`true` 覆盖本体外框中洞口以外的部分。
5. 播放中激活且启用的组件会在后续 `LateUpdate` 尝试首次刷新，即使实时更新关闭。需要初始隐藏时将对象设为 inactive。

把下面控制器放在**遮罩层级之外的独立激活对象**上，并通过序列化引用赋值。这样 `Close()` 不会停用控制器。

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
        mask.Init(); // Optional; does not show or hide the object.
        mask.areaSelectMode = FEasyUIMask.AreaSelectMode.UITarget;
        mask.targetUI = target;
        mask.invertMask = true;
        mask.RefreshMask();
    }

    public void Hide()
    {
        if (mask != null) mask.Close();
    }
}
```

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

## 许可证

MIT，见 [LICENSE](LICENSE)。
