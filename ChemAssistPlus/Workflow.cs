using System;
using System.Collections;
using System.Collections.Generic;
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
            SetReport(runPlan + (!usedUV || string.IsNullOrEmpty(uvResult) ? "" : "\n" + uvResult)
                + (usedTablets == 0 || string.IsNullOrEmpty(tabletResult) ? "" : "\n" + tabletResult)
                + (string.IsNullOrEmpty(compactOutcome) ? "" : "\n" + compactOutcome)
                + (string.IsNullOrEmpty(jobStatus) ? "" : "\n当前：" + jobStatus));
            SyncRunDetail(); RenderCompositionWidgets();
        }
        static void Status(string message) { jobStatus = message; Publish(); }
        static void Run(bool prep, bool overnight, bool checkOnly)
        {
            if (busy) { Status("已有自动操作正在进行，可点击停止。" ); return; }
            contextMachine = selectedMachine;
            try
            {
                var p = Probe();
                if (!p.Ok) { log?.Log("[开工受阻] " + p.Block); RecordFailure(p.Block); SetReport(Bad(p.Block)); contextMachine = null; return; }
                PrepareRoute(p.Recipe);
                if (!prep && (p.Solid != null || Amount(p.Pot, p.Recipe.ProductLiquid) <= 0))
                { SetReport("步骤7 · 成型前检查\n当前还不是提取完成的干料。请先开启自动处理并送入蒸发器，再由原生蒸发器过夜。"); contextMachine = null; return; }
                if (!prep && NativeChem.WetTotal(p.Pot) > WaterHelper.DRY_TOLERANCE_UL)
                { SetReport("步骤7 · 原液未干燥\n挥发液合计 " + Vol(NativeChem.WetTotal(p.Pot)) + "，需≤" + Vol(WaterHelper.DRY_TOLERANCE_UL) + "。请继续原生过夜蒸发。" ); contextMachine = null; return; }
                string blocked = Preflight(p.Recipe, p.Solid, p.Pot, prep, true);
                if (blocked != null || checkOnly) { if (blocked != null) RecordFailure(blocked); SetReport(lastReport); contextMachine = null; return; }
                if (prep) { p.Solids = lastCheckedPlan.Selected.ToList(); p.Solid = p.Solids.FirstOrDefault(); }
                steps.Clear(); jobStatus = ""; runPlan = lastReport.Replace("条件已满足，结束当天时处理。", "本批预检通过。"); actualUsage.Clear(); runNotes.Clear(); usedTablets = 0;
                compactOutcome = ""; tabletResult = null; uvResult = null; usedUV = false;
                if (!prep) lastCheckedPlan = null;
                lastFailure = null; RenderFailureBanner(); BeginRunComposition(p);
                Trace("制作前检查\n" + lastReport + "\n——本次实际过程——");
                busy = true;
                job = prep ? PrepareBatch(p) : ProduceBatch(p);
                nextJobTick = Time.time;
            }
            catch (Exception e)
            {
                contextMachine = null;
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
                    contextMachine = null; jobStatus = ""; Publish();
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
                finally { busy = false; contextMachine = null; }
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
            if (IsFanta(p.Recipe))
            {
                var fanta = PrepareFantaBatch(p);
                using (fanta as IDisposable)
                    while (fanta.MoveNext()) yield return fanta.Current;
                yield break;
            }
            PrepareMaterials(p);
            yield return null;

            // TickJob 手动推进任务；逐个转发 yield，取消时释放子迭代器以关火。
            var extraction = ExtractBatch(p);
            using (extraction as IDisposable)
                while (extraction.MoveNext()) yield return extraction.Current;

            var settling = SettleImpurities(p);
            using (settling as IDisposable)
                while (settling.MoveNext()) yield return settling.Current;

            FinishPreparedBatch(p);
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



