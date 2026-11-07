# F_Easy UI Mask

[English](README_en.md)

用于 Unity UGUI 的覆盖层遮罩插件，适合新手引导、区域高亮和 UI 点击拦截。本分支提供矩形与单 Image 精确轮廓两个组件。

- **反向遮罩**：目标区域透明，外围覆盖，适合突出按钮或面板。
- **正向遮罩**：覆盖目标区域，适合局部遮挡。
- 支持 Scene 视图拖动编辑、运行时目标跟随，以及按遮罩区域过滤点击。

> 本插件绘制覆盖层，不会像 Unity 的 `Mask` / `RectMask2D` 一样自动裁剪子 UI。

本开发分支包含尚未发布的 **P1 双组件实现**，包版本仍为 2.0.0。已在 Unity 2022.3.62f3c1 / Built-in / D3D11 的独立源码副本工程完成编译、代表性 Player 自动回归、精度/资源专项及迁移与预览检查；本地 UPM 导入受包解析器故障阻塞，完整验收尚未结束。详见[验证范围](Documentation~/Release.md)。下方固定标签安装的是已发布矩形版，不包含本分支的新组件。

| 组件 | 选区与形状 |
| --- | --- |
| `FEasyUIRectMask` | 2.0 矩形组件，保留 Manual、UITarget 和 Scene 手柄 |
| `FEasyUIMask` | 只绑定单个标准 Simple Image，提取实际网格、UV 和纹理 Alpha，包含孔洞 |

精确遮罩添加 **F_Easy UI Mask**，绑定 **Target UI**，勾选 **Invert Mask** 即形成洞口，无需区域模式。默认材质通过脚本导入器引用包内资源，缺失时可在 Inspector 的 **Package Resources → Restore Package Resources** 恢复。选中的活动对象使用异步 GPU 精确预览，Inspector 重建时保留已有快照，不激活禁用对象；目标祖先的 Mask/RectMask2D 可见裁剪不参与提取。

Alpha >= `alphaThreshold`（默认 0.5）定义二值轮廓，忽略目标颜色和 CanvasGroup Alpha，不要求源纹理 Read/Write。P1 拒绝 Sliced、Filled、Tiled、分离 Alpha、自定义材质/网格效果及非 Image 目标。显示与 CPU 点击共同提交；首份异步数据未就绪前为空，普通目标更新等待期间保留上一份完整结果。宿主变换、外框或观察投影变化会清空旧快照；代表性在途变化已通过检查，但任意同帧脚本/布局时序尚未穷尽，当前支持声明仍限于稳定宿主与观察投影。

`maxTextureSize` 默认 2048，有效上限 16～2048，精度受屏幕有效纹素大小限制；Inspector 显示已提交的纹素尺寸。最大双槽 RGBA32 GPU/CPU 数据约 64 MiB，另有网格、材质及驱动开销。本次 P1 回读中位数约 16.4 ms，最大约 104.76 ms，不承诺固定帧数。运行时新增组件须以序列化材质引用调用 `ConfigureResources(extraction, coverage)`，再设置目标并刷新，详见[行为参考](Documentation~/Reference.md)。

旧矩形脚本 GUID 随 `FEasyUIRectMask` 保留；旧 C# 类型、GetComponent、继承和反射名称需按用途迁移。旧类型名现在表示精确组件，部分代码可能仍编译但语义已变。场景/Prefab/Variant、owner 与 UnityEvent 的代表性迁移夹具已通过，不执行业务场景自动转换。以下矩形用法在本分支对应 **F_Easy UI Rect Mask / FEasyUIRectMask**。

## 环境要求

- Unity 2022.3 LTS；P1 源码副本验证版本为 **2022.3.62f3c1**，详细范围见发布维护说明。
- 使用 UGUI；`com.unity.ugui` 依赖会自动安装。
- 团结引擎和其他 Unity 版本尚未验证。

团结引擎的兼容性验证与适配计划在后续版本中推进，当前版本暂不承诺支持。完成验证后，会在 README 和更新日志中公布支持的团结版本；目前没有确定的发布时间。

## 安装

确保电脑已安装 Git，且 `git --version` 可用。新安装 Git 后，请重启 Unity Hub 和编辑器。

在 **Window → Package Manager → ＋ → Add package from git URL** 中输入：

```text
https://github.com/FiveT-53/F_EasyUIMask.git#v2.0.0
```

等待编译完成即可使用，无需下载源码或复制到 Assets。

后续仅维护 `main`，通过正式版本标签发布完整 UPM 包。`unity`、`tuanjie` 分支已删除，`#unity`、`#tuanjie` 安装链接停止使用；新安装请使用上述正式版本标签。旧版本由 `v1.0.0*` 标签及对应 GitHub Release 保留，旧开发快照由 `archive/unity-final`、`archive/tuanjie-final` 标签保留；归档标签不是正式发行版本。

