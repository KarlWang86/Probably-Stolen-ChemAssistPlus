# 新对话交接记录

更新日期：2026-11-09。当前源码与 Mods 安装版本为 **0.3.2**（历史压缩包未更新）。

## 最新开发约定（用户明确要求）

- 不用继续写测试代码，只打印关键步骤。
- 编译并更新 Mods 后，用户运行游戏，将日志和新需求回传，再继续迭代。
- 本轮新增的隔离测试源码已撤下；旧数学测试保留作历史资料，build.ps1 不运行测试。
- 保留必要的编译和原生 ModScanner 检查；不要把这些写成游戏内完整验证。

## 已接入功能

1. **低配板控温**：HeatingControl.cs 使用原生 Harmony Prefix，限定 busy/job/activePlate/工作瓶。0/1模块在 HandleHeating 前恢复本批目标；保留原生速度、化学tick与反应。开工检查补丁确实加载；模块变化、瓶子移走、关火、超温、原生异常时阻止该次加热并由工作流停止。正常/取消/失败出口关火并恢复原目标。高配板目标被修改时让出控制。
2. **空水罐自动换滤材**：FilterPlan.cs 只读规划；优先已装正确滤材的空 water_pitcher，次选松散备用滤材。执行前重新规划。先把旧滤材归还备用滤材所在库存，再原生装入新滤材；需真实空位，采用保守空间检查，不用 swapBuffer，不拆其他水罐滤材。安装失败尝试恢复旧滤材并停止。缺滤材默认提示并跳过，严格模式阻止；运行中耐久不足则停止并归还原液。
3. **刷新和分步预检**：InventoryReport.cs 在F10打开/选项变化/刷新按钮时更新；忙时保留实际日志。Preflight.cs 先列所有受阻和跳过提示，再列来源浓度、有效库存、溶液用量、溶剂比、设备和完整过程。Probe失败也显示设备/滤材库存。干料成型独立检查，不被湿料条件拦截。
4. **试剂计算**：去蛋白使用净酸碱差额，与原生HandleProteinPrecipitation一致；优先同类净酸/碱。部分试剂不足时预检阻止，避免明知不足仍部分投料再报错。溶剂带入杂质计入主要沉淀用量预估；后续试剂杂质和库存变化仍在执行期复核。
5. **关键步骤日志**：版本初始化、刷新/预检、实际投料、控温方式、每10游戏秒温度/原料进度、过滤换装、关火恢复、停止/异常。运行中不每帧写日志。

## 保持的边界

- 独立Mod ID chemical_workflow_assistant，不依赖或修改原作者DLL，不打包游戏/Harmony DLL。
- 按瓶中原溶液比例转移，水和杂质一起进入；不自动分离纯有效成分。
- 滤布→滤芯顺序固定，不用净水器。
- 自动处理到过滤后停止，蒸发仍由玩家原生过夜；干料再点批量成型。
- 保持灰底白字和分辨率适配。
- 不修改用户存档，不为验证自行消耗现有原料。

## 编译、运行和日志

在游戏根目录运行：`& 'Modding/ChemAssistPlus/build.ps1'`。
脚本编译、更新Mods并执行原生扫描，不运行测试，不启动游戏、不修改存档。
当前版本须完全退出并重新启动游戏后加载；F10窗口标题为0.3.2。
日志：`C:/Users/wangkan/AppData/LocalLow/Questing Goose Studio/Probably Stolen/Player.log`。
用户运行后发送该日志与新需求。可搜索“化学流程助手”“[预检]”“[加热进度]”“[加热清理]”。
未完成游戏内端到端验证，不据离线检查宣称全部可用。

## 本机存档与历史记录

本轮重新按修改时间核对，最新仍为 save_16.es3，436256字节，2026-11-09 19:15:41；没有改写。
上轮完整调查和旧需求保存在 Research/HANDOFF-0.1.3.md；其中“待接入”是历史状态，勿当成当前状态。
原样草稿 Research/Drafts 仅供历史研究，不参与编译；正式实现位于根目录。
原作者DLL研究基线SHA256：CB4623B78622F869AFBE16122C719C195DA696ABC9FC2708C8BF82905DE8E826，本轮核对未改变。
安装包与源码包位于游戏根目录“化学制剂助手”，使用0.2.0文件；旧版本包保留。

## 本机 2026-11-09 日志反馈与重新部署

