# Probably-Stolen-ChemAssistPlus

本页为开发者的仓库与构建说明。模组功能和用法见[根目录 README](../README.md)，当前版本见 [VERSION](../ChemAssistPlus/VERSION)。

## 独立工程

从仓库根目录执行开发操作；运行时源码项目位于 `ChemAssistPlus/`，不依赖仓库的绝对路径。
本仓库由游戏目录中的 Modding 完整复制而来，源码独立于 Steam 游戏安装目录；卸载游戏不会删除本仓库。
原游戏目录中的 Modding 保留为迁移时备份，后续开发请使用本仓库，避免两份源码分叉。

源码、历史测试、开发脚本、使用说明、研究记录及交接文档纳入 Git。
bin/、obj/、build/、staging/、releases/、ZIP、游戏/工具二进制与提取的 BIN 资源仅保留在本地，不上传。
游戏 DLL 仍需从自己的游戏安装中引用。

## 配置与构建

需要 .NET SDK（主 Mod 为 netstandard2.1，工具为 net8.0）及已安装的游戏。
辅助工具默认仍为net8.0；只有.NET 9 SDK及运行时的机器可先设置 `$env:PROBABLY_STOLEN_TOOL_FRAMEWORK = 'net9.0'`，该设置只影响三个辅助工具，不改变Mod的netstandard2.1目标。离线包源配置要求目标框架的引用包已在本机安装。
在仓库根目录的 `GameRoot.local.txt` 中写入游戏根目录的绝对路径（单行，无引号）；此文件已忽略，不包含在源码发布包中。
也可设置环境变量 `PROBABLY_STOLEN_GAME_ROOT`，优先级高于本地文件。
未配置时尝试仓库的父目录作为游戏根目录；独立克隆请显式配置。

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

只构建并运行原生 ModScanner，输出到 build/；部署、打包与工坊上传需按开发文档单独执行。
开发约定见 [AGENTS.md](../AGENTS.md)，当前状态见 [HANDOFF.md](HANDOFF.md)，操作见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 目录分工

```text
仓库根目录/
├─ ChemAssistPlus/          C#运行时源码、csproj、manifest.xml、VERSION
├─ docs/                    开发文档、使用说明、更新记录、工坊文案
├─ scripts/                 构建、扫描、查询、部署、打包与工坊发布入口
├─ Tools/                   NativeScan、AssemblyQuery、WorkshopPublisher源码
├─ Research/                调查报告、原生IL及历史备份，不参与Mod构建
├─ Screenshots/             README及工坊使用的截图
├─ Tests/                   历史测试存档，保留但不运行
├─ build/                   当前构建与扫描结果、工具成品（忽略）
├─ releases/                显式打包的安装包与源码包（忽略）
├─ AGENTS.md                仓库开发约定
├─ Directory.Build.props    运行时项目和辅助工具共用游戏引用路径
├─ NuGet.Config             共用离线包源配置
└─ GameRoot.local.txt       本机游戏路径（忽略）
```

`bin/`、`obj/`是各项目自身生成的缓存，可留在项目内。所有脚本按自身位置解析仓库路径；无需进入源码目录。源码包保留同样的目录关系和构建入口，按白名单排除Research、历史Tests、游戏DLL及本机路径配置。

## 备份到 GitHub

在仓库根目录执行：

```powershell
git add .
git commit -m "Describe changes"
git push
```

只有提交并推送的内容才会保存在 GitHub。MIT License 见 [LICENSE](../LICENSE)；游戏及第三方材料不因本仓库许可而改变权利归属。