如果工程中已有手动复制的插件，请先备份并移除那份插件，避免重复类型或程序集。升级时，在 Package Manager 中添加新版本的 Git URL 即可；固定版本不会自动更新。

## 快速上手：为按钮添加引导遮罩

1. 在 Canvas 下创建一个 UI Image，命名为 `GuideMask`，将 RectTransform 拉伸到希望覆盖的范围。
2. 删除它的 **Image 组件**，添加 **F_Easy UI Rect Mask** 组件。将 `GuideMask` 放在同一 Canvas 子节点列表的最后，使其显示在目标 UI 上方。
3. 将 **Color** 设为半透明黑色，例如 Alpha 为 `0.65`。
4. 将 **Area Select Mode** 设为 `UITarget`，把需要突出显示的按钮 RectTransform 拖到 **Target UI**。
5. 勾选 **Invert Mask**，形成“中间透明、外围变暗”的效果。目标会移动时，勾选 **Real Time Update**。
6. 进入 Play 查看效果。开启 **Raycast Target** 时，洞口允许点击，遮罩覆盖区域会阻挡点击。

Canvas 需要有 GraphicRaycaster，场景中需要 EventSystem，按钮才能正常接收点击。不要把目标按钮放在 `GuideMask` 下，以免关闭遮罩时也关闭按钮。

使用 Screen Space - Camera 或 World Space Canvas 时，请为遮罩和目标所属的根 Canvas 设置正确的摄像机。

### 手动指定区域

将 **Area Select Mode** 改为 `Manual`，通过 **Target Offset** 和 **Target Size** 设置区域。在 Scene 视图中点击 Inspector 的 **Edit Area**，可拖动中心和边缘调整；按 Escape 退出编辑。

`Target Offset` 以遮罩自身的 Pivot 为原点。编辑模式移动目标后，可选中遮罩并点击 **Refresh Preview** 更新预览。

## 常用设置

| 设置 | 用途 |
| --- | --- |
| `areaSelectMode` | `Manual` 手动设置；`UITarget` 使用目标 UI 的矩形范围 |
| `targetUI` | UITarget 模式下要跟随的 RectTransform |
| `targetOffset` / `targetSize` | Manual 模式的区域中心偏移与宽高，使用遮罩本地坐标 |
| `invertMask` | 开启：外围覆盖、内部透明；关闭：覆盖内部 |
| `realTimeUpdate` | 运行时持续跟随目标或参数变化 |
| `color` | 覆盖层颜色与透明度 |
| `raycastTarget` | 是否阻挡覆盖区域的 UI 点击；与颜色透明度独立 |

遮罩只在自身 RectTransform 范围内生效。目标超出这个范围的部分不会被覆盖。UITarget 使用矩形包围区域，因此旋转后的目标周围可能留有透明空角，不按图片的透明像素裁剪。

## 脚本控制

把下面脚本保存为 `MaskExample.cs`，挂到遮罩层级之外的独立对象，并在 Inspector 中绑定 `mask`。调用 `ShowFor(target)` 显示引导，调用 `Hide()` 关闭。

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIRectMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
        mask.areaSelectMode = FEasyUIRectMask.AreaSelectMode.UITarget;
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

- 关闭实时更新时，修改参数或移动目标后调用 `RefreshMask()`，让显示与点击区域一起更新。
- `RefreshMask()` 可以激活遮罩自身，但不会激活它的父对象，也不会启用被禁用的组件。
- `Close()` 会停用遮罩整个 GameObject，包括其子对象。需要初始隐藏时，将该对象设为 inactive。
- 目标缺失时遮罩会清空显示和点击阻挡；恢复引用后，开启实时更新可自动恢复，否则需要再次刷新。

完整 API、坐标与摄像机限制、编辑预览和 1.x 迁移说明见[行为与 API 参考](Documentation~/Reference.md)。

## 常见问题

- **找不到 Git 或下载失败**：检查 Git 是否在 PATH 中，以及当前网络能否访问 GitHub；重启 Hub 和编辑器后重试。
- **类型或程序集重复**：检查 Assets、本地包和嵌入包中是否存在另一份插件。
- **遮罩不显示**：检查对象及父对象是否激活、组件是否启用、颜色 Alpha 是否大于零，以及目标引用和 Canvas 摄像机是否正确。
- **洞口不跟随目标**：开启 Real Time Update，或在修改完成后调用 `RefreshMask()`。
- **洞口内仍无法点击**：检查 EventSystem、Canvas 的 GraphicRaycaster，以及目标前方是否还有其他开启 Raycast Target 的 UI。

反馈问题时，请附上编辑器版本、插件版本、复现步骤和 Console 的第一条完整错误。

## 许可证

[MIT](LICENSE)。版本更新见 [CHANGELOG](CHANGELOG.md)；参与维护请参阅[发布维护说明](Documentation~/Release.md)。