- 用户提供 xiaok 的 Player.log、GameLog_2026-11-09_20-27-04.txt 和 save_19.es3。Player.log 显示 chemical_workflow_assistant v0.1.3、Not loaded: Unknown，最终 10 registered / 0 enabled；本次 F10 无响应首先是模组未获信任加载，不是已经证实的按键代码故障。不要绕过游戏信任机制；由玩家在原生模组管理完成确认与启用。
- 本机 Mods 清单实际仍为0.1.3，DLL也与源码构建产物不一致。现已执行 build.ps1，重新编译并部署0.2.0到 Mods/ChemicalWorkflowAssistant；编译0警告0错误，原生ModScanner通过（249 methods，additional capabilities none）。实际加载及游戏内流程仍待玩家重启、确认启用后反馈。
- 本机使用 Windows PowerShell 5，脚本须保存UTF-8 BOM；清单须显式按UTF-8读取。游戏ModScanner使用新.NET API，现由 Tools/NativeScan (.NET 8控制台工具) 调用同一原生扫描器；这是部署检查工具，不是新增测试。游戏DLL未修改。
- 用户同时反馈第二天未生成蒸发后产物；原因尚未确认。用户最新指示“那先更新版本吧”，先完成部署。此轮未修改蒸发逻辑或任何存档。save_19含非标准转义，只在内存中兼容处理过顶层字段，尚未完成机器及原液状态分析。
- 本机日志路径为 C:/Users/xiaok/AppData/LocalLow/Questing Goose Studio/Probably Stolen/Player.log；旧交接的 wangkan 路径不适用于本机。
## 0.2.1：已启用后的异常与自动搅拌（2026-11-09）

- 最新 Player.log（20:47:20，GameLog_20-43-39）确认0.2.0 running=True trust=Match，F10库存刷新已执行。无产物此次首先有确定的预检异常：MachineHelper.GetModuleInv → MachineView.Walk → FindPlate → RefreshInventoryReport/Preflight 抛 InvalidCastException。没有制作步骤完成日志。
- 原生GetModuleInv固定读取body index 1，并强转GameGridInventory；加热板、搅拌机、成型机的module在index 0，index 1是其他槽位。Core.MachineView.Walk改为按module角色面板定位，保持原生模块数量/效果不变。
- 用户明确说明搅拌机只放原材料，要求自动搅拌。Probe现在可为有固体、无瓶的搅拌机只读规划工作瓶：玩家可操作的空液体容器、位于网格库存、直接父物品不是机器、排除过滤水罐、原生槽位可接受，优先最大容量。预检仍检查整批峰值容量；不造瓶、不倒空有料瓶。PrepareBatch在预检通过后移动该瓶，再原生BlendInto；开工前复核位置。预检和实际装瓶均有文字/日志。
- 已重新编译安装0.2.1，0警告0错误，原生扫描PASS（252 methods，无额外capabilities）。无新增测试，无游戏内端到端操作，无存档修改。用户须完全重启，F10标题0.2.1；如果游戏提示新版重新审核，在原生界面确认。
- save_21.es3为最新新档，dayCounter=2。只读解析见机器存在；完整成分与蒸发状态尚未完成分析。修复异常不能表述为蒸发链路已通过。
- 当前启动方式仍是点击“一键处理到过滤”：原料放输入格，库存有空瓶即可。处理后原液留在搅拌机，用户手动移至蒸发器并过夜，再批量成型。没有后台监视自动开工，也没有新增过夜蒸发逻辑。
## 0.2.2 当前行为（优先于上面的历史描述）

- 用户要求将一键处理改为开关：原料和容器就绪就自动开工，在当晚结束前完成并送入蒸发器。已改为F10“自动处理”开关，营业期间每2游戏秒检查所选搅拌机。需输入格有固体或瓶中有可识别湿半成品，且工作瓶实际位于容器槽；不在无工作瓶时自动开工。无需每批点击按钮。初始关闭、切档/禁用/手动停止关闭；关窗继续，暂停等待；异常关闭开关避免反复耗料。
- 新增AutoWorkflow.cs并加入显式Compile。预检阻碍每10游戏秒重试，日志按预检结论去重。只监视选中的一台搅拌机，不自动成型、不自动结束营业。
- 湿料预检增加蒸发器空槽、原生槽位接受与可用电池条件；尊重指定设备。过滤后重新选择/检查并原生MovePot至evaporator的container槽，日志“步骤7 · 已自动送入蒸发器”。运行期设备不可用则停止，过滤后原液保留，不重复过滤。玩家需等步骤7完成再结束营业，仍由原生过夜蒸发；没有拦截提前结束营业，也没有模拟蒸发。
- 新日志20:57:03确认0.2.1搅拌/投料成功，但刚启动加热即报“玩家修改了加热设置”。已核对原生IL：ModifyTag第三参数true跳过ValidateShapeState，GetTagReadonly读取modifiedState缓存，故开关仍读0。加热启停/目标与低配前缀现在用false刷新缓存；成型机选定配方同样改为false。新增加热设置生效日志。不增加原生速度或额外化学tick。
- 已构建/部署0.2.2，0警告0错误，原生ModScanner PASS（260 methods，capabilities none），安装DLL与构建一致。按约定不写/运行新测试，不操作游戏、不改存档。完整营业期自动处理到蒸发器的游戏验证待用户重启反馈。
## 0.2.3 用户纠正触发时机与原生API快速结算（最新，覆盖旧约定）

