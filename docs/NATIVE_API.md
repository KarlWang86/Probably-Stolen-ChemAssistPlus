# 已核实原生接口索引

核对基线：2026-11-12，本机程序集哈希与2026-11-10一致，界面为100-Rev13，启动日志内部版本0.46D；尚未以100-Rev14测试分支验证。0.4.3将清单修正为0.46，匹配Rev13现有菜单规则；点分1.0.0只适用于Rev14起的新规则。
Assembly-CSharp.dll SHA256：725CB999D019DFF1FF31BED353C7F9BB8D81A7605A15DE1F80F5A3A6CF7B1D05。
下列结论迁自既有核查材料；参数名可用 query-api.ps1 定向查询。历史摘录未记录生成时哈希，不能反推全部历史 IL 均来自此基线。

## 2026-11-12 官方API核对

- 官方变更：[APIChanges.zh-CN.md](https://github.com/dragonpan2/probably-stolen-modding/blob/main/APIChanges.zh-CN.md)；[版本与信任规则](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.zh-CN.md)。注意Rev14测试分支才支持GameVersions的1.0.0前缀匹配，并中性处理旧0.46/0.46D；Rev13仍将条目及Application.version归一化后比较，1.0.0在Rev13会标红。定向核对ModUIManager.VersionMatchesGame、NormalizeVersion及FormatGameVersions对应lambda，0.46匹配内部0.46D。版本声明不阻止加载，也不触发信任批准。
- HeatingControl对MachineHeatingPlate.HandleHeating的字面AccessTools查找、NativeControls对MachineryButton.callback的字面反射均为游戏成员，无需reflection-external。四个Harmony补丁只针对游戏方法（0.4.4新增WaterHelper.TransferLiquid混液确认）；不调用信任写入、QFSW控制台执行、路径文件辅助方法或外部库补丁。
- PlusPump.Update在主线程推进工作流，不使用线程/Task或其他组件的协程。未读取Application.version、未处理右键绑定、未在销毁容器后读取其窗口；本轮无相关迁移。Capabilities保持为空。

## 常用接口

- 基因扫描仪原生为开关工具，不是拖到种子上使用：GeneScanner只注册切换回调，玩家拥有且开启时设置PlayerStore.isGeneScannerScanning；PlantGeneticsHelper.CreateSeedGeneticsTooltip要求SEED_ITEM_TAG、可识别PlantDefinition及IsScannerOn，直接在物品提示中追加基因读数，没有单独扫描动作。2026-11-13程序集哈希仍与基线一致；证据Research/GeneScanner-20261113-il.txt、AmenitiesScanner-callbacks-20261113-il.txt、SeedGeneticsTooltip-20261113-il.txt、SeedItemTooltip-20261113-il.txt。种子包与种子本体需区分；本结论未核实玩家当时开关与目标物品状态。

- 0.4.11定向核对GraphUtils.SwapItemsInPlace，哈希仍与上方基线一致：通过EmporiumEntry.swapBufferElement依次交换两瓶原库存/形状；缓冲已占用时会尝试移出其物品，因此本Mod要求缓冲预先为空。原生PlaceAtShape直接构造SlotMarker并TryAcceptOnce，不替代完整槽位校验；本Mod交换前另行调用MayHaveValidInventorySlot、CheckIsStateValid、CheckChangeParentShapeStateModifier及MaxNumAdd/Remove做双向检查，仅两台机器的主容器槽、单只普通瓶及真实空接收瓶可交换。SwapItemsInPlace失败会尝试复原，调用后仍核对两瓶位置，异常记录实际位置停止，不宣称异常必然回原位。证据Research/GraphUtils-{SwapItemsInPlace,PlaceAtShape}-20261113-il.txt、GameSlotInventory-{MayHaveValidInventorySlot,CheckInventorySlotStillValid}-20261113-il.txt、SlotMarker-TryAcceptOnce-20261113-il.txt及GameItem/GameInventory对应定向IL。

| 签名（省略 System 前缀） | 参数、语义与副作用 | 用法 / 证据 |
| --- | --- | --- |
| List<KeyValuePair<string,int>> WaterHelper.RemoveAndReturn(GameItem,int) | 源瓶、取出体积 μL；真实扣除，各成分同比返回 | TakeLiquid → AddSolution；Research/transfer-il.txt、game-api.txt |
| void WaterHelper.TransferLiquid(GameItem,GameItem) | 源瓶、目标瓶；按余量同比转移，经过目标滤材并结算温度/液体特征。普通瓶/滴管/酒瓶原生手动倒液均进入此处；成型机TransferLiquidToChem用于制剂而非两瓶手动倒液 | MixingGuard Prefix在扣液前挂起；Research/mixguard-transfer-il.txt、mixguard-pour-callback-il.txt |
| GenericConfirmationUIManager.Instance.OpenPanel(string,string,string,Action,Action) | 正文、确认按钮、取消按钮、确认回调、取消回调；回调执行后原生ClosePanel。panel/bodyTMP公开，可检查是否被其他弹窗替换 | MixingGuard复用原生阻挡层/双按钮；Research/mixguard-confirmation-il.txt；只在原生对话框空闲时打开 |
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

- 0.4.9接入芬太加热器流程；本机程序集SHA256仍为725CB999D019DFF1FF31BED353C7F9BB8D81A7605A15DE1F80F5A3A6CF7B1D05。补查RemoveAndReturn：全取返回全部成分，部分取直接调用GetLiquidParts→SplitProportionally，再按返回数组扣除。SplitProportionally采用整数比例向下取整后按余数/原分量顺序补余数，不保证每次净有效量恰等于比例目标；DoseFanta先规划整份投料并约束不超目标，再真实扣液。证据Research/Assembly-CSharp-WaterHelper-{RemoveAndReturn,SplitProportionally}-il.txt。规划不调用原生化学反应；未游戏内验收。

- 2026-11-13芬太路线只读调查：程序集哈希仍与基线一致。管制用品拆包为2瓶前体+1瓶还原剂；稳定剂经原生酸碱沉淀转debris。合成每拍先按min(前体−稳定剂,还原剂)限量，实际耗还原剂为Round(反应量×0.75)、产物为Round(反应量×0.1)，须考虑尾量。前体/稳定剂/还原剂/酸碱/产物粒径均5且不挥发，现有孔径150→22过滤及蒸发不能分离残余试剂。配方每件需总干料500μL。完整流程、IL索引及入队/完成判定缺口见Research/FantaWorkflow-20261113.md；未改功能、未游戏内验收。

- 0.4.7定向核对：GameItem.GetTagReadonly从modifiedState取TagState并Clone；ModifyTag(...,true)修改state且跳过ValidateShapeState，原生TransferLiquid批量走此路径。TagSystem.GetTag会InitTagString，不能用它做无副作用的原始状态读取；图标改用state.dict.TryGetValue读取现有CURRENT_PART，蒸发器路线观察也共用该实际液体读取。证据Research/icon-refresh-{GetTagReadonly,ModifyTag,TagSystem-GetTag}-il.txt。不通过UI刷新去改材料或强制原生化学缓存。
- 0.4.6按玩家截图与已确认预览修正图标定位：GameItemElement.middle不等于槽位实际瓶图边界，库存gridSize×modifiedShape不能用于推算所有机器槽位图片坐标。改从background/fakeBackground的RectTransform.GetWorldCorners读取世界边界，转到middle/fakeMiddle局部空间并与有效祖先RectMask2D/Mask相交；节点用中心锚点/localPosition居中纵排。机器同样叠加本体左上横排，停止使用PixelWindow.AddHeaderButton。Unity几何运算只读，不修改原生物品形状或窗口布局；实际裁切/位置仍需游戏内验收。
- 原生制剂规则已于2026-11-12定向复核：ChemFinisherRecipe.CanOutput仅检查总体积≥requiredVolume，以及isRequireDry时WaterHelper.IsWet为false；咖啡因配方requiredVolume=5000μL、isRequireDry=true。IsWet把全部isEvaporative成分求和，>100μL为湿，与DRY_TOLERANCE_UL=100一致；不是“纯度达到98%即可”。截图酒精14.14mL+水2.30mL仍明显超标，须回蒸发器原液槽继续原生过夜；不修改制剂条件。证据Research/finisher-{rules,callbacks}-20261112-il.txt、finisher-recipe-canoutput-il.txt、finisher-recipes-il.txt。
- 原材料图标0.4.5：HydroponicDirectory.CoffeeBean/DreamCap/NightmareCap的原生sprite分别为Items/items_hydroponic_plant下coffee_bean/dream_cap/nightmare_cap；RenderHandler.TryLoadFromAtlas只读加载。HandleNomoreReaction消耗dream_cap_extract，故nomore也映射梦帽菇。TreeNodeRender.Instantiate().SetParent可建立独立显示节点；GameItemElement.middle/fakeMiddle为占格原点，rotator/fakeRotator才施加物品旋转；background/fakeBackground的image.enabled及alpha表示原生实物/拖影显隐。PixelWindow.AddHeaderButton经MachineryValidate在右上由右到左布置并为标题留宽，RemoveHeaderButton只ResetParent、不销毁，故需另外Destroy自建按钮节点。证据Research/containericons-*-il.txt；纯显示，无新增Harmony补丁。
- water_tablet 每片15mL氯；water_tablet_container 是外瓶，只计内部真实片；散片优先。
- 过滤孔标签字面量 FILTER_SIZE_TAG，WaterHelper 无此常量；原生每组分最多10000动作，本项目每批最多4096份。
- 机器槽位按角色找，不依赖 body 固定索引；蒸发只交原生 EndNight，不自行模拟产物。
- 蒸发器原液槽面板键为container，酒精/水回收容器槽为distillate；两者都经SetupWaterSlot设置。按玩家要求，自动移瓶只操作container，不改动distillate回收槽。证据：Research/Assembly-CSharp-MachineEvaporator-CreateMachineInventoryWindow-il.txt（IL_0051～IL_0085）和Assembly-CSharp-MachineEvaporator-_-il.txt（IL_00fc～IL_0117），程序集哈希与上方基线一致。
- WaterHelper.DRY_TOLERANCE_UL 用原生干燥容差，不写死。
- 历史 OnWaterPurificationTabletAdded/Added2 不作为当前投片路径依据。

## 结论不足时

先查本表和业务调用处，再执行 query-api.ps1 -Type WaterHelper -Method RemoveAndReturn。
需要副作用证据时加 -IL；完整结果写 Research/，终端不展开完整 IL。参考用法见 DEVELOPMENT.md。
