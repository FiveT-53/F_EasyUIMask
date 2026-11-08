# F_Easy UI Mask

[English](README_en.md)

用于 Unity UGUI 的覆盖层遮罩插件，适合新手引导、区域高亮和 UI 点击拦截。可以手动指定矩形区域，也可以让透明洞口跟随 UI 图片的轮廓。

- **反向遮罩**：目标区域透明，外围覆盖，适合突出按钮或面板。
- **正向遮罩**：覆盖目标区域，适合局部遮挡。
- 矩形区域支持 Scene 视图拖动编辑；两个组件均支持运行时目标跟随和遮罩区域内的点击过滤。

> 本插件绘制覆盖层，不会像 Unity 的 `Mask` / `RectMask2D` 一样自动裁剪子 UI。

## 选择组件

| 组件 | 适合的场景 |
| --- | --- |
| `FEasyUIRectMask` | 为按钮或面板留出矩形区域，或手动指定洞口的位置和大小 |
| `FEasyUIMask` | 沿单个 Simple Image 的轮廓镂空，适合圆形、星形、凹形和带透明孔洞的图标 |

当前源码包含这两个组件，尚未发布新的版本标签，包版本暂保留为 2.0.0。从已发布版本升级时，请先阅读下方的[升级说明](#从-200-升级)。

## 环境要求

- Unity 2022.3 LTS；已验证版本为 **2022.3.62f3c1**，详细环境与验证范围见[发布维护说明](Documentation~/Release.md)。
- 使用 UGUI；`com.unity.ugui` 依赖会自动安装。
- 精确遮罩需要设备支持异步 GPU 回读，不支持时会给出错误提示。
- 团结引擎和其他 Unity 版本尚未验证。

团结引擎的兼容性验证与适配计划在后续版本中推进，当前版本暂不承诺支持。完成验证后，会在 README 和更新日志中公布支持的团结版本；目前没有确定的发布时间。

## 安装

确保电脑已安装 Git，且 `git --version` 可用。新安装 Git 后，请重启 Unity Hub 和编辑器。

在 **Window → Package Manager → ＋ → Add package from git URL** 中输入以下链接，安装当前双组件版本的已验证提交：

```text
https://github.com/FiveT-53/F_EasyUIMask.git#db691f136199e7e6cf69b30f161c2240f71d413a
```

等待编译完成即可使用，无需下载源码或复制到 Assets。

新版本发布后，可改用对应的正式版本标签。已发布的 `#v2.0.0` 仍是旧矩形版，不包含这里的精确遮罩；旧版使用方式见 [v2.0.0 README](https://github.com/FiveT-53/F_EasyUIMask/blob/v2.0.0/README.md)。`#unity`、`#tuanjie` 安装链接已停止使用，分支与历史版本说明见[发布维护说明](Documentation~/Release.md)。

如果工程中已有手动复制的插件，请先备份并移除那份插件，避免重复类型或程序集。升级时，在 Package Manager 中添加新版本的 Git URL 即可；固定版本不会自动更新。

## 快速上手：为按钮添加引导遮罩

### 矩形洞口

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

### 沿图标轮廓镂空

1. 新建一个 UI Image 作为 `GuideMask`，删除 Image 组件后添加 **F_Easy UI Mask**。覆盖范围、层级和颜色按上面的步骤设置。
2. 将图标 Image 所在对象的 RectTransform 拖到 **Target UI**。图标需要使用标准 **Image**，**Image Type** 为 `Simple`，并已设置 Sprite。
3. 勾选 **Invert Mask**。选中遮罩，等待 Inspector 显示 **Ready**，即可在编辑器中查看预览；进入 Play 后同样会自动准备。
4. 图标需要移动或换图时，开启 **Real Time Update**；关闭实时更新时，编辑器可点击 **Refresh Preview**，运行时调用 `RefreshMask()`。

绑定的是图标自身，不会自动合并它的子节点。如果按钮背景和图标分别在两个对象上，请选择希望突出显示的那个 Image。

精确遮罩按 Sprite 的透明度判断轮廓，无需为源纹理开启 Read/Write。它目前只支持单个标准 Simple Image；Sliced、Filled、Tiled、RawImage、TMP、分离 Alpha、自定义材质及网格效果暂不支持。目标的颜色、CanvasGroup 透明度和父级 Mask/RectMask2D 裁剪也不参与轮廓提取。

第一次准备需要等待异步数据就绪，期间暂不显示或阻挡点击。更新后的显示和点击区域会一起生效。精度与资源开销的详细说明见[行为参考](Documentation~/Reference.md)。

## 常用设置

| 设置 | 用途 |
| --- | --- |
| `targetUI` | 要跟随的 RectTransform；精确组件要求目标自身带有受支持的 Image |
| `invertMask` | 开启：外围覆盖、内部透明；关闭：覆盖内部 |
| `realTimeUpdate` | 运行时持续跟随目标或参数变化 |
| `color` | 覆盖层颜色与透明度 |
| `raycastTarget` | 是否阻挡覆盖区域的 UI 点击；与颜色透明度独立 |
| `areaSelectMode`（矩形） | `Manual` 手动设置；`UITarget` 使用目标 UI 的矩形包围区域 |
| `targetOffset` / `targetSize`（矩形） | Manual 模式的区域中心偏移与宽高，使用遮罩本地坐标 |
| `alphaThreshold`（精确） | 透明度达到这个阈值的位置算作目标形状，默认 0.5 |
| `maxTextureSize`（精确） | 内部采样纹理的尺寸上限；降低上限可以减少大范围遮罩的资源开销，但轮廓可能变粗 |

两个遮罩都只在自身 RectTransform 范围内生效。矩形组件的 UITarget 使用矩形包围区域，旋转后的目标周围可能留有透明空角；需要按图片轮廓镂空时使用精确组件。

洞口放行只表示这个遮罩不拦截点击，下层按钮仍需满足自身的点击规则。将 Color 的 Alpha 设为零也不会自动关闭点击阻挡，需要时关闭 **Raycast Target**。

## 脚本控制

下面以精确组件为例。把脚本保存为 `MaskExample.cs`，挂到遮罩层级之外的独立对象，并在 Inspector 中绑定已配置好的 `mask`。调用 `ShowFor(target)` 显示引导，调用 `Hide()` 关闭。

```csharp
using UnityEngine;
using F_EasyUIMask;

public class MaskExample : MonoBehaviour
{
    [SerializeField] private FEasyUIMask mask;

    public void ShowFor(RectTransform target)
    {
        if (mask == null || target == null) return;
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

控制矩形组件时，将字段类型改为 `FEasyUIRectMask`，并在 `ShowFor()` 中设置 `mask.areaSelectMode = FEasyUIRectMask.AreaSelectMode.UITarget;`。运行时通过 `AddComponent` 创建精确遮罩时，还需要调用 `ConfigureResources()` 指定包内材质，示例见[行为参考](Documentation~/Reference.md)。

- 关闭实时更新时，修改参数或移动目标后调用 `RefreshMask()`，让显示与点击区域一起更新。
- `RefreshMask()` 可以激活遮罩自身，但不会激活它的父对象，也不会启用被禁用的组件。
- `Close()` 会停用遮罩整个 GameObject，包括其子对象。需要初始隐藏时，将该对象设为 inactive。
- 目标缺失时遮罩会清空显示和点击阻挡；恢复引用后，开启实时更新可自动恢复，否则需要再次刷新。

完整 API、坐标与摄像机限制、编辑预览和旧版迁移说明见[行为与 API 参考](Documentation~/Reference.md)。

## 从 2.0.0 升级

请先备份工程。旧版的矩形组件现在叫 `FEasyUIRectMask`，`FEasyUIMask` 则用于新的精确遮罩。

- 旧场景和 Prefab 中的矩形组件会保留为矩形遮罩，不会自动变成精确遮罩。
- 原来控制矩形遮罩的脚本，需要将字段、`GetComponent`、继承等位置的 `FEasyUIMask` 改为 `FEasyUIRectMask`。即使部分代码仍能编译，也请检查它是否指向了正确组件。
- 需要精确轮廓时，显式添加新的 **F_Easy UI Mask**，重新设置目标和参数。

## 常见问题

- **找不到 Git 或下载失败**：检查 Git 是否在 PATH 中，以及当前网络能否访问 GitHub；重启 Hub 和编辑器后重试。
- **类型或程序集重复**：检查 Assets、本地包和嵌入包中是否存在另一份插件。
- **遮罩不显示**：检查对象及父对象是否激活、组件是否启用、颜色 Alpha 是否大于零，以及目标引用和 Canvas 摄像机是否正确。
- **精确预览没有就绪**：先看 Inspector 的提示，确认目标为受支持的 Simple Image。材质引用丢失时，点击 **Package Resources → Restore Package Resources** 恢复。
- **洞口不跟随目标**：开启 Real Time Update，或在修改完成后调用 `RefreshMask()`。
- **洞口内仍无法点击**：检查 EventSystem、Canvas 的 GraphicRaycaster，以及目标前方是否还有其他开启 Raycast Target 的 UI。

反馈问题时，请附上编辑器版本、插件版本、复现步骤和 Console 的第一条完整错误。

## 许可证

[MIT](LICENSE)。版本更新见 [CHANGELOG](CHANGELOG.md)；参与维护请参阅[发布维护说明](Documentation~/Release.md)。
