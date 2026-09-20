<p align="center">
  <img src="https://finkkk.cn/upload/f_logo.webp" width="120" alt="Fink Framework logo">
</p>

<h1 align="center">Fink Framework</h1>

<p align="center">面向 Unity 中小型游戏项目的模块化开发框架</p>

<p align="center">
  <a href="https://www.finkkk.cn/fink-framework"><img src="https://img.shields.io/badge/Docs-阅读文档-2E86DE?style=flat-square" alt="Documentation"></a>
  <a href="https://github.com/finkkk/Fink-Framework/releases"><img src="https://img.shields.io/github/v/release/finkkk/Fink-Framework?label=Release&style=flat-square" alt="Release"></a>
  <a href="https://github.com/finkkk/Fink-Framework/stargazers"><img src="https://img.shields.io/github/stars/finkkk/Fink-Framework?style=flat-square" alt="Stars"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/finkkk/Fink-Framework?style=flat-square&cacheSeconds=0" alt="MIT License"></a>
</p>

---

## 简介

Fink Framework 是一套面向 Unity 游戏项目的开发基础框架，围绕 **UI 系统、数据管线、存档系统、资源加载、本地化系统与运行时基础服务** 提供完整支撑。框架同时覆盖场景、事件、对象池、计时器、输入、音频与调试工具等常用能力，并以清晰的模块边界减少重复建设，让团队更专注于玩法与内容。

框架主体约 3.8 万行 C# 源码，采用 Runtime / Editor 程序集拆分，支持按项目需求选择和组合模块。

## 核心能力

| 模块 | 能力概览 |
| --- | --- |
| UI 系统 | 重构后的 UI 架构，支持异步加载、面板生命周期、参数注入、转场、导航、模态遮罩、多 Surface 与多 Canvas 场景。 |
| 本地化系统 | 提供语言设置、运行时清单、文本与资源表、格式化、语言回退，以及导入、导出、质量检查等编辑器工具链。 |
| 数据管线 | 覆盖 Excel → C# → JSON → Binary 的处理流程，包含代码生成、字段校验、数据 QA、清单生成与路径管理。Binary 模式支持 AES 加密。 |
| 存档系统 | 提供强类型槽位/全局存档、UniTask 异步读写、自动存档、原子替换、即时与历史备份、损坏恢复及旧 Schema 成员重命名兼容；序列化、AES 与压缩能力复用 DataUtil。 |
| 资源加载 | 统一同步、异步与句柄式接口；通过 Provider 机制支持 Resources、Editor、File、Web、AssetBundle 与 Addressables。 |
| 项目设置 | 在 Project Settings 中集中管理框架、数据管线、存档系统、资源后端、输入、本地化与 UI 等配置；存档 AES 策略复用 Data Pipeline。 |
| 运行时基础服务 | 内置单例、事件、计时器、对象池、场景切换、输入、音频、日志、数学与 Gizmos 可视化工具。 |
| 编辑器工具 | 提供数据处理、本地化管理、UI 构建、项目统计、框架欢迎页与设置面板，形成从配置到导出的工作流。 |

## 适用场景

- 希望快速建立统一工程规范的 Unity 单人或小团队项目。
- 需要数据驱动配置、异步 UI、资源后端切换或多语言支持的项目。
- 希望将通用基础能力从业务层抽离，并保留后续扩展空间的项目。

## 快速开始

1. 从 [Releases](https://github.com/finkkk/Fink-Framework/releases) 下载最新 `unitypackage`，或直接克隆本仓库。
2. 导入后打开 Unity 的 **Project Settings → Fink Framework**，按项目需求完成 Data Pipeline、Save System、Resource Backend、Localization、UI 等配置。
3. 在业务代码中按模块接入 UI、存档、资源、本地化、事件与对象池能力；完整使用方式请查阅下方文档。

> 建议使用 Unity 2022 LTS 或 Unity 6 LTS 及更高版本。当前项目基于 Unity 2022.3.62f2 验证。
## 项目结构

```text
Assets/FinkFramework/
├── Runtime/                 # 运行时模块
│   ├── UI/                  # UI、导航、转场、模态与安全区域
│   ├── Localization/        # 本地化运行时系统
│   ├── Data/                # 数据读取、序列化与管线路径
│   ├── Save/                # 槽位/全局存档、备份恢复与 Schema 兼容
│   ├── ResLoad/             # Provider 化资源加载
│   ├── Audio/ Pool/ Timer/  # 常用运行时服务
│   └── ...
├── Editor/                  # 数据、本地化、UI、设置与统计工具
└── Plugins/                 # 随框架分发的第三方依赖
```

## 文档与下载

- [使用文档](https://www.finkkk.cn/fink-framework)
- [GitHub 仓库](https://github.com/finkkk/Fink-Framework)
- [GitHub Releases](https://github.com/finkkk/Fink-Framework/releases)
- [百度网盘镜像（提取码：2333）](https://pan.baidu.com/s/1obZYHwBI4ZVnavCiCPE8BA?pwd=2333)

## 依赖与致谢

框架感谢以下开源项目与社区贡献者提供的支持与启发：

- [Odin Serializer](https://github.com/TeamSirenix/odin-serializer)
- [UniTask](https://github.com/Cysharp/UniTask)
- [ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [Json.NET Converters（Wanzyee Studio）](https://assetstore.unity.com/packages/tools/input-management/json-net-converters-simple-compatible-solution-58621)
- 所有分享 Unity 技术与开源成果的开发者

## 开源协议与联系方式

本项目采用 [MIT License](LICENSE) 开源。

如果你在使用过程中遇到问题，或希望讨论框架设计、模块扩展、贡献代码等内容，欢迎加入QQ群聊一起讨论：

- **QQ群：** 1125852721

也可以欢迎各位开发者添加框架作者的个人联系方式进行交流：

- **QQ：** 2217183968
- **微信：** FLX2217183968
- **博客：** https://finkkk.cn
- **GitHub：** https://github.com/finkkk

你也可以在 GitHub Issue 区提交问题或建议，也可在博客文档下留言进行讨论。
