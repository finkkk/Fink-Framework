# Fink Framework 本地化模块需求文档

**文档版本：** v0.1  
**状态：** 需求基线  
**适用项目：** Fink Framework / Unity 游戏项目  
**编写日期：** 2026-08-31

---

## 1. 文档目的

本文档用于确定 Fink Framework 本地化模块的产品目标、数据格式、编辑流程、运行时 API、Unity 组件、编辑器工具和后续扩展边界。

后续开发应以本文档为功能基线。若开发过程中需要改变核心数据格式、调用方式或文件组织方式，应先更新本文档。

---

## 2. 产品定位

Fink Localization 是一套模块化、数据驱动、本地优先的 Unity 本地化系统。

它需要同时解决以下问题：

1. 程序可以通过 API 获取本地化文本。
2. UI 可以通过组件和 Inspector 下拉菜单绑定本地化文本。
3. 策划和翻译人员可以通过 Excel 编辑内容。
4. 程序员可以直接编辑简单的 JSON 文件。
5. 开发者可以通过 Unity 编辑器窗口管理语言、分类和翻译表。
6. 系统可以自动生成强类型 Key，减少手写长字符串 Key。
7. 语言切换后，已绑定的 UI 和资源可以自动刷新。
8. 系统可以检查缺失翻译、重复 Key、错误占位符和未使用 Key。
9. 后续可以扩展到 Addressables、AssetBundle、远程语言包以及外部翻译平台。

核心原则：

> 运行时数据保持极简；复杂的编辑、校验、代码生成和翻译协作能力放在编辑器与构建阶段。

---

## 3. 总体使用流程

```text
打开 Localization Setup
        ↓
配置默认语言和支持语言
        ↓
配置主分类
        ↓
自动生成语言目录和 JSON 文件
        ↓
在 Unity 窗口、JSON 或 Excel 中编辑 Key 和翻译
        ↓
校验本地化数据
        ↓
生成运行时数据和 LocalizationKeys.cs
        ↓
代码通过 API 获取文本
UI 通过 FinkLocalizedText 组件绑定
        ↓
运行时根据当前语言加载和刷新
```

---

## 4. 文件组织规范

采用“一个主分类一个目录，一个语言一个 JSON 文件”的组织方式。

```text
FinkFramework_Data/Localization/
    UI/
        en_us.json
        zh_cn.json
        ja_jp.json

    Common/
        en_us.json
        zh_cn.json
        ja_jp.json

    Battle/
        en_us.json
        zh_cn.json
        ja_jp.json

    Item/
        en_us.json
        zh_cn.json
        ja_jp.json

    Dialogue/
        Chapter01/
            en_us.json
            zh_cn.json
```

### 4.1 主分类

主分类用于：

- 组织文件
- 编辑器筛选
- 运行时按模块加载
- 生成 C# Key 的顶层命名空间

示例主分类：

```text
UI
Common
Battle
Item
Dialogue
```

主分类名称必须满足：

- 不为空
- 全局唯一
- 不能包含路径分隔符
- 不能与其他主分类重复
- 删除前必须检查是否仍有内容

### 4.2 语言文件命名

文件名使用小写下划线格式：

```text
en_us.json
zh_cn.json
zh_tw.json
ja_jp.json
ko_kr.json
```

系统内部使用标准化语言标识：

```text
en-US
zh-CN
zh-TW
ja-JP
ko-KR
```

文件名与内部语言标识之间由系统自动转换。

---

## 5. JSON 数据格式

语言文件必须保持简单的 JSON 对象映射：

```json
{
  "ui.main_menu.start_game": "Start Game",
  "ui.main_menu.settings": "Settings",
  "ui.main_menu.exit_game": "Exit Game",
  "battle.enemy_count": "Enemies remaining: {count}"
}
```

### 5.1 基本规则

- Key 必须唯一。
- Key 使用完整路径格式。
- Value 是当前语言的最终显示文本。
- JSON 文件不保存分类对象、翻译状态或复杂元数据。
- 默认语言文件必须尽量完整。
- 其他语言允许暂时缺失，运行时按 Fallback 规则处理。
- JSON 使用 UTF-8 编码。
- JSON 不支持注释。

### 5.2 Key 命名规则

推荐格式：

```text
主分类.功能分组.具体Key
```

示例：

```text
ui.main_menu.start_game
ui.main_menu.settings
item.sword.description
battle.enemy_count
dialogue.chapter01.blacksmith.greeting
```

