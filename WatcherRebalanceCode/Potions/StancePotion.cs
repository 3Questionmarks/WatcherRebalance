using System.Reflection;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Potions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

namespace WatcherRebalance.WatcherRebalanceCode.Potions;


// ============================================================================
// CALM POTION — CHOOSE CALM OR WRATH
// ============================================================================

[HarmonyPatch]
public static class CalmPotionOnUsePatch
{
    private static MethodBase? TargetMethod()
    {
        return typeof(CalmPotion)
            .GetMethod(
                "OnUse",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly,
                null,
                [
                    typeof(PlayerChoiceContext),
                    typeof(Creature)
                ],
                null);
    }

    [HarmonyPrefix]
    private static bool Prefix(
        PlayerChoiceContext __0,
        Creature? __1,
        ref Task __result)
    {
        __result = NewOnUse(__0, __1);
        return false;
    }

    private static async Task NewOnUse(
        PlayerChoiceContext choiceContext,
        Creature? target)
    {
        if (target?.Player == null)
            return;

        List<CardModel> cardsToChoose =
        [
            (CardModel)ModelDb
                .Card<CalmCard>()
                .MutableClone(),

            (CardModel)ModelDb
                .Card<WrathCard>()
                .MutableClone()
        ];

        foreach (CardModel card in cardsToChoose)
            card.Owner = target.Player;

        CardModel? chosenCard =
            await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                cardsToChoose,
                target.Player);

        switch (chosenCard)
        {
            case CalmCard:
                await StanceCmd.EnterCalm(
                    choiceContext,
                    target.Player,
                    null);
                break;

            case WrathCard:
                await StanceCmd.EnterWrath(
                    choiceContext,
                    target.Player,
                    null);
                break;
        }
    }
}


// ============================================================================
// CALM POTION — SHOW BOTH STANCE TOOLTIPS
// ============================================================================

[HarmonyPatch(
    typeof(CalmPotion),
    nameof(CalmPotion.ExtraHoverTips),
    MethodType.Getter)]
public static class CalmPotionHoverTipsPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        ref IEnumerable<IHoverTip> __result)
    {
        __result =
        [
            WatcherHoverTipFactory.FromStance<CalmStance>(),
            WatcherHoverTipFactory.FromStance<WrathStance>()
        ];
    }
}


// ============================================================================
// REMOVE WRATH POTION FROM WATCHER POTION POOL
// ============================================================================
//
// BaseLib registers custom potions by eventually calling:
//
//     ModHelper.AddModelToPool(poolType, modelType)
//
// Watcher's WrathPotion is registered into WatcherPotionPool through that call.
//
// We intercept only that exact registration. The WrathPotion model itself is
// still allowed to exist in ModelDb; it simply is not added to the Watcher
// potion pool, so it should not appear in normal Watcher potion rewards.
//
// IMPORTANT:
// This patch must be active before the Watcher mod constructs/registers its
// WrathPotion. That is normally true when Harmony patches are installed during
// mod initialization before ModelDb finishes initialization.
// ============================================================================

[HarmonyPatch(
    typeof(ModHelper),
    nameof(ModHelper.AddModelToPool),
    [
        typeof(Type),
        typeof(Type)
    ])]
public static class RemoveWrathPotionFromPoolPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Type __0,
        Type __1)
    {
        Type poolType = __0;
        Type modelType = __1;

        if (poolType == typeof(WatcherPotionPool) &&
            modelType == typeof(WrathPotion))
        {
            return false;
        }

        return true;
    }
}
