using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using Watcher.Code.Relics;

namespace WatcherRebalance.WatcherRebalanceCode.Relics;

[HarmonyPatch(typeof(VioletLotus), "get_Rarity")]
internal static class VioletLotusRarityPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref RelicRarity __result)
    {
        __result = RelicRarity.Rare;
    }
}