using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>What a legacy branch becomes in NDH.</summary>
internal abstract record BranchTranslation;

/// <summary>Connect with this NDH Produkt pinned, leaving the main this way.</summary>
internal sealed record TranslatedBranch(string Produkt, NdhBranchOutlet Outlet) : BranchTranslation;

/// <summary>
/// Not connected: marked in the drawing with <paramref name="Note"/> (English)
/// and reported with <paramref name="Reason"/> (Danish).
/// </summary>
internal sealed record UntranslatedBranch(string Note, string Reason) : BranchTranslation;

/// <summary>How one legacy part (by its block name, Navn) translates.</summary>
internal interface IBranchPartRule
{
    BranchTranslation Translate(LegacyBranch branch);
}

/// <summary>
/// The legacy branch part to NDH Produkt table (legacy-fjv-import.md
/// fittings-translation-settled-2026-09-18). Keyed on the block's real name,
/// not on its legacy element type: the types collide (a PertFlextra tee and a
/// twin steel tee are both "Lige afgrening"), the names do not. A part not in
/// the table is never guessed: it is marked (contract D9).
/// </summary>
internal static class LegacyBranchTranslator
{
    //NDH Produkt names, exactly as the NDH catalogue publishes them.
    private const string AfgreningMedSpring = "Afgrening med spring";
    private const string Parallelafgrening = "Parallelafgrening";
    private const string PraeisoleretTStykke = "Præisoleret T-stykke";
    private const string IndsvejstForgrening = "Indsvejst forgrening";
    private const string AquaThermT = "AquaTherm-T";
    private const string Afgreningsstuds = "Afgreningsstuds";
    private const string Svanehals = "Svanehals";
    private const string DirektePaasvejsning = "Direkte påsvejsning";

    private static readonly IBranchPartRule Pert318 = new MarkedRule(
        "PertFlextra/PertPIPE tees have no NDH part yet (NorsynDrawingTools #318)",
        "PertFlextra/PertPIPE-afgreninger findes ikke i NDH endnu (#318)");

    private static readonly IBranchPartRule AluPexPress321 = new MarkedRule(
        "AluPex press-coupling tees have no NDH part yet (NorsynDrawingTools #321)",
        "AluPex-preskoblingstees findes ikke i NDH endnu (#321)");

    private static readonly Dictionary<string, IBranchPartRule> ByNavn = new(StringComparer.Ordinal)
    {
        ["T ENKELT S2"] = new FixedRule(AfgreningMedSpring, NdhBranchOutlet.Perpendicular),
        ["T ENKELT S3"] = new FixedRule(AfgreningMedSpring, NdhBranchOutlet.Perpendicular),

        //A parallel branch leaves along its main.
        ["T PARALLEL S3 E"] = new FixedRule(Parallelafgrening, NdhBranchOutlet.AlongMain),
        ["PA TWIN S3"] = new FixedRule(Parallelafgrening, NdhBranchOutlet.AlongMain),
        ["T PARALLEL S3 E VARIABEL"] = new MarkedRule(
            "Parallelafgrening VARIABEL is not translated yet",
            "Parallelafgrening VARIABEL oversættes ikke endnu"),

        ["T TWIN S2"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["T TWIN S3"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["T-TWIN-S2-ISOPLUS"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["T-TWIN-S2-LOGSTOR"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["T-TWIN-S3-ISOPLUS"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["T-TWIN-S3-LOGSTOR"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),
        ["ALUPEX-PRÆ-TEE"] = new FixedRule(PraeisoleretTStykke, NdhBranchOutlet.Perpendicular),

        ["TEE KDLR"] = new FixedRule(IndsvejstForgrening, NdhBranchOutlet.Perpendicular),
        ["AT11-TEE"] = new FixedRule(AquaThermT, NdhBranchOutlet.Perpendicular),
        ["AFGRSTUDS"] = new FixedRule(Afgreningsstuds, NdhBranchOutlet.Perpendicular),

        //For now SH LIGE leaves at 90 degrees and SH VINKLET along the main.
        ["SH LIGE"] = new SvanehalsRule(Svanehals, NdhBranchOutlet.Perpendicular),
        ["SH VINKLET"] = new SvanehalsRule(Svanehals, NdhBranchOutlet.AlongMain),

        ["MATERIALESKIFT"] = new MaterialeskiftRule(DirektePaasvejsning),
        ["PRT-PIPE-MATSKIFT"] = new MaterialeskiftRule(DirektePaasvejsning),

        ["PRTFLX-TEE"] = Pert318,
        ["PRT-PIPE-TEE"] = Pert318,
        ["PRESKOBLING-TEE-PRT"] = Pert318,
        ["PRT-PIPE-PRESKOBLING-TEE"] = Pert318,
        //No NDH counterpart yet (D9); its own issue says what is missing.
        ["ALUPEX-PRESKOBLING-TEE"] = AluPexPress321,
    };

    public static BranchTranslation Translate(LegacyBranch branch) =>
        ByNavn.TryGetValue(branch.Navn, out IBranchPartRule? rule)
            ? rule.Translate(branch)
            : NoCounterpart(branch.Navn);

    internal static UntranslatedBranch NoCounterpart(string navn) => new(
        $"no NDH counterpart for legacy part '{navn}'",
        $"ingen NDH-modpart til den gamle del '{navn}'");

    private sealed class FixedRule(string produkt, NdhBranchOutlet outlet) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => new TranslatedBranch(produkt, outlet);
    }

    private sealed class MarkedRule(string note, string reason) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => new UntranslatedBranch(note, reason);
    }

    /// <summary>
    /// A svanehals is welded on top of a BONDED main; one found on a twin main
    /// is an error in the legacy drawing, marked for the drafter to fix there.
    /// </summary>
    private sealed class SvanehalsRule(string produkt, NdhBranchOutlet outlet) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => branch.MainType == PipeTypeEnum.Twin
            ? new UntranslatedBranch(
                $"svanehals '{branch.Navn}' sits on a twin main - legacy drawing error " +
                "(a svanehals is welded onto a bonded main)",
                "svanehals på en twin-hovedledning er en fejl i den gamle tegning")
            : new TranslatedBranch(produkt, outlet);
    }

    /// <summary>
    /// A materialeskift sitting on a STEEL main is welded straight onto it:
    /// Direkte påsvejsning. On any other main it has no NDH counterpart.
    /// </summary>
    private sealed class MaterialeskiftRule(string produkt) : IBranchPartRule
    {
        public BranchTranslation Translate(LegacyBranch branch) => branch.MainSystem == PipeSystemEnum.Stål
            ? new TranslatedBranch(produkt, NdhBranchOutlet.Perpendicular)
            : new UntranslatedBranch(
                $"no NDH counterpart for legacy part '{branch.Navn}' on a {branch.MainSystem} main",
                $"ingen NDH-modpart til den gamle del '{branch.Navn}' på en {branch.MainSystem}-hovedledning");
    }
}
