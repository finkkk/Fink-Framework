# 更新日志

本页面记录 Fink Framework 的版本更新历史。

---

### v0.3.5 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2026-1-4</span>

UI 系统参数化与异步加载体系完善

- UI 参数初始化支持：UI 面板新增统一的参数注入机制，支持同步 / 异步 / 句柄三种加载模式下的安全初始化。
- 异步加载流程重构：引入初始化钩子，确保参数注入先于生命周期调用，避免异步竞态问题。
- 多画布 API 补全：完善多 Canvas 场景下的参数化显示接口，提升 VR / WorldSpace UI 的一致性与可扩展性。
- UIManager 稳定性提升：优化面板缓存与等待逻辑，增强重复调用与加载中断场景下的可靠性。

---

### v0.3.4 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-27</span>

BUG修复与框架稳定性提升

- 资源清理回调修复：修复资源加载系统中手动清空记录时回调未做非空判断的问题，避免空引用异常。
- URP 程序集引用修正：修复 URP 相关程序集引用配置，完善可选依赖声明，提升框架在非 URP 项目下的兼容性与稳定性。
- LogUtil完善：修复日志工具在async调用链下无法正确获取调用者的问题。

---

### v0.3.3 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-26</span>

项目统计与数据归档工具完善

- 项目统计面板升级：新增统一的「项目数据统计与归档」编辑器面板，整合代码与资源统计能力。
- 统计维度补全：支持代码行数、Shader 行数及多类型资源（Prefab / 场景 / 纹理 / Addressables / AssetBundle）统计。
- 数据归档能力引入：新增统计报告与项目源码的文本归档导出，用于项目留档与技术材料整理。
- 归档流程安全化：引入完整参数校验与确认弹窗，避免误操作与无效导出。

---

### v0.3.2 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-24</span>

数据系统稳定化与路径机制完善

- 数据导出结构统一：JSON / Binary 数据导出继承 Excel 原始目录结构，消除扁平化路径问题。
- 文件路径逻辑重构：重构数据路径拼接与扩展名处理，明确路径职责边界，避免模式耦合。
- 数据文件自动定位：升级数据文件查找机制，支持 Binary / JSON 共存，Binary 优先、JSON 兜底。
- 运行时加载稳定性提升：修复 Binary 模式下 JSON 不可见问题，增强多模式切换可靠性。

---

### v0.3.1 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-22</span>

本次更新主要完善场景切换系统，补齐生命周期管理与异步控制能力，提升场景加载过程的安全性与稳定性。

- 重构 ScenesManager 场景切换流程，统一同步与异步加载行为。
- 新增场景切换生命周期事件（Before / After），支持场景加载前后扩展逻辑。
- 引入场景加载并发防护，防止重复或同时触发场景切换。
- 完善 SceneOperation，支持取消机制，避免异步等待卡死。
- 优化异步加载进度回调，统一对外暴露 0~1 的平滑进度值。

---

### v0.3.0 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-19</span>

**资源系统成型与项目分析能力补全**

本次更新标志着 Fink Framework 资源系统的正式成型。框架完整接入 AssetBundle 与 Addressables 两套资源后端，并统一纳入 Provider 插件体系。同时新增项目级统计工具，用于量化项目代码规模与资源构成，进一步提升框架在中大型工程中的可控性与工程视角。

- **资源加载后端扩展**：新增 AssetBundleProvider 与 AddressablesProvider，资源加载正式支持 AssetBundle / Addressables 多后端并存，并且新增项目配置中针对资源后端系统的配置。
- **资源系统架构完善**：ResManager 资源管理器能力补全，后端选择与加载逻辑解耦，为热更新与外部资源扩展奠定基础。
- **项目统计工具**：新增项目数据统计面板，支持代码行数统计及多类资源（材质、模型、贴图、音频、Prefab、Scene、AB、Addressables）规模分析。
- **编辑器工具链优化**：优化 EditorProvider、编辑器程序集结构与更新检查输出信息，提升整体稳定性与可维护性。

---

### v0.2.5 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-11</span>

本次更新主要聚焦于 UIBuilder 工具链的安全性与稳定性，并对 Resources 目录结构进行规范化调整，以避免资源命名冲突和无关资源被错误打包的问题。

- 优化 UIBuilder 面板生成流程，新增 PanelName 合法性校验，禁止空格、数字开头及非法字符，避免生成无效脚本。
- 增加 UI Prefab 重名前置检测机制，在生成脚本与触发编译前进行硬中断，防止产生半成品资源。
- 调整 Editor 侧资源目录结构，将编辑器专用资源统一迁移至 EditorResources，避免被错误打包进运行时。
- 规范 Runtime 侧 Resources 目录结构，统一包裹 FinkFramework 命名空间层级，降低与用户项目资源发生命名冲突的风险。
- 细化 UIBuilder 内部校验与异常处理逻辑，提升工具在复杂工程环境下的可靠性与可维护性。

---

### v0.2.4 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-11</span>

本次更新主要改进了欢迎面板系统与框架的设置面板交互体验，提升首次使用与日常开发时的易用性与视觉一致性。

- 重构欢迎面板系统：改为 每次打开项目自动弹出，并新增“下次不再显示”选项。
- 优化欢迎面板初始化流程，使用 EditorApplication.update 等待编辑器稳定后再弹出，提高弹出成功率。
- 欢迎面板与设置界面均统一支持手动触发版本检查。

---

### v0.2.3 <span style="font-size:0.8em; color:gray; font-weight:normal;">— 2025-12-10</span>

本次更新新增了框架的版本更新检查功能，并在全局配置面板中加入对应开关与检查间隔设置，帮助开发者在 Unity 编辑器内及时获知框架新版本。

- 新增版本更新检查模块（UpdateCheckUtil），支持编辑器启动时自动检测新版本。
- 新增 GlobalSettings 中的更新检查开关与检查频率设置。
- 版本信息改为从自有服务器 version.json 获取，避免 GitHub API 限流问题。
- 新增手动检测是否是最新版本的按钮，放置在欢迎面板和项目全局配置中的框架配置面板中。

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