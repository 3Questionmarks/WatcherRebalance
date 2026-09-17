using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


[HarmonyPatch]
public static class LessonLearnedPatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(
        typeof(LessonLearned),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // ---------------------------------------------------------
        // Find WatcherCardModel constructor.
        // ---------------------------------------------------------

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


        // ---------------------------------------------------------
        // Find WithDamage(int, int).
        // ---------------------------------------------------------

        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithDamage" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                    typeof(int));


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }


        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage(int, int).");
        }


        // =========================================================
        // APPLY CHANGES
        // =========================================================

        for (int i = 0; i < code.Count; i++)
        {
            // -----------------------------------------------------
            // COST
            //
            // Original:
            //     2
            //
            // New:
            //     3
            // -----------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(
                    code,
                    i - 5,
                    3);

                continue;
            }


            // -----------------------------------------------------
            // DAMAGE
            //
            // Original:
            //     WithDamage(10, 3)
            //
            // New:
            //     WithDamage(10, 0)
            // -----------------------------------------------------

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(
                    code,
                    i - 1,
                    0);

                continue;
            }
        }


        return code;
    }


    // =========================================================
    // UPGRADE-ONLY RETAIN
    // =========================================================

    [HarmonyPatch(
        typeof(LessonLearned),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        LessonLearned __instance)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "WithKeyword")
                        return false;


                    ParameterInfo[] parameters =
                        m.GetParameters();


                    return
                        parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                        typeof(CardKeyword);
                });


        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithKeyword(CardKeyword, UpgradeType).");
        }


        Type upgradeType =
            withKeyword
                .GetParameters()[1]
                .ParameterType;


        // UpgradeType.Add == 1
        object addUpgrade =
            Enum.ToObject(
                upgradeType,
                1);


        withKeyword.Invoke(
            __instance,
            [
                CardKeyword.Retain,
                addUpgrade
            ]);
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(
        typeof(LessonLearned),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        LessonLearned __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayWithCustomFx(
                __instance,
                __0,
                __1);

        return false;
    }


    private static async Task PlayWithCustomFx(
        LessonLearned card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
            return;


        // Determine whether Fatal is allowed to trigger.
        var shouldTriggerFatal =
            cardPlay.Target.Powers.All(
                p => p.ShouldOwnerDeathTriggerFatal());


        // =====================================================
        // ATTACK
        //
        // Blunt impact + blunt attack sound.
        // =====================================================

        var attackCommand =
            await CommonActions
                .CardAttack(
                    card,
                    cardPlay)
                .WithHitFx(
                    "vfx/vfx_attack_blunt",
                    tmpSfx: "blunt_attack.mp3")
                .Execute(
                    choiceContext);


        // =====================================================
        // FATAL
        // =====================================================

        if (!shouldTriggerFatal ||
            !attackCommand.Results.Any(
                r => r.Any(
                    g => g is
                    {
                        WasTargetKilled: true,
                        Receiver.IsSecondaryEnemy: false
                    })))
        {
            return;
        }


        var upgradableCards =
            PileType.Deck
                .GetPile(card.Owner)
                .Cards
                .Where(c => c.IsUpgradable)
                .ToList();


        if (upgradableCards.Count <= 0)
            return;


        await Cmd.Wait(0.5f);


        var cardModel =
            card.Owner
                .RunState
                .Rng
                .Niche
                .NextItem(
                    upgradableCards);


        if (cardModel == null)
            return;


        card.Owner
            .RunState
            .CurrentMapPointHistoryEntry?
            .GetEntry(
                card.Owner.NetId)
            .UpgradedCards
            .Add(
                cardModel.Id);


        cardModel.UpgradeInternal();
        cardModel.FinalizeUpgradeInternal();


        if (LocalContext.IsMe(card.Owner))
        {
            NRun.Instance?
                .GlobalUi
                .CardPreviewContainer
                .AddChildSafely(
                    NCardSmithVfx.Create(
                        [
                            cardModel
                        ])!);
        }
    }


    // =========================================================
    // INTEGER REPLACEMENT
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