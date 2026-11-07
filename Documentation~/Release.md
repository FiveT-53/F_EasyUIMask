# 发布维护 / Release maintenance

## P1 开发状态 / Development status (2026-11-07)

P1 已完成双组件源码实现，并在独立源码副本工程执行本节所列验证。包版本仍为 2.0.0，不代表已发布版本包含精确组件。下方 2.0/P0 历史记录不能继承为 P1 验收结论。

本轮验证环境：Unity **2022.3.62f3c1 (1623fc0bbb97)**、Built-in、Gamma、D3D11、Windows x64 Mono Development Player，RTX 3070 Laptop GPU。独立工程 `D:\UnityProject\FEasyUIMask.P1` 使用 `Assets` 源码副本及 `-noUpm`；没有覆盖原 P0 或业务工程。

| 已执行组 | 结果 |
| --- | --- |
| 功能 Player：形状、正反、投影、有效空/失效恢复、自身裁剪、刷新与启停 | 139 检查，0 失败 |
| 精度与资源/性能 Player | 108 检查，0 失败；18 精度样例、6,049,438 纹素 |
| v2.0.0 场景、Prefab、Variant、字段/引用、owner、消费脚本和 UnityEvent 迁移夹具 | 32 检查，0 失败 |
| 原始 RuntimeOnly UnityEvent 在实际 Play Mode 调用迁移后的 Close | 9 检查，0 失败 |
| 精确编辑预览、默认材质、Undo、禁用/跨 Stage、Prefab 不变脏和在途清理 | 修复编辑模式回读推进后，14 检查，0 失败 |
| 真实脚本重载、Domain Reload 开关、全局 Play 下 Prefab Stage 及退出资源清理 | 22 检查，0 失败 |
| 在途宿主/摄像机变化、刷新重入、身份重建、失效恢复、多实例、softness/嵌套 Stencil | 62 检查，0 失败 |
| Windows 实际鼠标输入经 EventSystem 派发 | 5 次点击，正反形状内外及外框规则均符合预期 |
| 用户手动测试：独立场景 R01～R07、P01～P15、L01～L04 | 用户确认 26 项全部通过，无新增问题；P14 切换透视时的瞬时投影警告保留待调查，未修复 |

精度样例中同提交 RT/CPU 逐纹素差异为 0；独立参考超过一个有效纹素 L∞ 的差异为 0。四档 120 个稳定帧无提取/回读、测得主线程分配 0 B；1000 次稳定轮询 0 B。100 次启停资源回到零基线。回读中位数约 16.4 ms、本次最大约 104.76 ms；没有固定帧数承诺。2048² RGBA32 双槽 GPU/CPU 数据上限约 64 MiB，不含其他开销。完整驱动显存峰值和 CPU 提交耗时未测。

**本地 UPM 导入仍未通过**：初始工程以及使用完整工程设置/绝对包路径的新工程均在包解析器阶段报 `The "path" argument must be of type string. Received undefined`。上述源码副本结果不能表述为 UPM 安装通过，也不能替代远端安装验收。包默认资源已在源码副本中验证，UGUI 所需 Shader 变体已随代表性 Player 构建运行。

原 P0 工程含 Assets/本地包副本及原型材质；本轮未改动它。后续复用前须核对重复 GUID，保留其测试资产。包内正式材质沿用 Unity 已生成的 P0 材质及 meta；精确脚本使用原型 partial 的独立 GUID，旧矩形脚本和 Shader 移动保留各自 GUID。矩形 GUID 的场景/Prefab 迁移已在独立夹具工程核对，任意业务消费脚本仍须按用途迁移类型。

宿主位置/旋转/缩放/外框，以及摄像机 view/projection/viewport 的代表性在途变化用例已通过同帧射线失效和恢复后画面对照；尚未证明任意脚本执行顺序、动画与自动布局组合的同帧时序。因此当前支持声明仍限于稳定宿主/观察投影，不自动扩大边界。完整矩形 Inspector/Scene 手柄回归、任意业务交互、真实 GPU 故障注入、远端安装、压缩图集、其他平台/管线/API/Unity 版本及团结尚未完成。P1 完整验收退出条件尚未满足。

P1 passed 386 automated checks across functionality, precision/resources, migration, editor previews, reload/lifecycle and scheduling, plus five actual Windows mouse clicks, in isolated source-copy projects on the environment above. The editor readback loop was fixed and retested. Local UPM import fails before compilation with a resolver error, including a retry with full project settings and an absolute package path; source-copy results do not certify UPM installation. Representative dynamic-host tests passed, but arbitrary same-frame script/layout ordering is unverified, so stable host/observer remains the declared boundary. Full acceptance, other platforms and release are pending. Version remains 2.0.0.

