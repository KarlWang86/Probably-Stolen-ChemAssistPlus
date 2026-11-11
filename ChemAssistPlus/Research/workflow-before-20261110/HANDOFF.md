# ChemAssistPlus 接续交接 · 0.3.4

用户要求：每次新版本同步CHANGELOG.md与本文件。主记录从0.3.2开始；此前记录归档Research/HANDOFF-before-0.3.2.md，无需常规读取。架构见ARCHITECTURE.md。

## 当前任务与部署状态

本轮新增0.3.4：按当前浓度优先回收高纯酒精；统一预检与执行的有效溶剂目标公式；没有新原料/待提取原浆时不将已占用蒸发器当作失败；核对净水药片原生机制与库存遍历，按仓库散片优先、瓶内真实单片其次计算与取用。已编译、安装并通过原生扫描，具体核验见末节。0.3.3发布产物从WorkshopUpload/content备份至staging/0.3.3，0.3.2备份仍保留。

部署变化：玩家最新日志实际加载工坊条目3836818025的0.3.3，原本地Mods目录已为空。本次重新安装本地Mods/ChemicalWorkflowAssistant 0.3.4；已核对原生ModLoader同ID本地优先于工坊，不修改订阅缓存、不自动上传。WorkshopUpload/content及工坊线上仍是0.3.3；若用户后续请求发布，再同步上传包并更新同一条目，不新建条目。

## 现行行为（覆盖旧归档）

- F10为450x600、3:4；溶剂一项一行使用原溶液/库存；原材料、搅拌后、提取后、净化后、过夜后五行总剂量/纯度。6px细滚动条，底栏自动处理/次日弹窗/刷新/关闭。成型入口隐藏，内部接口保留。这是0.3.2确认的布局。
- 自动默认true，只在最终原生结束当天按钮触发。不因放料白天启动。首种原料批量，其他种类不动。无任务不装瓶，预检只读。执行时暂缓原生过夜，送蒸发器原液槽后恢复同一原生按钮一次；失败关火/保液/续原生过夜，自动开关不自动关闭。
- 0.3.3酒精不足时PlanWholeMaterials选择可负担的完整原料份数，余料固体留输入格。BuildBatchPlan按Selected计算试剂和峰值；Run保存完整InputSolids但只执行Selected，首拍重检。原分浆/存余浆代码已移除。
- 酸/碱按本侧减另一侧的正净量计算真实有效浓度；其他试剂按实际成分。Sources对酒精每次最高浓度优先，同浓度才优先旧瓶，解决70%旧瓶压过新蒸出100%酒精的问题。其他试剂preferredSources保留已用来源直到耗尽/移走，切档清空。预检估算和TakeLiquid同来源规则，SolventTargetForParts统一有效溶剂目标，按微升向上取整各瓶原溶液并同比搬运水/杂质。日志有来源ID/浓度/量。ProteinReagent已有净方向则保持，等量时优先够量且浓度高的一侧，无97%常量。
- FilterPlan全局最低正耐久优先，旧滤材须安全归还。FilterBatch按Liquid的粒径/下限/耐久消耗/去除量计划每份大小，RemoveAndReturn后逐项原生HandleFilter；已过滤份额只同步缓冲，在finally回瓶，不反复重滤同一份。当前耗尽再选下一同类。
- 药片在滤布→滤芯之后、送蒸发器之前。Tablets递归统计仓库散片及water_tablet_container内真实单片，外瓶不计片，散片优先；日志分别记录散片/瓶内/总数及实际耗用来源。DissolveInto耗物品，HandleChlorine结算。已重查当前游戏代码：每片15mL氯，要求有水，化学污染最低(int)(整瓶总体积*0.007f)，酒精计入总体积；旧净水回调无当前投片调用。最新日志0.045/0.035mL低于3.190/2.932mL下限，未投片符合原生机制。报告显示具体污染量/下限，不强投无效药片；有作用场景真实耗片仍待玩家验证。证据见Research/PurificationMechanics.md与native-0.3.4-il.txt。
- WaitingForDistillation供自动入口和面板共用：无新固体且无待提取原浆、蒸发器均有工作瓶时静默正常过夜，面板展示已有批次，不报槽位占用错误。有新原料仍正常检查空位；缺电提醒独立保留。
- 原生FindAllItem(true)已核对覆盖前后台/储藏区/展示柜/垃圾箱/文档/隐藏及营业后库存/售货机等，再递归背包、药片瓶、机器内部；按拥有标签筛选并去重。溶剂来源排除目标瓶与含其他配方原料/产物的溶液。
- Charged改为CanDrawPowerSource(battery,max(1,machinery_drawn_power))；PowerMonitor每2秒扫描已有湿料的蒸发器（也包括无新原料的旧批次），面板常驻设备/瓶号和缺电原因，状态变化与新一天后延迟游戏Notify小通知。补电后清除，不自动换电池。未执行实际流程仍不自动F10；小通知独立于F10弹窗开关。
- 实际产物留蒸发器原液槽的原工作瓶，馏出槽只回收溶剂。蒸发仍交原生EndNight，本mod不模拟蒸发，不造物品。

