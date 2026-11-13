# Mod 开发目录

本目录保存开发源码和研究资料，游戏不会直接加载这里的脚本。

当前项目为 `ChemAssistPlus`（化学流程助手，独立 Mod）。继续开发请先读：

1. [架构说明](ChemAssistPlus/ARCHITECTURE.md)：模块职责、执行路径、状态与原生接口边界。
2. [交接记录](ChemAssistPlus/HANDOFF.md)：用户要求、当前版本、未完成项、存档结论与验证方法。
3. [使用说明](ChemAssistPlus/使用说明.txt)：已发布版本的玩家操作方式。

实际运行目录：`D:/software/steam/steamapps/common/Probably Stolen/Mods/ChemicalWorkflowAssistant`。
源代码目录：`D:/software/steam/steamapps/common/Probably Stolen/Modding/ChemAssistPlus`。

编译后必须把 DLL 和清单更新到运行目录；游戏已经加载的程序集需重启才能更新。
