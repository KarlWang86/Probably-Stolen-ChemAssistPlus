# Probably-Stolen-ChemAssistPlus

Probably Stolen 化学流程助手（ChemAssistPlus），当前版本见 [VERSION](ChemAssistPlus/VERSION)。

## 独立工程

开发主目录：`D:\work\Probably-Stolen-ChemAssistPlus\ChemAssistPlus`。
本仓库由游戏目录中的 Modding 完整复制而来，源码独立于 Steam 游戏安装目录；卸载游戏不会删除本仓库。
原游戏目录中的 Modding 保留为迁移时备份，后续开发请使用本仓库，避免两份源码分叉。

源码、历史测试、开发脚本、使用说明、研究记录及交接文档纳入 Git。
bin/、obj/、build/、staging/、releases/、ZIP、游戏/工具二进制与提取的 BIN 资源仅保留在本地，不上传。
游戏 DLL 仍需从自己的游戏安装中引用。

## 配置与构建

需要 .NET SDK（主 Mod 为 netstandard2.1，工具为 net8.0）及已安装的游戏。
在 `ChemAssistPlus/GameRoot.local.txt` 中写入游戏根目录的绝对路径（单行，无引号）；此文件已忽略。
本机已配置为 `E:\SteamLibrary\steamapps\common\Probably Stolen`。
也可设置环境变量 `PROBABLY_STOLEN_GAME_ROOT`，优先级高于本地文件。
未配置时兼容原有 Modding/ChemAssistPlus 相对目录。

在 `ChemAssistPlus` 目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

只构建并运行原生 ModScanner，输出到 build/；部署、打包与工坊上传需按开发文档单独执行。
开发约定见 [AGENTS.md](ChemAssistPlus/AGENTS.md)，当前状态见 [HANDOFF.md](ChemAssistPlus/HANDOFF.md)，操作见 [DEVELOPMENT.md](ChemAssistPlus/DEVELOPMENT.md)。

## 备份到 GitHub

在仓库根目录执行：

```powershell
git add .
git commit -m "Describe changes"
git push
```

只有提交并推送的内容才会保存在 GitHub。MIT License 见 LICENSE；游戏及第三方材料不因本仓库许可而改变权利归属。
