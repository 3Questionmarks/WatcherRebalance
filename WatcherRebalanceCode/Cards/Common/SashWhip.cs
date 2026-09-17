using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(SashWhip), "OnPlay")]
public static class SashWhipPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        SashWhip __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayWithCustomFx(
            __instance,
            ctx,
            cardPlay);

        return false;
    }

    private static async Task PlayWithCustomFx(
        SashWhip card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitFx(
                "vfx/vfx_big_slash",
                tmpSfx: "silent_attack")
            .Execute(ctx);

        bool wasLastCardPlayedAttack =
            CombatManager.Instance.History.CardPlaysStarted
                .LastOrDefault(e =>
                    e.CardPlay.Card.Owner == card.Owner &&
                    e.CardPlay.Card != card)
                ?.CardPlay.Card.Type == CardType.Attack;

        if (!wasLastCardPlayedAttack ||
            cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.Apply<WeakPower>(
            ctx,
            cardPlay.Target,
            card);
    }
}