using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Compatibility;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(WindmillStrike))]
public static class WindmillStrikePatch
{
    private const string RepeatKey = "Repeat";


    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Rebalanced:
    //
    // Cost 1
    // Retain
    // Deal 3(5) damage 2 times
    //
    // Repeat starts at 2 and does not change on upgrade.
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // -----------------------------------------------------
        // WatcherCardModel constructor
        //
        // Original:
        // base(2, ...)
        //
        // Rebalanced:
        // base(1, ...)
        // -----------------------------------------------------

        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                new[]
                {
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                });


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }


        bool costPatched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor ||
                constructor != watcherCardConstructor)
            {
                continue;
            }


            // Cost is five stack arguments before the constructor call.
            ReplaceInt(
                code,
                i - 5,
                2);


            costPatched = true;
            break;
        }


        if (!costPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Windmill Strike's base cost.");
        }


        // -----------------------------------------------------
        // WithDamage
        //
        // Original:
        // WithDamage(7, 3)
        //
        // Rebalanced:
        // WithDamage(3, 2)
        //
        // Result:
        // 3 -> 5
        // -----------------------------------------------------

        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithDamage" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int));


        if (withDamage == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithDamage(int, int).");
        }


        bool damagePatched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;


            ReplaceInt(
                code,
                i - 2,
                3);


            ReplaceInt(
                code,
                i - 1,
                2);


            damagePatched = true;
            break;
        }


        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Windmill Strike's damage.");
        }


        // -----------------------------------------------------
        // WithVar
        //
        // Original:
        //
        // WithVar("RetainIncrease", 4, 1)
        //
        // Rebalanced:
        //
        // WithVar("Repeat", 2, 0)
        //
        // We reuse the original dynamic-var construction,
        // but turn it into our hit counter.
        // -----------------------------------------------------

        MethodInfo? withVar =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithVar" &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(int));


        if (withVar == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithVar(string, int, int).");
        }


        bool repeatPatched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withVar))
                continue;


            // Expected stack:
            //
            // this
            // "RetainIncrease"
            // 4
            // 1
            // WithVar(...)
            //
            // Change to:
            //
            // this
            // "Repeat"
            // 2
            // 0
            // WithVar(...)
            //

            code[i - 3].operand =
                RepeatKey;


            ReplaceInt(
                code,
                i - 2,
                2);


            ReplaceInt(
                code,
                i - 1,
                0);


            repeatPatched = true;
            break;
        }


        if (!repeatPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to create Windmill Strike's Repeat variable.");
        }


        return code;
    }


    // =========================================================
    // ON PLAY
    // =========================================================
    //
    // Replace the original single-hit attack with a multi-hit
    // attack using the current Repeat value.
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        WindmillStrike __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result =
            PlayRebalanced(
                __instance,
                choiceContext,
                cardPlay);

        return false;
    }


    private static async Task PlayRebalanced(
        WindmillStrike card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
            return;


        int hitCount =
            card.DynamicVars[RepeatKey].IntValue;

        string hitVfx = hitCount switch
        {
            <= 3 => "vfx/vfx_attack_slash",
            <= 6 => "vfx/vfx_big_slash",
            _ => "vfx/vfx_big_slash_impact"
        };

        string hitSfx = hitCount >= 7
            ? "heavy_attack.mp3"
            : "slash_attack.mp3";

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitCount(hitCount)
            .WithHitFx(
                hitVfx,
                tmpSfx: hitSfx)
            .Execute(choiceContext);
    }


    // =========================================================
    // RETAIN EFFECT
    // =========================================================
    //
    // Original:
    // Increase Damage whenever retained.
    //
    // Rebalanced:
    // Increase Repeat by 1 whenever retained.
    // =========================================================

    [HarmonyPatch(nameof(WindmillStrike.AfterFlush))]
    [HarmonyPrefix]
    private static bool AfterFlushPrefix(
        WindmillStrike __instance,
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards,
        ref Task __result)
    {
        if (retainedCards.Contains(__instance))
        {
            __instance
                .DynamicVars[RepeatKey]
                .UpgradeValueBy(1);
        }


        __result =
            Task.CompletedTask;


        return false;
    }


    // =========================================================
    // HELPER
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