## 核心代码与API易错点

显式编译列表见csproj；新增PowerMonitor.cs已加入。机器槽位按角色找，不用固定body索引。ModifyTag(...,true)跳过modifiedState刷新，GetTagReadonly会读旧缓存；业务状态更新要false/ValidateShapeState。
TransferLiquidToChem是SET非累加；真实投料RemoveAndReturn+AddSolution/AddLiquid，所有成分同比。WaterHelper.GetLiquidParts/SplitProportionally返回新数组。HandleFilter公开，滤孔标签字面量FILTER_SIZE_TAG（WaterHelper没有该常量）。过滤原生每组分最多10000动作；本版按此分份，整批最多4096份。
EndDayProcessingHook Prefix重入标记，pendingEndDay保存原生请求；每32拍让帧，本批提取最多900拍。CancelJob迭代器finally关火。nightDidWork仅实际操作后true；无实际工作不弹次日F10，Popup默认true。
StoreUIManager.Instance.Notify(message,"default")为原生小通知（第二参是颜色/气泡键，不是标题）。BeginDay调度提醒，延迟2s避开黑屏；同状态每一天最多一次。

## 继续验证与工作约定

- 不新增/运行测试、不自行启动游戏、不改存档、不替用户消耗原料。只编译+原生ModScanner+安装，完整游戏效果等玩家日志。
- Windows PowerShell 5，脚本UTF8 BOM，执行powershell -NoProfile -ExecutionPolicy Bypass -File Modding/ChemAssistPlus/build.ps1。普通权限读AppData日志可能拒绝，需要工具提权读取；不要改日志/存档。
- 安装目录Mods/ChemicalWorkflowAssistant。若DLL被游戏锁定，先编译/扫描暂存，等用户退出再部署，不能强退游戏。
- validate.ps1对订阅OneClickCraft.dll的历史哈希会警告；当前已订阅文件哈希808D473AEAFC396554877FE82602F9C75ADD72343E537192A9EBF26F39E1ABAD，属外部更新，不要改它。本Mod独立无其DLL依赖。
- 游戏日志目录C:/Users/xiaok/AppData/LocalLow/Questing Goose Studio/Probably Stolen。待用户验证0.3.4高纯酒精优先、预估和实际原溶液剂量、无新料正常蒸发等待、散片优先与瓶内单片统计/耗用。离线PASS不可称游戏验收。

## 0.3.4 最终部署核验

2026-11-10：编译0警告0错误，已安装Mods/ChemicalWorkflowAssistant；实际安装DLL原生ModScanner PASS，355 methods，额外capabilities none。程序集/清单/F10标题/使用说明均0.3.4，构建/安装DLL SHA256一致：954956BD77D292914091D21BD4C20758D7574F8918154DEB21AC9AF59377B3C6。未新增/运行测试、未启动游戏、未改存档。用户重启后验证本地0.3.4；工坊线上和原上传包未改，后续发布需另行同步。

## 0.3.3 最终部署核验

2026-11-10：编译0警告0错误，已安装Mods/ChemicalWorkflowAssistant；实际安装DLL原生ModScanner PASS，345 methods，额外capabilities none。安装清单0.3.3，构建/安装DLL SHA256一致：101552FD804AC6DA310686828B5433556A0046D2839E5C22F357B0DF839C5C2A。未新增/运行测试、未启动游戏、未改存档。游戏内六项行为待玩家正常游玩确认。
