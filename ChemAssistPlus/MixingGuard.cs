using System;
using System.Linq;
using HarmonyLib;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class MixRequest
        {
            public PlayerStore Store;
            public GameItem Source, Target;
            public GameInventory SourceParent, TargetParent;
            public int[] SourceParts, TargetParts;
            public int TargetCapacity;
            public GenericConfirmationUIManager Dialog;
            public string Body;
        }

        static bool mixingGuardEnabled;
        static MixRequest pendingMix, approvedMix;

        static bool HasRecipe(int[] parts, Recipe recipe) =>
            Part(parts, recipe.FeedLiquids[0]) > 0 || Part(parts, recipe.ProductLiquid) > 0;

        // Only the material-derived precursor and product identify a recipe.
        // FeedLiquids[1] is a solvent/reagent and must never identify a conflicting batch.
        static double MaterialPercentage(int[] parts, Recipe recipe) => 100d
            * new[] { recipe.FeedLiquids[0], recipe.ProductLiquid }.Distinct().Sum(x => (long)Part(parts, x))
            / Math.Max(1L, parts.Sum(x => (long)x));

        static string MixReason(GameItem source, GameItem target, int[] incoming, int[] targetParts)
        {
            var from = RecipeCatalog.Recipes.Where(x => HasRecipe(incoming, x)).ToList();
            var into = RecipeCatalog.Recipes.Where(x => HasRecipe(targetParts, x)).ToList();
            if (from.Count == 0 || into.Count == 0) return null;
            if (from.Any(a => into.Any(b => a != b))) return "两瓶含不同配方，混合后会混装在同一瓶。";
            if ((NativeChem.WetTotal(source) <= WaterHelper.DRY_TOLERANCE_UL)
                != (NativeChem.WetTotal(target) <= WaterHelper.DRY_TOLERANCE_UL))
                return "一瓶已蒸干，另一瓶仍有挥发液；混合会重新弄湿干料。";
            double difference = Math.Abs(MaterialPercentage(incoming, from[0]) - MaterialPercentage(targetParts, from[0]));
            return difference > MergeConcentrationPoints + 1e-9
                ? "同配方浓度相差 " + difference.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                    + " 个百分点（超过 " + MergeConcentrationPoints + "）。" : null;
        }

        static string BatchState(GameItem item, int[] parts) => string.Join("\n", RecipeCatalog.Recipes
            .Where(x => HasRecipe(parts, x)).Select(x => LiquidName(x.ProductLiquid)
                + (x.FeedLiquids[0] != x.ProductLiquid && Part(parts, x.FeedLiquids[0]) > 0 ? "（含原浆）" : "") + " "
                + MaterialPercentage(parts, x).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%"))
            + "\n挥发液 " + Vol(NativeChem.WetTotal(item))
            + (NativeChem.WetTotal(item) <= WaterHelper.DRY_TOLERANCE_UL ? " · 已蒸干" : " · 未蒸干");

        static void MixingNotice(string message)
        {
            log?.Log("[混液确认] " + message);
            if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(message, "default");
        }

        static void ClearMixConfirmation()
        {
            var request = pendingMix;
            pendingMix = approvedMix = null;
            // Do not close a native dialog which has since replaced ours.
            if (request?.Dialog != null && request.Dialog.bodyTMP != null && request.Dialog.bodyTMP.text == request.Body)
                request.Dialog.ClosePanel();
            RestoreMixDialogView();
        }

        public static void TickMixConfirmation()
        {
            var request = pendingMix;
            if (request != null && (!PlayerStore.IsInstanceExist() || PlayerStore.Instance != request.Store
                || request.Dialog == null || request.Dialog.panel == null || !request.Dialog.panel.activeInHierarchy
                || request.Dialog.bodyTMP == null || request.Dialog.bodyTMP.text != request.Body))
                pendingMix = null;
            if (mixDialogView != null && mixDialogView.Request != pendingMix) RestoreMixDialogView();
        }

        static void ConfirmMix(MixRequest request)
        {
            if (!mixingGuardEnabled || pendingMix != request) return;
            pendingMix = null;
            try
            {
                // Approval belongs to this pair, in this save, with precisely the contents shown.
                // Heating/reactions, moving bottles, or loading a save invalidate the request.
                if (!PlayerStore.IsInstanceExist() || PlayerStore.Instance != request.Store
                    || !Present(request.Source) || !Present(request.Target)
                    || request.Source.parentInventory != request.SourceParent
                    || request.Target.parentInventory != request.TargetParent
                    || WaterHelper.GetTotalCapacity(request.Target) != request.TargetCapacity
                    || !WaterHelper.GetLiquidParts(request.Source).SequenceEqual(request.SourceParts)
                    || !WaterHelper.GetLiquidParts(request.Target).SequenceEqual(request.TargetParts))
                {
                    MixingNotice("瓶子或成分已变化，本次未倒液；请重新操作并确认。");
                    return;
                }
                approvedMix = request;
                WaterHelper.TransferLiquid(request.Source, request.Target);
                WaterHelper.UpdateWaterBottleSound(request.Source);
                WaterHelper.UpdateWaterBottleSound(request.Target);
                log?.Log("[混液确认] 玩家允许：瓶 #" + request.Source.uniqueId + " → 瓶 #"
                    + request.Target.uniqueId + "；已调用原生倒液。");
            }
            catch (Exception error)
            {
                log?.Error(error.ToString());
                MixingNotice("倒液发生异常，已停止；请检查两瓶当前成分。");
            }
            finally { approvedMix = null; }
        }

        internal static bool BeforeLiquidTransfer(GameItem source, GameItem target)
        {
            if (!mixingGuardEnabled) return true;
            if (approvedMix != null && approvedMix.Source == source && approvedMix.Target == target)
            {
                approvedMix = null; // One native invocation, never a lasting bypass.
                return true;
            }
            if (source == null || target == null || source == target
                || !source.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)
                || !target.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)) return true;
            try
            {
                int amount = Math.Min(WaterHelper.GetTotalVolume(source), WaterHelper.GetFreeCapacity(target));
                if (amount <= 0) return true;
                var sourceParts = WaterHelper.GetLiquidParts(source);
                var targetParts = WaterHelper.GetLiquidParts(target);
                var incoming = WaterHelper.SplitProportionally(sourceParts, amount);
                string reason = MixReason(source, target, incoming, targetParts);
                if (reason == null) return true;
                if (pendingMix != null) return false;
                var dialog = GenericConfirmationUIManager.Instance;
                if (dialog == null || dialog.panel == null || dialog.panel.activeInHierarchy)
                {
                    MixingNotice("此次原液混合需要确认，尚未倒液；请关闭当前对话框后重试。");
                    return false;
                }
                // Native automatic sales also use TransferLiquid. Do not queue a deferred
                // operation on unowned or missing items, or while our workflow is running.
                if (busy || !PlayerStore.IsInstanceExist() || !Present(source) || !Present(target))
                {
                    MixingNotice("此次原液混合需要确认，本次倒液已阻止；请在空闲时手动操作并确认。");
                    return false;
                }
                var request = new MixRequest { Store = PlayerStore.Instance, Source = source, Target = target,
                    SourceParent = source.parentInventory, TargetParent = target.parentInventory,
                    SourceParts = sourceParts, TargetParts = targetParts,
                    TargetCapacity = WaterHelper.GetTotalCapacity(target), Dialog = dialog };
                request.Body = "<size=117%>原液混合确认</size>\n" + reason + "\n\n来源瓶 #" + source.uniqueId
                    + "\n" + BatchState(source, sourceParts)
                    + "\n\n接收瓶 #" + target.uniqueId + "\n" + BatchState(target, targetParts)
                    + "\n\n最多转入 " + Vol(amount) + "，仍要混合吗？";
                pendingMix = request;
                dialog.OpenPanel(request.Body, "确定混合", "取消", () => ConfirmMix(request), () =>
                {
                    if (pendingMix != request) return;
                    pendingMix = null;
                    log?.Log("[混液确认] 玩家取消：瓶 #" + source.uniqueId + " → 瓶 #" + target.uniqueId + "；未倒液。");
                });
                ShowReadableMixDialog(request);
                log?.Log("[混液确认] 等待玩家选择：瓶 #" + source.uniqueId + " → 瓶 #" + target.uniqueId);
                return false;
            }
            catch (Exception error)
            {
                if (pendingMix?.Source == source && pendingMix.Target == target) ClearMixConfirmation();
                log?.Error(error.ToString());
                MixingNotice("混液检查未完成，本次未倒液；请检查容器后重试。");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.TransferLiquid), new[] { typeof(GameItem), typeof(GameItem) })]
    static class LiquidMixingHook
    {
        static bool Prefix(GameItem sourceContainer, GameItem targetContainer) =>
            Plus.BeforeLiquidTransfer(sourceContainer, targetContainer);
    }
}