Key 使用小写英文、数字、下划线和点号，避免空格、中文和特殊符号。

### 5.3 占位符

占位符使用命名格式：

```json
{
  "welcome": "Welcome, {playerName}",
  "battle.enemy_count": "Enemies remaining: {count}"
}
```

代码调用：

```csharp
Localization.Format("battle.enemy_count", ("count", 5));
```

不允许要求业务代码为了传递少量文本参数而手动创建 `Dictionary`。格式化 API 需要提供以下简化调用形式：

```csharp
// 一个命名参数
Localization.Format("battle.enemy_count", ("count", enemyCount));

// 多个命名参数
Localization.Format(
    "battle.result",
    ("playerName", playerName),
    ("score", score));

// 可选：匿名对象形式
Localization.Format(
    "battle.enemy_count",
    new { count = enemyCount });

// 只有一个占位符时，可直接传值
Localization.Format("battle.enemy_count", enemyCount);
```

直接传值的重载只适用于文本中存在且仅存在一个占位符的情况；占位符数量不匹配时必须输出明确错误。

不同语言必须保持占位符名称一致。编辑器必须检查：

- 占位符未闭合
- 占位符名称拼写不同
- 翻译缺少占位符
- 翻译多出无效占位符

基础版本支持普通命名占位符。复数、性别和复杂 ICU MessageFormat 作为后续扩展，不改变基础 JSON 结构。

### 5.4 元数据

基础语言文件不保存描述、状态、截图等复杂信息。

后续如有需要，可以增加编辑器专用元数据文件：

```text
localization.meta.json
```

示例：

```json
{
  "ui.main_menu.start_game": {
    "description": "主菜单开始游戏按钮",
    "tags": ["UI", "MainMenu"],
    "maxLength": 20
  }
}
```

元数据不影响运行时语言文件。

---

## 6. Localization Settings 配置

本地化模块需要独立的 `LocalizationSettingsAsset`，保持与 Fink 全局配置和其他模块解耦。

### 6.1 基础配置

- 是否启用本地化模块
- 本地化数据根目录
- 默认语言
- 支持语言列表
- 主分类列表
- 默认 Fallback 语言
- JSON 文件命名规则
- C# Key 生成路径
- 运行时加载模式

### 6.2 运行时加载模式

至少支持：

```text
Load All
按需加载模块
按需加载语言包
```

后续支持：

```text
Resources
StreamingAssets
File
AssetBundle
Addressables
HTTP / 远程语言包
```

### 6.3 Fallback 配置

支持默认回退链：

```text
zh-HK → zh-TW → zh-CN → en-US
ja-JP → en-US
ko-KR → en-US
```

找不到翻译时：

1. 查找当前语言。
2. 查找当前语言的区域或基础语言回退。
3. 查找配置的 Fallback 语言。
4. 找不到时返回默认语言文本或 Key，并输出日志。

---

## 7. Unity 编辑器窗口

建议入口：

```text
Fink Framework > Localization > Setup
Fink Framework > Localization > Localization Window
```

### 7.1 初始化向导

初始化向导需要支持：

1. 创建本地化配置。
2. 设置默认语言。
3. 添加支持语言。
4. 添加主分类。
5. 选择数据根目录。
6. 选择代码生成路径。
7. 生成目录和空语言文件。

生成结果示例：

```text
UI/en_us.json
UI/zh_cn.json
UI/ja_jp.json
Battle/en_us.json
Battle/zh_cn.json
Battle/ja_jp.json
```

### 7.2 本地化表编辑器

窗口需要以表格形式展示当前主分类：

| Key | en-US | zh-CN | ja-JP |
|---|---|---|---|
| main_menu.start_game | Start Game | 开始游戏 | ゲーム開始 |
| main_menu.settings | Settings | 设置 | 設定 |

编辑器中可以隐藏主分类前缀，但保存时写入完整 Key：

```text
编辑器显示：main_menu.start_game
JSON 保存：ui.main_menu.start_game
```

需要支持：

- 新增 Key
- 修改 Key
- 修改各语言 Value
- 删除 Key
- 搜索 Key
- 按语言筛选
- 按缺失翻译筛选
- 按主分类筛选
- 默认语言预览
- 语言列显示/隐藏
- 未保存修改提示
- Undo / Redo

### 7.3 文件生成与同步

