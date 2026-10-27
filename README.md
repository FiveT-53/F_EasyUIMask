# F_Easy UI Mask

> 🏠 **English**: [README_en.md](README_en.md)

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
- 依赖 Unity UI（`com.unity.ugui`，包清单声明版本为 `1.0.0`）。UPM 安装会请求该依赖；手动安装前请在包管理器中安装 Unity UI。
- `.meta` 文件与引擎相关，请确保根据您的引擎安装对应版本

上述版本为最低兼容目标，尚未完成跨版本引擎验收。本次已使用 Unity 2022.3.62f3 的程序集进行源码编译检查；因本机 Editor 许可证不可用，干净导入、Scene 操作和 Player 构建未验证。Unity 2019.4 与团结 1.8.4 仍待对应环境验证。

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

### 源码维护

通用源码先在 `main` 修改并提交，再同步到 `unity` 和 `tuanjie`。保留引擎分支已有的 `.meta`，仅由对应引擎为新增资源生成 `.meta`；不要跨引擎复制或重新生成现有资源 GUID。

## 快速开始

1. 通过 `添加组件` 为 `UI` GameObject 添加 `F_Easy UI Mask` 组件，建议使用空 GameObject
2. 选择 `区域选择模式`：
   - **手动模式**：调整 `偏移量` 和 `尺寸`，使用 Scene 中的 `编辑区域` 按钮进行可视化编辑
   - **目标 UI 模式**：将目标 RectTransform 拖入 `目标` 字段
3. 切换 `反转遮罩` 以在前进和反转模式间切换
4. 播放模式中，激活且启用的组件会在后续 `LateUpdate` 尝试首次刷新；关闭 `实时更新` 时也会尝试一次。之后修改目标、尺寸或正反模式时，调用 `RefreshMask()` 提交；开启 `实时更新` 后会逐帧检查变化。

```csharp
// 从代码中刷新
mask.GetComponent<F_EasyUIMask.FEasyUIMask>().RefreshMask();
```

`RefreshMask()` 会尝试激活遮罩自身，但不会激活父节点或启用被禁用的组件；每次有效的显式刷新都会请求顶点更新，实际显示在正常 Canvas 重建阶段生效。实时更新关闭时，仅赋值字段会保留上一份已提交的区域和模式，直到显式刷新或结构变化触发刷新。目标由自动布局控制时，请在布局完成后显式刷新。

迁移注意：`Awake()` / `Init()` 不再自动隐藏整个 GameObject，依赖默认隐藏的调用方应将对象初始设为 inactive。刷新检测到缺少 Canvas、目标或必需摄像机时，仅清空本组件显示和命中，宿主保持激活。实时开启时可在配置恢复后自动恢复；实时关闭时，一次失败请求会被消费，需显式刷新或等待启用、父节点、Canvas 结构变化触发新请求。

当前分批修复已接入统一状态和刷新骨架；目标四角投影、非中心 Pivot、越界网格及辅助对象的域重载归属恢复仍待后续批次。编辑模式的预览刷新尚待编辑器批次接入。

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
