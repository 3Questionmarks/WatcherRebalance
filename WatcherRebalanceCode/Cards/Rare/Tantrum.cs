using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;

[HarmonyPatch(typeof(Tantrum))]
public static class TantrumPatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
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

    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Tantrum __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayWithCustomFx(
            __instance,
            __0,
            __1);

        return false;
    }

    private static async Task PlayWithCustomFx(
        Tantrum card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // =====================================================
        // TANTRUM HITS
        // =====================================================

        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitCount(
                card.DynamicVars.Repeat.IntValue)
            .WithHitFx(
                "vfx/vfx_dramatic_stab",
                tmpSfx: "ironclad_attack")
            .Execute(choiceContext);

        // =====================================================
        // FIRE BURST AFTER HITS
        // =====================================================

        SfxCmd.Play("event:/sfx/characters/attack_fire");

        await Cmd.Wait(0.2f);

        VfxCmd.PlayOnCreatureCenter(
            cardPlay.Target,
            "vfx/vfx_fire_burst");

        // =====================================================
        // ENTER WRATH
        // =====================================================

        await StanceCmd.EnterWrath(
            choiceContext,
            card.Owner,
            cardPlay.Card);

        await Cmd.Wait(0.25f);
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