- 用户纠正0.2.2：“放入原材料就开始自动流程”不对，必须当天结束前才开始；并要求参考已订阅一键化学，用API结算，避免实时加热等待。
- 已移除Update中的TickAutoProcessing与定时自动开工。开关仅待命。新增Harmony Prefix拦截EndOfDayUIManager.OnEndDayButtonClick（最终过夜按钮，原方法黑屏回调会EndNight→BeginDay→SaveGame）。有本批湿料则return false暂缓，CloseUI(false)并打开助手显示处理，Run完成送入蒸发器后由ResumeEndDay以重入标记调用同一原方法一次。没有湿料正常过夜；重复点击/忙时不重复开工。失败/预检不通过不续过夜，显示原因；补齐后重新点击。CancelJob/切档/禁用清除pending manager/store，避免以后意外过夜。
- 原生IL已核对：EmporiumEntry.Update不按storeState限制onOneSecondItemFunc；EndOfDayUIManager.CloseUI(false)不改Time.timeScale，不需要改营业状态或自己推进全局时间。
- 参考订阅DLL OneClickCraft.dll（SHA256仍CB4623...8E826），OneShot.RunCore使用HeatStep/SetTemp循环900次并调用ChemicalHelper.HandleTemperatureReaction。当前实现没有引用它的DLL，不复制其原液剥离/净水器/即时蒸发逻辑。
- 用户此次授权覆盖旧handoff“不额外化学tick/不快速结算”边界：pendingEndDay存在时仅本批调用MachineHeatingPlate.HandleHeating(slot,2,plate)，刷新工作瓶ValidateShapeState，再ChemicalHelper.HandleTemperatureReaction并刷新；保留原生模块加热参数和耗料反应，不直接写温度/生成产品。提取上限900拍、达安全温度后连续180拍无进展停止，每32拍yield，TickJob此时每帧推进而非0.25秒。沉淀同样按原生反应快速推进（原有480拍/轮上限）。蒸发仍交给当晚原生EndNight，成型仍手动另行原生按钮批量执行。
- 已构建并部署0.2.3，0警告0错误，原生扫描PASS（264 methods，无额外capabilities）；没有新测试、没有修改存档或自行运行真实游戏。完整“点击结束当天→快速处理→蒸发器→当晚结算”验证待用户重启后日志。
## 0.2.4 全部输入原料与提取物去向（最新）

- 用户新档save_24，日志21:16:46：0.2.3确实结束当天才触发，处理1份梦帽菇，14拍提取7.15mL，过滤后把工作瓶#26送入蒸发器#17并继续原生过夜。
- 只读解析save_24（dayCounter=2）确认：搅拌机#15剩梦帽菇#71/#72；蒸发器#17 child uuid37/原液槽node4为瓶装水#26，CURRENT_PART_DREAM_CAP_EXTRACT=7150，水和酒精均0；另重金属1、化学污染物59、酸1微升。电池#19剩34能量。产物没有丢失，也没换瓶，仍在蒸发器原液槽原工作瓶；馏出槽用于溶剂收集，不是药品产物槽。
- 用户要求全部原料：NativeChem.Blendables取得所选输入格全部有化学体积的可搅拌物；Probe保存Solids快照，保留Solid首项用于兼容旧分支。不同配方或与所选成品不匹配的原料整批阻止。Estimated逐份按原生舍入/损耗累加成分；Preflight按全部原料计算容量峰值、溶剂和沉淀试剂。开工首拍核对输入列表未变化，并重做整批预检，随后对每份原生BlendInto。日志列n/总数、物品ID和最终入瓶量。不从仓库补取固体。
- InventoryReport加EvaporatorContents：F10刷新即显示蒸发器原液槽瓶号、提取物和剩余挥发液、干燥状态；Probe失败也会附上。新增PlayerStore.BeginDay Postfix只读输出[过夜产物核对]，不移动或消耗物品；异常只记录不传播。步骤7明确说明产物仍留原液槽原瓶。
- 已编译安装0.2.4，0警告0错误，原生扫描PASS（275 methods，无额外capabilities）。无新测试、无改存档，整批游戏验证待用户重启反馈。最新实际已验证的产物数据来自0.2.3的save_24，不能声称0.2.4已游戏内验证。
## 0.2.5 默认开启、夜间静默、翌日报告（最新，覆盖旧行为）