The user also confirmed all 26 numbered manual-scene cases passed. A transient projection warning when switching P14 to perspective remains an open development observation; the user reported normal rendering and clicks after the switch. Its cause has not been independently confirmed or fixed.

## 单一包 / One package

后续仅维护 `main`，同时维护源码和完整 UPM 包，通过正式版本标签发布。保留所有资源及文件夹的 meta，新增资源首次导入时生成并提交。不要定期删除或重建 GUID。旧版本由 `v1.0.0*` 标签及对应 GitHub Release 保留；旧开发快照由附注标签 `archive/unity-final`、`archive/tuanjie-final` 保留，非正式发行版本。`unity`、`tuanjie` 分支已删除，`#unity`、`#tuanjie` 安装链接停止使用；新安装使用正式版本标签（如 `#v2.0.0`）。

Maintain only `main`, whose repository root is the complete UPM package, and publish through official version tags. Commit metadata with assets and preserve GUIDs. The `v1.0.0*` tags and their GitHub Releases preserve old versions; the annotated `archive/unity-final` and `archive/tuanjie-final` tags preserve development snapshots, not official releases. The `unity` and `tuanjie` branches have been deleted. Stop using `#unity` and `#tuanjie` installation URLs; use official version tags such as `#v2.0.0` for new installations.

## 发布步骤 / Release steps

1. 在独立 Unity 开发工程中以本地包方式开发；测试工程不放在包根目录。
2. 修改源码、package.json 版本、CHANGELOG 及双语 README；同步新增/删除/移动资源的 meta。
3. 在声明支持的编辑器中验证编译、运行时、Inspector/Prefab 和失败恢复；不要宣称尚未验证的引擎兼容。
4. 提交并推送 main。用没有 Assets/本地/嵌入插件副本的新工程，通过远端 HTTPS Git URL 安装。
5. 检查 packages-lock.json 中 source=git、URL 和提交哈希；验证依赖、组件、Inspector、重启后导入和代表功能。
6. 验证通过后在该提交创建不可变版本标签（如 v2.0.0，与 package.json 一致），推送标签，README 提供固定版本安装链接。不要移动已发布标签。
7. 用户更新分支包时重新添加同一 Git URL；固定标签用户主动切换新标签。不要要求用户清空整个 Library/锁文件。

PackageCache 是安装结果，不是源码修改位置。UPM Git 安装需要 Git 和网络，但不需要用户手动下载本包。团结必须用实际团结编辑器验证后再加入支持矩阵。

## 验证记录 / Verification scope (2026-10-28)

- Unity 2022.3.62f3c1：26 项用户手动快速验收通过，包含运行时遮罩/点击、失败恢复、Scene 编辑、Undo 和 Prefab。
- 独立工程通过远端 HTTPS Git URL 安装 main 与 v2.0.0；检查 Git 来源、稳定 GUID、UGUI 自动依赖和 Runtime/Editor 编译。
- 安装后的原生后台回归通过手动/实时失败恢复、重复失败及 RESET 的网格和材质绑定检查。
- 完整 Player 构建与运行、复杂投影专项、其他 Unity 版本和团结仍未完成验证。不得将上述检查扩展表述为这些平台/场景已通过。
- 发布包保留标准 Unity meta；团结应先验证同一份包，有明确不兼容证据后再决定适配方式。

Manual quick acceptance and remote Git package import were verified on Unity 2022.3.62f3c1. Native recovery checks inspect mesh and material bindings. Full Player builds/runs, complex projections, other Unity versions and Tuanjie remain unverified.

## 后续团结支持计划 / Future Tuanjie support

目前团结编辑器运行环境尚未就绪，兼容性验证与适配延后到后续更新，不作为当前版本已支持的平台，也不承诺具体发布日期。

环境就绪后，先使用现有标准 UPM 包在选定的团结版本中执行干净 Git URL 安装、编译、运行时遮罩与点击、失败恢复、Scene/Undo/Prefab 以及 Player 构建运行检查。根据实际不兼容问题适配，优先保持单一包和稳定 GUID，不预先重建引擎分支或整套 meta。

验证通过后，在新版本 README、CHANGELOG 和发布说明中列出具体团结版本及限制，再发布新的版本标签；不修改已发布的 v2.0.0 标签。

Tuanjie testing is deferred until a working editor environment is available. Validate the existing UPM package first, adapt only demonstrated incompatibilities, and publish verified versions and limitations in a future release. Preserve stable GUIDs and existing release tags. No schedule is committed.
