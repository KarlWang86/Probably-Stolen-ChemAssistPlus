# ChemAssistPlus 开发约定

- 继续工作先读 HANDOFF.md、CHANGELOG.md，架构见 ARCHITECTURE.md。主版本记录从0.3.2开始，旧归档不代表现行行为。
- 用户要求每次新版本都同步修改记录和handoff，写明行为、原因、验证结果和未完成事项；更新程序集、manifest、UI标题及使用说明的版本号。
- 用户约定不新增/运行测试，采用编译、游戏原生ModScanner、部署和用户游玩日志验证；不自行启动游戏或改写存档/消耗游戏材料。
- 源码在本目录，运行产物在 ../../Mods/ChemicalWorkflowAssistant。用Windows PowerShell 5执行build.ps1，-NoProfile -ExecutionPolicy Bypass；脚本UTF8 BOM。DLL锁定时等待用户退出，不强制结束游戏。
- 业务使用原生耗料/化学API，不直接造产品。不得用覆盖式TransferLiquidToChem向已有原液累加。投料、过滤、异常保液与原生过夜的行为边界见架构。
