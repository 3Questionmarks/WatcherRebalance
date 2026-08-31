using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Cards.Token;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Powers;
using DevotionPower = WatcherRebalance.WatcherRebalanceCode.Powers.DevotionPower;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// DEVOTION CONSTRUCTOR
// =============================================================
//
// Original:
//
//     Cost 1
//     WithPower<Watcher DevotionPower>(2, 1, false)
//     Mantra tooltip
//
// Rework:
//
//     Cost 2
//     Upgrade: Cost 1
//     Miracle tooltip
//
// The original Watcher DevotionPower DynamicVar is left in the
// constructed card, but its gameplay behavior is never used
// because OnPlay is completely replaced below.
//
// =============================================================

[HarmonyPatch(
    typeof(Devotion),
    MethodType.Constructor)]
public static class DevotionConstructorPatch
{
    // =========================================================
    // CONSTRUCTOR TRANSPILER
    // =========================================================

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // =====================================================
        // FIND WATCHER CARD CONSTRUCTOR
        // =====================================================

        ConstructorInfo? watcherCardConstructor =
            typeof(WatcherCardModel)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(constructor =>
                    constructor.GetParameters().Length == 5);


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }


        // =====================================================
        // COST: 1 -> 2
        // =====================================================

        bool changedCost = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;

            // WatcherCardModel(
            //     int cost,
            //     CardType type,
            //     CardRarity rarity,
            //     TargetType targetType,
            //     bool shouldShowInCardLibrary)
            //
            // Cost is the first of the five constructor arguments.
            ReplaceInt(
                code,
                i - 5,
                2);

            changedCost = true;
            break;
        }

        if (!changedCost)
        {
            throw new Exception(
                "WatcherRebalance: Failed to change Devotion base cost.");
        }


        // =====================================================
        // REPLACE MANTRA TOOLTIP WITH MIRACLE TOOLTIP
        // =====================================================
        //
        // Original:
        //
        //     WithTip(typeof(MantraPower));
        //
        // Replace:
        //
        //     typeof(MantraPower)
        //
        // with:
        //
        //     typeof(Miracle)
        //
        // This preserves Watcher's normal card-tooltip helper
        // instead of adding a second tooltip afterwards.
        // =====================================================

        bool replacedTooltip = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].opcode != OpCodes.Ldtoken)
                continue;

            if (code[i].operand is not Type type)
                continue;

            if (type != typeof(MantraPower))
                continue;


            code[i].operand =
                typeof(Miracle);


            replacedTooltip = true;
            break;
        }


        if (!replacedTooltip)
        {
            throw new Exception(
                "WatcherRebalance: Could not replace Devotion's Mantra tooltip with Miracle.");
        }


        return code;
    }


    // =========================================================
    // UPGRADE COST
    // =========================================================
    //
    // Base:
    //
    //     2 Energy
    //
    // Upgrade:
    //
    //     1 Energy
    //
    // BaseLib explicitly uses negative values to REDUCE cost.
    // =========================================================

    [HarmonyPostfix]
    private static void Postfix(
        Devotion __instance)
    {
        MethodInfo? withCostUpgradeBy =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithCostUpgradeBy" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(int));


        if (withCostUpgradeBy == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCostUpgradeBy(int).");
        }


        withCostUpgradeBy.Invoke(
            __instance,
            [-1]);
    }


    // =========================================================
    // IL HELPERS
    // =========================================================

    private static bool LoadsInt(
        CodeInstruction instruction,
        int value)
    {
        return value switch
        {
            0 =>
                instruction.opcode == OpCodes.Ldc_I4_0,

            1 =>
                instruction.opcode == OpCodes.Ldc_I4_1 ||
                instruction.opcode == OpCodes.Ldc_I4_S &&
                instruction.operand is sbyte shortValue &&
                shortValue == 1 ||
                instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int intValue &&
                intValue == 1,

            2 =>
                instruction.opcode == OpCodes.Ldc_I4_2 ||
                instruction.opcode == OpCodes.Ldc_I4_S &&
                instruction.operand is sbyte shortValue &&
                shortValue == 2 ||
                instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int intValue &&
                intValue == 2,

            _ =>
                instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int intValue &&
                intValue == value
        };
    }


    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];


        CodeInstruction replacement =
            value switch
            {
                0 => new CodeInstruction(OpCodes.Ldc_I4_0),
                1 => new CodeInstruction(OpCodes.Ldc_I4_1),
                2 => new CodeInstruction(OpCodes.Ldc_I4_2),
                3 => new CodeInstruction(OpCodes.Ldc_I4_3),
                4 => new CodeInstruction(OpCodes.Ldc_I4_4),
                5 => new CodeInstruction(OpCodes.Ldc_I4_5),
                6 => new CodeInstruction(OpCodes.Ldc_I4_6),
                7 => new CodeInstruction(OpCodes.Ldc_I4_7),
                8 => new CodeInstruction(OpCodes.Ldc_I4_8),

                _ =>
                    new CodeInstruction(
                        OpCodes.Ldc_I4,
                        value)
            };


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        code[index] =
            replacement;
    }
}


// =============================================================
// DEVOTION ON PLAY
// =============================================================
//
// IMPORTANT:
//
// This is deliberately a SEPARATE Harmony patch class from the
// constructor patch.
//
// The previous version mixed TargetMethod() with individually
// annotated constructor patches. Harmony rejects that structure
// and aborts PatchAll.
//
// =============================================================

[HarmonyPatch(
    typeof(Devotion),
    "OnPlay")]
public static class DevotionOnPlayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Devotion __instance,
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
        Devotion card,
        PlayerChoiceContext choiceContext)
    {
        await MegaCrit.Sts2.Core.Commands.PowerCmd
            .Apply<DevotionPower>(
                choiceContext,
                card.Owner.Creature,
                1,
                card.Owner.Creature,
                card);
    }
}