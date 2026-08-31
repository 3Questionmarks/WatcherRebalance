using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Watcher.Code.Abstract;

namespace WatcherRebalance.WatcherRebalanceCode.Patches;

[HarmonyPatch]
public static class HideWatcherWishTokens
{
    // =========================================================
    // TARGET
    // =========================================================
    //
    // BecomeAlmighty, FameAndFortune and LiveForever all inherit
    // from WishableWatcherCard.
    //
    // Their constructors therefore do NOT directly call
    // WatcherCardModel. WishableWatcherCard is the class that
    // ultimately calls WatcherCardModel and supplies the optional
    // shouldShowInCardLibrary argument.
    //
    // Patch that shared constructor instead.
    // =========================================================

    private static MethodBase TargetMethod()
    {
        ConstructorInfo? constructor =
            typeof(WishableWatcherCard)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .SingleOrDefault();

        if (constructor == null)
        {
            throw new MissingMethodException(
                "Could not find WishableWatcherCard constructor.");
        }

        return constructor;
    }


    // =========================================================
    // TRANSPILER
    // =========================================================

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // Find WatcherCardModel's constructor:
        //
        // (
        //     int cost,
        //     CardType type,
        //     CardRarity rarity,
        //     TargetType target,
        //     bool shouldShowInCardLibrary
        // )

        ConstructorInfo? watcherCardConstructor =
            typeof(WatcherCardModel)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(constructor =>
                {
                    ParameterInfo[] parameters =
                        constructor.GetParameters();

                    return
                        parameters.Length == 5 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType.IsEnum &&
                        parameters[2].ParameterType.IsEnum &&
                        parameters[3].ParameterType.IsEnum &&
                        parameters[4].ParameterType == typeof(bool);
                });


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
            {
                continue;
            }

            if (constructor != watcherCardConstructor)
            {
                continue;
            }


            // =================================================
            // FIND THE FINAL BOOLEAN ARGUMENT
            // =================================================
            //
            // WishableWatcherCard currently omits the optional
            // shouldShowInCardLibrary parameter, so C# supplies:
            //
            //     true
            //
            // to WatcherCardModel.
            //
            // The boolean is the LAST constructor argument, so
            // it should be loaded immediately before the call.
            // Change true -> false.
            // =================================================

            if (i <= 0)
            {
                throw new InvalidOperationException(
                    "WatcherCardModel constructor call had no preceding instruction.");
            }


            if (!IsLoadInt(code[i - 1], 1))
            {
                throw new InvalidOperationException(
                    "Could not find shouldShowInCardLibrary=true " +
                    "before WishableWatcherCard's WatcherCardModel constructor call.");
            }


            ReplaceInt(
                code,
                i - 1,
                0);


            return code;
        }


        throw new InvalidOperationException(
            "Could not find WatcherCardModel constructor call " +
            "inside WishableWatcherCard.");
    }


    // =========================================================
    // INTEGER HELPERS
    // =========================================================

    private static bool IsLoadInt(
        CodeInstruction instruction,
        int value)
    {
        return value switch
        {
            0 =>
                instruction.opcode == OpCodes.Ldc_I4_0 ||
                instruction.opcode == OpCodes.Ldc_I4_S &&
                instruction.operand is sbyte sbyteValue &&
                sbyteValue == 0 ||
                instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int intValue &&
                intValue == 0,

            1 =>
                instruction.opcode == OpCodes.Ldc_I4_1 ||
                instruction.opcode == OpCodes.Ldc_I4_S &&
                instruction.operand is sbyte sbyteValue &&
                sbyteValue == 1 ||
                instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int intValue &&
                intValue == 1,

            _ => false
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
                0 => new CodeInstruction(
                    OpCodes.Ldc_I4_0),

                1 => new CodeInstruction(
                    OpCodes.Ldc_I4_1),

                _ => new CodeInstruction(
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