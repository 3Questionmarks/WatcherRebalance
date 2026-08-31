using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(Worship))]
public static class WorshipPatch
{
    /*
     * WORSHIP
     *
     * Base:
     *
     * Retain.
     * Gain 5 Mantra.
     *
     * Upgrade:
     *
     * If you enter Divinity this turn,
     * choose a Smite, Miracle, or Insight
     * to add to your Hand.
     */


    // =========================================================
    // RETAIN
    // =========================================================
    //
    // Original:
    //
    // WithKeyword(
    //     CardKeyword.Retain,
    //     UpgradeType.Add);
    //
    // New:
    //
    // WithKeyword(
    //     CardKeyword.Retain,
    //     UpgradeType.None);
    //
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        ConstructorTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithKeyword" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(CardKeyword));


        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithKeyword.");
        }


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withKeyword))
                continue;


            // UpgradeType.None = 0

            ReplaceInt(
                code,
                i - 1,
                0);


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch " +
                "Worship's Retain keyword.");
        }


        return code;
    }


    // =========================================================
    // TOKEN TOOLTIPS
    // =========================================================
    //
    // Only Worship+ needs these because only the upgrade
    // can generate one of the three cards.
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Worship __instance)
    {
        MethodInfo? withTips =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithTips",
                new[]
                {
                    typeof(
                        Func<
                            CardModel,
                            IEnumerable<IHoverTip>>)
                });


        if (withTips == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithTips.");
        }


        Func<CardModel, IEnumerable<IHoverTip>>
            tokenTips =
                card =>
                {
                    if (!card.IsUpgraded)
                    {
                        return
                            Array.Empty<IHoverTip>();
                    }


                    return new IHoverTip[]
                    {
                        HoverTipFactory.FromCard<Smite>(),
                        HoverTipFactory.FromCard<Miracle>(),
                        HoverTipFactory.FromCard<Insight>()
                    };
                };


        withTips.Invoke(
            __instance,
            new object[]
            {
                tokenTips
            });
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Worship __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedWorship(
                __instance,
                __0);


        return false;
    }


    private static async Task PlayRebalancedWorship(
        Worship card,
        PlayerChoiceContext ctx)
    {
        // -----------------------------------------------------
        // Worship+ applies its temporary trigger BEFORE Mantra.
        //
        // This is necessary because Worship's own 5 Mantra
        // may be the thing that enters Divinity.
        // -----------------------------------------------------

        if (card.IsUpgraded)
        {
            await PowerCmd.Apply<WorshipPower>(
                ctx,
                card.Owner.Creature,
                1,
                card.Owner.Creature,
                card);
        }


        // Preserve Worship's normal 5 Mantra.

        await CommonActions
            .ApplySelf<MantraPower>(
                ctx,
                card);
    }


    // =========================================================
    // IL HELPER
    // =========================================================

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];


        CodeInstruction replacement =
            new(
                OpCodes.Ldc_I4,
                value);


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        code[index] =
            replacement;
    }
}