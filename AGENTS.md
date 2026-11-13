# 开发约定

- 每轮只先读本文件与 docs/HANDOFF.md。按问题查 docs/ARCHITECTURE.md 模块表、docs/NATIVE_API.md 或 docs/MOD_REFERENCE.md；docs/CHANGELOG.md、Research/ 不常规通读。
- 先用 rg 搜索方法，再读相关片段；接口结论不足、程序集哈希改变或相关异常时才定向查询。完整 IL 保存 Research/，只将短结论及证据位置写回接口文档。
- 一轮聚焦一个可验证目标，先交付最小可见/可运行模块，再依玩家日志迭代。日志写关键步骤、实际用量、设备、进度和停止原因，不逐帧输出。
- 日常运行时源码只改 ChemAssistPlus/；文档放 docs/，操作脚本放 scripts/，辅助工具放 Tools/；研究、截图、历史测试分别放根目录 Research/、Screenshots/、Tests/；不改订阅缓存、不自动上传、不自行启动游戏、不改存档/消耗材料、不强退游戏。不新增或运行测试。
- 使用原生耗料/化学 API，不直接造产品；物料与状态边界见 docs/ARCHITECTURE.md，具体 API 风险见 docs/NATIVE_API.md。
- PowerShell 脚本用 UTF8 BOM；Windows PowerShell 5 执行：powershell -NoProfile -ExecutionPolicy Bypass -File <脚本>。
- build.ps1 只编译到 build/ 并运行原生 ModScanner，不部署、不打包。离线 PASS 不代表游戏内验收。
- 专项本地验证才显式运行 deploy.ps1；先核对同类工坊启用状态，用参数记录已确认状态。DLL 锁定时等玩家退出。release.ps1 仅显式留档/发布时打包。
- VERSION 是唯一版本源；构建同步程序集、清单、UI/日志和使用说明。新版本更新 docs/CHANGELOG.md；当前状态只记 docs/HANDOFF.md，架构变化才更新 docs/ARCHITECTURE.md。
- 推进新版本前先检查 Git；若有旧版本未提交内容，先按旧版本提交，再修改 VERSION 和开发新版本，避免多个版本混在同一次提交。2026-11-12 玩家明确本次0.4.1可合并提交，此后按本约定执行。
- 发布以目标语言的Steam提交回调均成功为完成标准；保留必要的发布前检查及简短自动结果文件，不再自动进行发布后远端查询、逐字比对、公开API/日志复核或额外撰写详细回执报告。再次验证由玩家人工处理，明确要求时再查。
- 工坊简介只写用法、功能和当前版本改动，保留末尾鸣谢；不重复全部历史版本。完整历史保留在CHANGELOG.md及工坊更新记录，WorkshopChangelog.md只提取当前VERSION条目。
