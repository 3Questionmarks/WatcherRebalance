using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class CollectPowerPatch
{
    [HarmonyPatch(
        typeof(PowerModel),
        "get_ExtraHoverTips")]
    [HarmonyPostfix]
    private static void ExtraHoverTipsPostfix(
        PowerModel __instance,
        ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not CollectPower)
            return;

        __result =
            __result.Concat(
                new IHoverTip[]
                {
                    HoverTipFactory.FromCard<Collect>(),
                    HoverTipFactory.FromCard<Miracle>(true)
                });
    }
}