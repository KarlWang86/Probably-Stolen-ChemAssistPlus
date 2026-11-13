# 新对话交接记录

日期：2026-11-09。先读本文件与 ARCHITECTURE.md，勿把未接入草稿当作已发布功能。

## 当前落地状态

- 已安装版本为 **0.1.3**，源清单和 `Mods/ChemicalWorkflowAssistant/manifest.xml` 均为此版本。
- 已完成的 UI 修复：标签间距、自适应画布、滚动、按钮/下拉框灰底白字。
- 现有自动流程为湿料处理至过滤，玩家原生过夜后再批量成型；支持半成品续做。
- 本轮只增加架构文档、源码主要功能注释，保留新功能草稿；没有发布 0.2.0，也没有把草稿复制到 Mods。
- 曾尝试通过系统 python.exe 批量修改业务代码，但该 Python 启动失败，脚本没有执行；不要据此认为 Plus.cs/Workflow.cs 已接入新功能。
- 新草稿原先短暂加进 csproj，但依赖未完成。已移到 `Research/Drafts`，撤下 Compile 与 Harmony 引用，保持当前发布源码可编译。

## 用户确认的需求与边界

1. 保留并优化一键自动制作。独立新 Mod，便于单独上传，不修改原作者 Mod。
2. 设备允许自动选已有空闲设备，也可明确指定；指定后不可用应提示，不偷偷换。
3. 滤布→滤芯顺序固定。只优化手动部分，不用要过夜的净水器。
4. 缺滤芯应提示并跳过；已有滤布的水罐应可继续使用。有备用滤芯时自动卸滤布、装滤芯，省去手动换装，拆下物品要安全归还。
5. 投料必须按瓶中原溶液比例转移，水与杂质一起进入，不能自动分离纯有效成分。
6. 缺料提示按步骤显示：原料、试剂、滤材、容器；列机器、实际材料、溶剂比例、完整过程和用量。
7. F10 打开应刷新当前仓库和本批预检，先列各步骤缺料；切换配方/设备后也应更新。
8. 用户问低配加热板能否通过接口模拟控温，已找到可实现的原生调用点，但尚未接入。
9. UI 要灰底白字，类似 dropdown，保持分辨率适配。
10. 用户已在本机登录 Steam，允许查看本机存档；不需要继续等待家里传文件。

## 本机存档只读结论

路径：`C:/Users/wangkan/AppData/LocalLow/Questing Goose Studio/Probably Stolen`。

最近核对 `save_16.es3`：436256 字节，修改时间 2026-11-09 19:15:41。继续前重新按修改时间找最新档，Steam 同步可能更新。目录还包含 saves_index.es3 与日备份。未修改用户存档。

此前提供桌面附件：`C:/Users/wangkan/Desktop/save_16.es3` 和 `Player.log`。桌面日志显示 0.1.3 已加载、原一键制作关闭，没有工作流 Trace 或异常记录；本机旧 Player.log 未必对应当前测试，检查时间再下结论。

save_16 的关键状态：

- 搅拌机 uid15：容器槽有工作瓶 uid27。
- 瓶 uid27：容量750mL，约26℃；蒙茂菇原浆42.9mL、水7.8mL、微生物3.9mL、物理杂质3.9mL、矿物0.975mL、蛋白3.9mL，总计63.375mL；酒精和提取物均为0。
- 加热板 uid16：0个模块，原生目标99℃。现有0.1.3因此预检不通过，属于 Mod 限制。
- 蒸发器 uid17：电池 uid19 有40/100能量，机器内另有纸条，**没有工作原液瓶**。
- 水罐 uid29：容量3L，已装滤布 uid30；该存档 FILTER_SIZE_TAG 为150；未找到精滤芯。
- 两瓶酒精 uid143/144：各250mL，酒精175mL+水75mL，即70%。
- 酸液 uid146：有效239524uL/总249984uL；另有碱液 uid147 与溶脂剂 uid145。

解释用户“已经搅拌、过夜没发生”：当时只完成搅拌，没补酒精/加热提取，且原液瓶没放进蒸发器。过夜不会自动从搅拌机提取或蒸发。原料仍在，没有证据表明损坏。

ES3 为文本 JSON，个别字符串含非法转义，普通 ConvertFrom-Json 会报错。仅为读取可用以下宽容解析，**不要把规范化后的 JSON 写回存档**：

```powershell
function Read-LooseJson([string]$s) {
    ([regex]::Replace($s, '(?<!\\)((?:\\\\)*)\\([^"\\/bfnrtu])', '$1$2')) |
        ConvertFrom-Json -AsHashtable -Depth 100
}
$save = Read-LooseJson (Get-Content -LiteralPath $savePath -Raw)
$store = $save.playerStore.value
$items = (Read-LooseJson $store.mainInvJSON).saveItems
```

库存内部还是嵌套 JSON 字符串。childItems 是数组索引，与 childItemInventoryNode 对应；不要把数组索引误当 uniqueId。避免打印整个一行数百KB的存档。

## 待接入：低配加热板控温

草稿：`Research/Drafts/HeatingControl.cs.draft`，**未编译，不能直接启用**。

已核对本机 Assembly-CSharp.dll：

