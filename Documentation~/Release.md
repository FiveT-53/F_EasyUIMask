# 发布维护 / Release maintenance

## 单一包 / One package

`main` 同时维护源码和完整 UPM 包。保留所有资源及文件夹的 meta，新增资源首次导入时生成并提交。不要定期删除或重建 GUID。`unity`、`tuanjie` 仅保留历史，不再同步发布。

The repository root on `main` is the complete package. Commit metadata with assets and preserve GUIDs. Historical engine branches are not release targets.

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
