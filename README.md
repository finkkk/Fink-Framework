<p align="center">
  <img src="https://finkkk.cn/upload/f_logo.webp" width="120" alt="logo">
</p>

---

# Fink Framework

**Fink Framework** 是一套面向 Unity 中小型游戏项目的 **模块化开发框架**。  
框架来源于长期的项目实践积累，涵盖 **数据驱动管线、UI 系统、资源加载、对象池、运行时工具链、调试可视化** 等核心能力，旨在为 Unity 项目提供 **稳定、高效、可维护** 的基础设施。

框架完全开源，可直接集成至任意 Unity 项目中。

---

# 文档地址（Documentation）

框架详细使用教程请查阅文档：
<p align="center">
  <a href="https://finkkk.cn/docs/fink-framework" target="_blank">
    查看完整文档
  </a>
</p>

>  **注意：当前版本仅支持 .NET Framework（.NET Standard / .NET Core / IL2CPP 源生成尚未适配）。**

---

## 1. 核心特性

### • 数据管线系统
支持 Excel → 自动生成 C# 数据类 → JSON → 加密二进制的完整流程。  
包含类型校验、字段 QA 检查、模板自动生成、灵活的解析逻辑与自定义 Converter 扩展机制。

### • UI 管理与多画布体系
内置多层级 Main UI、WorldSpace UI、VR HUD。  
支持异步加载、生命周期钩子、逻辑与表现分离、自动事件绑定等。

### • 资源加载系统
提供统一的同步/异步接口、内存缓存策略。  
支持 Editor 模式加载（`EditorResManager`）与运行时加载（`ResManager`）双通道。

### • 可配置对象池系统
自动注册、预加载、复用上限、自动清理，并带有调试可视化布局。

### • 轻量事件系统
无任何额外依赖，适合中小规模系统的事件分发与监听。

### • 全局计时器管理
支持多计时器、间隔回调、受/不受 timeScale、对象池复用、唯一 ID 管理等。

### • 运行时工具链
包含输入管理、日志系统、数学工具、字符串处理、JSON 纠错与清洗、Gizmos 可视化调试等。

### • 场景管理与模块化工具链
提供场景切换器、Gizmos 调试器、自动单例（普通 + Mono）、编辑器扩展工具等。

整体框架结构清晰、模块解耦，可在项目初期作为稳定基础设施使用，也可在中后期根据需求灵活裁剪。

---

## 2. 下载与开源地址

**GitHub 源代码（主下载渠道）：**  
https://github.com/finkkk/Fink-Framework

其他下载镜像（可选）：

- **百度网盘：**（待补充）
- **123 网盘：**（待补充）
- **蓝奏云：**（待补充）

你可以在 GitHub Releases 中获取最新的 `unitypackage`，或通过上述镜像直接下载。

---

## 3. 联系方式

如需交流、反馈或合作，欢迎联系：

- **QQ：** 2217183968  
- **微信：** FLX2217183968  
- **博客：** https://finkkk.cn  
- **GitHub：** https://github.com/finkkk  

你也可以在仓库 Issue 区或文章底部留言。

---
