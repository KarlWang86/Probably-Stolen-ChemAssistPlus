using System;
using System.Linq;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Native batch transfers write state with skipValidate=true. GetTagReadonly reads
        // modifiedState, which can still describe the previous contents. Never mutate either here.
        static int LiveLiquidAmount(GameItem item, string id)
        {
            if (item?.state?.dict == null) return 0;
            return item.state.dict.TryGetValue(Liquid.GetCurrentPartFromId(id), out var tag) ? Math.Max(0, tag.GetInt()) : 0;
        }
        static long LiveLiquidVolume(GameItem item) => Liquid.Liquids.Sum(x => (long)LiveLiquidAmount(item, x.identifier));
        static Core.Recipe LiveLiquidRecipe(GameItem item)
        {
            if (item == null) return null;
            var matches = Core.RecipeCatalog.Recipes.Where(x => LiveLiquidAmount(item, x.FeedLiquids[0]) > 0
                || LiveLiquidAmount(item, x.ProductLiquid) > 0).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
