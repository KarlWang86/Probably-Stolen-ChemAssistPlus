# 已核实原生接口索引

核对基线：2026-11-10，游戏清单支持 0.46/0.46D；实际以程序集哈希为准。
Assembly-CSharp.dll SHA256：725CB999D019DFF1FF31BED353C7F9BB8D81A7605A15DE1F80F5A3A6CF7B1D05。
下列结论迁自既有核查材料；参数名可用 query-api.ps1 定向查询。历史摘录未记录生成时哈希，不能反推全部历史 IL 均来自此基线。

## 常用接口

| 签名（省略 System 前缀） | 参数、语义与副作用 | 用法 / 证据 |
| --- | --- | --- |
| List<KeyValuePair<string,int>> WaterHelper.RemoveAndReturn(GameItem,int) | 源瓶、取出体积 μL；真实扣除，各成分同比返回 | TakeLiquid → AddSolution；Research/transfer-il.txt、game-api.txt |
| void WaterHelper.AddLiquid(GameItem,string,int) | 目标瓶、成分ID、μL；累加成分 | AddSolution 逐项加入；Research/game-api.txt |
| void ChemicalHelper.TransferLiquidToChem(GameItem,GameItem,int) | 覆盖式 SET，不用于向已有原液累加 | Research/transfer-il.txt、probe-transfer-il.txt |
| int[] WaterHelper.GetLiquidParts(GameItem) / SplitProportionally(int[],int) | 返回新数组；比例分割用于预估，不替代真实耗料 | BatchPlan.cs；Research/game-api.txt |
| int WaterHelper.HandleFilter(string,int,GameItem,GameItem) | identifier成分ID、amount μL、sourceContainer源容器、filter滤材；扣耐久并结算该成分，需按原生动作上限分份 | FilterBatch；Research/purification-api-il.txt；本轮签名核对：Research/Assembly-CSharp-WaterHelper-HandleFilter-api.txt |
| void ChemicalHelper.BlendInto(GameItem,GameItem,int) | 原料、工作瓶、数量；原生搅拌耗料 | Workflow.cs；Research/game-api.txt |
| void ChemicalHelper.DissolveInto(GameItem,GameItem) | 药片、目标瓶；加入全部成分并销毁片；空间不足可能挤掉原液，调用前检查容量 | Purification.cs；Research/native-0.3.4-il.txt |
| ChemicalHelper.HandleChlorine | 要求水与氯均>0；每轮最多15000μL氯，额度×5.1；先化学污染后微生物，实际消耗氯 | 化学下限 (int)(总体积×0.007f)，微生物可到0；Research/PurificationMechanics.md |
| void WaterHelper.OnUVUsed(GameItem) | 目标液体容器；一次将全部microbe转为等体积water，不改其他成分；函数自身不校验灯。UVFilter使用回调无耗电/耐久/计时 | 目标需非空且有LIQUID_CONTAINER_TAG；刷新水特征、重置扫描结果，自动调用须先确认拥有灯；Research/UVPurificationMechanics.md、uv-filter-native-il.txt |
| bool PowerHelper.CanDrawPowerSource(GameItem,int) | 电源、启动耗电；不能仅检查剩余电量>0 | Charged 使用 max(1,machinery_drawn_power)；Research/extra-api.txt |
| GameItem GameItem.ModifyTag(string,Action<TagState>,bool) | true 跳过 modifiedState 刷新，GetTagReadonly 可能读旧缓存 | 业务更新用 false/ValidateShapeState；Research/game-api.txt |
| PlayerStore.FindAllItem(true) | 库存根遍历；还需递归背包/瓶/机器，按拥有标签筛选并去重 | Core.cs；Research/native-0.3.4-il.txt |
| StoreUIManager.Instance.Notify(message,"default") | 第二参是颜色/气泡键，不是标题 | PowerMonitor.cs；新一天延迟2秒通知 |

## 数据与边界

- water_tablet 每片15mL氯；water_tablet_container 是外瓶，只计内部真实片；散片优先。
- 过滤孔标签字面量 FILTER_SIZE_TAG，WaterHelper 无此常量；原生每组分最多10000动作，本项目每批最多4096份。
- 机器槽位按角色找，不依赖 body 固定索引；蒸发只交原生 EndNight，不自行模拟产物。
- WaterHelper.DRY_TOLERANCE_UL 用原生干燥容差，不写死。
- 历史 OnWaterPurificationTabletAdded/Added2 不作为当前投片路径依据。

## 结论不足时

先查本表和业务调用处，再执行 query-api.ps1 -Type WaterHelper -Method RemoveAndReturn。
需要副作用证据时加 -IL；完整结果写 Research/，终端不展开完整 IL。参考用法见 DEVELOPMENT.md。
