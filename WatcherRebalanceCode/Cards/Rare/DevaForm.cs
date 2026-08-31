using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Core;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// DEVA FORM - CARD TOOLTIPS
// =============================================================
//
// Add previews for:
//
// - Calm
// - Wrath
// - Divinity
// - Dexterity
// - Strength
// - Intangible
//
// This is deliberately separate from the OnPlay Harmony patch.
// =============================================================

[HarmonyPatch(
    typeof(DevaForm),
    MethodType.Constructor)]
public static class DevaFormTooltipPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        DevaForm __instance)
    {
        MethodInfo? withTips =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithTips" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(Func<CardModel, IEnumerable<IHoverTip>>));


        if (withTips == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithTips.");
        }


        Func<CardModel, IEnumerable<IHoverTip>> devaFormTips =
            _ =>
            [
                // Stances
                WatcherHoverTipFactory.FromStance<CalmStance>(),
                WatcherHoverTipFactory.FromStance<WrathStance>(),
                WatcherHoverTipFactory.FromStance<DivinityStance>(),

                // Powers
                HoverTipFactory.FromPower<DexterityPower>(),
                HoverTipFactory.FromPower<StrengthPower>(),
                HoverTipFactory.FromPower<IntangiblePower>()
            ];


        withTips.Invoke(
            __instance,
            [devaFormTips]);
    }
}


// =============================================================
// DEVA FORM - ON PLAY
// =============================================================
//
// Completely replaces the original DevaPower effect.
//
// Playing Deva Form now applies our custom DevaFormPower.
// =============================================================

[HarmonyPatch(
    typeof(DevaForm),
    "OnPlay")]
public static class DevaFormOnPlayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        DevaForm __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0);


        return false;
    }


    private static async Task NewOnPlay(
        DevaForm card,
        PlayerChoiceContext choiceContext)
    {
        await MegaCrit.Sts2.Core.Commands.PowerCmd
            .Apply<DevaPower>(
                choiceContext,
                card.Owner.Creature,
                1,
                card.Owner.Creature,
                card);
    }
}