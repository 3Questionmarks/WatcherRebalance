using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class PrayPatch
{
    [HarmonyPatch(typeof(Pray), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Pray __instance)
    {
        AddMiracleTooltip(__instance);
    }

    [HarmonyPatch(typeof(Pray), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Pray __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = NewOnPlay(
            __instance,
            ctx,
            cardPlay);

        return false;
    }

    private static async Task NewOnPlay(
        Pray card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        int currentMantra =
            card.Owner.Creature
                .GetPower<MantraPower>()
                ?.Amount
            ?? 0;

        int mantraGain =
            card.DynamicVars
                .Power<MantraPower>()
                .IntValue;

        // Evaluate Pray's conditional effects using the amount of
        // Mantra we would have immediately after gaining this card's
        // Mantra, before reaching 10 can convert Mantra into Divinity.
        int mantraForEffects =
            currentMantra + mantraGain;

        await CommonActions.ApplySelf<MantraPower>(
            ctx,
            card);

        int mantraPerInsight =
            card.IsUpgraded
                ? 2
                : 3;

        int insightCap =
            card.IsUpgraded
                ? 5
                : 3;

        int insightCount =
            Math.Min(
                mantraForEffects / mantraPerInsight,
                insightCap);
        
        if (insightCount > 0)
        {
            await WatcherCmd.GiveCards<Insight>(
                card.Owner,
                insightCount,
                PileType.Draw,
                CardPilePosition.Random);
        }

        //if (mantraForEffects >= 5)
        //{
        //    await WatcherCmd.GiveCard<Miracle>(
        //        card.Owner,
        //        PileType.Hand,
        //        CardPilePosition.Top,
        //        skipAnimation: true);
        //}
    }

    private static void AddMiracleTooltip(
        Pray card)
    {
        MethodInfo? withTip =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithTip" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(TooltipSource));

        if (withTip == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTip(TooltipSource).");
        }

        //var miracleTip =
        //    new TooltipSource(
        //        _ => HoverTipFactory.FromCard<Miracle>(false));

        //withTip.Invoke(
        //    card,
        //    [miracleTip]);
    }
}
