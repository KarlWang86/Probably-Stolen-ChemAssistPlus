using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    // 执行层：手动推进 IEnumerator，每 0.25 游戏秒至多推进一次，所有操作在主线程。
    // Run 先 Probe/Preflight；PrepareBatch 处理湿料至过滤；ProduceBatch 调原生成型按钮。
    // CancelJob/迭代器 finally 负责关火。失败保留当前原液，不承诺撤销已完成的游戏操作。
    public static partial class Plus
    {
        static IEnumerator job;
        static float nextJobTick;

        static readonly StringBuilder steps = new StringBuilder();
        static string jobStatus = "";
        static string runPlan = "";
        static readonly Dictionary<string, long> actualUsage = new Dictionary<string, long>();
        static readonly List<string> runNotes = new List<string>();
        static int usedTablets;
        static string compactOutcome = "";

        static void Trace(string message)
        {
            steps.AppendLine(message);
            Publish();
            log?.Log(message);
        }
        static void Publish()
        {
            SetReport(runPlan + (string.IsNullOrEmpty(uvResult) ? "" : "\n" + uvResult)
                + (string.IsNullOrEmpty(tabletResult) ? "" : "\n" + tabletResult)
                + (string.IsNullOrEmpty(compactOutcome) ? "" : "\n" + compactOutcome)
                + (string.IsNullOrEmpty(jobStatus) ? "" : "\n当前：" + jobStatus));
            SyncRunDetail(); RenderCompositionWidgets();
        }
        static void Status(string message) { jobStatus = message; Publish(); }
        static void Run(bool prep, bool overnight, bool checkOnly)
        {
            if (busy) { Status("已有自动操作正在进行，可点击停止。" ); return; }
            contextBlender = selectedBlender;
            try
            {
                var p = Probe();
                if (!p.Ok) { log?.Log("[开工受阻] " + p.Block); RecordFailure(p.Block); SetReport(Bad(p.Block)); contextBlender = null; return; }
                PrepareRoute(p.Recipe);
                if (!prep && (p.Solid != null || Amount(p.Pot, p.Recipe.ProductLiquid) <= 0))
                { SetReport("步骤7 · 成型前检查\n当前还不是提取完成的干料。请先开启自动处理并送入蒸发器，再由原生蒸发器过夜。"); contextBlender = null; return; }
                if (!prep && NativeChem.WetTotal(p.Pot) > WaterHelper.DRY_TOLERANCE_UL)
                { SetReport("步骤7 · 原液未干燥\n挥发液合计 " + Vol(NativeChem.WetTotal(p.Pot)) + "，需≤" + Vol(WaterHelper.DRY_TOLERANCE_UL) + "。请继续原生过夜蒸发。" ); contextBlender = null; return; }
                string blocked = Preflight(p.Recipe, p.Solid, p.Pot, prep, true);
                if (blocked != null || checkOnly) { if (blocked != null) RecordFailure(blocked); SetReport(lastReport); contextBlender = null; return; }
                if (prep) { p.Solids = lastCheckedPlan.Selected.ToList(); p.Solid = p.Solids.FirstOrDefault(); }
                steps.Clear(); jobStatus = ""; runPlan = lastReport.Replace("条件已满足，结束当天时处理。", "本批预检通过。"); actualUsage.Clear(); runNotes.Clear(); usedTablets = 0;
                compactOutcome = ""; tabletResult = null; uvResult = null;
                if (!prep) lastCheckedPlan = null;
                lastFailure = null; RenderFailureBanner(); BeginRunComposition(p);
                Trace("制作前检查\n" + lastReport + "\n——本次实际过程——");
                busy = true;
                job = prep ? PrepareBatch(p) : ProduceBatch(p);
                nextJobTick = Time.time;
            }
            catch (Exception e)
            {
                contextBlender = null;
                RecordFailure("开工检查异常：" + e.Message);
                // No iterator or material operation has started; keep the nightly queue intact.
                SetReport("检查失败，本次尚未开始制作：" + e.Message); log?.Error(e.ToString());
            }
        }
        public static void TickJob()
        {
            if (job == null || Time.time < nextJobTick) return;
            nextJobTick = Time.time + (pendingEndDay != null ? 0f : 0.25f);
            try
            {
                if (!PlayerStore.IsInstanceExist()) { CancelJob("已离开存档，停止自动操作。"); return; }
                if (!job.MoveNext())
                {
                    (job as IDisposable)?.Dispose(); job = null; busy = false;
                    contextBlender = null; jobStatus = ""; Publish();
                    if (pendingEndDay != null)
                    {
                        SyncRunDetail();
                        nightBatchReports.Add(nightBatchLabel + "：\n" + lastReport);
                        nightBatchLabel = null;
                        if (StartNextNightBatch()) return;
                        ResumeEndDay();
                    }
                }
            }
            catch (Exception e) { log?.Error(e.ToString()); CancelJob("已中断：" + e.Message + "\n已完成操作不回退，剩余原液留在当前机器或工作瓶中。", false, true); }
        }
        public static void CancelJob(string reason, bool disableAuto = false, bool continueEndDay = false)
        {
            var deferredManager = pendingEndDay; var deferredStore = pendingEndDayStore;
            pendingEndDay = null; pendingEndDayStore = null;
            string completedBatches = nightBatchReports.Count > 0 ? NightSummary() + "\n\n" : "";
            string stoppedBatch = nightBatchLabel;
            ClearNightQueue();
            if (disableAuto) autoProcessing = false;
            window?.Get("auto_processing")?.SetBool(autoProcessing, false);
            AutoWaiting((autoProcessing ? "本次停止，开关仍开启；" : "已关闭；") + reason);
            var running = job; job = null;
            try { (running as IDisposable)?.Dispose(); }
            catch (Exception e) { reason += "\n结束清理异常：" + e.Message; log?.Error(e.ToString()); }
            finally
            {
                try { StopPlate(); }
                catch (Exception e) { reason += "\n关闭加热异常，请检查加热板：" + e.Message; log?.Error(e.ToString()); }
                finally { busy = false; contextBlender = null; }
            }
            if (running != null) { jobStatus = ""; compactOutcome = Bad("本次已停止，剩余原液保留。"); runNotes.Add(Bad(reason)); Trace(reason); }
            if (continueEndDay && deferredManager != null)
            {
                RecordFailure(reason);
                ContinueNativeEndDay(deferredManager, deferredStore, completedBatches + "本晚处理未完成：" + stoppedBatch + "\n" + lastReport + "\n" + reason);
            }
        }
        static void CheckPot(NativeChem.ProbeResult p, GameItem plate = null)
        {
            if (!Owned(p.Pot) || !Present(p.Pot)) throw new InvalidOperationException("工作瓶已离开可操作库存。");
            if (plate != null && MachineView.Walk(plate).Container?.childItem != p.Pot)
                throw new InvalidOperationException("工作瓶已从本次加热板移走。");
        }
        static void MovePot(GameItem pot, GameSlotInventory slot)
        {
            if (slot == null || (slot.childItem != null && slot.childItem != pot)) throw new InvalidOperationException("目标容器槽被占用。");
            if (slot.childItem == pot) return;
            if (!GraphUtils.CanAccept(slot, pot)) throw new InvalidOperationException("原生规则不允许把工作瓶移入此槽位。");
            GraphUtils.TryAcceptAll(slot, pot);
            if (slot.childItem != pot) throw new InvalidOperationException("移动工作瓶没有完成；已停止。");
        }
        static string Composition(GameItem pot)
        {
            return string.Join("；", Liquid.Liquids.Select(l => new { l, n = Amount(pot, l.identifier) }).Where(x => x.n > 0)
                .Select(x => x.l.displayName + " " + Vol(x.n)));
        }
        static int Dose(string id, int need, GameItem pot, string stage)
        {
            if (need <= 0) return 0;
            var details = new NativeChem.TakeLog();
            long beforeVolume = WaterHelper.GetTotalVolume(pot);
            int transferred = TakeLiquid(null, id, need, pot, details);
            actualUsage.TryGetValue(id, out long previousVolume);
            actualUsage[id] = previousVolume + WaterHelper.GetTotalVolume(pot) - beforeVolume;
            Trace(stage + "：需要" + LiquidName(id) + "有效成分 " + Vol(need) + "，实际 " + Vol(transferred));
            foreach (var line in details.Sources) Trace("  使用 " + line);
            if (transferred < need) throw new InvalidOperationException("[缺试剂] " + LiquidName(id) + "还差有效成分 " + Vol(need - transferred) + "。原液已保留，可补料后续做。");
            return transferred;
        }
        static IEnumerator PrepareBatch(NativeChem.ProbeResult p)
        {
            // 结束当天：按酒精选整数份原料→原生API搅拌提取→沉淀→两级过滤→紫外线→药片→蒸发器。
            // 只结算当前工作瓶，不推进全局时钟、不调用净水器或模拟蒸发。
            var c = p.Recipe;
            var currentSolids = FirstKind(p.Parts.Input);
            if (currentSolids.Count != p.InputSolids.Count || currentSolids.Any(x => !p.InputSolids.Contains(x)))
                throw new InvalidOperationException("搅拌机输入格原料已变化，尚未搅拌；请重新结束当天以检查整批。");
            string changedConditions = Preflight(c, p.Solid, p.Pot, true, true);
            if (changedConditions != null) throw new InvalidOperationException("整批开工条件变化，尚未搅拌：" + changedConditions);
            if (!lastCheckedPlan.Selected.SequenceEqual(p.Solids))
                throw new InvalidOperationException("库存或瓶容量变化导致可处理份数改变，尚未搅拌；请重新检查。");
            if (c.Low > 0 && !AlreadyExtracted(p.Pot, c))
            {
                var checkPlate = FindPlate(c);
                if (checkPlate == null || (MachineBlender.GetUpgradeCount(checkPlate) < 2 && !HeatingHookReady()))
                    throw new InvalidOperationException("加热条件已变化，尚未搅拌或补料。");
            }
            Trace("步骤1 · 原料与容器：" + Name(p.Blender) + " #" + p.Blender.uniqueId + "；工作瓶 " + Name(p.Pot) + "，容量 " + Vol(WaterHelper.GetTotalCapacity(p.Pot)));
            if (p.AutoPot)
            {
                if (p.Solids.Count == 0 || p.Solids.Any(x => x.parentInventory != p.Parts.Input
                    || ChemicalHelper.GetTotalVolume(x) <= 0 || Amount(x, c.FeedLiquids[0]) <= 0))
                    throw new InvalidOperationException("没有实际可处理原料，已取消自动装瓶。");
                CheckPot(p);
                MovePot(p.Pot, p.Parts.Container);
                Trace("  自动装入库存空瓶 " + Name(p.Pot) + " #" + p.Pot.uniqueId + "；随后原生搅拌。");
            }
            if (p.Parts.Container.childItem != p.Pot) throw new InvalidOperationException("工作瓶不在本批搅拌机容器槽，尚未搅拌。");
            Trace("  初始成分：" + Composition(p.Pot));
            if (p.Solid != null)
            {
                CheckPot(p);
                int before = WaterHelper.GetTotalVolume(p.Pot), loss = MachineBlender.GetLoss(p.Blender);
                if (p.Solids.Sum(x => (long)ChemicalHelper.GetTotalVolume(x)) > WaterHelper.GetFreeCapacity(p.Pot))
                    throw new InvalidOperationException("[缺容器] 工作瓶装不下整批原料，尚未搅拌。");
                int index = 0;
                foreach (var material in p.Solids)
                {
                    CheckPot(p);
                    if (material.parentInventory != p.Parts.Input || Amount(material, c.FeedLiquids[0]) <= 0)
                        throw new InvalidOperationException("本批原料被移走或成分变化，停止剩余搅拌。");
                    string sourceName = Name(material); int sourceId = material.uniqueId;
                    ChemicalHelper.BlendInto(material, p.Pot, loss);
                    MarkAutoWork();
                    Trace("步骤2 · 搅拌 " + (++index) + "/" + p.Solids.Count + "：使用 " + sourceName + " #" + sourceId + "，原生损耗 " + loss + "%");
                }
                Trace("  整批搅拌完成：共 " + index + " 份，进入工作瓶 " + Vol(WaterHelper.GetTotalVolume(p.Pot) - before));
                runNotes.Add("已搅拌 " + index + " 份原料。");
            }
            else Trace("步骤2 · 识别为半成品，跳过搅拌，不消耗新固体。");
            CaptureRunStage(1, "搅拌液（本次实际）", c.FeedLiquids[0], p.Pot);
            yield return null;
            CheckPot(p);
            int feed = Amount(p.Pot, c.FeedLiquids[0]);
            GameItem plate = null;
            if (ReactableFeed(p.Pot, c) > 0 && c.Low > 0)
            {
                // Revalidate the heating hook before any reagent is consumed.
                plate = FindPlate(c);
                if (plate == null) throw new InvalidOperationException("[缺设备] 无可用的空闲加热板。");
                if (MachineBlender.GetUpgradeCount(plate) < 2 && !HeatingHookReady()) throw new InvalidOperationException("低配板控温补丁未加载，尚未补料。");
                if (SceneSnapshot.Temp(p.Pot) > c.High) throw new InvalidOperationException("原液温度超过此配方的安全区间，请先冷却。");
                string solvent = c.FeedLiquids.Length > 1 ? c.FeedLiquids[1] : null;
                if (solvent != null)
                {
                    int target = SolventTarget(c, feed, solvent);
                    Trace("步骤3 · 溶剂/反应物：" + LiquidName(c.FeedLiquids[0]) + " " + Vol(feed) + "；目标有效"
                        + LiquidName(solvent) + " " + Vol(target) + "（有效成分比 1:" + (target / (double)Math.Max(1, feed)).ToString("0.####", CultureInfo.InvariantCulture) + "）");
                    Dose(solvent, Math.Max(0, target - Amount(p.Pot, solvent)), p.Pot, "  按瓶中原溶液比例补料，水和杂质一同进入");
                }
                if (!Owned(plate) || !PlateAvailable(plate)) throw new InvalidOperationException("本批加热板不可用，停止操作。");
                MovePot(p.Pot, MachineView.Walk(plate).Container);
                originalTarget = plate.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt();
                activeTarget = c.Target; activeModules = MachineBlender.GetUpgradeCount(plate); heatingFailure = null;
                activePlate = plate;
                // Refresh modifiedState: GetTagReadonly reads that cache, not raw state.
                plate.ModifyTag(MachineHeatingPlate.CURRENT_TARGET_TEMP, s => s.SetInt(c.Target), false);
                plate.ModifyTag(MachineHeatingPlate.HEATING_PLATE_STATE, s => s.SetInt(2), false);
                Trace("  加热设置已生效：开关=" + plate.GetTagReadonly(MachineHeatingPlate.HEATING_PLATE_STATE).GetInt()
                    + "；目标=" + plate.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt() + "℃。");
                Trace("步骤4 · 原生加热提取：使用 " + Name(plate) + "，目标 " + c.Target + "℃；由游戏自己的时间、加热和化学反应推进。");
                Trace("  控温方式：" + activeModules + "模块；" + (activeModules < 2 ? "本批前缀控温" : "原生可调目标") + "；补丁已加载=" + HeatingHookReady() + "；结束后恢复原目标 " + originalTarget + "℃。");
                float lastProgress = Time.time, started = Time.time, nextHeatLog = Time.time; int lastFeed = feed;
                try
                {
                    if (pendingEndDay != null)
                    {
                        // End-of-day settlement: advance only this batch using native APIs.
                        // Yield between small chunks so cancel/UI input remains responsive.
                        int simulatedTicks = 0, stagnantTicks = 0, previousFeed = ReactableFeed(p.Pot, c);
                        Trace("  结束当天快速结算：调用原生加热与化学反应，不等待现实加热时间；最多900拍。");
                        while (ReactableFeed(p.Pot, c) > 0)
                        {
                            CheckPot(p, plate);
                            if (!ControlHeating(MachineView.Walk(plate).Container, plate)) throw new InvalidOperationException(heatingFailure);
                            if (++simulatedTicks > 900) throw new InvalidOperationException("结束当天提取达到900拍上限，已停止并保留原液；本次不继续过夜。");
                            MachineHeatingPlate.HandleHeating(MachineView.Walk(plate).Container, 2, plate);
                            MarkAutoWork();
                            if (heatingFailure != null) throw new InvalidOperationException(heatingFailure);
                            p.Pot.ValidateShapeState();
                            if (SceneSnapshot.Temp(p.Pot) > c.High + 0.1) throw new InvalidOperationException("快速结算检测到超温，已停止并保留原液。");
                            ChemicalHelper.HandleTemperatureReaction(p.Pot);
                            p.Pot.ValidateShapeState();
                            int remaining = ReactableFeed(p.Pot, c);
                            stagnantTicks = remaining < previousFeed || SceneSnapshot.Temp(p.Pot) < c.Low ? 0 : stagnantTicks + 1;
                            previousFeed = remaining;
                            if (stagnantTicks > 180) throw new InvalidOperationException("快速结算反应连续180拍没有进展，已停止；请检查稳定剂、溶剂或其他Mod影响。");
                            if (simulatedTicks % 32 == 0)
                            {
                                Status("结束当天结算：" + simulatedTicks + "拍；" + SceneSnapshot.Temp(p.Pot).ToString("0.0") + "℃；原料剩 " + Vol(remaining));
                                yield return null;
                            }
                        }
                        Trace("  结束当天提取结算完成：" + simulatedTicks + "拍；提取物 " + Vol(Amount(p.Pot, c.ProductLiquid)) + "。");
                    }
                    while (ReactableFeed(p.Pot, c) > 0)
                    {
                        if (heatingFailure != null) throw new InvalidOperationException(heatingFailure);
                        CheckPot(p, plate);
                        if (MachineBlender.GetUpgradeCount(plate) != activeModules) throw new InvalidOperationException("加热板模块被改动，已停止。");
                        if ((activeModules >= 2 && plate.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt() != c.Target)
                            || plate.GetTagReadonly(MachineHeatingPlate.HEATING_PLATE_STATE).GetInt() == 0)
                            throw new InvalidOperationException("玩家修改了加热设置，自动流程已让出控制。");
                        double temp = SceneSnapshot.Temp(p.Pot);
                        if (temp > c.High + 0.1) throw new InvalidOperationException("检测到超温，已关闭加热。");
                        int current = ReactableFeed(p.Pot, c);
                        if (Time.time >= nextHeatLog)
                        {
                            nextHeatLog = Time.time + 10f;
                            log?.Log("[加热进度] " + Name(plate) + " #" + plate.uniqueId + "；" + temp.ToString("0.0") + "℃ / 目标 " + c.Target
                                + "℃；原料剩 " + Vol(current) + "；提取物 " + Vol(Amount(p.Pot, c.ProductLiquid)) + "；已等待 " + (Time.time - started).ToString("0") + "游戏秒");
                        }
                        if (current < lastFeed) { lastProgress = Time.time; lastFeed = current; }
                        if (Time.time - lastProgress > 180 && temp >= c.Low) throw new InvalidOperationException("反应超过180秒没有进展。请检查稳定剂、溶剂或其他 Mod 影响；原液保留。");
                        if (Time.time - started > 900) throw new InvalidOperationException("本轮加热等待超过15分钟，已停止并保留原液。");
                        Status("加热板 " + Name(plate) + " · " + temp.ToString("0.0") + "℃；原料剩 " + Vol(current) + "；提取物 " + Vol(Amount(p.Pot, c.ProductLiquid)));
                        yield return null;
                    }
                }
                finally { StopPlate(); }
                if (heatingFailure != null) throw new InvalidOperationException(heatingFailure);
                Trace("  提取结束，已关火。" + LiquidName(c.ProductLiquid) + " " + Vol(Amount(p.Pot, c.ProductLiquid)));
                MovePot(p.Pot, p.Parts.Container);
            }
            else Trace("步骤3~4 · 已有对应有效成分，跳过溶剂补料和提取。");
            if (Amount(p.Pot, c.ProductLiquid) <= 0) throw new InvalidOperationException("没有检测到目标有效成分，停止后续操作。");
            CaptureRunStage(2, "实际", c.ProductLiquid, p.Pot);

            Trace("步骤5 · 去蛋白/去脂肪：按本瓶真实成分补料，等待原生反应。");
            for (int round = 0; round < 4; round++)
            {
                CheckPot(p);
                int protein = Amount(p.Pot, "protein"), fat = Amount(p.Pot, "fat");
                int need = checked((int)DoseMath.ProteinNeed(protein, Amount(p.Pot, "acid"), Amount(p.Pot, "base")));
                if (need > 0)
                {
                    string reagent = ProteinReagent(p.Pot, Amount(p.Pot, "acid"), Amount(p.Pot, "base"), need);
                    if (Sources(reagent, p.Pot).Any()) Dose(reagent, need, p.Pot, "  去蛋白");
                    else if (strict) throw new InvalidOperationException("[缺试剂] 去蛋白需要" + LiquidName(reagent) + "，避免用相反试剂中和已有净酸碱。");
                    else Trace("  [缺试剂] 无" + LiquidName(reagent) + "，保留蛋白残余；未投相反试剂。");
                }
                int lipo = Math.Max(0, (fat + 7) / 8 - Amount(p.Pot, "lipobond_agent"));
                if (lipo > 0)
                {
                    if (Sources("lipobond_agent", p.Pot).Any()) Dose("lipobond_agent", lipo, p.Pot, "  去脂肪");
                    else if (strict) throw new InvalidOperationException("[缺试剂] 去脂肪需要溶脂剂。");
                    else Trace("  [缺试剂] 无溶脂剂，保留脂肪残余。");
                }
                int stagnant = 0, last = protein + fat;
                for (int tick = 0; tick < 480; tick++)
                {
                    if (pendingEndDay != null)
                    {
                        CheckPot(p);
                        ChemicalHelper.HandleTemperatureReaction(p.Pot);
                        MarkAutoWork();
                        p.Pot.ValidateShapeState();
                    }
                    int remaining = Amount(p.Pot, "protein") + Amount(p.Pot, "fat");
                    if (remaining == 0 || stagnant >= 12) break;
                    stagnant = remaining >= last ? stagnant + 1 : 0; last = remaining;
                    Status("等待原生沉淀：蛋白 " + Vol(Amount(p.Pot, "protein")) + "；脂肪 " + Vol(Amount(p.Pot, "fat")));
                    if (pendingEndDay == null || tick % 32 == 31) yield return null;
                }
                if (Amount(p.Pot, "protein") + Amount(p.Pot, "fat") == 0) break;
                if (Amount(p.Pot, "protein") + Amount(p.Pot, "fat") >= protein + fat) break;
            }
            Trace("  反应后：" + Composition(p.Pot));
            CheckPot(p);
            Trace("步骤6 · 固定顺序：滤布预过滤 → 水过滤器精滤。只使用已有且可用的水壶/滤材，不使用净水器。");
            var filtered = new StringBuilder(); var warnings = new List<string>();
            try { FilterBatch(p.Pot, filtered, warnings); if (filtered.Length > 0) MarkAutoWork(); }
            finally
            {
                if (filtered.Length > 0) Trace(filtered.ToString());
                foreach (var warning in warnings) Trace("  [过滤提示] " + warning);
            }
            Trace("  过滤后：" + Composition(p.Pot));
            PurifyWithUV(p.Pot);
            PurifyBatch(p.Pot);
            Trace("  药片净水后：" + Composition(p.Pot));
            CaptureRunStage(3, "实际", c.ProductLiquid, p.Pot);
            CheckPot(p);
            var evaporator = FindEvaporator(p.Pot, c);
            if (evaporator == null) throw new InvalidOperationException("过滤已完成，" + EvaporatorBlockReason(p.Pot) + "原液已保留，自动开关仍开启。");
            var destination = DeliverToEvaporator(evaporator, p.Pot, c);
            runPotId = destination.uniqueId;
            compactOutcome = Good(ChemNames.ProductName(c.ProductId) + " → 蒸发器 #" + evaporator.uniqueId + " · 接收瓶 #" + destination.uniqueId
                + (destination != p.Pot ? "（同类合并；源空瓶 #" + p.Pot.uniqueId + "）" : ""));
            runNotes.Add(Good("提取物合计 " + Vol(Amount(destination, c.ProductLiquid)) + " 已送入蒸发器原液槽，接收瓶 #" + destination.uniqueId + "。"));
            Trace("步骤7 · 已自动送入蒸发器：" + Name(evaporator) + " #" + evaporator.uniqueId + "；接收瓶 " + Name(destination)
                + " #" + destination.uniqueId + "；挥发液合计 " + Vol(NativeChem.WetTotal(destination)) + "；电池可用。请保持原液和电池在设备内，结束营业后由原生过夜蒸发。需回收溶剂时自行在馏出槽放收集瓶。");
            Trace("  产物位置：提取物合计 " + Vol(Amount(destination, c.ProductLiquid)) + " 留在蒸发器原液槽的接收瓶 #" + destination.uniqueId
                + " 中；过夜后也是取这个瓶子。馏出槽只回收蒸出的溶剂，不是药品产物槽。");
            Trace("步骤8 · 过夜后：把干料瓶放回搅拌机容器槽，点“过夜后批量成型”。成型前再次检查干燥度、纯度、输出空间。");
        }
        static IEnumerator ProduceBatch(NativeChem.ProbeResult p)
        {
            // 干燥度在 Run 检查；每次产出前复核瓶位置、配方、空间与最低纯度。
            // 实际耗料、等级、污染和成就交给原生按钮回调，避免复制游戏内部规则。
            var finisher = FindId("chem_finisher");
            var parts = MachineView.Walk(finisher);
            MovePot(p.Pot, parts.Container);
            finisher.ModifyTag(MachineChemFinisher.SELECTED_CHEM_ID_TAG, s => s.SetString(p.Recipe.ProductId), false);
            var recipe = ChemFinisherRecipe.GetRecipeById(p.Recipe.ProductId);
            var produce = NativeControls.ProduceAction(finisher);
            if (recipe == null || produce == null) throw new InvalidOperationException("原生成型接口无法识别，未消耗原液。");
            Trace("步骤8 · 成型：使用 " + Name(finisher) + "；配方 " + ChemNames.ProductName(recipe.identifier)
                + "；每件原液 " + Vol(recipe.requiredVolume) + "；原生污染 " + MachineChemFinisher.GetLoss(finisher) + "%");
            int count = 0;
            while (recipe.CanOutput(p.Pot))
            {
                CheckPot(p);
                if (parts.Container.childItem != p.Pot) throw new InvalidOperationException("工作瓶已被移出成型机，停止自动操作。");
                if (finisher.GetTagReadonly(MachineChemFinisher.SELECTED_CHEM_ID_TAG).GetString() != recipe.identifier)
                    throw new InvalidOperationException("成型机配方被修改，自动流程已让出控制。");
                if (!NativeControls.CanFitProduct(finisher, recipe.identifier)) { Trace("  [缺输出空间] 已停止；尚未压制的原液保留在成型机中。"); break; }
                int before = WaterHelper.GetTotalVolume(p.Pot);
                produce(); // The game's own button callback handles transfer, impurity, grading and achievements.
                int used = before - WaterHelper.GetTotalVolume(p.Pot);
                if (used <= 0) throw new InvalidOperationException("原生按钮没有产出，已停止重试。");
                count++; Status("已产出 " + count + " 件；本件实际用原液 " + Vol(used) + "；瓶中剩 " + Vol(WaterHelper.GetTotalVolume(p.Pot)));
                yield return null;
            }
            Trace("  本批成型结束：共 " + count + " 件；余液 " + Vol(WaterHelper.GetTotalVolume(p.Pot)) + "，瓶子留在所选成型机容器槽。");
        }
    }

    internal static class NativeControls
    {
        // 版本敏感适配点：反射 MachineryButton.callback，定位成型机原生生产按钮。
        // 游戏更新若改变闭包方法名，应先停用自动成型并重新核对，不能猜测按钮。
        static readonly FieldInfo Callback = typeof(MachineryButton).GetField("callback", BindingFlags.NonPublic | BindingFlags.Instance);
        static IEnumerable<MachineryButton> Buttons(PixelElement element)
        {
            if (element is MachineryButton button) yield return button;
            else if (element is MachineryIntake intake)
                foreach (var b in Buttons(intake.content)) yield return b;
            else if (element is MachineryPanelRow row)
                foreach (var panel in row.Panels) foreach (var b in Buttons(panel.content)) yield return b;
            else if (element is MachineryPanel panel)
                foreach (var b in Buttons(panel.content)) yield return b;
            else if (element is MachineryStack stack)
                foreach (var child in stack.elements) foreach (var b in Buttons(child)) yield return b;
        }
        public static Action ProduceAction(GameItem item)
        {
            foreach (var button in Buttons(item?.contentWindow?.childElement))
            {
                var action = Callback?.GetValue(button) as Action;
                if (action?.Method.DeclaringType?.DeclaringType == typeof(MachineChemFinisher)
                    && action.Method.Name == "<ChemFinisher>b__4") return action;
            }
            return null;
        }
        public static bool HasProduceAction(GameItem item) => ProduceAction(item) != null;
        public static bool CanFitProduct(GameItem item, string product)
        {
            // Detached preview has no owned-item ID, inventory or saved-state registration.
            var preview = DirectoryMaster.Create<GameItem>(product);
            if (preview == null) return false;
            try { return GraphUtils.CanAccept(MachineView.Walk(item).Output, preview); }
            finally { preview.Destroy(); }
        }
    }
}



