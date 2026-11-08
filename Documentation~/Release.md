# 发布维护 / Release maintenance

## 3.0.0 发布与验证范围 / Release and verification scope (2026-11-08)

**3.0.0** 提供矩形组件 `FEasyUIRectMask` 与单个 Simple Image 精确组件 `FEasyUIMask`，发布标签为 `v3.0.0`。本版已完成原 P1 阶段实现及本节所列验证；2026-11-08 用户确认 UPM 安装、构建运行测试无误、功能正常，并授权合并和发布 3.0。`v2.0.0` 保留为旧矩形版，旧类型调用方需按迁移说明更新。下方 2.0/P0 历史记录不能继承为新功能的验收结论。

Agent 自动化验证环境：Unity **2022.3.62f3c1 (1623fc0bbb97)**、Built-in、Gamma、D3D11、Windows x64 Mono Development Player，RTX 3070 Laptop GPU。独立工程 `D:\UnityProject\FEasyUIMask.P1` 使用 `Assets` 源码副本及 `-noUpm`；没有覆盖原 P0 或业务工程。用户随后执行的 UPM/构建运行测试单独登记，不将该源码副本环境当作其新增实测配置。

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
| 用户手动测试：独立场景 R01～R07、P01～P15、L01～L04 | 用户确认 26 项全部通过；P14 瞬时投影警告已确认测试切换时序原因，用户接受当前表现、不予修复 |
| 用户 UPM 安装、构建运行与功能复验 | 2026-11-08 用户确认正常，并授权合并；未补造自动化计数或新的环境/日志证据 |

用户随后发现编辑器精确预览间歇丢失。本次修复稳定编辑对象的宿主尺寸依据，并将预览会话从瞬时 Inspector 生命周期中独立出来；新增 113 项编辑预览专项及 6 项普通 Play/全局 Play 下 Prefab 检查通过，包含实际 GPU 覆盖像素与 CPU 样点一致性、Inspector 更替不重采样、退选和退出资源清理。此批结果与上表历史 386 项分开记录；2026-11-08 用户反馈当前 P1 功能正常，不扩充 UPM 或其他环境的支持声明。

精度样例中同提交 RT/CPU 逐纹素差异为 0；独立参考超过一个有效纹素 L∞ 的差异为 0。四档 120 个稳定帧无提取/回读、测得主线程分配 0 B；1000 次稳定轮询 0 B。100 次启停资源回到零基线。回读中位数约 16.4 ms、本次最大约 104.76 ms；没有固定帧数承诺。2048² RGBA32 双槽 GPU/CPU 数据上限约 64 MiB，不含其他开销。完整驱动显存峰值和 CPU 提交耗时未测。

**UPM 后续验收已由用户确认通过**：本轮提供的固定 Git 安装入口为 `db691f136199e7e6cf69b30f161c2240f71d413a`，用户随后明确反馈“UPM 安装和构建运行测试无误，功能也正常，可以进行合并”。该结果独立于上述源码副本验证。用户未另附锁文件、完整引擎版本或构建后端日志，不将反馈扩大为其他平台或特定后端的验证。

早期 agent 本地 UPM 尝试曾遇到包解析器报 `The "path" argument must be of type string. Received undefined`，作为历史环境问题保留，不再列为本版的未通过项。包默认资源和 UGUI 所需 Shader 变体另有既有源码副本 Player 构建运行证据。本版发布整理仅更新版本与文档，不修改已验收的 Runtime、Editor 或资源行为。

原 P0 工程含 Assets/本地包副本及原型材质；本轮未改动它。后续复用前须核对重复 GUID，保留其测试资产。包内正式材质沿用 Unity 已生成的 P0 材质及 meta；精确脚本使用原型 partial 的独立 GUID，旧矩形脚本和 Shader 移动保留各自 GUID。矩形 GUID 的场景/Prefab 迁移已在独立夹具工程核对，任意业务消费脚本仍须按用途迁移类型。

