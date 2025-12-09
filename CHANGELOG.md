# 更新日志

本页面记录 Fink Framework 的版本更新历史。

---

### v0.2.3 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-10</span>

本次更新新增了框架的版本更新检查功能，并在全局配置面板中加入对应开关与检查间隔设置，帮助开发者在 Unity 编辑器内及时获知框架新版本。

- 新增版本更新检查模块（UpdateCheckUtil），支持编辑器启动时自动检测新版本。
- 新增 GlobalSettings 中的更新检查开关与检查频率设置。
- 版本信息改为从自有服务器 version.json 获取，避免 GitHub API 限流问题。

---

### v0.2.2 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-09</span>

本次更新重点解决数据管线在实际使用中出现的类型解析冲突、模板路径错误等影响工作流的问题。

**数据类型查找（DataUtil.FindType）**：修复 Excel 表名与 UnityEditor 内部类发生同名冲突时导致的数据解析失败问题；引入命名空间优先级机制，使框架始终优先匹配自动生成的数据类，避免“Progress.Item”等 Unity 内部类型被误用。
**数据导出（DataExportTool）**：增强错误隔离能力，确保在部分表解析失败时不会影响其他数据的导出流程；进一步优化文件路径拼接逻辑，提高跨平台稳定性。
**数据容器模板路径（DataGenTool）**：修复容器类模板文件路径引用错误的问题，确保容器类始终能够正确生成，避免空白文件或路径找不到的问题。

---

### v0.2.1 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-09</span>

本次更新聚焦于编辑器工具链的稳定性修复，并对欢迎界面、全局配置系统、UI Builder 等模块进行了重要的可靠性提升，确保框架在复杂工程场景下的编辑体验更加顺畅与可控。

- **欢迎面板（WelcomeWindow）**：修复了在 Unity 打开 Project Settings 时会重复触发 [InitializeOnLoad] 回调、导致编辑器卡死的问题。
- **全局配置（GlobalSettingsAsset）**：修复自动创建 ScriptableObject 时，当父目录不存在会报错的问题；新增递归目录创建机制，确保配置文件路径始终有效并可正确生成。
- **Odin 集成修正**：清理了数个与 Unity 2022/2023 版本相关的过期 API 警告；优化 Unity Serialization Utility 中 PrefabRoot 检测逻辑以适配最新 API。
- **UI Builder（代码生成）**：修复生成的 UI 脚本中字段与方法缩进错位的问题；模板输出格式现已严格对齐 C# 规范，便于 IDE 自动格式化与团队协作。

---

### v0.2.0 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-09</span>

**架构级重构与生态扩展**

本次更新是框架发布以来规模最大的升级，涉及核心架构的全面重构。引入了 UniTask 全异步流程、Provider 插件模式以及程序集拆分，大幅提升了框架的可扩展性与 IL2CPP 兼容性。同时新增了强大的 UI 代码生成工具，进一步解放生产力。

- **架构全面升级**：命名空间统一更为 `Fink Framework`；引入 `UniTask` 实现全异步流；实施程序集拆分与 Provider 插件模式；移除 Odin Serializer Emit/AOT 功能以完美支持 IL2CPP。
- **UI Builder**：新增可视化 UI 面板生成工具，支持一键生成脚本模板、自动扫描并绑定控件字段/事件逻辑，以及自动创建挂载脚本的 Prefab。
- **资源系统重构**：完全重构为 Provider 插件模式，新增本地与网络资源加载器，完善多级缓存体系。
- **全局配置重构**：配置系统迁移至 `ScriptableObject` 驱动方式并接入 Unity Project Settings 面板；重构欢迎面板与编辑器窗口公共样式。
- **工程规范**：强制统一项目文件为 UTF-8 编码；优化代码命名规范与语义清晰度。

---

### v0.1.2 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-11-26</span>

本次更新重点重构了音频与日志系统，增强了定时器与输入模块的稳定性。

- **音频系统**：引入 AudioMixer 自动加载，实现 Music/SFX 音轨分离；音量控制改为 `0~1` 映射至 `-80~0 dB` 标准；优化 PlaySound 异步加载与资源贴载逻辑。
- **日志系统**：增强调用堆栈解析，支持识别 Lambda、闭包及协程环境下的类名，优化缓存策略。
- **定时器**：新增 `SetTimeout`（一次性定时器）及 `PauseAll/ResumeAll`（全局暂停/恢复）接口。
- **输入系统**：优化回调遍历逻辑（ToArray 快照），防止注册/卸载时的集合修改异常。
- **其他**：资源管理器新增手动清空接口；规范化部分对象池脚本命名。

---

### v0.1.1 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-11-25</span>

本次更新大幅增强了事件系统与数据管线，新增自动绑定工具以减少样板代码。

- **EventAutoBinder**：新增 `Bind()` (OnDestroy 解绑) 与 `BindAuto()` (OnEnable/Disable 自动管理)，支持 0-2 参数自动解绑。
- **粘性事件 (Sticky Event)**：新增事件类型，支持新监听者自动接收最近一次广播的事件值。
- **ExcelReaderTool**：统一数据导表工具链，新增 `skipColumn` 跳列机制及未知数据类型预警。
- **事件系统优化**：区分同名事件的不同参数类型，提升类型安全与匹配效率。
- **其他**：扩充 `MathsUtil` 通用计算方法库；优化 `.gitignore` 配置与编辑器资源目录结构。

---

### v0.1.0 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-11-25</span>

**Fink Framework 首次发布**

框架正式对外发布，提供了一套面向 Unity 的模块化基础设施，旨在降低重复开发成本，构建统一的工程架构。

- **基础架构**：包含单例模式、对象池、消息中心等核心底层。
- **UI 系统**：基于 UGUI 的模块化管理方案。
- **资源管理**：支持同步/异步加载的资源管线。
- **流程控制**：提供基础的场景与流程管理能力。

---
*后续更新将持续添加至本页。*