编辑器需要支持：

- 根据配置生成目录
- 根据配置补齐缺失语言文件
- 从 JSON 读取到编辑器
- 将编辑器内容保存回 JSON
- 检测外部 JSON 是否发生变化
- 重新载入外部变化
- 防止覆盖未确认的外部修改

---

## 8. JSON 和 Excel 编辑支持

### 8.1 JSON

JSON 是程序和 Git 友好的直接编辑格式。

编辑器需要提供：

- 导入 JSON
- 导出 JSON
- JSON 格式校验
- JSON 编码校验
- JSON Key 冲突检查

### 8.2 Excel

Excel 用于策划和翻译人员编辑。

推荐工作簿结构：

```text
Localization.xlsx
    ├── UI
    ├── Common
    ├── Battle
    ├── Item
    └── Dialogue_Chapter01
```

表格格式：

| Key | en-US | zh-CN | ja-JP |
|---|---|---|---|
| main_menu.start_game | Start Game | 开始游戏 | ゲーム開始 |

需要支持：

- JSON 导出为 Excel
- Excel 导入为 JSON
- 按主分类生成 Sheet
- 按语言生成列
- 缺失翻译高亮
- 导入前预览变更
- 导入后输出变更报告
- Key、占位符和重复项校验

Excel 是编辑和交换格式，运行时不直接读取 Excel。

---

## 9. C# 强类型 Key 生成

编辑器根据 JSON Key 自动生成 C# 静态类。

示例：

```csharp
public static class LocalizationKeys
{
    public static class UI
    {
        public static class Main_menu
        {
            public const string Start_game =
                "ui.main_menu.start_game";
        }
    }
}
```

使用方式：

```csharp
Localization.Get(LocalizationKeys.UI.Main_menu.Start_game);
```

代码生成规则：

- 自动将 Key 路径转换为嵌套静态类。
- 自动处理大小写、空格、横线和非法字符。
- 以数字开头的名称自动增加前缀。
- C# 保留字需要转义或转换。
- 发现生成名称冲突时必须报告错误。
- 生成文件带有自动生成标记，不允许手动编辑。
- 重新生成时保留稳定的文件路径和命名空间。

默认生成路径建议：

```text
Assets/FinkFramework/Runtime/Localization/Generated/LocalizationKeys.cs
```

Key 重命名在第一版中视为“旧 Key 删除 + 新 Key 新增”，编辑器需要提供引用扫描和迁移提示。

---

## 10. 运行时 API

### 10.1 基础查询

```csharp
string text = Localization.Get("ui.main_menu.start_game");
```

### 10.2 强类型 Key 查询

```csharp
string text = Localization.Get(
    LocalizationKeys.UI.Main_menu.Start_game);
```

### 10.3 参数格式化

```csharp
string text = Localization.Format("battle.enemy_count", ("count", enemyCount));
```

不要求调用方手动创建参数字典。至少支持：

```csharp
Localization.Format("battle.enemy_count", enemyCount);
Localization.Format("battle.enemy_count", ("count", enemyCount));
Localization.Format("battle.result", ("name", playerName), ("score", score));
Localization.Format("battle.enemy_count", new { count = enemyCount });
```

当 Key 含有占位符时，代码生成器还应生成带参数的强类型访问方法：

```csharp
Localization.Text.Battle.Enemy_count(enemyCount);
```

当 Key 不含占位符时，生成属性：

```csharp
Localization.Text.UI.Main_menu.Start_game;
```

生成器根据占位符名称生成参数名称，并在生成阶段检查不同语言之间的占位符一致性。

### 10.4 语言管理

```csharp
string currentLocale = Localization.CurrentLocale;

await Localization.SetLocaleAsync("zh-CN");
```

需要提供：

- 获取当前语言
- 获取支持语言列表
- 设置当前语言
- 异步切换语言
- 保存玩家语言选择
- 从系统语言初始化
- 语言变化事件
- 当前语言是否已初始化

### 10.5 语言变化事件

```csharp
Localization.OnLocaleChanged += HandleLocaleChanged;
```

切换语言后需要通知所有已绑定对象刷新。

### 10.6 缺失 Key 行为

开发环境：

- 输出错误日志
- 显示 Key 或可配置错误文本
- 编辑器中标记缺失引用

正式环境：

- 优先显示 Fallback 文本
- 可配置是否输出警告
- 不应因为单个 Key 缺失导致游戏崩溃