宿主位置/旋转/缩放/外框，以及摄像机 view/projection/viewport 的代表性在途变化用例已通过同帧射线失效和恢复后画面对照；尚未证明任意脚本执行顺序、动画与自动布局组合的同帧时序。因此当前支持声明仍限于稳定宿主/观察投影，不自动扩大边界。完整矩形 Inspector/Scene 手柄人工回归、任意业务交互、真实 GPU 故障注入、压缩图集、其他平台/管线/API/Unity 版本及团结未获得新增验证；用户跳过的关闭 Domain Reload 手动路线保持跳过。以上未测范围不因本次合并自动转为支持声明。

**3.0.0** is published as `v3.0.0`. Its P1 implementation passed 386 automated checks across functionality, precision/resources, migration, editor previews, reload/lifecycle and scheduling, plus five actual Windows mouse clicks, in isolated source-copy projects on the environment above. On 2026-11-08, following the supplied Git URL pinned to `db691f136199e7e6cf69b30f161c2240f71d413a`, the user reported successful UPM installation, build/run and normal functionality, and authorized merging and releasing 3.0. This is user-reported acceptance, without additional user-provided environment logs. Earlier agent-side UPM resolver failures remain historical observations. Arbitrary same-frame script/layout ordering is unverified; stable host/observer remains the declared boundary. Release preparation changes the version and documentation without changing the accepted implementation or assets.

The user also confirmed all 26 numbered manual-scene cases passed. The transient P14 perspective-switch projection warning was traced to test-switch timing. The user accepted it without changing the plugin or test case and reported normal stable rendering and clicks.

A later intermittent precision-preview report led to stable edit-mode host dimensions and object-owned preview sessions across Inspector replacements. Another 113 edit-preview checks and six ordinary Play/Prefab checks passed, including actual GPU coverage pixels, matching CPU samples and resource cleanup. These are separate from the historical 386 checks. The user's later UPM/build/run confirmation does not expand the tested platform or rendering-pipeline scope.

## 单一包 / One package

后续仅维护 `main`，同时维护源码和完整 UPM 包，通过正式版本标签发布。保留所有资源及文件夹的 meta，新增资源首次导入时生成并提交。不要定期删除或重建 GUID。旧版本由 `v1.0.0*` 标签及对应 GitHub Release 保留；旧开发快照由附注标签 `archive/unity-final`、`archive/tuanjie-final` 保留，非正式发行版本。`unity`、`tuanjie` 分支已删除，`#unity`、`#tuanjie` 安装链接停止使用。当前双组件版使用 `#v3.0.0`，`#v2.0.0` 仅用于旧矩形版。

Maintain only `main`, whose repository root is the complete UPM package, and publish through official version tags. Commit metadata with assets and preserve GUIDs. The `v1.0.0*` tags and their GitHub Releases preserve old versions; the annotated `archive/unity-final` and `archive/tuanjie-final` tags preserve development snapshots, not official releases. The `unity` and `tuanjie` branches have been deleted. Stop using `#unity` and `#tuanjie` installation URLs. Install the two-component release with `#v3.0.0`; `#v2.0.0` installs only the old rectangle version.

## 发布步骤 / Release steps

1. 在独立 Unity 开发工程中以本地包方式开发；测试工程不放在包根目录。
2. 修改源码、package.json 版本、CHANGELOG 及双语 README；同步新增/删除/移动资源的 meta。
3. 在声明支持的编辑器中验证编译、运行时、Inspector/Prefab 和失败恢复；不要宣称尚未验证的引擎兼容。
4. 提交并推送 main。用没有 Assets/本地/嵌入插件副本的新工程，通过远端 HTTPS Git URL 安装。
5. 检查 packages-lock.json 中 source=git、URL 和提交哈希；验证依赖、组件、Inspector、重启后导入和代表功能。
6. 验证通过后在该提交创建不可变版本标签（如 v3.0.0，与 package.json 一致），推送标签，README 提供固定版本安装链接。不要移动已发布标签。
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
