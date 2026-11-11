# 模块与状态边界

## 执行路径

Unity 主线程 PlusPump → 最终结束当天按钮 → 按编号快照所有待处理搅拌机 → 当前批只读预检 → 暂缓原生过夜 → 首种原料按酒精库存及工作瓶容量选择完整份数 → BlendInto → 同比补溶剂 → 原生加热/反应 → 去蛋白/脂肪 → 滤布 → 滤芯 → 按需紫外线消毒 → 含氯药片 → 独立蒸发器原液槽 → 下一台重新预检并逐批复用加热器 → 队列结束恢复原生过夜一次。
产物留原工作瓶，馏出槽回收溶剂。放料白天不开工，无任务正常过夜；没有新料且蒸发器全占用时正常等待。

## 按问题定位

| 问题 | 文件 / 搜索入口 |
| --- | --- |
| 入口、配方、物品递归、按角色找槽位 | Core.cs；ProbeResult |
| 来源浓度/排序、真实投料、过滤、F10 | Plus.cs；Sources、TakeLiquid、SolventTargetForParts、FilterBatch |
| 完整份数、试剂/峰值预估 | BatchPlan.cs；PlanWholeMaterials、BuildBatchPlan；DoseMath.cs |
| 只读预检、去蛋白方向 | Preflight.cs；ProteinReagent |
| 实际执行、反应、耗料与进度 | Workflow.cs；Run；HeatingControl.cs |
| 滤材耐久排序、换装与归还 | FilterPlan.cs；Plus.cs 的 FilterBatch |
| 散片/瓶内片、真实耗片、净化下限 | Purification.cs；Tablets |
| 紫外线设备检查、原生消毒、结果与跳过原因 | Purification.cs；PurifyWithUV；Workflow.cs 的 Publish |
| 过夜拦截、防重入、无新料等待 | AutoWorkflow.cs；WaitingForDistillation、EndDayProcessingHook |
| 蒸发器接收、同类相近浓度合并、转液回收 | Distillation.cs；CanReceiveBatch、DeliverToEvaporator |
| 湿料缺电持续监测与小通知 | PowerMonitor.cs；Charged；InventoryReport.cs |
| 面板布局与预计/实际报告 | CompositionPanel.cs、PanelLayout.cs；450×720、按设备纵向紧凑列表、当前用量/本晚结果切换、固定底栏 |
| 构建、扫描、查询、部署与打包 | DEVELOPMENT.md；Tools/NativeScan、Tools/AssemblyQuery |

## 状态与物料边界

- Plus 的 partial 文件共享当前存档会话状态；切档清任务、来源偏好、报告和挂起过夜。
- 开工前复核完整输入快照与 Selected；未选固体留输入格。预检不取片、不耗料。
- pendingEndDay 保存原生请求，重入标记防止重复过夜；每32拍让帧，提取最多900拍。
- nightBlenders 每晚快照并且每台只访问一次；不重复处理限量余料。前批送入蒸发器并关闭/释放加热器后才启动下一批。不同配方不合液；已有接收瓶仅在同种已提取湿半成品、有效成分浓度差≤5个百分点、容量足够整批且有电时接收；空接收瓶可直接装液。优先接收瓶，次选空槽，干料/待提取原料不混入。预检保守检查预计体积与同类接收可能，最终按实际浓度/容量/供电复核。
- DeliverToEvaporator 同步原生RemoveAndReturn→AddSolution，不跨帧悬空；finally根据接收瓶实际增加量归还未转移成分。合并不换接收瓶，源空瓶留搅拌机；runPotId转向实际接收瓶，净化前四阶段保留本批快照、过夜阶段显示接收瓶合计。预检失败继续下一台，执行异常/停止/切档清队列；nightBatchReports 汇总文字结果；nightDetails 独立保存每批阶段数组、实际试剂用量/开工库存、接收瓶编号及配方。过夜按各接收瓶分别补阶段快照，合并批次过夜量为接收瓶合计。previewDetails 每次只读刷新列出全部待处理搅拌机，失败原因仅归所属批次，不叠加旧失败横幅。
- 失败/取消通过 finally 关火、保液并恢复过夜；自动开关不因此关闭。
- 过滤按原生元数据与耐久分份，每份每道一次；已处理液体同步缓冲并 finally 回瓶，不跨帧悬空。
- nightDidWork 仅实际操作后为 true；仅实际工作且 Popup 开启才次日弹 F10。供电小通知独立，每日/状态去重。
- API 的签名、单位与副作用只在 NATIVE_API.md 维护。
