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
    [HarmonyPatch(
        typeof(Pray),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Pray __instance)
    {
        AddMiracleTooltip(
            __instance);
    }


    [HarmonyPatch(
        typeof(Pray),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Pray __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0,
                __1);


        return false;
    }


    private static async Task NewOnPlay(
        Pray card,
        PlayerChoiceContext choiceContext,
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


        // Calculate before gaining Mantra because reaching the threshold can
        // immediately consume it by entering Divinity.
        int mantraForEffects =
            currentMantra +
            mantraGain;


        await CommonActions.ApplySelf<MantraPower>(
            choiceContext,
            card);


        const int mantraPerInsight =
            3;


        int insightCount;


        if (Config.ScalingDivinityRequirement)
        {
            // Scaling mechanic:
            //
            // 1 Insight per 3 Mantra with no cap.
            insightCount =
                mantraForEffects /
                mantraPerInsight;
        }
        else
        {
            // Existing rebalance:
            //
            // 1 Insight per 3 Mantra, maximum 3.
            const int insightCap =
                3;


            insightCount =
                Math.Min(
                    mantraForEffects /
                    mantraPerInsight,
                    insightCap);
        }


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
                .FirstOrDefault(method =>
                    method.Name == "WithTip" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(TooltipSource));


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