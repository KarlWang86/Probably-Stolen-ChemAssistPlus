# 常用操作

源码项目为仓库根目录下的 ChemAssistPlus/；文档位于 docs/，脚本位于 scripts/。独立目录需在仓库根目录的 GameRoot.local.txt 中填写游戏根目录（单行、无引号），或设置优先级更高的 PROBABLY_STOLEN_GAME_ROOT 环境变量；本地路径文件不提交。项目引用和脚本共用该配置，详见仓库 README。


在仓库根目录执行，Windows PowerShell 5，统一前缀：
`powershell -NoProfile -ExecutionPolicy Bypass -File`。

辅助工具默认net8.0；本机仅有.NET 9时，先在当前PowerShell设置 `$env:PROBABLY_STOLEN_TOOL_FRAMEWORK = 'net9.0'`，再运行下列脚本。主Mod目标框架不变。

| 目标 | 接在前缀后的命令 | 输出与边界 |
| --- | --- | --- |
| 日常构建 | .\scripts\build.ps1 | build/ 中 DLL、清单、说明、原生扫描结果；不部署/打包 |
| 重扫已有构建 | .\scripts\validate.ps1 | build/验证结果.txt 与 validation.json（记录文件/游戏哈希） |
| 准备工坊内容与排版 | .\scripts\prepare-workshop.ps1 | 核对已扫描构建、备份旧上传工作区、同步3个运行文件/用法与功能/当前版本改动/鸣谢与VDF；配置预检，不登录/上传 |
| 可选人工核查 | .\scripts\publish-workshop.ps1 -CheckOnly | 需要时只读查询english/schinese与本地是否一致；不提交，日常发布无需另跑 |
| 发布并同步简介 | .\scripts\publish-workshop.ps1 | english上传内容/封面/标题/简介，schinese同步标题/简介；两次Steam提交均成功即完成，自动保存简短结果 |
| 单独重试简中简介 | .\scripts\publish-workshop.ps1 -SchineseOnly | 仅在默认语言已提交当前构建时使用，更新schinese标题/简介，不上传运行文件 |
| 查签名与参数 | .\scripts\query-api.ps1 -Type WaterHelper -Method RemoveAndReturn | 终端短结果，完整结果保存在 Research/ |
| 查指定方法 IL | .\scripts\query-api.ps1 -Type ChemicalHelper -Method HandleChlorine -IL | 仅显示保存路径；类型/方法支持单独 * 匹配全部 |
| 查参考程序集 | .\scripts\query-api.ps1 -Assembly <DLL路径> -Type <类型> -Method <方法> | 只读元数据，不执行目标方法 |
| 专项本地部署 | .\scripts\deploy.ps1 -WorkshopState Disabled | 用户已核对禁用同类工坊项时；重新扫描后只复制3个运行文件 |
| 同ID工坊并存部署 | .\scripts\deploy.ps1 -WorkshopState SameIdLocalPriorityConfirmed | 已核对同ID本地优先且无其他同类项冲突时使用 |
| 留档/发布打包 | .\scripts\release.ps1 | releases/<版本-时间>/ 下安装包+源码包；不自动上传 |

VERSION 为版本唯一来源，build 自动同步程序集、清单、UI/日志及使用说明；修改记录中的历史版本不替换。
推进新版本前先检查Git工作区：如有旧版本未提交的源码、文档或发布记录，先按旧版本完成提交，再更新VERSION并开发新版本，避免多版本内容混在同一提交。2026-11-12玩家明确本次0.4.1不拆分，此后遵循该约定；发布后的回执/交接记录可另作当前版本的记录提交。
构建失败会撤销旧 validation.json，部署前重新扫描；扫描器和查询工具放 build/tools/。
原生扫描在 build/scan-input/ 对3个运行文件的副本执行，再核对原文件哈希，避免工具被误判为运行载荷。
项目 NuGet.Config 清空外部包源，使用本机 SDK 引用包。
不需要部署时只执行 build.ps1，不必查询工坊状态。

## 界面排版修改

涉及图标位置、排列方向、间距、缩放或其他排版问题时，先展示布局样式预览，标明容器本体、机器本体与机器窗口等定位对象，以及单种／多种原料的排列效果。玩家确认布局后再修改实际游戏界面，避免凭文字理解反复改动。预览应说明哪些是位置示意，不能把示意当成游戏内验收；实现后仍需检查裁切、缩放、旋转与拖动显示。

## 文件分工

根目录 AGENTS.md 固定约定；docs/HANDOFF.md 当前状态；docs/ARCHITECTURE.md 按模块定位；
NATIVE_API.md 原生接口缓存；MOD_REFERENCE.md 参考实现缓存；CHANGELOG.md 历史变更；
路径以仓库根目录为基准：Research/ 完整IL、旧调查与整理前备份。历史 Tests/ 保留但不运行，不纳入发布源码包。

