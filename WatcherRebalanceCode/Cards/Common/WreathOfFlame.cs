using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(WreathOfFlame))]
public static class WreathOfFlamePatch
{
    /*
     * WREATH OF FLAME
     *
     * Common
     *
     * Gain 5 Vigor.
     * Upgrade a card in your Hand for the
     * rest of this combat.
     *
     * Upgrade:
     * Upgrade All Cards in your Hand instead.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]
            );

        if (watcherCardConstructor == null)
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel constructor."
            );


        bool vigorPatched = false;
        bool rarityPatched = false;


        for (int i = 0; i < code.Count; i++)
        {
            // =================================================
            // VIGOR
            // =================================================
            //
            // Locate:
            //
            // WithPower<VigorPower>(5, 3, false)
            //
            // Stack immediately before call:
            //
            // this       <-- i - 4
            // 5          <-- i - 3
            // 3          <-- i - 2
            // false      <-- i - 1
            // call       <-- i
            //
            // Change to:
            //
            // WithPower<VigorPower>(6, 0, false)
            // =================================================

            if (!vigorPatched &&
                code[i].operand is MethodInfo calledMethod &&
                calledMethod.IsGenericMethod &&
                calledMethod.Name == "WithPower")
            {
                Type[] genericArguments =
                    calledMethod.GetGenericArguments();

                ParameterInfo[] parameters =
                    calledMethod.GetParameters();

                if (genericArguments.Length == 1 &&
                    genericArguments[0] == typeof(VigorPower) &&
                    parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(bool))
                {
                    if (i < 3)
                        throw new Exception(
                            "WatcherRebalance: Invalid Wreath Vigor IL."
                        );

                    // 5 -> 5 Vigor
                    ReplaceInt(code, i - 3, 5);
                    // Upgrade amount 3 -> 0
                    ReplaceInt(code, i - 2, 0);
                    // showTooltip false -> true
                    ReplaceInt(code, i - 1, 1);

                    vigorPatched = true;
                }
            }


            // =================================================
            // RARITY
            // =================================================

            if (!rarityPatched &&
                code[i].operand is ConstructorInfo calledCtor &&
                calledCtor == watcherCardConstructor)
            {
                if (i < 3)
                    throw new Exception(
                        "WatcherRebalance: Invalid Wreath rarity IL."
                    );

                ReplaceInt(
                    code,
                    i - 3,
                    (int)CardRarity.Common
                );

                rarityPatched = true;
            }
        }


        if (!vigorPatched)
            throw new Exception(
                "WatcherRebalance: Failed to patch Wreath of Flame Vigor."
            );

        if (!rarityPatched)
            throw new Exception(
                "WatcherRebalance: Failed to patch Wreath of Flame rarity."
            );


        return code;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        WreathOfFlame __instance,
        PlayerChoiceContext ctx,
        ref Task __result)
    {
        __result = PlayRebalancedWreathOfFlame(
            __instance,
            ctx
        );

        return false;
    }


    private static async Task PlayRebalancedWreathOfFlame(
        WreathOfFlame card,
        PlayerChoiceContext ctx)
    {
        // Gain flat 6 Vigor.
        await CommonActions.ApplySelf<VigorPower>(
            ctx,
            card
        );


        // =====================================================
        // UPGRADED:
        // Upgrade All Cards in Hand.
        // =====================================================

        if (card.IsUpgraded)
        {
            foreach (CardModel handCard in
                     PileType.Hand
                         .GetPile(card.Owner)
                         .Cards
                         .Where(c => c.IsUpgradable))
            {
                CardCmd.Upgrade(handCard);
            }

            return;
        }


        // =====================================================
        // BASE:
        // Select one card in Hand to upgrade.
        //
        // Same mechanism used by Armaments.
        // =====================================================

        CardModel? selectedCard =
            await CardSelectCmd.FromHandForUpgrade(
                ctx,
                card.Owner,
                card
            );

        if (selectedCard == null)
            return;

        CardCmd.Upgrade(selectedCard);
    }


    // =========================================================
    // IL HELPER
    // =========================================================

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original = code[index];

        var replacement =
            new CodeInstruction(
                OpCodes.Ldc_I4,
                value
            );

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}