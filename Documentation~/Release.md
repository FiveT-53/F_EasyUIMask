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
