# 常用操作

开发主目录现为 D:\work\Probably-Stolen-ChemAssistPlus\ChemAssistPlus。独立目录需在 GameRoot.local.txt 中填写游戏根目录（单行、无引号），或设置优先级更高的 PROBABLY_STOLEN_GAME_ROOT 环境变量；本地路径文件不提交。项目引用和脚本共用该配置，详见仓库 README。


在本目录执行，Windows PowerShell 5，统一前缀：
`powershell -NoProfile -ExecutionPolicy Bypass -File`。

| 目标 | 接在前缀后的命令 | 输出与边界 |
| --- | --- | --- |
| 日常构建 | .\build.ps1 | build/ 中 DLL、清单、说明、原生扫描结果；不部署/打包 |
| 重扫已有构建 | .\validate.ps1 | build/验证结果.txt 与 validation.json（记录文件/游戏哈希） |
| 准备工坊内容与排版 | .\prepare-workshop.ps1 | 核对已扫描构建、备份旧上传工作区、同步3个运行文件/多行BBCode简介/逐版本记录/鸣谢与VDF；配置预检，不登录/上传 |
| 查签名与参数 | .\query-api.ps1 -Type WaterHelper -Method RemoveAndReturn | 终端短结果，完整结果保存在 Research/ |
| 查指定方法 IL | .\query-api.ps1 -Type ChemicalHelper -Method HandleChlorine -IL | 仅显示保存路径；类型/方法支持单独 * 匹配全部 |
| 查参考程序集 | .\query-api.ps1 -Assembly <DLL路径> -Type <类型> -Method <方法> | 只读元数据，不执行目标方法 |
| 专项本地部署 | .\deploy.ps1 -WorkshopState Disabled | 用户已核对禁用同类工坊项时；重新扫描后只复制3个运行文件 |
| 同ID工坊并存部署 | .\deploy.ps1 -WorkshopState SameIdLocalPriorityConfirmed | 已核对同ID本地优先且无其他同类项冲突时使用 |
| 留档/发布打包 | .\release.ps1 | releases/<版本-时间>/ 下安装包+源码包；不自动上传 |

VERSION 为版本唯一来源，build 自动同步程序集、清单、UI/日志及使用说明；修改记录中的历史版本不替换。
构建失败会撤销旧 validation.json，部署前重新扫描；扫描器和查询工具放 build/tools/。
原生扫描在 build/scan-input/ 对3个运行文件的副本执行，再核对原文件哈希，避免工具被误判为运行载荷。
项目 NuGet.Config 清空外部包源，使用本机 SDK 引用包。
不需要部署时只执行 build.ps1，不必查询工坊状态。

## 文件分工

AGENTS.md 固定约定；HANDOFF.md 当前状态；ARCHITECTURE.md 按模块定位；
NATIVE_API.md 原生接口缓存；MOD_REFERENCE.md 参考实现缓存；CHANGELOG.md 历史变更；
Research/ 完整IL、旧调查与整理前备份。历史 Tests/ 保留但不运行，不纳入发布源码包。
CHANGELOG.md按每个有据可查的版本记录完整变化，已从归档补回早期版本，缺失版本不补写。WorkshopChangelog.md为工坊逐版本精简稿，必须与完整记录保持相同版本集合及顺序；Steam简介上限为8000 UTF-8字节，生成与上传预检均要求小于8000，不能只查字符数。WorkshopDescription.template.txt按用法/功能→逐版本记录→末尾鸣谢排版，prepare-workshop.ps1嵌入WorkshopChangelog并另存完整CHANGELOG-source.md。

旧 bin/obj 是构建缓存，staging/ 是历史安装备份，既有 zip 未改动。日常输出统一到 build/。
源码发布包按白名单收录源码/文档/脚本，不包含原生与参考 DLL、Research 原始材料或测试。

## Steam 创意工坊上传（需要发布时才读）

