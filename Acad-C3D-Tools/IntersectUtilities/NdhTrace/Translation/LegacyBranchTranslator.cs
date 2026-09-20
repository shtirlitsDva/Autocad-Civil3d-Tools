using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// WHO HEARS WHAT A LEGACY BRANCH BECOMES. Three outcomes, each its own method,
/// so no caller tests a translation's type to find out what to do with it.
/// </summary>
internal interface IBranchAudience
{
    /// <summary>Connect, with this Produkt pinned, leaving the main this way.</summary>
    void Connect(string produkt, NdhBranchOutlet outlet);

    /// <summary>
    /// Not connected: <paramref name="note"/> (English) goes in the drawing,
    /// <paramref name="reason"/> (Danish) in the report.
    /// </summary>
    void CannotConnect(string note, string reason);

    /// <summary>
    /// Nothing to connect and nothing to say - a part NDH deliberately does not
    /// model. Silence is the law here, not an oversight
    /// (legacy-fjv-import.md &lt;every-block-settled-2026-09-21&gt;).
    /// </summary>
    void Ignore(string why);
}

/// <summary>What a legacy branch becomes in NDH.</summary>
internal abstract record BranchTranslation
{
    internal abstract void Tell(IBranchAudience audience);
}

/// <summary>Connect with this NDH Produkt pinned, leaving the main this way.</summary>
internal sealed record TranslatedBranch(string Produkt, NdhBranchOutlet Outlet) : BranchTranslation
{
    internal override void Tell(IBranchAudience audience) => audience.Connect(Produkt, Outlet);
}

/// <summary>
/// Not connected: marked in the drawing with <paramref name="Note"/> (English)
/// and reported with <paramref name="Reason"/> (Danish).
/// </summary>
internal sealed record UntranslatedBranch(string Note, string Reason) : BranchTranslation
{
    internal override void Tell(IBranchAudience audience) => audience.CannotConnect(Note, Reason);
}

/// <summary>A part NDH deliberately does not model. Skipped in silence.</summary>
internal sealed record IgnoredBranch(string Why) : BranchTranslation
{
    internal override void Tell(IBranchAudience audience) => audience.Ignore(Why);
}

/// <summary>How one legacy part (by its block name, Navn) translates.</summary>
internal interface IBranchPartRule
{
    BranchTranslation Translate(LegacyBranch branch);
}

/// <summary>
/// WHERE A LEGACY BRANCH LEAVES ITS MAIN - and nothing else.
///
/// WHICH PRODUKT a block is belongs to <see cref="LegacyPartRegister"/>, which
/// covers the whole company register and is generated from the reference table.
/// This class used to carry its own copy of that answer for the branch blocks,
/// which is two tables that can silently disagree; it now asks. What stays here
/// is the one fact the register cannot hold, because it is not a fact about the
/// BLOCK: the outlet, and the two readings that depend on the MAIN the part
/// happens to sit on.
///
/// Keyed on the block's real name, not on its legacy element type: the types
/// collide (a PertFlextra tee and a twin steel tee are both "Lige afgrening"),
/// the names do not.
/// </summary>
internal static class LegacyBranchTranslator
{
    //The one Produkt name this class owns, and it is deliberately NOT the
    //register's answer for the block: a materialeskift is Direkte påsvejsning
    //HERE, welded onto a steel main, and Materialeskift where it sits inline
    //between two systems. The block name cannot tell the two apart, so POSITION
    //decides, and this is the position that knows (legacy-fjv-import.md,
    //2026-09-21).
    private const string DirektePaasvejsning = "Direkte påsvejsning";

    private static readonly IBranchPartRule Square = new OutletRule(NdhBranchOutlet.Perpendicular);
    private static readonly IBranchPartRule Along = new OutletRule(NdhBranchOutlet.AlongMain);

