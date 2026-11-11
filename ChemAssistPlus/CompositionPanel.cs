using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        const int StageCount = 5;
        static readonly string[] StageNames = { "原材料", "搅拌后", "提取后", "净化后", "过夜后" };
        sealed class StageSnapshot { public string Primary; public int[] Parts; public bool Estimated; }
        sealed class SupplySnapshot
        {
            public string Id, Name;
            public double Required, Available;
            public bool Known = true, Short, Limited, Tablets;
        }
        static StageSnapshot[] runStages = new StageSnapshot[StageCount], displayStages = new StageSnapshot[StageCount];
        static SupplySnapshot[] runSupplies = Array.Empty<SupplySnapshot>(), displaySupplies = Array.Empty<SupplySnapshot>();
        static BatchPlan lastCheckedPlan;
        static int runPotId;
        static string runPrimary, lastFailure;

        static string CompactNumber(double number) => number > 0 && number < 0.1 ? "<0.1" : number.ToString("0.#");
        static string CompactVolume(double ul) => CompactNumber(ul / 1000d) + " mL";
        static void RecordFailure(string reason)
        {
            lastFailure = reason; log?.Log("[面板原因] " + reason); RenderFailureBanner();
        }
        static void RenderFailureBanner()
        {
            string text = ""; // Failure reasons are displayed once, with their batch.
            if (!string.IsNullOrEmpty(powerWarning)) text += (text.Length > 0 ? "\n" : "") + Bad(powerWarning);
            window?.Get("failure")?.SetText(text)?.SetVisible(text.Length > 0);
        }
        sealed class BatchDisplay
        {
            public string Title, Detail, Primary;
            public int PotId, Tablets;
            public bool Actual;
            public StageSnapshot[] Stages = new StageSnapshot[StageCount];
            public SupplySnapshot[] Supplies = Array.Empty<SupplySnapshot>();
            public readonly System.Collections.Generic.Dictionary<string, long> Usage = new System.Collections.Generic.Dictionary<string, long>();
        }
        static readonly System.Collections.Generic.List<BatchDisplay> nightDetails = new System.Collections.Generic.List<BatchDisplay>();
        static readonly System.Collections.Generic.List<BatchDisplay> previewDetails = new System.Collections.Generic.List<BatchDisplay>();
        static BatchDisplay runDetail;
        static bool displayNightDetails;
        static void AddCompositionWidgets(CustomUIBuilder ui) => ui.AddLabel("", "batch_details");
        static void BindCompositionWidgets() => RenderCompositionWidgets();
        static void RestoreRunDisplay()
        {
            displayStages = runStages; displaySupplies = runSupplies;
            displayNightDetails = true;
        }
        static void SyncRunDetail()
        {
            if (runDetail == null) return;
            runDetail.Detail = lastReport; runDetail.PotId = runPotId; runDetail.Primary = runPrimary;
            runDetail.Tablets = usedTablets; runDetail.Usage.Clear();
            foreach (var pair in actualUsage) runDetail.Usage[pair.Key] = pair.Value;
        }
        static void RenderCompositionWidgets()
        {
            if (window?.IsAlive != true) return;
            var text = new System.Text.StringBuilder();
            foreach (var batch in displayNightDetails ? nightDetails : previewDetails)
            {
                text.AppendLine("<color=#B9D9E8>" + batch.Title + "</color>");
                if (!string.IsNullOrEmpty(batch.Detail)) text.AppendLine(batch.Detail.Trim());
                var supplies = batch.Supplies.Where(x => batch.Actual
                    ? x.Tablets ? batch.Tablets > 0 : batch.Usage.TryGetValue(x.Id, out long used) && used > 0
                    : x.Tablets || x.Required > 0 || x.Short || !x.Known).ToArray();
                if (supplies.Length > 0)
                {
                    text.AppendLine("<color=#B9D9E8>试剂 · " + (batch.Actual ? "实际 / 开工库存" : "预计 / 库存") + "</color>");
                    foreach (var supply in supplies)
                    {
                        batch.Usage.TryGetValue(supply.Id, out long used);
                        double amount = batch.Actual ? supply.Tablets ? batch.Tablets : used : supply.Required;
                        string value = supply.Tablets && !batch.Actual ? "待定" : !batch.Actual && !supply.Known ? "缺货"
                            : CompactNumber(amount / (supply.Tablets ? 1 : 1000d));
                        string line = supply.Name + "  " + value + " / " + CompactNumber(supply.Available / (supply.Tablets ? 1 : 1000d))
                            + (supply.Tablets ? " 片" : " mL");
                        text.AppendLine(!batch.Actual && supply.Short ? Bad(line) : line);
                    }
                }
                if (batch.Stages.Any(x => x != null)) text.AppendLine("<color=#B9D9E8>处理结果 · 剂量 / 纯度</color>");
                for (int i = 0; i < StageCount; i++)
                {
                    var snapshot = batch.Stages[i]; if (snapshot == null) continue;
                    long total = snapshot.Parts.Sum(x => (long)x);
                    double purity = total > 0 ? 100d * Part(snapshot.Parts, snapshot.Primary) / total : 0;
                    text.AppendLine(StageNames[i] + "  " + (snapshot.Estimated ? "约 " : "") + CompactVolume(total) + " / " + purity.ToString("0.#") + "%");
                }
                text.AppendLine();
            }
            if (text.Length == 0 && displayNightDetails) text.Append("暂无本晚处理记录。");
            window.Get("batch_details")?.SetText(text.ToString().TrimEnd());
            LayoutRebuilder.MarkLayoutForRebuild(window.ContentRoot);
        }
        static StageSnapshot Snapshot(string title, string primary, int[] parts) =>
            new StageSnapshot { Primary = primary, Parts = (int[])parts.Clone(), Estimated = title.Contains("预计") };
        static int[] RawParts(NativeChem.ProbeResult p) => p.Solids.Count == 0 ? WaterHelper.GetLiquidParts(p.Pot)
            : Liquid.Liquids.Select(l => p.Solids.Sum(x => Amount(x, l.identifier))).ToArray();
        static void PreviewComposition(NativeChem.ProbeResult p, BatchPlan plan)
        {
            displayStages = new StageSnapshot[StageCount];
            displayStages[0] = Snapshot("当前", p.Recipe.FeedLiquids[0], RawParts(p));
            displayStages[1] = Snapshot("预计", p.Recipe.FeedLiquids[0], plan.Kept);
            displaySupplies = plan.Supplies.ToArray();
            displayNightDetails = false;
            previewDetails.Add(new BatchDisplay { Title = Name(p.Blender) + " #" + p.Blender.uniqueId,
                Detail = PlanSummary(p, plan) + (!p.Ok ? "\n" + Bad(p.Block) : ""),
                Stages = displayStages, Supplies = displaySupplies });
        }
        static void BeginRunComposition(NativeChem.ProbeResult p)
        {
            runStages = new StageSnapshot[StageCount];
            runStages[0] = Snapshot("实际", p.Recipe.FeedLiquids[0], RawParts(p));
            runSupplies = lastCheckedPlan?.Supplies.ToArray() ?? Array.Empty<SupplySnapshot>();
            runPotId = p.Pot.uniqueId; runPrimary = p.Recipe.ProductLiquid;
            runDetail = new BatchDisplay { Title = Name(p.Blender) + " #" + p.Blender.uniqueId,
                Stages = runStages, Supplies = runSupplies, Actual = true };
            nightDetails.Add(runDetail); SyncRunDetail(); RestoreRunDisplay();
        }
        static void CaptureRunStage(int stage, string title, string primary, GameItem pot)
        {
            runStages[stage] = Snapshot(title, primary, WaterHelper.GetLiquidParts(pot));
            RestoreRunDisplay(); RenderCompositionWidgets();
        }
        static void CaptureOvernightStage()
        {
            var items = Scan();
            foreach (var batch in nightDetails.Where(x => x.Actual && x.PotId != 0 && x.Primary != null))
            {
                var pot = items.FirstOrDefault(x => x.uniqueId == batch.PotId);
                if (pot != null) batch.Stages[4] = Snapshot("实际", batch.Primary, WaterHelper.GetLiquidParts(pot));
            }
            RenderCompositionWidgets();
        }
    }

    // Three aligned columns: name, short center track, right-aligned dosage/purity.
    public sealed class CompositionBarView : MonoBehaviour
    {
        RectTransform fill;
        Image fillImage;
        TextMeshProUGUI nameLabel, valueLabel;
        public void Build()
        {
            var original = GetComponentInChildren<TMP_Text>(true);
            var font = original != null ? original.font : TMP_Settings.defaultFontAsset;
            if (original != null) original.enabled = false;
            var group = GetComponent<LayoutGroup>(); if (group != null) group.enabled = false;
            var fitter = GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0; layout.preferredWidth = 0; layout.flexibleWidth = 1;
            layout.minHeight = layout.preferredHeight = 28; layout.flexibleHeight = 0; layout.layoutPriority = 5;
            var background = Child("Track");
            background.anchorMin = new Vector2(0, 0.5f); background.anchorMax = new Vector2(1, 0.5f);
            background.offsetMin = new Vector2(74, -6); background.offsetMax = new Vector2(-164, 6);
            var track = background.gameObject.AddComponent<Image>(); track.color = new Color(0.10f, 0.13f, 0.17f, 1); track.raycastTarget = false;
            fill = Child("Fill", background); Stretch(fill, 0, 0);
            fillImage = fill.gameObject.AddComponent<Image>(); fillImage.raycastTarget = false;
            nameLabel = Label("Name", font);
            nameLabel.rectTransform.anchorMin = Vector2.zero; nameLabel.rectTransform.anchorMax = new Vector2(0, 1);
            nameLabel.rectTransform.offsetMin = new Vector2(2, 0); nameLabel.rectTransform.offsetMax = new Vector2(72, 0);
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            valueLabel = Label("DoseAndShare", font);
            valueLabel.rectTransform.anchorMin = new Vector2(1, 0); valueLabel.rectTransform.anchorMax = Vector2.one;
            valueLabel.rectTransform.offsetMin = new Vector2(-160, 0); valueLabel.rectTransform.offsetMax = new Vector2(-2, 0);
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;
        }
        RectTransform Child(string name, Transform parent = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent ?? transform, false); return rect;
        }
        static void Stretch(RectTransform rect, float left, float right)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-right, 0);
        }
        TextMeshProUGUI Label(string name, TMP_FontAsset font)
        {
            var text = Child(name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = 13; text.color = Color.white; text.faceColor = new Color32(255, 255, 255, 255);
            text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            return text;
        }
        public void Render(string label, float fraction, string value, Color color)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage.color = color; nameLabel.text = label; valueLabel.text = value;
        }
    }
}
