# F_Easy UI Mask

简化 UI 遮罩配置。支持配置正反向的矩形遮罩，提供两种区域选择模式：手动模式和目标 UI 模式。

## 功能特点

- **正向/反向遮罩** — 可切换裁剪目标区域或覆盖除目标区域外的所有区域
- **两种区域选择模式**
  - *手动模式*：通过 Scene 视图中的控制手柄直接定义偏移量和尺寸
  - *目标 UI 模式*：自动跟随指定的 RectTransform
- **射线检测过滤** — 支持 `raycastTarget`，可控制点击是否能穿透遮罩区域
- **实时更新** — 可选每帧刷新，适用于跟随移动的目标
- **自定义检视面板** — 内置 Scene 控制手柄，直观编辑区域
- **撤销支持** — Scene 视图中的所有编辑操作均可撤销

## 已知限制

- 目前仅支持**矩形**遮罩

## 环境要求

- 团结引擎 1.8.4+ / Unity 2019.4+
- `.meta` 文件与引擎相关，请确保根据您的引擎安装对应版本

> [!NOTE]
> 团结引擎与 Unity 的 GUID **不兼容**。请务必根据您的引擎安装对应分支。

## 安装

### 通过包管理器 (UPM)

1. 打开 `窗口 > 包管理器`
2. 点击左上角 `+` → `从 git URL 添加包`
3. 根据您的引擎选择对应的分支地址：

| 引擎 | 分支 | 安装地址 |
|------|------|---------|
| 团结引擎 | `tuanjie` | `https://github.com/FiveT-53/F_EasyUIMask.git#tuanjie` |
| Unity | `unity` | `https://github.com/FiveT-53/F_EasyUIMask.git#unity` |

> 若不指定分支，默认安装的是 `main` 分支（仅含源码，不含 `.meta` 文件），无法直接使用。

### 通过 Git URL 指定版本

也可以锁定到特定版本标签：

```
https://github.com/FiveT-53/F_EasyUIMask.git#v1.0.0-tuanjie
https://github.com/FiveT-53/F_EasyUIMask.git#v1.0.0-unity
```

### 手动安装

手动安装时，请下载对应引擎的分支，然后将 `FEasyUIMask` 文件夹复制到项目的 `Assets` 文件夹中。

## 快速开始

1. 通过 `添加组件` 为 `UI` GameObject 添加 `F_Easy UI Mask` 组件，建议使用空 GameObject
2. 选择 `区域选择模式`：
   - **手动模式**：调整 `偏移量` 和 `尺寸`，使用 Scene 中的 `编辑区域` 按钮进行可视化编辑
   - **目标 UI 模式**：将目标 RectTransform 拖入 `目标` 字段
3. 切换 `反转遮罩` 以在前进和反转模式间切换
4. 在代码中需要时调用 `RefreshMask()` 手动更新遮罩区域，如果启用了 `实时更新`，也需要先手动调用一次 `RefreshMask()`

```csharp
// 从代码中刷新
mask.GetComponent<F_EasyUIMask.FEasyUIMask>().RefreshMask();
```

## API 参考

### 公共属性

| 属性 | 类型 | 描述 |
|------|------|------|
| `areaSelectMode` | `AreaSelectMode` | `Manual`（手动）或 `UITarget`（目标 UI） |
| `realTimeUpdate` | `bool` | 每帧更新（可能影响性能） |
| `invertMask` | `bool` | 切换前进/反转遮罩 |
| `targetOffset` | `Vector2` | 遮罩区域中心偏移量（手动模式） |
| `targetSize` | `Vector2` | 遮罩区域尺寸（手动模式） |
| `targetUI` | `RectTransform` | 目标 UI 引用（目标 UI 模式） |

### 公共方法

| 方法 | 描述 |
|------|------|
| `RefreshMask()` | 立即重新计算并更新遮罩区域 |
| `Close()` | 隐藏遮罩 GameObject |

## 许可证

MIT 许可证。详见 [LICENSE](LICENSE)。
