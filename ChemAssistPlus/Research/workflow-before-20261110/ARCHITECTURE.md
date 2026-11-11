# 化学流程助手架构 0.3.4

新对话先读 HANDOFF.md，再读 CHANGELOG.md。主记录从0.3.2开始；旧记录仅在 Research/HANDOFF-before-0.3.2.md 归档，旧行为不可作为当前依据。
开发目录 Modding/ChemAssistPlus；安装目录 Mods/ChemicalWorkflowAssistant。csproj显式列举源码，游戏/Harmony DLL不打包。

## 流程与状态

Unity主线程PlusPump检查F10、任务、延迟晨报和每2秒库存/供电。自动处理默认开启，只拦最终EndOfDayUIManager.OnEndDayButtonClick；只读预检后暂缓原生过夜，送瓶后恢复同一按钮一次。平时放料不开工，无任务正常过夜。
仅输入格首种原料。按酒精选整数份，未选固体留格；开工重检完整输入快照与选择列表。
原生BlendInto→同比补溶剂→本批原生加热/反应API→去蛋白/脂肪→滤布→滤芯→含氯药片→蒸发器原液槽→原生EndNight。产物留同一工作瓶，馏出槽回收溶剂，不模拟蒸发或直接生成产品。
失败关火、保留剩余物料、续原生过夜一次，开关不自动关闭。切档清引用/来源偏好/报告/挂起过夜。仅实际工作且次日弹窗开启才自动F10；供电小通知独立。

## 模块

- Core.cs：入口、配方、递归物品图、按角色找机器槽位；ProbeResult保存输入快照和实际执行列表。
- Plus.cs：生命周期、页面、Sources/TakeLiquid、AddSolution、FilterBatch。酒精按当前浓度优先，缓存只在同浓度时优先；其他试剂保留已用瓶直到耗尽/移走。酸碱用正净量（本侧减反侧）计算浓度。SolventTargetForParts共用预检与执行的有效溶剂目标公式；原溶液按各来源真实浓度同比转移。AddLiquid累加，禁止覆盖式TransferLiquidToChem。
- BatchPlan.cs：逐份损耗、整数份数、原生比例分割模拟试剂和峰值容量；没有余浆暂存路径。
- Preflight.cs：只读整批检查。已有净酸碱时沿同方向去蛋白；两侧相等时优先库存足够且来源净浓度高的一侧。DoseMath按实际成分换算，无97%常量。
- Workflow.cs：主线程迭代器、实际耗料与步骤日志；夜间快速反应每32拍让帧，提取最多900拍。开工复核选择后才移动/消耗。
- FilterPlan.cs：所有可用滤材按耐久升序计划，安全换装并归还旧件。Plus.FilterBatch按真实耐久和Liquid元数据限制每份大小，逐组分调用原生HandleFilter，耗尽重选同类。已处理液体同步缓冲，finally回瓶，避免同一份重复过滤；无跨帧悬空液体。
- Purification.cs：真实散片和瓶内片合计，散片优先，日志记录来源类型；DissolveInto耗片、HandleChlorine结算。原生要求有水，化学污染保留当前总体积*0.007f的下限。无作用不投片；数量、具体污染量/下限和原因进入简洁报告。原生摘录见Research/native-0.3.4-il.txt。
- AutoWorkflow.cs：过夜防重入、开关、晨报、蒸发器预检；WaitingForDistillation共用自动入口与面板判断，无新原料/待提取原浆且设备全占用时作为正常等待，不报告占用失败。
- PowerMonitor.cs：持续监测已有湿料蒸发器；CanDrawPowerSource按machinery_drawn_power核查启动电量。面板常驻原因、状态变化和每天开始延迟小通知；不换电池。
- InventoryReport.cs：只读预览、BeginDay同瓶核对与供电提醒调度。
- CompositionPanel.cs / PanelLayout.cs：450x600、3:4；溶剂每项使用/库存，五阶段各一行剂量/纯度，6px滚动条，固定四控件底栏；成型入口隐藏，接口保留。
- build.ps1 / validate.ps1 / Tools/NativeScan：编译、安装、原生扫描；Windows PowerShell 5用UTF8 BOM脚本，执行时-NoProfile -ExecutionPolicy Bypass。

## 验证

按用户约定不新增/运行测试、不自行开游戏、不改存档。编译/原生扫描不能等价于实际耗料、过滤接续或UI验证，待玩家正常游玩日志。
