using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    // 架构入口：见本目录 ARCHITECTURE.md 与 HANDOFF.md。
    // 独立 Mod；Plus 的 partial 文件共享当前存档会话状态，均由 Unity 主线程驱动。
    // 本文件负责：生命周期、设备选择、配方识别、真实溶液投料、过滤、开工预检和 F10 页面。
    // Workflow.cs 负责执行；Core.cs 封装游戏对象遍历；PanelLayout.cs 只处理显示。
    // 运行目录是 Mods/ChemicalWorkflowAssistant；此目录是开发源码，不会被游戏直接加载。
    public static partial class Plus
    {
        static string DisplayVersion => typeof(Plus).Assembly.GetName().Version.ToString(3);
        public const string Id = "chemical_workflow_assistant";
        static ModLog log;
        static GameObject pump;
        static CustomUIWindow window;
        static KeyCode hotkey = KeyCode.F10;
        // 设备自动选择；会话切换时清空引用，避免操作上一存档的对象。
        static readonly Dictionary<GameItem, MachineryButton> headers = new Dictionary<GameItem, MachineryButton>();
        static GameItem selectedBlender, contextBlender, contextPot;
        static Recipe contextChain;
        static bool busy;
        static readonly bool strict = true;
        static readonly Dictionary<string, GameItem> preferredSources = new Dictionary<string, GameItem>();
        static string lastReport = "选择搅拌机，先检查再制作。F10 打开/关闭本面板。";

        public static void Init(ModManifest manifest) { log = new ModLog(manifest); log.Log("化学流程助手 " + DisplayVersion + " 初始化；运行日志记录关键步骤，不记录每帧状态。"); }
        public static void Enable()
        {
            mixingGuardEnabled = true;
            if (pump != null) return;
            log?.Log("模组已启用；低配板控温、自动换滤材、F10库存预检已接入。");
            pump = new GameObject("ChemAssistPlus.Pump");
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<PlusPump>();
            ModHook.OnGameLoadedNormal += ResetSession;
        }
        public static void Disable()
        {
            mixingGuardEnabled = false;
            ClearMixConfirmation();
            CancelJob("模组已禁用。", true);
            ModHook.OnGameLoadedNormal -= ResetSession;
            ModHelper.RemoveGameOptions(Id);
            foreach (var pair in headers) { try { pair.Key.contentWindow?.RemoveHeaderButton(pair.Value); } catch { } }
            headers.Clear();
            window?.Close(); window = null;
            if (pump != null) UnityEngine.Object.Destroy(pump);
            pump = null;
        }
        static void ResetSession()
        {
            ClearMixConfirmation();
            CancelJob("存档已切换。", true);
            headers.Clear(); selectedBlender = contextBlender = contextPot = null;
            contextChain = null; busy = false;
            preferredSources.Clear(); powerWarning = lastPowerNotice = null; powerNoticeAt = 0;
            autoProcessing = true; pendingEndDay = null; pendingEndDayStore = null; resumingEndDay = false; autoWaitReason = lastPreflightLog = null;
            awaitingMorningReport = morningReportReady = nightDidWork = false; nightReport = null;
            nightDetails.Clear(); previewDetails.Clear(); runDetail = null;
            lastNightResult = null; showingNightResult = false;
            lastFailure = null; runStages = new StageSnapshot[StageCount]; displayStages = new StageSnapshot[StageCount];
            runSupplies = displaySupplies = Array.Empty<SupplySnapshot>(); lastCheckedPlan = null; runPotId = 0; runPrimary = null;
            window?.Close(); window = null;
            lastReport = "已载入存档，自动处理默认开启。点击结束当天时静默处理，第二天显示结果。";
        }
        public static void RegisterOptions()
        {
            ModHelper.AddGameOptionKeyBind(Id, ModHelper.OPTION_SECTION_KEYBIND, "plus_panel",
                () => "化学制作助手·优化版", () => hotkey, v => hotkey = v);
        }
        public static void Poll()
        {
            if (!PlayerStore.IsInstanceExist()) return;
            UpdatePowerWarning();
            foreach (var item in Scan().Where(x => x.IsID("blender")))
            {
                if (item.contentWindow == null || headers.ContainsKey(item)) continue;
                var captured = item;
                var button = new MachineryButton("化学助手", () => { selectedBlender = captured; Open(); });
                item.contentWindow.AddHeaderButton(button);
                headers[item] = button;
            }
            if (window?.IsAlive == true && window.IsVisible && !busy && !showingNightResult) RefreshInventoryReport();
        }
        public static void CheckKey()
        {
            if (PlayerStore.IsInstanceExist() && !KeyBindHelper.SuppressHotkeys && Input.GetKeyDown(hotkey))
            { if (window != null && window.IsAlive && window.IsVisible) window.Hide(); else Open(); }
        }
        public static bool ReadyForUi() => PlayerStore.IsInstanceExist() && CustomUIManager.Instance != null;
        // 每次重新遍历玩家拥有物品（包括机器内物品），此处没有库存缓存。
        // F10 和选项改变时主动刷新；运行中保留实际日志。
        static List<GameItem> Scan() => SceneSnapshot.Run().All.Where(Owned).Distinct().ToList();
        static bool Owned(GameItem item) => item != null && GeneralHelper.IsItemOwned(item);
        static int Amount(GameItem item, string id) => item == null ? 0 : SceneSnapshot.Amount(item, id);
        static string Vol(double ul) => (ul / 1000d).ToString("0.###", CultureInfo.InvariantCulture) + " mL";
        static string Name(GameItem item) => item == null ? "未选择" : NativeChem.NameOf(item);
        static string LiquidName(string id) => ChemNames.Name(id);
        static bool Present(GameItem item) => item != null && Scan().Contains(item);
        static bool SlotFree(GameItem item)
        {
            var parts = MachineView.Walk(item);
            return parts?.Container != null && (parts.Container.childItem == null || parts.Container.childItem == contextPot);
        }
        static bool Charged(GameItem item)
        {
            var battery = NativeChem.FindBattery(item);
            return battery != null && PowerHelper.CanDrawPowerSource(battery, Math.Max(1, item.GetTagReadonly("machinery_drawn_power").GetInt()));
        }
        public static GameItem FindId(string id)
        {
            return Scan().Where(x => x.IsID(id)).OrderByDescending(x => SlotFree(x))
                .ThenByDescending(x => id == "evaporator" ? Charged(x) ? 1 : 0 : MachineBlender.GetUpgradeCount(x)).FirstOrDefault();
        }
        static bool PlateAvailable(GameItem p) => SlotFree(p) && p.GetTagReadonly(MachineHeatingPlate.HEATING_PLATE_STATE).GetInt() == 0;
        public static GameItem FindPlate(Recipe chain)
        {
            // 自动选择只借用空闲且关闭的板，低配板由本批限定的原生前缀控温。
            return Scan().Where(x => x.IsID("heating_plate") && PlateAvailable(x))
                .OrderByDescending(MachineBlender.GetUpgradeCount).FirstOrDefault();
        }
        static bool Coarse(GameItem pitcher) => WaterHelper.GetFilterItem(pitcher)?.IsID("kotton_fabric") == true;
        static bool Fine(GameItem pitcher)
        {
            var f = WaterHelper.GetFilterItem(pitcher);
            return f != null && (f.IsID("water_filter") || f.IsID("water_filter_adv"));
        }
        static GameItem Filter(string key) => PlanFilter(key)?.Pitcher;
        public static GameItem FindPitcher() => Filter("cloth") ?? Filter("fine");

        static Recipe Detect(GameItem pot, GameItem solid)
        {
            if (solid != null) return NativeChem.ChainOfSolid(solid);
            var matches = RecipeCatalog.Recipes.Where(c => Amount(pot, c.FeedLiquids[0]) > 0 || Amount(pot, c.ProductLiquid) > 0).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
        public static NativeChem.ProbeResult Probe()
        {
            // 只处理所选搅拌机输入格的全部原料，不从仓库另取固体。
            var result = new NativeChem.ProbeResult();
            var blender = contextBlender ?? selectedBlender;
            if (blender == null) blender = Scan().FirstOrDefault(x => x.IsID("blender") &&
                (MachineView.Walk(x)?.Container?.childItem != null || NativeChem.FirstBlendable(MachineView.Walk(x)?.Input) != null));
            if (!Present(blender)) { result.Block = "没有可用搅拌机；放入原料后显示用量。"; return result; }
            result.Blender = blender; result.Parts = MachineView.Walk(blender);
            result.Solids = FirstKind(result.Parts.Input);
            result.InputSolids = result.Solids.ToList();
            result.Solid = result.Solids.FirstOrDefault();
            result.Pot = result.Parts?.Container?.childItem;
            // Establish a real, matching batch before even planning an automatic bottle.
            if (result.Solids.Count == 0 && (result.Pot == null || !RecipeCatalog.Recipes.Any(c =>
                Amount(result.Pot, c.FeedLiquids[0]) > 0 || Amount(result.Pot, c.ProductLiquid) > 0)))
            { result.Block = "没有待处理原料或半成品；不会自动放入容器。"; return result; }
            result.Recipe = Detect(result.Pot, result.Solid);
            if (result.Recipe == null) { result.Block = "首种原料暂不支持，或半成品含多种配方；请分开存放。"; return result; }
            if (result.Solids.Any(x => Amount(x, result.Recipe.FeedLiquids[0]) <= 0))
            { result.Block = "第一种原料无法匹配配方，本次不处理。"; return result; }
            if (result.Pot != null && RecipeCatalog.Recipes.Any(c => c != result.Recipe
                && (Amount(result.Pot, c.FeedLiquids[0]) > 0 || Amount(result.Pot, c.ProductLiquid) > 0)))
            { result.Block = "工作瓶含其他配方，请换空瓶或分开处理。"; return result; }
            if (result.Pot == null && result.Solid != null && result.Parts.Container != null)
            {
                // Only borrow loose empty vessels, never another machine's working bottle or a filter pitcher.
                result.Pot = Scan().Where(x => LooseBottle(x) && WaterHelper.GetTotalVolume(x) == 0
                    && GraphUtils.CanAccept(result.Parts.Container, x))
                    .OrderByDescending(WaterHelper.GetTotalCapacity).ThenBy(x => x.uniqueId).FirstOrDefault();
                result.AutoPot = result.Pot != null;
            }
            if (result.Pot == null) { result.Block = "搅拌机缺工作瓶，库存也没有可自动装入的空瓶。请准备空瓶后再开始；尚未搅拌。"; return result; }
            if (result.Solid == null && Amount(result.Pot, result.Recipe.FeedLiquids[0]) <= 0 && Amount(result.Pot, result.Recipe.ProductLiquid) <= 0)
            { result.Block = "瓶中没有对应原浆或提取物。"; return result; }
            contextPot = result.Pot; contextChain = result.Recipe;
            return result;
        }
        static int ReactableFeed(GameItem pot, Recipe chain) => chain.ProductId == "fanta_pill"
            ? Math.Max(0, Amount(pot, "methylol") - Amount(pot, "stabilizing_agent")) : Amount(pot, chain.FeedLiquids[0]);
        public static bool AlreadyExtracted(GameItem pot, Recipe chain) => (chain.FeedLiquids[0] == chain.ProductLiquid || ReactableFeed(pot, chain) == 0) && Amount(pot, chain.ProductLiquid) > 0;
        public static int SolventTarget(Recipe chain, int feed, string solvent)
        {
            return SolventTargetForParts(chain, feed, solvent, Amount(contextPot, "stabilizing_agent"));
        }
        static int SolventTargetForParts(Recipe chain, int feed, string solvent, int stabilizer)
        {
            if (chain.ProductId == "fanta_pill") return (int)Math.Ceiling(Math.Max(0, feed - stabilizer) * 0.75);
            if (chain.ProductId == "caffeine_pill") return (int)Math.Ceiling(feed * (solvent == "water" ? 0.25 : 0.75));
            return checked(feed * 5);
        }
        static int EffectiveAmount(GameItem item, string id) => id == "acid" ? Math.Max(0, Amount(item, id) - Amount(item, "base"))
            : id == "base" ? Math.Max(0, Amount(item, id) - Amount(item, "acid")) : Amount(item, id);
        static int EffectivePart(int[] parts, string id) => id == "acid" ? Math.Max(0, Part(parts, id) - Part(parts, "base"))
            : id == "base" ? Math.Max(0, Part(parts, id) - Part(parts, "acid")) : Part(parts, id);
        static IEnumerable<GameItem> Sources(string liquid, GameItem target)
        {
            preferredSources.TryGetValue(liquid, out var preferred);
            return SceneSnapshot.Run().LiquidContainers.Where(x => Owned(x) && x != target && EffectiveAmount(x, liquid) > 0)
                .Where(x => !RecipeCatalog.Recipes.Any(c => (c.FeedLiquids[0] != liquid && Amount(x, c.FeedLiquids[0]) > 0)
                    || (c.ProductLiquid != liquid && Amount(x, c.ProductLiquid) > 0)))
                // Newly distilled alcohol must outrank the previously used ordinary bottle.
                .OrderByDescending(x => liquid == "alcohol" ? (double)EffectiveAmount(x, liquid) / Math.Max(1, WaterHelper.GetTotalVolume(x)) : 0)
                .ThenByDescending(x => x == preferred)
                .ThenByDescending(x => (double)EffectiveAmount(x, liquid) / Math.Max(1, WaterHelper.GetTotalVolume(x)))
                .ThenBy(x => x.uniqueId);
        }
        // Real proportional transfer; the original removed only the active chemical.
        public static int TakeLiquid(SceneSnapshot ignored, string id, int needed, GameItem target, NativeChem.TakeLog takeLog)
        {
            // 按源溶液实际浓度计算总体积；水/杂质与有效成分同比转移，不提纯或凭空拆分。
            if (needed <= 0) return 0;
            int moved = 0;
            foreach (var source in Sources(id, target).ToList())
            {
                for (int attempt = 0; attempt < 3 && moved < needed; attempt++)
                {
                    int active = EffectiveAmount(source, id), total = WaterHelper.GetTotalVolume(source);
                    if (active <= 0 || total <= 0) break;
                    long requested = DoseMath.RequiredSolution(needed - moved, active, total);
                    int pour = (int)Math.Min(Math.Min(requested, total), WaterHelper.GetFreeCapacity(target));
                    if (pour <= 0) break;
                    int before = EffectiveAmount(target, id);
                    double temperature = SceneSnapshot.Temp(source);
                    // TransferLiquidToChem SETS each component; it does not add to existing liquid.
                    var transferredParts = WaterHelper.RemoveAndReturn(source, pour);
                    if (transferredParts.Any(x => x.Value > 0)) MarkAutoWork();
                    AddSolution(target, transferredParts, temperature);
                    WaterHelper.UpdateContainerGoodType(source);
                    WaterHelper.UpdateContainerGoodType(target);
                    int actual = EffectiveAmount(target, id) - before;
                    preferredSources[id] = source;
                    moved += actual;
                    takeLog?.Sources.Add(Name(source) + " #" + source.uniqueId + "：当前净浓度 " + (100d * active / total).ToString("0.###")
                        + "%；溶液 " + Vol(pour) + "，净有效成分 " + Vol(actual));
                    if (WaterHelper.IsEmpty(source)) takeLog?.Emptied.Add(Name(source));
                    if (actual <= 0) throw new InvalidOperationException("同比补料未增加目标成分，已停止继续消耗试剂。");
                }
                if (moved >= needed) break;
            }
            return moved;
        }
        static void AddSolution(GameItem target, IEnumerable<KeyValuePair<string, int>> parts, double temperature)
        {
            var grouped = parts.GroupBy(x => x.Key).Select(x => new KeyValuePair<string, int>(x.Key, checked(x.Sum(v => v.Value)))).ToList();
            int incoming = checked(grouped.Sum(x => x.Value));
            if (incoming == 0) return;
            if (incoming > WaterHelper.GetFreeCapacity(target)) throw new InvalidOperationException("回瓶空间不足，停止转移。");
            ChemicalHelper.HandleReceiveLiquidTemp(target, incoming, temperature);
            target.ValidateShapeState();
            double mixed = SceneSnapshot.Temp(target);
            foreach (var part in grouped) WaterHelper.AddLiquid(target, part.Key, part.Value);
            target.ModifyTag(ChemicalHelper.CONTAINER_TEMP_TAG, s => s.SetDouble(mixed), false);
        }
        // Bound each raw portion by the current filter's real durability. A portion passes
        // each native filter stage once; already-filtered liquid is buffered until that stage ends.
        static bool FilterPortionFits(int[] parts, GameItem filter)
        {
            long cost = 0; int lastCost = 0;
            int size = filter.GetTagReadonly("FILTER_SIZE_TAG").GetInt();
            for (int i = 0; i < parts.Length; i++)
            {
                var liquid = Liquid.Liquids[i];
                if (parts[i] <= 0 || liquid.identifier == "microbe" || size > liquid.particleSize || liquid.filterVolumeRemoved <= 0) continue;
                int floor = (int)(parts[i] * liquid.filterMinimumPurity);
                long actions = ((long)parts[i] - floor + liquid.filterVolumeRemoved - 1) / liquid.filterVolumeRemoved;
                if (actions > 10000) return false;
                if (actions > 0) { cost += actions * liquid.filterDurabilityUsage; lastCost = liquid.filterDurabilityUsage; }
            }
            return cost == 0 || cost - lastCost < DurabilityHelper.GetRealDurability(filter);
        }
        public static void FilterBatch(GameItem pot, StringBuilder report, List<string> warnings)
        {
            foreach (string key in new[] { "cloth", "fine" })
            {
                var buffered = new List<KeyValuePair<string, int>>();
                int chunks = 0, original = WaterHelper.GetTotalVolume(pot);
                double temperature = SceneSnapshot.Temp(pot);
                try
                {
                    while (WaterHelper.GetTotalVolume(pot) > 0)
                    {
                        if (++chunks > 4096) throw new InvalidOperationException("过滤分批达到上限，剩余原液保留。");
                        var pitcher = EnsureFilter(key, report);
                        if (pitcher == null) throw new InvalidOperationException(FilterReason(key) + "剩余原液保留。");
                        if (pitcher == pot) throw new InvalidOperationException("工作瓶不能同时作为过滤水壶。");
                        var material = WaterHelper.GetFilterItem(pitcher);
                        var parts = WaterHelper.GetLiquidParts(pot);
                        int low = 0, high = Math.Min(parts.Sum(), WaterHelper.GetTotalCapacity(pitcher));
                        while (low < high)
                        {
                            int middle = low + (high - low + 1) / 2;
                            if (FilterPortionFits(WaterHelper.SplitProportionally(parts, middle), material)) low = middle;
                            else high = middle - 1;
                        }
                        if (low <= 0) throw new InvalidOperationException("滤材当前耐久不足以处理最小份额，原液保留。");
                        int durability = DurabilityHelper.GetRealDurability(material);
                        var taken = WaterHelper.RemoveAndReturn(pot, low).ToList();
                        int start = buffered.Count;
                        buffered.AddRange(taken); // Keep every removed component recoverable even if a native call fails.
                        MarkAutoWork();
                        for (int i = 0; i < taken.Count; i++)
                        {
                            var component = taken[i];
                            int filtered = WaterHelper.HandleFilter(component.Key, component.Value, pot, material);
                            buffered[start + i] = new KeyValuePair<string, int>(component.Key, filtered);
                        }
                        report.AppendLine((key == "cloth" ? "滤布" : "滤芯") + " #" + material.uniqueId + "：处理 " + Vol(low)
                            + "，耐久 " + durability + " → " + DurabilityHelper.GetRealDurability(material));
                    }
                }
                finally
                {
                    AddSolution(pot, buffered, temperature);
                    WaterHelper.UpdateContainerGoodType(pot);
                }
                report.AppendLine((key == "cloth" ? "滤布完成" : "精滤完成") + "：" + chunks + "批，" + Vol(original) + " → " + Vol(WaterHelper.GetTotalVolume(pot)));
            }
        }
        static void PrepareRoute(Recipe chain)
        {
            if (chain.ProductId == "caffeine_pill") RecipeCatalog.ApplyRoute(chain, "alcohol");
        }
        static void SetReport(string text)
        {
            lastReport = text;
            if (window?.IsAlive == true) { window.Get("report")?.SetText(text); RenderFailureBanner(); RenderCompositionWidgets(); RefreshPanelText(); }
        }
        public static void Open() => OpenPanel(true);
        static void OpenPanel(bool refresh)
        {
            if (!ReadyForUi()) return;
            if (!busy) SelectWorkingBlender();
            showingNightResult = !refresh;
            if (!refresh) RestoreRunDisplay();
            window?.Close();
            var ui = CustomUIManager.Window(Id + ".plus", "化学流程助手 · " + DisplayVersion, CustomUIManager.LAYER_PAPER)
                .UseMetaPrefabs(false).UseMachineryStyle(true)
                .SetSize(450, 720).SetDraggable(true).SetCloseOnEscape(true);
            ui.AddLabel(autoProcessing ? "结束当天自动处理" : "自动处理已关闭", "auto_status");
            ui.BeginRow().AddButton("当前用量", RefreshInventoryReport)
                .AddButton("本晚结果", () => { showingNightResult = true; RestoreRunDisplay(); SetReport("本晚处理结果（按搅拌机排列）"); }).End();
            ui.BeginScroll(554).AddLabel("", "failure").AddLabel(lastReport, "report");
            AddCompositionWidgets(ui);
            ui.End();
            ui.BeginRow()
                .AddToggle("自动处理", autoProcessing, SetAutoProcessing, "auto_processing")
                .AddToggle("次日弹窗", showMorningPopup, SetMorningPopup, "morning_popup")
                .AddButton("刷新", RefreshInventoryReport)
                .AddButton("关闭", () => window?.Hide()).End();
            window = ui.Show();
            BindCompositionWidgets(); RenderFailureBanner();
            FitPanel(true);
            if (refresh) RefreshInventoryReport();
        }
    }

    public sealed class PlusPump : MonoBehaviour
    {
        // Unity 主线程驱动：按键、协程状态机、布局、每 2 秒补挂搅拌机入口。
        // 不用后台线程调用 Unity 或库存 API；游戏暂停时工作流随 Time.time 暂停。
        float nextPoll;
        bool optionsReady;
        void Update()
        {
            try
            {
                if (!optionsReady && Plus.ReadyForUi()) { Plus.RegisterOptions(); optionsReady = true; }
                Plus.CheckKey();
                Plus.TickMixConfirmation();
                Plus.TickJob();
                Plus.TickMorningReport();
                Plus.TickPanelLayout();
                if (Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + 2f; Plus.Poll(); }
            }
            catch (Exception e) { Debug.LogWarning("[ChemAssistPlus] " + e.Message); nextPoll = Time.unscaledTime + 10f; }
        }
    }
}