- 原生加热板每秒回调先把0模块目标强制设99℃，1模块160℃，再调用 `MachineHeatingPlate.HandleHeating(GameSlotInventory,int,GameItem)`。
- HandleHeating 读取目标并调用原生 HeatLiquidOnStove；低档速度因子10/30，3模块乘4、4模块乘16。原生升温会封顶到目标，不需要写容器温度。
- 可用 Harmony Prefix，在 HandleHeating 读取前，仅对当前 busy/job/activePlate/工作瓶匹配的一批低配板覆写目标。其余加热速度和反应由游戏推进。
- 原生 ModLoader 会自动 `new Harmony("mod." + ID).PatchAll(assembly)`，禁用时自动卸载，无需自行重复 PatchAll。

接入必须一起完成：

1. csproj 加 Harmony 原生引用（Private=false）和正式新源码 Compile。
2. 工作流增加 activeTarget/originalTarget/activeModules，开火前保存原设置；所有正常/取消/失败出口关火并恢复原目标。
3. FindPlate 与 Preflight 移除“至少2模块”硬限制；开工前检查本补丁确实加载，失败时不消耗原料。
4. 低配板每秒原生强制重置目标不可被误判为玩家抢控制；模块变化、瓶子移走、关火仍应停止。
5. **草稿在 Prefix 异常分支 throw 会冒泡到游戏循环，应改成关火并安全阻止本次原生加热，交由工作流报告，不能照抄。**
6. 加热范围只限本批，结束或禁用后其他板正常；不提高模块速度，不额外跑反应。

## 待接入：同一水罐自动换滤材

草稿：`Research/Drafts/FilterPlan.cs.draft`，含 PlanFilter、EnsureFilter、RefreshInventoryReport 等，未接入。

- 原生水罐工厂为 AmenitiesItemDirectory.WaterPitcher，内部是 GameSlotInventory；接受滤材的原生槽位可以通过 MachineView.Find 定位。
- WaterHelper.GetFilterItem 返回水罐内带 WATER_FILTER 的子物品。
- 原生 `GraphUtils.SwapItemsInPlace(GameItem,GameItem)` 会校验双方位置，使用游戏 swapBuffer，并在放置失败时尝试回滚。可用来把已装滤布与仓库备用滤芯交换。
- 优先用已装正确滤材的空水罐；其次使用空罐+松散备用滤材；不擅自拆其他水罐的滤材。
- 开工预检只能规划，不得为了检查实际交换物品；执行时重新确认所有权、位置、空罐、滤材耐久、拆下滤材存放空间。
- 草稿只取第一备用滤材，未验证所有候选位置/空间、交换缓冲区、严格模式下失败行为；应补齐安全选择与异常报告。
- 不要把耐久失效、水罐占用、无空间的换装失败都误报成“缺滤芯”。
- FilterBatch 改调用 EnsureFilter，Filter/FilterReason/严格检查统一使用只读计划，UI 指定水罐下拉不要只列已装正确滤材的罐。

## 待接入：打开 F10 刷新和步骤预检

- Open 当前重建下拉列表，但显示的是旧 lastReport，没有主动预检。
- Open 完成后刷新库存/预检；提供刷新按钮；工作机、配方、路线、strict、指定设备改变后也刷新。
- 正在工作时保留实际过程记录，避免重开窗/刷新用静态预检覆盖运行日志。
- 先列所有步骤受阻/缺料，再写预计用量、来源有效成分库存、溶剂比、所用机器/滤材和后续过夜步骤。
- 即使 Probe 失败，也可显示库存设备与滤材统计，说明哪些用量还需原料/瓶子才能计算，不要只返回第一条错误。
- Preflight 的蛋白需求目前减的是 acid+base；执行期使用 abs(acid-base)，需核对原生中和逻辑后统一，避免估算和执行不一致。
- 非严格模式下部分试剂不足：现代码有来源就 Dose，Dose 不足会抛错；提示要反映这个行为，或在实现时明确设计跳过/部分处理策略。
- 干料成型预检不要被已经完成的湿料溶剂、沉淀、过滤条件阻挡。

## 编译和验证

在游戏根目录 PowerShell：

```powershell
& 'Modding/ChemAssistPlus/build.ps1'
```

该脚本会更新 Mods，执行投料数学测试并用游戏原生 ModScanner 扫描。只想检查编译可运行 dotnet build，不必部署。

- ModScanner 引用列表传程序集 BaseName，不能传完整 DLL 路径。
- 不把0Harmony、原Mod DLL、游戏 DLL打入新 Mod。
- 完成新功能后才升版本并更新 manifest、窗口标题、说明、验证记录及安装/源码包。
- 本轮没有端到端游戏内验证；不要把数学测试/扫描通过写成“真实游戏全部验证”。

## 原 Mod 与既有包

原工坊项目：App 4348910，Workshop 3830072589，名称“化学一键制作”，研究时v1.3.9。
DLL：`D:/software/steam/steamapps/workshop/content/4348910/3830072589/OneClickCraft.dll`。
研究基线 SHA256：`CB4623B78622F869AFBE16122C719C195DA696ABC9FC2708C8BF82905DE8E826`。
从未修改它。哈希变动可能是工坊更新，应重新核对而非覆盖。

现有0.1.3安装/源码zip在游戏根目录的 `化学制剂助手`，本轮注释与交接文档尚未重新打包进旧zip。
用户对照表在 `化学制剂助手/化学制剂对照表.txt`。

新对话可直接说：

> 先读 Modding/README.md、Modding/ChemAssistPlus/ARCHITECTURE.md 和 HANDOFF.md。按交接记录继续完成低配加热板控温、水罐自动换滤材、F10打开刷新与步骤缺料预检，编译更新Mods，保持灰底白字和原生过夜边界。
