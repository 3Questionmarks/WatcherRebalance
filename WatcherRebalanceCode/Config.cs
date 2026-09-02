using BaseLib.Config;

namespace WatcherRebalance.WatcherRebalanceCode;

public sealed class Config : SimpleModConfig
{
    // ========================================================================
    // CORE CHANGES
    // ========================================================================

    [ConfigSection("Core Changes")]
    [ConfigHoverTip]
    public static bool RebalancedStrengthScaling { get; set; } =
        true;


    // ========================================================================
    // STANCE SETTINGS
    // ========================================================================
    
    // ------------------------------------------------------------------------
    // CALM
    // ------------------------------------------------------------------------

    [ConfigSection("Calm")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int CalmEnergyOnEnter { get; set; } = 0;

    [ConfigSection("Calm")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int CalmEnergyOnExit { get; set; } = 2;

    [ConfigSection("Calm")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double CalmDamageMultiplier { get; set; } = 1.0;

    [ConfigSection("Calm")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double CalmDamageTakenMultiplier { get; set; } = 1.0;


    // ------------------------------------------------------------------------
    // WRATH
    // ------------------------------------------------------------------------

    [ConfigSection("Wrath")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int WrathEnergyOnEnter { get; set; } = 0;

    [ConfigSection("Wrath")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int WrathEnergyOnExit { get; set; } = 0;

    [ConfigSection("Wrath")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double WrathDamageMultiplier { get; set; } = 2.0;

    [ConfigSection("Wrath")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double WrathDamageTakenMultiplier { get; set; } = 2.0;


    // ------------------------------------------------------------------------
    // DIVINITY
    // ------------------------------------------------------------------------

    [ConfigSection("Divinity")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int DivinityEnergyOnEnter { get; set; } = 3;

    [ConfigSection("Divinity")]
    [ConfigSlider(0, 5, 1)]
    [ConfigHoverTip]
    public static int DivinityEnergyOnExit { get; set; } = 0;

    [ConfigSection("Divinity")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double DivinityDamageMultiplier { get; set; } = 3.0;

    [ConfigSection("Divinity")]
    [ConfigSlider(0.5, 5.0, 0.1)]
    [ConfigHoverTip]
    public static double DivinityDamageTakenMultiplier { get; set; } = 1.0;


    // ========================================================================
    // REMOVED CARDS
    // ========================================================================

    [ConfigSection("Removed Cards")]
    [ConfigHoverTip]
    public static bool RestorePressurePoints { get; set; } =
        false;


    //[ConfigSection("Removed Cards")]
    //[ConfigHoverTip]
    //public static bool RestoreRushdown { get; set; } =
    //    false;


    [ConfigSection("Removed Cards")]
    [ConfigHoverTip]
    public static bool RestoreConjureBlade { get; set; } =
        false;


    [ConfigSection("Removed Cards")]
    [ConfigHoverTip]
    public static bool RestoreJudgment { get; set; } =
        false;


    [ConfigSection("Removed Cards")]
    [ConfigHoverTip]
    public static bool RestoreScrawl { get; set; } =
        false;


    [ConfigSection("Removed Cards")]
    [ConfigHoverTip]
    public static bool RestoreWish { get; set; } =
        false;


    // ========================================================================
    // FUN STUFF
    // ========================================================================

    [ConfigSection("Fun Stuff")]
    [ConfigHoverTip]
    public static bool EnableBlasphemyEasterEgg { get; set; } =
        false;
}