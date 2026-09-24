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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Commands;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;
using WatcherRebalance.WatcherRebalanceCode.MechanicTweaks;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// ============================================================================
// BLASPHEMY CONSTRUCTOR
// ============================================================================

[HarmonyPatch(
    typeof(Blasphemy),
    MethodType.Constructor)]
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


    // ========================================================================
    // COST: 1 -> 2
    // ========================================================================

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

        bool changedCost =
            false;

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;

            CodeInstruction original =
                code[i - 5];

            CodeInstruction replacement =
                new(
                    OpCodes.Ldc_I4_2);

            replacement.labels.AddRange(
                original.labels);

            replacement.blocks.AddRange(
                original.blocks);

            code[i - 5] =
                replacement;

            changedCost =
                true;

            break;
        }

        if (!changedCost)
        {
            throw new Exception(
                "WatcherRebalance: Failed to change Blasphemy base cost to 2.");
        }

        return code;
    }


    // ========================================================================
    // MANTRA VARIABLE
    // ========================================================================
    //
    // Keep the card's canonical 10-Mantra variable.
    //
    // This is used when ScalingDivinityRequirement is disabled.
    // ========================================================================

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
}


// ============================================================================
// BLASPHEMY DESCRIPTION VARIABLES
// ============================================================================
//
// CardModel.GetDescriptionForPile() calls AddExtraArgsToDescription(LocString).
// Patch that actual base method and filter to Blasphemy.
//
// ============================================================================

[HarmonyPatch]
public static class BlasphemyDescriptionArgsPatch
{
    private static MethodBase? TargetMethod()
    {
        return typeof(CardModel)
            .GetMethod(
                "AddExtraArgsToDescription",
                BindingFlags.Instance |
                BindingFlags.NonPublic,
                null,
                [
                    typeof(LocString)
                ],
                null);
    }


    [HarmonyPostfix]
    private static void Postfix(
        CardModel __instance,
        LocString __0)
    {
        if (__instance is not Blasphemy blasphemy)
            return;

        bool scaling =
            Config.ScalingDivinityRequirement;

        __0.Add(
            "ScalingDivinity",
            scaling);

        if (!scaling)
            return;

        int mantraGain =
            ScalingMantra.BaseRequirement;

        try
        {
            if (blasphemy.IsMutable &&
                blasphemy.Owner?.PlayerCombatState != null)
            {
                mantraGain =
                    ScalingMantra.GetBlasphemyMantraGain(
                        blasphemy.Owner);
            }
        }
        catch
        {
            mantraGain =
                ScalingMantra.BaseRequirement;
        }

        __0.Add(
            "BlasphemyMantra",
            (decimal)mantraGain);
    }
}


// ============================================================================
// PENDING SECOND BLASPHEMY
// ============================================================================

internal static class BlasphemyPunishment
{
    public static readonly HashSet<CardModel> Pending =
        [];
}


// ============================================================================
// BLASPHEMY ON PLAY
// ============================================================================

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


        // ====================================================================
        // NORMAL / FIRST BLASPHEMY
        // ====================================================================

        if (!Config.EnableBlasphemyEasterEgg ||
            !creature.HasPower<BlasphemerPower>())
        {
            if (Config.ScalingDivinityRequirement)
            {
                // ============================================================
                // SCALING MODE
                // ============================================================
                //
                // Gain exactly enough Mantra to reach the current Divinity
                // requirement.
                // ============================================================

                int mantraGain =
                    ScalingMantra.GetBlasphemyMantraGain(
                        card.Owner);

                if (mantraGain > 0)
                {
                    await PowerCmd.Apply<MantraPower>(
                        choiceContext,
                        creature,
                        mantraGain,
                        creature,
                        card);
                }
            }
            else
            {
                // ============================================================
                // FIXED MODE
                // ============================================================
                //
                // Existing behavior:
                //
                // Gain 10 Mantra.
                // ============================================================

                await CommonActions.ApplySelf<MantraPower>(
                    choiceContext,
                    card);
            }


            // Blasphemer must be applied AFTER the Mantra gain.
            await CommonActions.ApplySelf<BlasphemerPower>(
                choiceContext,
                card,
                1);

            return;
        }


        // ====================================================================
        // SECOND BLASPHEMY — EASTER EGG
        // ====================================================================

        BlasphemyPunishment.Pending.Add(
            card);
    }
}


// ============================================================================
// AFTER SECOND BLASPHEMY
// ============================================================================

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
        await __result;

        if (__2.Card is not Blasphemy blasphemy)
            return;

        if (!BlasphemyPunishment.Pending.Remove(
                blasphemy))
        {
            return;
        }

        if (!Config.EnableBlasphemyEasterEgg)
            return;

        await RunPunishment(
            blasphemy,
            __1);
    }


    // ========================================================================
    // PREPARE JUDGEMENT
    // ========================================================================

    private static async Task RunPunishment(
        Blasphemy card,
        PlayerChoiceContext choiceContext)
    {
        // Hand
        await ExhaustPile(
            choiceContext,
            card,
            PileType.Hand);

        // Draw
        await ExhaustPile(
            choiceContext,
            card,
            PileType.Draw);

        // Discard
        await ExhaustPile(
            choiceContext,
            card,
            PileType.Discard);

        await Task.Delay(
            100);


        // ====================================================================
        // TEN JUDGEMENTS
        // ====================================================================

        await WatcherCmd.GiveCard<Unworthy>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Traitor>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Repent>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Forsaken>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Heretic>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Apostate>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Profane>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Condemned>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Unforgiven>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);

        await Task.Delay(
            100);


        await WatcherCmd.GiveCard<Judgement>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Random,
            skipAnimation: true);
    }


    // ========================================================================
    // EXHAUST PILE
    // ========================================================================

    private static async Task ExhaustPile(
        PlayerChoiceContext choiceContext,
        Blasphemy blasphemy,
        PileType pileType)
    {
        CardPile pile =
            pileType.GetPile(
                blasphemy.Owner);

        // Snapshot because exhausting cards mutates the pile.
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