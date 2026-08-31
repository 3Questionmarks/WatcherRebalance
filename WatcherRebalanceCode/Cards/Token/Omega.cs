using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch]
public static class OmegaPatch
{
    // =========================================================
    // CONSTRUCTOR
    //
    // Original:
    // 3 Energy
    // WithPower<OmegaPower>(50, 10, false)
    //
    // Rebalanced:
    // X Energy
    // 20(30) damage per X
    // =========================================================

    [HarmonyPatch(typeof(Omega), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // WatcherCardModel constructor:
        //
        // WatcherCardModel(
        //     int cost,
        //     CardType type,
        //     CardRarity rarity,
        //     TargetType targetType,
        //     bool shouldShowInCardLibrary)
        //
        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);


        // Watcher's special helper:
        //
        // WithPower<T>(
        //     int baseVal,
        //     int upgrade,
        //     bool showTooltip)
        //
        MethodInfo? withPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[2].ParameterType ==
                    typeof(bool));


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }

        if (withPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel.WithPower<T>(int, int, bool).");
        }


        MethodInfo omegaWithPower =
            withPower.MakeGenericMethod(
                typeof(OmegaPower));


        bool costPatched = false;
        bool powerPatched = false;


        for (int i = 0; i < code.Count; i++)
        {
            // -------------------------------------------------
            // COST
            //
            // Original:
            //
            // base(3, CardType.Power, ...)
            //
            // New:
            //
            // base(0, CardType.Power, ...)
            //
            // HasEnergyCostX is patched separately.
            // -------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(
                    code,
                    i - 5,
                    0);

                costPatched = true;

                continue;
            }


            // -------------------------------------------------
            // OMEGA DAMAGE
            //
            // Original:
            //
            // WithPower<OmegaPower>(
            //     50,
            //     10,
            //     false)
            //
            // New:
            //
            // WithPower<OmegaPower>(
            //     20,
            //     10,
            //     false)
            //
            // Gives:
            //
            // 20 base
            // 30 upgraded
            // -------------------------------------------------

            if (code[i].Calls(omegaWithPower))
            {
                ReplaceInt(
                    code,
                    i - 3,
                    20);

                ReplaceInt(
                    code,
                    i - 2,
                    10);

                powerPatched = true;
            }
        }


        if (!costPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Omega's base cost.");
        }

        if (!powerPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Omega's damage amount.");
        }


        return code;
    }


    // =========================================================
    // ADD RETAIN
    // =========================================================

    [HarmonyPatch(typeof(Omega), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Omega __instance)
    {
        AddRetain(__instance);
    }


    private static void AddRetain(
        Omega card)
    {
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
                "WatcherRebalance: Could not find ConstructedCardModel.WithKeyword.");
        }


        // UpgradeType is a protected nested enum,
        // so we cannot reference it directly here.
        //
        // UpgradeType.None == 0
        //
        Type upgradeType =
            withKeyword
                .GetParameters()[1]
                .ParameterType;

        object none =
            Enum.ToObject(
                upgradeType,
                0);


        withKeyword.Invoke(
            card,
            [
                CardKeyword.Retain,
                none
            ]);
    }


    // =========================================================
    // MAKE OMEGA X-COST
    //
    // Native X-cost cards override HasEnergyCostX.
    // Omega does not, so patch CardModel's actual getter
    // and only change the result for Omega.
    // =========================================================

    [HarmonyPatch(
        typeof(CardModel),
        "get_HasEnergyCostX")]
    [HarmonyPostfix]
    private static void HasEnergyCostXPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__instance is Omega)
        {
            __result = true;
        }
    }


    // =========================================================
    // ON PLAY
    //
    // X is resolved when Omega is played.
    //
    // OmegaPower amount becomes:
    //
    //     20(30) × X
    //
    // OmegaPower itself already handles dealing its Amount
    // to all enemies at the end of the owner's turn.
    // =========================================================

    [HarmonyPatch(typeof(Omega), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Omega __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedOmega(
                __instance,
                __0);

        return false;
    }


    private static async Task PlayRebalancedOmega(
        Omega card,
        PlayerChoiceContext choiceContext)
    {
        int x =
            ResolveEnergyXValue(card);


        int damagePerX =
            card.DynamicVars
                .Power<OmegaPower>()
                .IntValue;


        int totalDamage =
            damagePerX * x;


        // X = 0 means Omega applies no power.
        if (totalDamage <= 0)
            return;


        await PowerCmd.Apply<OmegaPower>(
            choiceContext,
            card.Owner.Creature,
            totalDamage,
            card.Owner.Creature,
            card);
    }


    // =========================================================
    // RESOLVE X
    //
    // CardModel.ResolveEnergyXValue() is protected, so invoke
    // the game's real implementation through reflection.
    // =========================================================

    private static int ResolveEnergyXValue(
        Omega card)
    {
        MethodInfo? resolveEnergyXValue =
            AccessTools.Method(
                typeof(CardModel),
                "ResolveEnergyXValue");


        if (resolveEnergyXValue == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find CardModel.ResolveEnergyXValue.");
        }


        object? result =
            resolveEnergyXValue.Invoke(
                card,
                null);


        if (result is not int value)
        {
            throw new Exception(
                "WatcherRebalance: CardModel.ResolveEnergyXValue returned an unexpected value.");
        }


        return value;
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


        var replacement =
            new CodeInstruction(
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