# 模块配置结构

配置分类与 Project Settings > Fink Framework 页面对应。框架基础设置保存在 GlobalSettingsAsset，功能参数分别保存在各模块 SO 中；不保留旧根配置字段或兼容代理。

默认配置全部直接位于 `Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/`，不创建分类子文件夹：

| 设置页面 | 配置文件 |
| --- | --- |
| Framework | GlobalSettingsAsset.asset |
| Data Pipeline | DataSettingsAsset.asset |
| Save System | SaveSettingsAsset.asset |
| Input System | InputSettingsAsset.asset |
| Localization | LocalizationSettingsAsset.asset |
| Resource Backend | ResourceBackendSettingsAsset.asset |
| UI Settings | UISettingsAsset.asset |
| Audio System | AudioSettingsAsset.asset |

选择 AssetBundle 或 Addressables 后端时，后端子配置按需创建，也直接放在 Settings 下。

## 配置归属

GlobalSettingsAsset 保存更新检查、环境覆盖、构建检查、全局脚本根目录和 7 个功能模块引用。各模块保存本模块详细参数；数据管线与存档各自保存 AES 开关与密钥，不再存在 Shared 分类或独立加密 SO。

Framework > 构建设置集中管理编辑器资源路径检查与音频配置检查，两个开关默认开启、互相独立，由 Editor/Utils/BuildCheckUtil 统一执行。关闭音频构建检查只跳过打包前的 Mixer、输出分组及资产目录校验，不影响音频模块开关、设置页提示或运行时初始化校验；音频模块本身关闭时自动跳过音频检查。

音频和本地化开关仅在各自模块配置页管理，Framework 页不再提供模块开关。两个开关修改后立即保存。音频关闭时访问 Instance 会抛出明确的未启用异常，TryGetInstance / IsEnabled 只返回空或 false，不创建服务；本地化关闭时不构造数据库或加载语言表，订阅请求被忽略，查询返回未启用结果并仅提示一次。场景中的本地化组件不订阅、不发起异步刷新，保留原有显示内容。运行中关闭还会撤销已有帧监听或语言订阅并释放模块持有的运行时资源。主动调用仍需执行开关检查，关闭保证无持续运行的模块工作，并非消除调用方自身或配置读取的开销。Audio System 页与音频 SO Inspector 共用绘制和校验，只配置 Mixer，以及全局、音乐、音效输出分组。音乐和音效下拉限定在全局分组的已有层级内。全局音量与音乐、音效分类音量相乘，API 直接调整框架自己的 AudioSource，不需要暴露 Mixer 参数；其他分组、效果、Snapshot 和外部播放器由用户管理。

## 加载与编辑

打开任一配置页时，GlobalSettingsEditorLoader.LoadOrCreate() 检测并补齐缺失配置、绑定根引用；已有配置直接复用，不重置参数。页面打开期间删除配置，下次绘制也会自动补建。打包前同样先补齐配置，再检查音频输出。仅残留 .meta 的缺失资产允许补建；路径被其他文件占用或资产类型不匹配属于内容冲突，会报告具体错误，不覆盖已有文件。

运行时从 `FinkFramework/Settings/GlobalSettingsAsset` 加载根配置，各模块 RuntimeLoader 沿根引用读取。子配置可以位于其他参与构建的目录，实际运行不扫描资产，也不依赖默认创建路径。

```csharp
var data = DataSettingsRuntimeLoader.Current;
var save = SaveSettingsRuntimeLoader.Current;
var ui = UISettingsRuntimeLoader.Current;
var backend = ResBackendSettingsRuntimeLoader.Current.ResourceBackend;
bool usesResources = ui.UsesResources(backend);
```

RuntimeLoader 必须在 Unity 主线程访问。编辑器程序集注册补建入口，运行时程序集不引用 AssetDatabase。已有模块引用直接复用；编辑器中缺失时补建持久化 SO，Player 中则先查找默认 Resources 资产，仍缺失时创建并缓存内存默认 SO，不写安装目录。Current / TryGet 不因配置文件缺失报错或返回空值。内存默认值随运行周期清理。选用 AssetBundle / Addressables 时，其缺失子配置也按相同规则补齐；自定义后端的数据类型由用户提供。

默认值采用各配置类型的字段初始值；缺失音频配置会使用默认启用状态和内置 Mixer。已有配置中明确关闭的模块保持关闭，不创建对应服务、播放器或语言数据库。配置内容错误（例如输出分组属于其他 Mixer）仍报告具体原因。

运行中在本地化设置页重新启用模块时，会重新绑定已有活动的本地化组件；主动禁用的组件保持禁用。音频关闭使在途播放请求失效，但不取消资源系统已经发出的共享加载；加载结束后只归还该请求持有的引用，不恢复播放。