- 用户反馈当晚F10弹出且开关关闭，要求默认开启、当晚不弹、次日弹。日志21:25:06确认三份梦帽菇全部搅拌，但酒精跨两瓶补料时实际量175→-135.5→96mL，随后缺酒精异常，CancelJob关闭开关。
- 已核对原生IL：ChemicalHelper.TransferLiquidToChem调用AddLiquidToChemFromList，后者对每个组分SetInt而非Add，不能用于向已有溶液累加。改为WaterHelper.RemoveAndReturn(source,pour)按比例取全部组分，AddSolution按id合并后逐组分WaterHelper.AddLiquid累加。用公开HandleReceiveLiquidTemp计算整份混温并刷新缓存，再添加组分后还原混温。滤材分批回瓶也用同一累加函数，避免同种成分被后一批覆盖。AddLiquidToChemFromList/TransferLiquidToChem已不用于业务转移。
- autoProcessing静态默认true，ResetSession默认true（清旧任务/报告）；手动关闭或停止才关闭。CancelJob新增disableAuto与continueEndDay参数，运行异常保留开关，不在白天重试；TickJob异常在关火/Dispose后继续原生过夜一次，并记录失败总结。禁用/切档取消上一任务，不续过夜。
- BeforeEndDay不再Open。没有湿料/Probe受阻/预检受阻直接排队结果并放行原生过夜；有任务则暂缓原生流程，关闭原生结算UI并隐藏已有助手窗口，静默快速处理后续过夜。相同pending请求仍防重复。
- QueueMorningReport保存当晚结果；PlayerStore.BeginDay原有只读产物核对钩子补充次日瓶号/成分，排队1.5秒（unscaled）后PlusPump.TickMorningReport调用OpenPanel(false)显示报告。Open()手动打开仍实时刷新，早晨报告不被新一轮预检覆盖。报告/任务切档清除。
- 已构建部署0.2.5，0警告0错误，原生ModScanner PASS（286 methods，无额外capabilities）。没有新增测试，没有改存档或自行运行游戏；跨瓶补料与次日弹窗仍需用户重启后反馈。
## 0.2.6 无原料不装瓶、无处理不弹窗（最新）

- 用户要求：无原材料不自动放容器；没有自动处理步骤，第二天不弹F10。
- 最新日志21:52:16确认0.2.5已完成两次各3份梦帽菇整批、跨两瓶酒精补料正确（214.5/214.501mL）、24拍提取21.451mL并送蒸发器。日志没有“自动装入库存空瓶”记录，不能从该日志断言用户看到的无原料放瓶由哪个调用造成。
- Probe现在先确认实际固体或含可识别原浆/提取物的工作瓶、识别配方、核对全部固体匹配，再允许规划自动空瓶；无原料立即返回且提示不会装瓶。PrepareBatch自动移动空瓶前再次检查Solids非空、仍在输入格、化学体积和配方成分大于0。只读刷新不移动瓶子。
- 新增nightDidWork，由真正的原生搅拌、实际补料取出、加热/沉淀处理、过滤或送入蒸发器标记。仅pendingEndDay批次可标记。QueueMorningReport在没有执行步骤时只写日志，不设置awaitingMorningReport；无原料、Probe失败、预检受阻、开工前失败均不自动弹窗。处理后失败仍可显示结果。
- 每次新结束当天请求（非恢复调用/非重复待处理）清除之前报告和工作标记；切档也清除，防旧报告跨夜弹出。
- 已编译部署0.2.6，0警告0错误，原生ModScanner PASS（290 methods，无额外capabilities）；无新测试、无改存档，当前优化的游戏验证待用户重启反馈。
## 0.3.1 最新核心重构（用户第1~10点需求，覆盖旧UI/过滤/配额约定）