---

## 11. UI 本地化组件

### 11.1 FinkLocalizedText

第一版核心组件：

```text
FinkLocalizedText
```

支持目标：

- `UnityEngine.UI.Text`
- `TMPro.TMP_Text`
- 其他可扩展文本组件

Inspector 使用方式：

```text
主分类：UI
功能分组：Main_menu
Key：Start_game
```

组件内部保存完整 Key：

```text
ui.main_menu.start_game
```

组件功能：

- Awake 或 OnEnable 时加载文本
- 语言变化时自动刷新
- 支持占位符参数
- 支持默认文本预览
- 支持 Key 下拉选择
- 支持搜索 Key
- Key 缺失时显示错误状态
- 可配置是否等待异步加载完成

### 11.2 资源本地化组件

后续支持：

```text
FinkLocalizedImage
FinkLocalizedAudio
FinkLocalizedFont
FinkLocalizedAsset<T>
```

用于根据语言替换：

- 图片
- Sprite
- 音频
- 字体
- Prefab
- ScriptableObject
- Addressables 资源

---

## 12. 编辑器 QA 功能

编辑器需要提供统一的本地化检查窗口。

### 12.1 数据检查

- JSON 格式错误
- Key 重复
- Key 命名不规范
- 默认语言缺失
- 目标语言缺失
- 占位符不一致
- 空 Value
- 无效语言代码
- 文件命名错误

### 12.2 工程扫描

扫描：

- 场景
- Prefab
- UI 组件
- 脚本中的本地化引用
- 数据表中的 Key

输出：

- 使用但不存在的 Key
- 存在但未使用的 Key
- 重复绑定
- 未绑定的静态文本
- 不支持的本地化组件

### 12.3 Pseudo-localization

后续提供伪本地化语言，用于检查：

- UI 长文本溢出
- 文本截断
- 字体字符缺失
- 特殊字符显示问题
- 未接入本地化的硬编码文本
- RTL 适配问题

---

## 13. 运行时资源加载

本地化模块不直接绑定某一种资源后端，而是通过 Fink 的资源 Provider 体系加载。

需要兼容：

```text
res://
file://
addr://
ab://
http://
https://
```

与现有 `ResManager` 集成后，本地化数据可以使用：

- Resources 内置加载
- StreamingAssets 文件加载
- AssetBundle 加载
- Addressables 加载
- HTTP 远程语言包加载

运行时需要支持：

- 语言文件缓存
- 模块缓存
- 异步加载
- 预加载
- 加载失败回退
- 语言包版本校验
- 资源释放

---

## 14. 架构目录建议

```text
Assets/FinkFramework/Runtime/Localization/
    LocalizationManager.cs
    LocalizationSettingsAsset.cs
    LocaleInfo.cs
    LocalizationDatabase.cs
    LocalizationFormatter.cs
    LocalizationFallback.cs
    LocalizationProvider.cs
    LocalizationKey.cs
    LocalizedText.cs
    LocalizedAsset.cs
    Generated/
        LocalizationKeys.cs

Assets/FinkFramework/Editor/Modules/Localization/
    LocalizationSetupWindow.cs
    LocalizationWindow.cs
    LocalizationSettingsEditor.cs
    LocalizationJsonImporter.cs
    LocalizationJsonExporter.cs
    LocalizationExcelImporter.cs
    LocalizationExcelExporter.cs
    LocalizationCodeGenerator.cs
    LocalizationScanner.cs
    LocalizationQATool.cs

FinkFramework_Data/Localization/
    UI/
    Common/
    Battle/
    Item/
    Dialogue/
```

---

## 15. 模块边界

### 15.1 本地化模块负责

- 语言管理
- Key 查询
- 文本格式化
- Fallback
- 本地化数据加载
- UI 绑定
- 编辑器窗口
- JSON / Excel 导入导出
- Key 代码生成
- 本地化 QA

### 15.2 本地化模块不负责

- 完整的翻译管理平台
- 机器翻译服务
- 翻译人员账号权限
- 翻译供应商管理
- 云端项目管理
- 复杂的在线协作系统

后续通过 Exchange Provider 接口适配 Crowdin、Lokalise、Phrase 或其他平台。

---

## 16. 分阶段开发计划

### Phase 1：基础运行时闭环

必须完成：

