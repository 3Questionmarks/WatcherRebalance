using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Relics;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Relics;

internal static class HolyWaterFreeTokenCards
{
    internal const int Amount = 5;

    internal static bool AppliesTo(
        object listener,
        Player player)
    {
        return listener is HolyWater holyWater &&
               holyWater.Owner == player;
    }
}

[HarmonyPatch(typeof(HolyWater))]
internal static class HolyWaterTooltipPatch
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