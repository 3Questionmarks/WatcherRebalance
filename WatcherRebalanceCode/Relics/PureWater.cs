using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Relics;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Relics;

internal static class PureWaterFreeTokenCards
{
    internal const int Amount = 2;

    internal static bool AppliesTo(
        object listener,
        Player player)
    {
        return listener is PureWater pureWater &&
               pureWater.Owner == player;
    }
}

[HarmonyPatch(typeof(PureWater))]
internal static class PureWaterTooltipPatch
{
    [HarmonyPatch("get_ExtraHoverTips")]
    [HarmonyPostfix]
    private static void ExtraHoverTipsPostfix(
        ref IEnumerable<IHoverTip> __result)
    {
        __result =
            __result.Append(
                WatcherRebalanceTips.Token());
    }
}