- LocalizationSettingsAsset
- 语言列表
- JSON 读取
- 默认语言
- Fallback
- `Localization.Get`
- `Localization.SetLocaleAsync`
- `OnLocaleChanged`
- 缺失 Key 日志
- `FinkLocalizedText`

验收目标：

```text
准备两个 JSON 文件
绑定一个 TMP 文本
切换语言后文本自动刷新
```

### Phase 2：Unity 编辑器配置与编辑

必须完成：

- 初始化向导
- 配置语言
- 配置主分类
- 自动生成目录和语言文件
- Unity 表格编辑器
- Key 下拉选择
- JSON 保存和重新加载

验收目标：

```text
不手动创建目录和 JSON 文件
通过 Unity 窗口创建语言、分类和 Key
```

### Phase 3：代码生成与 QA

必须完成：

- LocalizationKeys.cs 生成
- Key 命名校验
- 占位符校验
- Missing Key 检查
- Unused Key 检查
- 场景和 Prefab 扫描

验收目标：

```csharp
Localization.Get(LocalizationKeys.UI.Main_menu.Start_game);
```

### Phase 4：Excel 与高级编辑

必须完成：

- Excel 导入
- Excel 导出
- 按主分类生成 Sheet
- 导入预览
- 变更报告
- 缺失翻译筛选

### Phase 5：资源本地化与高级格式化

支持：

- 图片
- 音频
- 字体
- Prefab
- Addressables
- 复数
- 性别和条件格式化
- Pseudo-localization
- RTL

### Phase 6：远程与商业翻译平台扩展

支持：

- 远程语言包
- 增量更新
- 版本回滚
- Crowdin Adapter
- Lokalise Adapter
- Phrase Adapter
- XLIFF Adapter

---

## 17. 非功能需求

### 17.1 模块化

- 本地化模块可以单独启用或关闭。
- 不应强制依赖 Addressables。
- 不使用 Excel 时，运行时不能依赖 Excel 库。
- 不使用 TMP 时，核心运行时仍应可编译。
- 外部平台适配器必须可选。

### 17.2 性能

- 不应在每次 `Get` 时重新读取文件。
- 已加载语言表需要缓存。
- 支持按模块加载，避免大型对话表一次性全部载入。
- 语言切换时只刷新已注册的本地化对象。
- 常用 UI 表支持预加载。

### 17.3 稳定性

- JSON 错误必须给出文件路径和行列信息。
- Key 缺失不能导致游戏崩溃。
- 语言切换过程需要支持异步等待。
- 场景切换后本地化组件能够正常重新绑定。
- 远程语言包失败时必须回退到本地包。

### 17.4 安全性

- 翻译平台 Token 不得写入运行时包。
- 远程语言包需要支持 HTTPS。
- 远程数据应支持版本号和校验值。
- 外部文件导入需要限制路径范围，避免覆盖项目外文件。

---

## 18. 第一版完成标准

第一版至少满足以下完整流程：

```text
打开 Unity 本地化配置窗口
    ↓
配置 en-US、zh-CN
    ↓
配置 UI、Common 两个主分类
    ↓
自动生成对应目录和 JSON 文件
    ↓
在窗口中新增 ui.main_menu.start_game
    ↓
填写英文和中文
    ↓
生成 LocalizationKeys.cs
    ↓
在场景中添加 FinkLocalizedText
    ↓
通过下拉菜单选择 UI / Main_menu / Start_game
    ↓
运行游戏显示当前语言
    ↓
调用 SetLocaleAsync("zh-CN")
    ↓
UI 自动显示“开始游戏”
```

如果以上流程稳定完成，说明本地化模块的核心使用闭环已经成立。

---

## 19. 参考方案

- [Unity Localization 官方文档](https://docs.unity3d.com/ja/current/Manual/com.unity.localization.html)
- [Unity Locale Fallback](https://docs.unity.cn/Packages/com.unity.localization@1.4/manual/Locale.html)
- [I2 Localization 工作方式](https://inter-illusion.com/assets/I2LocalizationManual/Howitworks.html)
- [Crowdin Unity 集成](https://store.crowdin.com/unity)
- [Lokalise 软件本地化流程](https://developers.lokalise.com/docs/software-localization-workflow)
- [Phrase Strings CLI](https://support.phrase.com/hc/en-us/articles/5808300599068-Using-the-CLI-Strings)
- [Unicode CLDR Plural Rules](https://cldr.unicode.org/index/cldr-spec/plural-rules)
