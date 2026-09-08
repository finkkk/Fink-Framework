# Fink Framework 自动更新需求

## 1. 目标

在用户项目中，通过 GitHub Release 一键更新 Fink Framework，避免手动删除和重新导入框架文件。

本需求只针对用户项目中的框架副本，不影响框架开发仓库工作区。

## 2. 更新范围

更新器只允许操作：

```text
Assets/FinkFramework/**
```

以下内容必须保留，不得被更新器修改：

- `Assets/FinkFramework_Assets`
- `FinkFramework_Data`
- `Assets/StreamingAssets/FinkFramework_Data`
- `ProjectSettings/FinkFramework`
- `Assets/Scripts` 等项目生成代码目录

## 3. 发布包要求

每个 GitHub Release 提供一个完整的 Unity Package：

```text
FinkFramework-vX.Y.Z.unitypackage
```

包内只能包含 `Assets/FinkFramework/**`，并且必须包含对应 `.meta` 信息，以保持 Unity 资源 GUID 不变。

## 4. 用户更新流程

1. 通过 GitHub Release API 获取最新版本。
2. 使用语义化版本号比较本地版本和远端版本，允许跨版本更新。
3. 用户点击“立即更新”后，显示覆盖警告：

   > 更新会覆盖 `Assets/FinkFramework` 内的框架源码、Editor 工具、内置资源和插件文件。对这些文件的本地修改将会丢失；框架配置、数据文件和项目生成文件不会被修改。

4. 下载并校验 `.unitypackage`。
5. 自动备份当前 `Assets/FinkFramework` 到 `Library/FinkFrameworkUpdateBackup`。
6. 使用 `AssetDatabase.DeleteAsset` 删除旧框架目录。
7. 使用 `AssetDatabase.ImportPackage` 导入新版本。
8. 刷新 AssetDatabase，等待 Unity 完成资源导入和脚本编译。
9. 更新成功后删除临时备份；失败时使用备份自动恢复。

## 5. 安全要求

- 默认自动创建备份，不增加额外的“是否备份”选择。
- 下载、校验和准备工作完成前，不得删除现有框架目录。
- 校验包内路径，拒绝 `../`、绝对路径和框架目录以外的文件。
- 导入失败、文件被占用、权限错误或编译失败时，必须尝试回滚。
- Unity 异常退出后，下一次打开项目时检查未完成的更新记录并提供恢复。
- 不使用 `git pull` 更新用户项目。

## 6. 现有代码改造点

- 扩展 `UpdateCheckUtil`：读取 Release 的 `assets`，获取 `.unitypackage` 下载地址。
- 增加下载、校验、备份、导入和回滚流程。
- 更新成功后刷新版本状态和界面提示。
- 保留现有手动检查更新入口。

## 7. 完成标准

- 新增文件能够正常导入。
- 旧版本已删除的框架文件不会残留。
- 用户配置、数据和生成文件不受影响。
- 框架资源 GUID 不因更新而变化。
- 更新失败可以恢复到更新前状态。
