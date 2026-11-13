# 净水药片原生机制核查（0.3.4，2026-11-10）

直接读取当前安装游戏 Probably Stolen_Data/Managed/Assembly-CSharp.dll 的元数据/IL，未启动游戏、未执行化学函数、未读取或修改存档。精确摘录见 native-0.3.4-il.txt。

1. AmenitiesItemDirectory.WaterPurificationTablet 定义 water_tablet，调用 InitChemItem(item,true,true)，氯含量15mL。water_tablet_container是外容器，应递归统计其中真实water_tablet；外瓶不能按一片计。
2. InitChemItem 的交互回调 b__4 检查目标 LIQUID_CONTAINER_TAG，再调用 ChemicalHelper.DissolveInto。该函数把药片所有成分加入目标并Destroy药片。空间不足的原生分支会先挤走液体；本Mod预先检查空间，避免损失原液。
3. ChemicalHelper.HandleChlorine 要求氯和水均大于0；每次最多处理15000微升氯，提供氯量*5.1的去污额度。先去化学污染，余量用于微生物。化学污染仅去除高于(int)(WaterHelper.GetTotalVolume(pot)*0.007f)的部分。总体积包括酒精、提取物等，不是仅水量；微生物可降到0。氯参与消耗，剩余还会自然降解。
4. WaterHelper.OnWaterPurificationTabletAdded/Added2是不同旧逻辑；遍历当前程序集方法及嵌套类型，没有发现调用它们的实际药片投放路径，不能据其固定去污量替换当前含氯反应。
5. 最新游戏日志01:30:41化学污染0.045mL、门槛约3.190mL；01:45:48化学污染0.035mL、门槛约2.932mL，均无微生物。两批投片不能降低污染，保留药片符合原生规则；它不证明有作用场景已实际耗片，仍待玩家日志。

药片库存与实际取片都使用同一个Tablets()列表，递归全库存、仅真实单片、按是否位于water_tablet_container中升序，再按ID排序，保证散片优先。预检不取片，执行仅调用原生耗料API。

酒精用量按有效成分计算：目标扣除工作瓶已有酒精，再从来源按浓度降序补齐。每瓶用量向上取整至微升，逐瓶扣除有效量。100%酒精100mL等效于70%酒精约142.858mL，杂质和水与实际倒入体积同比转移，不固定按70%计算。

库存边界：PlayerStore.FindAllItem(true)的根节点包括frontInvWindow、backInvWindow、backInvinvElementCounter、invWindow、showcaseWindow、trashInvWindow、docInvWindow、hiddenWindow、afterhourWindow、vendingFountainWindow、vendingMachineWindow；再递归嵌套图并去重，检查拥有标签。来源还排除目标瓶与含其他配方原料/产物的瓶子。
