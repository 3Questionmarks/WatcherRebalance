using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(WindmillStrike))]
public static class WindmillStrikePatch
{
    /*
     * WINDMILL STRIKE REBALANCE
     *
     * Original:
     * Cost 2
     * Retain.
     * Deal 7(10) damage.
     * When Retained, increase damage by 4(5) this combat.
     *
     * New:
     * Cost 1
     * Retain.
     * Deal 4(5) damage.
     * When Retained, increase damage by 4(5) this combat.
     *
     * The original AfterFlush behavior is left untouched.
     */


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
        // Change:
        // base(2, ...)
        //
        // To:
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


            /*
             * Stack immediately before constructor:
             *
             * this
             * cost
             * card type
             * rarity
             * target type
             * shouldShowInCardLibrary
             *
             * Therefore cost is i - 5.
             */

            ReplaceInt(
                code,
                i - 5,
                1);


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
        // WithDamage(7, 3);
        //
        // New:
        // WithDamage(4, 1);
        //
        // Result:
        // 4 -> 5
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
                4);


            ReplaceInt(
                code,
                i - 1,
                1);


            damagePatched = true;
            break;
        }


        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Windmill Strike's damage.");
        }


        return code;
    }


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


        code[index] = replacement;
    }
}