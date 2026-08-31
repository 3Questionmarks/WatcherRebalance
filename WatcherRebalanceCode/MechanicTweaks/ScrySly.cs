/*Moved to its own mod that is a dependency for this one
using BaseLib.Commands;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace WatcherRebalance.WatcherRebalanceCode.MechanicTweaks;

[HarmonyPatch(typeof(ScryCmd))]
public static class ScrySlyPatch
{
    [HarmonyPatch(
        nameof(ScryCmd.Execute),
        [
            typeof(PlayerChoiceContext),
            typeof(Player),
            typeof(int)
        ])]
    [HarmonyPostfix]
    private static async Task<ScryResult> ExecutePostfix(
        Task<ScryResult> __result,
        PlayerChoiceContext choiceContext)
    {
        ScryResult result =
            await __result;

        foreach (CardModel card in result.Discarded)
        {
            if (!card.Keywords.Contains(CardKeyword.Sly))
                continue;

            await CardCmd.AutoPlay(
                choiceContext,
                card,
                null,
                AutoPlayType.SlyDiscard);
        }

        return result;
    }
}
*/