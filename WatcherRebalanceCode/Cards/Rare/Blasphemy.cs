using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Commands;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// BLASPHEMY CONSTRUCTOR PATCH
// =============================================================
//
// Original:
//
//     1 Energy
//     Apply BlasphemerPower
//     Enter Divinity
//     Exhaust
//     Upgrade: Retain
//
// Rebalance:
//
//     2 Energy
//     Gain 10 Mantra
//     Apply BlasphemerPower
//     Exhaust
//     Upgrade: Retain
// =============================================================

[HarmonyPatch(typeof(Blasphemy), MethodType.Constructor)]
public static class BlasphemyConstructorPatch
{
    private static readonly MethodInfo? WithMantraPower =
        typeof(WatcherCardModel)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                if (method.Name != "WithPower")
                    return false;

                if (!method.IsGenericMethodDefinition)
                    return false;

                ParameterInfo[] parameters =
                    method.GetParameters();

                return
                    parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(int) &&
                    parameters[2].ParameterType == typeof(bool);
            });


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


        // =====================================================
        // BASE COST: 1 -> 2
        // =====================================================

        bool changedCost = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;


            CodeInstruction original =
                code[i - 5];


            CodeInstruction replacement =
                new CodeInstruction(
                    OpCodes.Ldc_I4_2);


            replacement.labels.AddRange(
                original.labels);

            replacement.blocks.AddRange(
                original.blocks);


            code[i - 5] =
                replacement;


            changedCost = true;
            break;
        }


        if (!changedCost)
        {
            throw new Exception(
                "WatcherRebalance: Failed to change Blasphemy base cost to 2.");
        }


        return code;
    }


    // =========================================================
    // ADD MANTRA VARIABLE
    // =========================================================

    [HarmonyPostfix]
    private static void Postfix(
        Blasphemy __instance)
    {
        if (WithMantraPower == null)
            return;


        MethodInfo mantraMethod =
            WithMantraPower.MakeGenericMethod(
                typeof(MantraPower));


        mantraMethod.Invoke(
            __instance,
            [
                10,
                0,
                true
            ]);
    }


    private static bool IsLoadInt(
        CodeInstruction instruction,
        int value)
    {
        if (value != 1)
            return false;


        return
            instruction.opcode == OpCodes.Ldc_I4_1 ||

            instruction.opcode == OpCodes.Ldc_I4_S &&
            instruction.operand is sbyte shortValue &&
            shortValue == 1 ||

            instruction.opcode == OpCodes.Ldc_I4 &&
            instruction.operand is int intValue &&
            intValue == 1;
    }
}


// =============================================================
// PENDING SECOND BLASPHEMY
// =============================================================
//
// We still want Blasphemy itself to completely finish playing
// before the punishment begins.
//
// Therefore OnPlay only marks the card.
//
// Hook.AfterCardPlayed starts the punishment afterwards.
// =============================================================

internal static class BlasphemyPunishment
{
    public static readonly HashSet<CardModel> Pending =
        [];
}


// =============================================================
// BLASPHEMY PLAY PATCH
// =============================================================

[HarmonyPatch]
public static class BlasphemyOnPlayPatch
{
    private static MethodBase? TargetMethod()
    {
        return typeof(Blasphemy)
            .GetMethod(
                "OnPlay",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly,
                null,
                [
                    typeof(PlayerChoiceContext),
                    typeof(CardPlay)
                ],
                null);
    }


    [HarmonyPrefix]
    private static bool Prefix(
        Blasphemy __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0,
                __1);


        return false;
    }


    private static async Task NewOnPlay(
        Blasphemy card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature creature =
            card.Owner.Creature;


        // =====================================================
        // FIRST BLASPHEMY
        // =====================================================
        //
        // Gain 10 Mantra first.
        //
        // THEN apply Blasphemer so our Mantra prevention logic
        // does not block this initial gain.
        // =====================================================

        if (!creature.HasPower<BlasphemerPower>())
        {
            await CommonActions.ApplySelf<MantraPower>(
                choiceContext,
                card);


            await CommonActions.ApplySelf<BlasphemerPower>(
                choiceContext,
                card,
                1);


            return;
        }


        // =====================================================
        // SECOND BLASPHEMY
        // =====================================================
        //
        // Don't start the punishment inside OnPlay.
        //
        // Allow Blasphemy to finish first.
        // =====================================================

        BlasphemyPunishment.Pending.Add(
            card);
    }
}


// =============================================================
// AFTER SECOND BLASPHEMY PLAYS
// =============================================================

[HarmonyPatch(
    typeof(Hook),
    nameof(Hook.AfterCardPlayed))]
public static class BlasphemyAfterCardPlayedPatch
{
    [HarmonyPostfix]
    private static async Task Postfix(
        Task __result,
        CombatState __0,
        PlayerChoiceContext __1,
        CardPlay __2)
    {
        // Let the game's normal AfterCardPlayed hook finish.
        await __result;


        if (__2.Card is not Blasphemy blasphemy)
            return;


        if (!BlasphemyPunishment.Pending.Remove(
                blasphemy))
        {
            return;
        }


        await RunPunishment(
            blasphemy,
            __1);
    }


    // =========================================================
    // PREPARE JUDGEMENT
    // =========================================================
    //
    // Unlike the previous version, this method STOPS after the
    // ten cards have been generated.
    //
    // Nothing automatically plays them here.
    //
    // The player is returned to normal control and can inspect
    // the cards for as long as desired.
    //
    // They resolve when the player presses End Turn.
    // =========================================================

    private static async Task RunPunishment(
        Blasphemy card,
        PlayerChoiceContext choiceContext)
    {
        // =====================================================
        // ERASE HAND
        // =====================================================

        await ExhaustPile(
            choiceContext,
            card,
            PileType.Hand);


        // =====================================================
        // ERASE DRAW PILE
        // =====================================================

        await ExhaustPile(
            choiceContext,
            card,
            PileType.Draw);


        // =====================================================
        // ERASE DISCARD PILE
        // =====================================================

        await ExhaustPile(
            choiceContext,
            card,
            PileType.Discard);


        // Short dramatic pause after the deck disappears.
        await Task.Delay(100);


        // =====================================================
        // GENERATE THE TEN JUDGEMENTS
        // =====================================================
        //
        // Each one enters at a random position.
        //
        // Keep your 200ms stagger between cards.
        // =====================================================

        await WatcherCmd.GiveCard<Unworthy>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Traitor>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Repent>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Forsaken>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Heretic>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Apostate>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Profane>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Condemned>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Unforgiven>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(100);


        await WatcherCmd.GiveCard<Judgement>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);


        // =====================================================
        // DONE
        // =====================================================
        //
        // Deliberately stop here.
        //
        // No CardCmd.AutoPlay.
        //
        // The player now has control and can inspect the ten
        // cards until they decide to end their turn.
        // =====================================================
    }


    // =========================================================
    // EXHAUST AN ENTIRE PILE
    // =========================================================

    private static async Task ExhaustPile(
        PlayerChoiceContext choiceContext,
        Blasphemy blasphemy,
        PileType pileType)
    {
        CardPile pile =
            pileType.GetPile(
                blasphemy.Owner);


        // Snapshot because CardCmd.Exhaust mutates the pile.
        List<CardModel> cards =
            pile.Cards
                .ToList();


        foreach (CardModel card in cards)
        {
            if (ReferenceEquals(
                    card,
                    blasphemy))
            {
                continue;
            }


            await CardCmd.Exhaust(
                choiceContext,
                card,
                causedByEthereal: false,
                skipVisuals: false);
        }
    }
}