游戏机制资料按主题存放于 Research/，保留来源、整理日期与验证状态；已核实的原生接口短结论再写入 NATIVE_API.md。超科技物品的获取、六类货品用途、供电与升级限制见 [Research/HypertechMechanics.md](../Research/HypertechMechanics.md)（社区资料整理，尚未逐项核对本机原生实现）。

CHANGELOG.md按每个有据可查的版本记录完整变化，已从归档补回早期版本，缺失版本不补写。WorkshopChangelog.md保留各版精简稿，生成简介只要求当前VERSION条目唯一且有内容，不再要求历史版本集合一致；Steam简介上限为8000 UTF-8字节，生成与上传预检均要求小于8000，不能只查字符数。WorkshopDescription.template.txt按用法/功能→当前版本改动→末尾鸣谢排版，prepare-workshop.ps1只嵌入WorkshopChangelog的当前版本，另存完整CHANGELOG-source.md。旧版本内容由工坊更新记录和仓库保留，不再全部重复到简介。

旧 bin/obj 是构建缓存，staging/ 是历史安装备份，既有 zip 未改动。日常输出统一到 build/。
源码发布包按白名单收录源码/文档/脚本，不包含原生与参考 DLL、Research 原始材料或测试。

## Steam 创意工坊上传（需要发布时才读）

