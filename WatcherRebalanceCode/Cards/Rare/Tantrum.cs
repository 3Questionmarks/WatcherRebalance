using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// TANTRUM
// =============================================================
//
// Original:
//     Uncommon
//
// Rebalance:
//     Rare
//
// No gameplay changes.
// =============================================================

[HarmonyPatch(
    typeof(Tantrum),
    MethodType.Constructor)]
public static class TantrumPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


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


        bool patched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;


            // WatcherCardModel(
            //     cost,
            //     type,
            //     rarity,
            //     target,
            //     shouldShowInCardLibrary)
            //
            // Rarity is the third argument, therefore i - 3.
            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Rare);


            patched = true;
            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to change Tantrum rarity to Rare.");
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