- 删除指定设备、计算器、机器/成品/咖啡产线下拉、最低纯度输入、严格过滤开关、重复停止按钮。自动选择有原料的搅拌机，FirstKind只选输入格第一种可搅拌原料的全部同ID物品，其他种类不动。咖啡固定酒精线。物理过滤严格检查默认true。
- F10单页：用量摘要、三阶段成分条、干料批量成型；底部同一行自动处理/次日弹窗/刷新/关闭。两个开关默认true，自动开关同时停止任务；次日弹窗偏好进程内保留，关闭不自动弹，无实际处理也不弹。无界面时的结束当天触发/静默过夜语义沿用0.2.6。
- BatchPlan.cs为只读整批用量/酒精配额/余浆规划。按首种原料逐份损耗预测成分；酒精不足且库存>0时用原生SplitProportionally规划保留份额，先原生搅拌整批，再把多余原浆同比移入闲置空容器或同配方原浆容器，优先large_bottled_water/bottled_water/small_bottled_water；不倒已有水、不借其他机器瓶/过滤水罐。预检确保余浆空间、整批搅拌峰值和本次试剂峰值。酒精为0或工作瓶容量/余浆空间不足仍保留原料阻止开工。余浆后续手动放回搅拌机续做。
- 来源按真实浓度预测原溶液体积，保持所有组分同比进瓶。蛋白酸/碱与溶脂剂按本次保留原浆计算；酒精分批黄色，其他缺试剂红色，足够绿色。面板打开且不是次日结果时每2秒只读刷新。
- Purification.cs：物理滤布→滤芯后、蒸发前处理药片。原生water_tablet含15000uL chlorine；water_tablet_container是内含9片的容器，SceneSnapshot递归发现内部单片。调用DissolveInto消耗真实单片，再HandleChlorine，刷ValidateShapeState。不是调用旧OnWaterPurificationTabletAdded接口。原生含氯反应需要water>0，对chemical_contaminant有total*0.007的残余下限；每次最多用15000uL chlorine，按5.1倍预算除化学污染/微生物。仅有效改善时投片；无水不投，药片不足提示保留残余，不用净水器，不保证零污染。
- 源码研究记录Research/purification-api-il.txt；同机新安装BetterLiquidScanner 1.2.2目录workshop/4348910/3831472187。只读参考Render/CreateRow/LayoutRowLabel的“左名、彩色占比条、右侧剂量和百分比”，记录Research/scanner-layout-il.txt；无扫描仪DLL依赖或修改。
- CompositionPanel.cs：三阶段只读快照、预测/实际标题区分；独立CompositionBarView以Unity Image+TMP绘制，原生AddLabel提供布局槽，28px行高，固定右侧剂量/占比，颜色按有效成分/水/酒精/污染物/试剂区分；最多7行，超出合并微量组分。报告阶段快照在真实节点捕获。PanelLayout跳过自绘横条内部文字的自动高度，保留其固定行；仍按画布适配窗口。
- RecordFailure保留真正未处理/中止原因，面板顶部显示。无实际步骤的预检失败不自动弹，但手动F10可看。EvaporatorBlockReason细分没有设备/槽占用/瓶不适配/未装电池/无可用电量；运行失败保留原液并继续正常过夜，开关不自动关闭。
- Workflow Publish改为简要预计/实际试剂用量与结果；详细步骤仍写Player.log。准备批次时捕获原材料，分配余浆后捕获实际搅拌液，药片后捕获蒸馏前原液；手动打开有新批次时旧快照标上次。
- 当前原生扫描检测到订阅一键制作DLL已更新，SHA256为808D473AEAFC396554877FE82602F9C75ADD72343E537192A9EBF26F39E1ABAD（不再是历史CB4623...）。本Mod独立，不依赖它。
- 0.3.1保留编译和原生扫描；不新增/运行测试，不自行开游戏、不修改存档。整批限酒精储存、药片、原因记录及新UI实际布局均需用户重启反馈，不能据编译/扫描称游戏内验收完成。
### 0.3.1 最终部署核验

最终编译0警告0错误；实际Mods DLL原生ModScanner PASS，334 methods，额外capabilities none。首次最终复制因用户运行中的游戏映射DLL失败；用户明确回复已退出后已重新运行build.ps1成功完成部署。实际安装清单与程序集均0.3.1，安装DLL与构建输出哈希一致。暂存Modding/ChemAssistPlus/staging/0.3.1也保留同一最终产物。新UI未自行打开游戏验收，待玩家正常启动反馈。
