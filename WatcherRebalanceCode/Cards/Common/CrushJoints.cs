using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(CrushJoints), "OnPlay")]
public static class CrushJointsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        CrushJoints __instance,
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
        CrushJoints card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitFx(
                "vfx/vfx_bloody_impact",
                tmpSfx: "blunt_attack.mp3")
            .Execute(ctx);

        bool wasLastCardPlayedSkill =
            CombatManager.Instance.History.CardPlaysStarted
                .LastOrDefault(e =>
                    e.CardPlay.Card.Owner == card.Owner &&
                    e.CardPlay.Card != card)
                ?.CardPlay.Card.Type == CardType.Skill;

        if (!wasLastCardPlayedSkill)
            return;

        await CommonActions.Apply<VulnerablePower>(
            ctx,
            card,
            cardPlay);
    }
}