从[已完成的上传对话](thread://01a1c121-bf52-73b2-8f9e-19c6db05c1d6?hostId=local)提取，并与现有脚本/VDF/回执核对。此处是操作知识；历史上传证据见 Research/SteamWorkshop-20261110.md，当前版本状态见 HANDOFF.md。

### 目录与参数

上传工作区为游戏根目录的 WorkshopUpload/，与 Modding/ 同级；不用每次新建工作区。
content/ 只放 ChemAssistPlus.dll、manifest.xml、使用说明.txt。
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
4. prepare-workshop.ps1末尾自动执行upload.ps1 -CheckOnly，也可单独运行：`powershell -NoProfile -ExecutionPolicy Bypass -File <游戏根目录>\WorkshopUpload\upload.ps1 -CheckOnly`。不登录或上传；验证配置、必要文件、多行文本一致性及成品哈希。工坊实际渲染需上传后再核实。
5. 需要上传时由玩家双击 WorkshopUpload/upload.cmd，或在交互终端执行 upload.ps1。脚本询问登录用户名，随后执行 SteamCMD 登录与 workshop_build_item。密码/Steam Guard 由玩家直接在 SteamCMD 输入；既有缓存登录曾成功复用。
6. 核实本次提交结果，保存 upload-result-<版本>.json，至少记录版本、条目ID、AppID、内容 ManifestID、DLL SHA256、可见性与成功证据。现有 upload.ps1 不会自动生成该回执。

SteamCMD 等价命令（在其目录运行；含空格的路径加引号）：
`steamcmd.exe -tcp +login <登录用户名> +workshop_build_item "<upload.vdf绝对路径>" +quit`。
若已进入 Steam>，执行 `workshop_build_item "<upload.vdf绝对路径>"`。
登录输错可重新运行；交互窗口未显示时由玩家直接启动 upload.cmd，避免反复后台启动等待输入的实例。

2026-11-11实证：默认连接反复No Connection，-tcp后上传成功，upload.ps1已固定传入-tcp；不因此修改系统代理或Steam客户端配置。超长简介14277字节会在内容上传后提交时报Invalid Parameter，压缩至7524字节后成功；内容可能复用前次上传的Manifest，须以本次最终OK/Success与公开API的hcontent_file核实，不把早先失败的Manifest当作已提交。

默认语言简介与独立schinese简介可能不同：SteamCMD默认更新english，已存在的简体中文记录会覆盖默认简介。公开API默认简介一致不足以证明中文页同步，须分别检查english与schinese页面。玩家要求暂时只保留默认简介，独立中文简介删除/回退任务仍需工坊网页已登录会话；不要未经核实声称已删除。

### 上传、下载、运行分别核验

| 阶段 | 足够的证据 / 排查入口 |
| --- | --- |
| 上传已提交 | 本次 console_log.txt 有 Committing update...Success / Success；workshop_log.txt 有 Upload finished for workshop item 3836818025 : OK，核对时间、ID和新内容 ManifestID |
| 订阅文件已更新 | steamapps/workshop/content/4348910/3836818025/manifest.xml 版本正确，DLL SHA256 与上传回执一致 |
| 游戏已加载 | 本次启动的 Player.log 出现 audit: chemical_workflow_assistant，版本正确、source=Workshop、workshop=3836818025、running=True |

上传日志位于 WorkshopUpload/steamcmd/logs/（console_log.txt、workshop_log.txt）。
不能只看退出码0、旧日志中的 Success 或 VDF 已有ID；ID只表明目标条目，不证明本次提交成功。
首次新建后若ID仍为0，先查日志与账号条目，避免重试新建重复条目。

上传成功但订阅仍旧时，先检查 Steam“下载”页面是否排队/暂停、游戏自动更新是否关闭，必要时手动点更新。
本次实证：重启 Steam、取消再订阅后仍旧，手动更新后才下载完成；不用反复重新上传。
Steam 客户端 workshop_log.txt（本机 D:/Steam/logs/）及 steamapps/workshop/appworkshop_4348910.acf 可区分“服务端已识别新 Manifest”与“本机已安装”；按条目ID定向搜索即可，不通读全部日志。

订阅不会覆盖游戏 Mods/ 中的本地副本。切换订阅版时先退出游戏，将本地 ChemicalWorkflowAssistant 移到 Mods 外备份，保留开发工程和上传工作区，不改写订阅缓存。同ID本地优先时尤其要用游戏 audit 日志确认实际来源；重启游戏才能重新加载程序集。
