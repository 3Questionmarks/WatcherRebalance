using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(FlyingSleeves))]
public static class FlyingSleevesPatch
{
    /*
     * FLYING SLEEVES REBALANCE
     *
     * Base:
     * Retain.
     * Deal 4 damage 2 times.
     *
     * Upgrade:
     * Retain.
     * Deal 4 damage 3 times.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Original:
    // WithDamage(4, 2);
    //
    // New:
    // WithDamage(4, 0);
    //

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? replacement = AccessTools.Method(
            typeof(FlyingSleevesPatch),
            nameof(ReplaceDamage)
        );

        if (withDamage == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithDamage."
            );
        }

        if (replacement == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find FlyingSleevesPatch.ReplaceDamage."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;

            var newInstruction = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replacement
            );

            newInstruction.labels.AddRange(code[i].labels);
            newInstruction.blocks.AddRange(code[i].blocks);

            code[i] = newInstruction;

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Flying Sleeves damage."
            );
        }

        return code;
    }


    private static ConstructedCardModel ReplaceDamage(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        if (withDamage == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithDamage."
            );
        }

        object? result = withDamage.Invoke(
            card,
            new object[]
            {
                4, // Base damage
                0  // Damage no longer upgrades
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithDamage returned an unexpected result."
            );
        }

        return constructedCard;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        FlyingSleeves __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedFlyingSleeves(
            __instance,
            choiceContext,
            cardPlay
        );

        return false;
    }


    private static async Task PlayRebalancedFlyingSleeves(
        FlyingSleeves card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        int hitCount =
            card.IsUpgraded
                ? 3
                : 2;

        await CommonActions.CardAttack(
                card,
                cardPlay
            )
            .WithHitFx("vfx/vfx_attack_slash")
            .WithHitCount(hitCount)
            .Execute(ctx);
    }
}