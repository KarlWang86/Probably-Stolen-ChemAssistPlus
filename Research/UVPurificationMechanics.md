# 紫外线净化原生核查（2026-11-11）

只读检查当前安装游戏 Assembly-CSharp.dll 元数据与 IL；SHA256：725CB999D019DFF1FF31BED353C7F9BB8D81A7605A15DE1F80F5A3A6CF7B1D05。未启动游戏、未调用游戏函数、未修改存档。证据：uv-filter-native-il.txt、uv-water-feature-il.txt。

- AmenitiesItemDirectory.UVFilter 定义净水用品，目标条件为非空且带 LIQUID_CONTAINER_TAG；canItemTargetItem 回调直接返回 true。
- 使用回调 <UVFilter>b__15_4 仅调用 WaterHelper.OnUVUsed(target)。该路径没有供电检查、扣电、耐久扣除、销毁灯或等待计时；本结论针对该物品原生使用路径，不泛指其他灯具。
- OnUVUsed 读取 microbe 与 water，把 microbe 置0，把 water 设为原水量加原微生物量。一次处理全部微生物，总体积保持不变；没有最低水量条件，也不按批量或浓度限制。
- 没有单独的细菌处理分支；此函数处理的是 microbe 成分，不可据此声称清除所有毒素或化学污染。
- 此函数没有改写提取物、酒精、化学污染、蛋白或脂肪等其他成分。末尾 UpdateWaterFeature(target,false) 更新水特征/纯度显示、重置扫描结果，并可能清除试纸检测标记；不提供水质已重新检测的保证。
- 原生注册ID为 uv_filter，InitDirectory 的 b__0_10 创建 UVFilter；证据见 uv-directory-registration-il.txt。
- 核查时助手在 FilterBatch 完成后立即调用 PurifyBatch；Purification.cs 的 ChlorineUseful 在有水且仍有微生物或化学污染超原生下限时投片。

0.3.6已接入（游戏内待验证）：两级过滤完成、液体全部回工作瓶后，在 PurifyBatch 前调用 PurifyWithUV。按玩家拥有的 uv_filter 查找设备，并复核原生目标条件，仅在微生物量>0时调用 OnUVUsed；刷新瓶状态并记录实际工作。清除微生物后按现有规则判定药片需求。没有灯则继续现有药片路径；没有微生物则跳过。报告包含结果或跳过原因，日志附灯与瓶编号、水量前后变化。

这一步可减少仅为微生物而投的药片，不保证每批省片：化学污染仍可能需要投片；已无微生物的批次不会受益。推荐位置基于现有助手流程和原生副作用，不以现实中的浑浊度影响作为游戏机制证据。