从[已完成的上传对话](thread://01a1c121-bf52-73b2-8f9e-19c6db05c1d6?hostId=local)提取，并与现有脚本/VDF/回执核对。此处是操作知识；历史上传证据见 Research/SteamWorkshop-20261110.md，当前版本状态见 HANDOFF.md。

### 目录与参数

上传工作区为游戏根目录的 WorkshopUpload/，与 Modding/ 同级；不用每次新建工作区。
content/ 只放 ChemAssistPlus.dll、manifest.xml、使用说明.txt。说明源文档为 docs/使用说明.md，构建时保留 Markdown 内容并输出为使用说明.txt，沿用部署、扫描与工坊的运行文件清单。
upload.ps1、upload.cmd、upload.vdf、steamcmd/、preview.png、简介/更新说明及回执均放在 content/ 外。

| upload.vdf 字段 | 本项目配置 / 含义 |
| --- | --- |
| appid | 4348910，来自本机 appmanifest_4348910.acf |
| publishedfileid | 3836818025，沿用已有条目；仅首次新建用0，成功后保留回填ID |
| contentfolder | WorkshopUpload/content 的绝对路径；直接上传目录内容，不上传zip |
| previewfile | preview.png 的绝对路径，JPG/PNG；由玩家提供截图，备用截图不会自动成为附加图片 |
| title / description | 标题“化学助手”；简介按实际功能编写，不能只更新版本号 |
| changenote | 从 CHANGELOG.md 当前版本提炼，另存本次更新说明.txt |
| visibility | 当前配置0（公开），玩家2026-11-11明确已公开；保留0，勿沿用早期私密2。1好友、2私密、3不列出 |

VDF 文件名应为 upload.vdf，不是 .vdf.txt；路径建议用正斜杠。
upload.ps1 已支持VDF的多行引号值和转义字符；简介/更新说明使用真实换行，保留BBCode标题、列表和作者链接，不压成单行、不用字面量反斜杠n或HTML换行替代。prepare-workshop.ps1负责VDF引号/反斜杠转义；upload.ps1逐字核对VDF与本地简介/更新说明，并核对成品哈希。

### 更新同一条目的最短流程

1. 执行 build.ps1，得到通过原生扫描的 build/。上传无需先部署到 Mods，也无需先生成zip。
2. 更新完整CHANGELOG.md、工坊精简稿WorkshopChangelog.md与WorkshopDescription.template.txt；保留所有已记录版本，不把历史行为误写成当前功能，鸣谢放末尾并保留链接，生成简介须小于8000 UTF-8字节。
3. 执行prepare-workshop.ps1：核对VERSION与build/validation.json及三个运行文件哈希，备份旧WorkshopUpload/content、VDF、简介/更新说明/版本记录与哈希记录至archive/，再同步新的运行文件、简介、完整CHANGELOG-source.md和当次更新说明；保留条目ID、封面与可见性。release.ps1仍只打包。
4. prepare-workshop.ps1末尾自动执行本地upload.ps1 -CheckOnly，检查配置、必要文件、多行文本及成品哈希，不登录或上传。
5. 直接显式运行 publish-workshop.ps1 发布，无需先另跑语言-CheckOnly。工具复用已在线Steam客户端会话，核对一次条目归属及公开状态；固定AppID4348910、条目3836818025，不修改可见性、不启动游戏。先提交english（运行文件、封面、标题/简介、更新说明），再提交schinese（同份标题/简介，不重复上传内容）。
6. 两次SubmitItemUpdate均返回k_EResultOK且无需接受新协议即视为发布完成，立即向玩家报告。工具自动保存简短的WorkshopUpload/upload-result-<版本>-languages.json（文件哈希及提交结果），成功状态为submitted；历史verified回执保留。失败状态incomplete，按分阶段结果处理；仅中文失败可用-SchineseOnly重试。
7. 发布后不再自动查询远端简介逐字比对、调用公开API、翻查上传日志或另写详细回执报告/专门记录提交。再次验证、页面显示、订阅下载及游戏实际加载由玩家人工处理；玩家明确要求时再查。-CheckOnly保留为可选只读核查入口，不是默认发布步骤。

旧SteamCMD入口upload.cmd/upload.ps1保留备用，只更新默认语言，不能替代上述双语言发布。它会询问登录用户名，密码/Steam Guard由玩家直接输入；SteamCMD登录可能替换Steam客户端会话，之后需恢复客户端在线再调用SteamUGC工具。

SteamCMD 等价命令（在其目录运行；含空格的路径加引号）：
`steamcmd.exe -tcp +login <登录用户名> +workshop_build_item "<upload.vdf绝对路径>" +quit`。
若已进入 Steam>，执行 `workshop_build_item "<upload.vdf绝对路径>"`。
登录输错可重新运行；交互窗口未显示时由玩家直接启动 upload.cmd，避免反复后台启动等待输入的实例。

2026-11-11实证：默认连接反复No Connection，-tcp后上传成功，upload.ps1已固定传入-tcp；不因此修改系统代理或Steam客户端配置。超长简介14277字节会在内容上传后提交时报Invalid Parameter，压缩至7524字节后成功；内容可能复用前次上传的Manifest，须以本次最终OK/Success与公开API的hcontent_file核实，不把早先失败的Manifest当作已提交。

语言由提交字段指定，不由文字内容识别：english栏也可以保存中文。SteamUGC.SetItemUpdateLanguage(handle, "english"/"schinese")指定本次标题及简介的语言，未调用时默认为english。每种语言使用独立更新句柄并提交，SetLanguage指定查询语言。网页清空中文内容被标题必填校验阻止，已改为保留两个语言并自动同步，不再尝试删除/回退。SteamCMD官方VDF示例未明确语言键，不凭猜测添加language字段。

官方依据：[SetItemUpdateLanguage](https://partner.steamgames.com/doc/api/ISteamUGC#SetItemUpdateLanguage)、[语言代码](https://partner.steamgames.com/doc/store/localization/languages)、[SteamCMD工坊说明](https://partner.steamgames.com/doc/features/workshop/implementation#SteamCmd_Integration)。API代码为english、schinese、tchinese；本项目目前只同步前两种。

### 人工按需核验（非发布必做步骤）

发布时可用 `-AdditionalPreview @('截图1.png','截图2.png')` 显式追加工坊截图。只在完整发布的english提交中调用AddItemPreviewFile，schinese提交不重复追加，主封面仍保持原配置。图片应为小于1 MB的PNG/JPG/GIF，存放在content外；回执记录图片路径与SHA256。该参数会追加图片，完整发布成功后不要为重试中文再次传图。PowerShell 5通过命令行传多个图片时使用 `-Command` 中的数组调用。

| 阶段 | 足够的证据 / 排查入口 |
| --- | --- |
| 发布完成 | 当前发布工具目标语言的SubmitItemUpdate均k_EResultOK，无需接受新协议；旧SteamCMD入口以本次提交Success为准 |
| 订阅文件已更新 | steamapps/workshop/content/4348910/3836818025/manifest.xml 版本正确，DLL SHA256 与上传回执一致 |
| 游戏已加载 | 本次启动的 Player.log 出现 audit: chemical_workflow_assistant，版本正确、source=Workshop、workshop=3836818025、running=True |

上传日志位于 WorkshopUpload/steamcmd/logs/（console_log.txt、workshop_log.txt）。
不能只看退出码0、旧日志中的 Success 或 VDF 已有ID；ID只表明目标条目，不证明本次提交成功。
首次新建后若ID仍为0，先查日志与账号条目，避免重试新建重复条目。

上传成功但订阅仍旧时，先检查 Steam“下载”页面是否排队/暂停、游戏自动更新是否关闭，必要时手动点更新。
本次实证：重启 Steam、取消再订阅后仍旧，手动更新后才下载完成；不用反复重新上传。
Steam 客户端 workshop_log.txt（本机 D:/Steam/logs/）及 steamapps/workshop/appworkshop_4348910.acf 可区分“服务端已识别新 Manifest”与“本机已安装”；按条目ID定向搜索即可，不通读全部日志。

订阅不会覆盖游戏 Mods/ 中的本地副本。切换订阅版时先退出游戏，将本地 ChemicalWorkflowAssistant 移到 Mods 外备份，保留开发工程和上传工作区，不改写订阅缓存。同ID本地优先时尤其要用游戏 audit 日志确认实际来源；重启游戏才能重新加载程序集。