    private static readonly Dictionary<string, IBranchPartRule> ByNavn = new(StringComparer.Ordinal)
    {
        ["T ENKELT S2"] = Square,
        ["T ENKELT S3"] = Square,

        //A parallel branch leaves along its main.
        ["T PARALLEL S3 E"] = Along,
        ["PA TWIN S3"] = Along,
        //VARIABEL is in the register as NOT IMPLEMENTED; the outlet is stated
        //anyway, so the day the part exists this row needs no edit.
        ["T PARALLEL S3 E VARIABEL"] = Along,

        ["T TWIN S2"] = Square,
        ["T TWIN S3"] = Square,
        ["T-TWIN-S2-ISOPLUS"] = Square,
        ["T-TWIN-S2-LOGSTOR"] = Square,
        ["T-TWIN-S3-ISOPLUS"] = Square,
        ["T-TWIN-S3-LOGSTOR"] = Square,
        ["ALUPEX-PRÆ-TEE"] = Square,

        ["TEE KDLR"] = Square,
        ["AT11-TEE"] = Square,
        ["AFGRSTUDS"] = Square,

        //For now SH LIGE leaves at 90 degrees and SH VINKLET along the main.
        ["SH LIGE"] = new SvanehalsRule(NdhBranchOutlet.Perpendicular),
        ["SH VINKLET"] = new SvanehalsRule(NdhBranchOutlet.AlongMain),

        ["MATERIALESKIFT"] = new MaterialeskiftRule(),
        ["PRT-PIPE-MATSKIFT"] = new MaterialeskiftRule(),

        //The four Pert branches and the two stik need no row of their own any
        //more: the register says NOT IMPLEMENTED for the first four (#318) and
        //NOT NEEDED for the stik, and the outlet below is what they would use.
        ["PRTFLX-TEE"] = Square,
        ["PRT-PIPE-TEE"] = Square,
        ["PRESKOBLING-TEE-PRT"] = Square,
        ["PRT-PIPE-PRESKOBLING-TEE"] = Square,
        ["STIKAFGRENING"] = Square,
        ["STIKTEE"] = Square,

        //AluPex onto AluPex only; NDH refuses a bonded main or a child in another material.
        ["ALUPEX-PRESKOBLING-TEE"] = Square,
    };

    public static BranchTranslation Translate(LegacyBranch branch) =>
        ByNavn.TryGetValue(branch.Navn, out IBranchPartRule? rule)
            ? rule.Translate(branch)
            : FromRegister(branch.Navn, NdhBranchOutlet.Perpendicular);

    internal static UntranslatedBranch NoCounterpart(string navn) => new(
        $"no NDH counterpart for legacy part '{navn}'",
        $"ingen NDH-modpart til den gamle del '{navn}'");

    /// <summary>
    /// The register's verdict for this block, said in the branch path's own
    /// words. The four verdicts collapse to three here because a branch has no
    /// use for the difference between a part we do not model and a thing that
    /// is not a part: both are silence.
    /// </summary>
    private static BranchTranslation FromRegister(string navn, NdhBranchOutlet outlet)
    {
        BranchRelay relay = new BranchRelay(outlet);
        LegacyPartRegister.Of(navn).Tell(navn, relay);
        return relay.Translation;
    }

    private sealed class BranchRelay(NdhBranchOutlet outlet) : ILegacyVerdictAudience
    {
        //Set by exactly one of the three below. The register always says one of
        //them - including for a name it does not know - so it is never unset.
        public BranchTranslation Translation { get; private set; } =
            new IgnoredBranch("the register said nothing");

        public void Translates(string navn, string produkt) =>
            Translation = new TranslatedBranch(produkt, outlet);

        public void Missing(string navn, string note, string reason) =>
            Translation = new UntranslatedBranch(note, reason);

        public void Skipped(string navn, string why) => Translation = new IgnoredBranch(why);
    }

    private sealed class OutletRule(NdhBranchOutlet outlet) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) =>
            FromRegister(branch.Navn, outlet);
    }

    /// <summary>
    /// A svanehals is welded on top of a BONDED main; one found on a twin main
    /// is an error in the legacy drawing, marked for the drafter to fix there.
    /// </summary>
    private sealed class SvanehalsRule(NdhBranchOutlet outlet) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => branch.MainType == PipeTypeEnum.Twin
            ? new UntranslatedBranch(
                $"svanehals '{branch.Navn}' sits on a twin main - legacy drawing error " +
                "(a svanehals is welded onto a bonded main)",
                "svanehals på en twin-hovedledning er en fejl i den gamle tegning")
            : FromRegister(branch.Navn, outlet);
    }

    /// <summary>
    /// A materialeskift sitting on a STEEL main is welded straight onto it:
    /// Direkte påsvejsning. On any other main it has no NDH counterpart.
    ///
    /// This is the one rule that does NOT ask the register, and deliberately:
    /// the register answers what the BLOCK is, which for a materialeskift is
    /// the inline part of the same name. Here it is a branch, and a branch is a
    /// different part.
    /// </summary>
    private sealed class MaterialeskiftRule : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => branch.MainSystem == PipeSystemEnum.Stål
            ? new TranslatedBranch(DirektePaasvejsning, NdhBranchOutlet.Perpendicular)
            : new UntranslatedBranch(
                $"no NDH counterpart for legacy part '{branch.Navn}' on a {branch.MainSystem} main",
                $"ingen NDH-modpart til den gamle del '{branch.Navn}' på en {branch.MainSystem}-hovedledning");
    }
}
