using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// THE LEGACY BLOCK REGISTER: what every block of the company's FJV register
/// becomes in NDH.
///
/// Authority: NorsynDrawingTools <c>docs/reference/legacy-block-to-produkt.md</c>
/// (the table) and <c>docs/shared-understanding/legacy-fjv-import.md</c>
/// &lt;every-block-settled-2026-09-21&gt; (what it means). GENERATED from that
/// table - edit the table, not this file.
///
/// The key is the block's NAVN and never its legacy Type: the types collide
/// across systems (a PertFlextra tee and a twin steel tee are both
/// "Lige afgrening"), the names do not. Every Version variant of a block
/// shares its block's verdict, because the verdict is about the PART.
///
/// A BLOCK RESOLVES TO ONE OF FOUR THINGS and the last three are NOT one
/// answer - which is the whole reason this is not a nullable string. A
/// Produkt translates; NOT IMPLEMENTED is a real part NDH does not have yet
/// and is MARKED; NOT NEEDED is a part we deliberately do not model and is
/// skipped in SILENCE; NOT A FITTING is not a part at all. The last two are
/// kept apart because they age differently - the first bucket empties as the
/// catalogue grows and the others never do.
/// </summary>
internal static class LegacyPartRegister
{
    //NDH Produkt names, exactly as the catalogue publishes them.
    private const string AfgreningMedSpring = "Afgrening med spring";
    private const string Afgreningsstuds = "Afgreningsstuds";
    private const string AquathermT = "AquaTherm-T";
    private const string Bueroer = "Buerør";
    private const string Endebund = "Endebund";
    private const string FRoer = "F-rør";
    private const string IndsvejstForgrening = "Indsvejst forgrening";
    private const string Kedelroersboejning = "Kedelrørsbøjning";
    private const string Kedelroersreduktion = "Kedelrørsreduktion";
    private const string Materialeskift = "Materialeskift";
    private const string Parallelafgrening = "Parallelafgrening";
    private const string PreskoblingTStykke = "Preskobling T-stykke";
    private const string PraeisoleretTStykke = "Præisoleret T-stykke";
    private const string PraeisoleretBoejningAlupex = "Præisoleret bøjning (AluPex)";
    private const string PraeisoleretBoejningAquatherm = "Præisoleret bøjning (AquaTherm)";
    private const string PraeisoleretBoejningPertflextra = "Præisoleret bøjning (PertFLEXTRA)";
    private const string PraeisoleretBoejningPertpipe = "Præisoleret bøjning (PertPIPE)";
    private const string PraeisoleretBoejningStaal = "Præisoleret bøjning (stål)";
    private const string PraeisoleretReduktionStaal = "Præisoleret reduktion (stål)";
    private const string Reduktion = "Reduktion";
    private const string Svanehals = "Svanehals";
    private const string Ventil = "Ventil";
    private const string YRoer = "Y-rør";

    private static readonly Dictionary<string, LegacyVerdict> ByNavn =
        new(StringComparer.Ordinal)
    {
        ["T ENKELT S2"] = new PartIsProdukt(AfgreningMedSpring),
        ["T ENKELT S3"] = new PartIsProdukt(AfgreningMedSpring),
        ["T PARALLEL S3 E"] = new PartIsProdukt(Parallelafgrening),
        ["T PARALLEL S3 E VARIABEL"] = new PartIsProdukt(Parallelafgrening),
        ["AFGRSTUDS"] = new PartIsProdukt(Afgreningsstuds),
        ["ENDEBUND"] = new PartIsProdukt(Endebund),
        ["VENTIL E"] = new PartNotImplemented("engangsventil: NDH's Ventil is a different part and must not be substituted", "engangsventil findes ikke i NDH endnu - NDH's Ventil er ikke den samme del"),
        ["F MODEL"] = new PartIsProdukt(FRoer),
        ["F-MODEL-GLD"] = new PartIsProdukt(FRoer),
        ["F-MODEL-ISOPLUS"] = new PartIsProdukt(FRoer),
        ["F-MODEL-LOGSTOR"] = new PartIsProdukt(FRoer),
        ["H-MODEL-ISOPLUS-ALUPEX"] = new PartNotImplemented("H-rør has no NDH counterpart, for AluPex or for steel", "H-rør findes ikke i NDH endnu"),
        ["BØJN KDLR"] = new PartIsProdukt(Kedelroersboejning),
        ["BØJN KDLR SIMPEL"] = new PartIsProdukt(Kedelroersboejning),
        ["BØJN KDLR v2"] = new PartIsProdukt(Kedelroersboejning),
        ["AT-ELBW-90D"] = new PartIsProdukt(PraeisoleretBoejningAquatherm),
        ["AT-ELBW-45D"] = new PartIsProdukt(PraeisoleretBoejningAquatherm),
        ["AT-ELBW-30D"] = new PartIsProdukt(PraeisoleretBoejningAquatherm),
        ["AT-ELBW-15D"] = new PartIsProdukt(PraeisoleretBoejningAquatherm),
        ["BØJN KDLR VERT 45"] = new PartIsProdukt(Kedelroersboejning),
        ["BØJN KDLR VERT"] = new PartIsProdukt(Kedelroersboejning),
        ["BØJN KDLR VERT 5D"] = new PartIsProdukt(Kedelroersboejning),
        ["T TWIN S2"] = new PartIsProdukt(PraeisoleretTStykke),
        ["T TWIN S3"] = new PartIsProdukt(PraeisoleretTStykke),
        ["T-TWIN-S2-ISOPLUS"] = new PartIsProdukt(PraeisoleretTStykke),
        ["T-TWIN-S3-ISOPLUS"] = new PartIsProdukt(PraeisoleretTStykke),
        ["T-TWIN-S2-LOGSTOR"] = new PartIsProdukt(PraeisoleretTStykke),
        ["T-TWIN-S3-LOGSTOR"] = new PartIsProdukt(PraeisoleretTStykke),
        ["TEE KDLR"] = new PartIsProdukt(IndsvejstForgrening),
        ["AT11-TEE"] = new PartIsProdukt(AquathermT),
        ["PA TWIN S3"] = new PartIsProdukt(Parallelafgrening),
        ["PRÆBØJN 90GR ENKELT"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["PRÆBØJN 90GR TWIN"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["PRÆBØJN-90GR-TWIN-GLD"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["PRÆBØJN 90GR VARIABEL"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["PRÆBØJN-90GR-VARIABEL-V1"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["PRÆBØJN-90GR-VARIABEL-GLD"] = new PartIsProdukt(PraeisoleretBoejningStaal),
        ["VENTIL TWIN"] = new PartIsProdukt(Ventil),
        ["VENTIL-TWIN-GLD"] = new PartIsProdukt(Ventil),
        ["VENTIL-TWIN-ISOPLUS"] = new PartIsProdukt(Ventil),
        ["VENTIL-TWIN-LOGSTOR"] = new PartIsProdukt(Ventil),
        ["VENTIL-ENKELT-GLD"] = new PartIsProdukt(Ventil),
        ["PRÆRED LOGSTOR"] = new PartIsProdukt(PraeisoleretReduktionStaal),
        ["PRÆRED ISOPLUS"] = new PartIsProdukt(PraeisoleretReduktionStaal),
        ["RED KDLR"] = new PartIsProdukt(Kedelroersreduktion),
        ["RED KDLR x2"] = new PartIsProdukt(Kedelroersreduktion),
        ["AT11-REDUCER"] = new PartIsProdukt(Reduktion),
        ["SH LIGE"] = new PartIsProdukt(Svanehals),
        ["SH VINKLET"] = new PartIsProdukt(Svanehals),
        ["SVEJSEPUNKT"] = new PartNotAFitting("a weld mark, not a part"),
        ["SVEJSEPUNKT-NOTXT"] = new PartNotAFitting("a weld mark, not a part"),
        ["SVEJSEPUNKT-V2"] = new PartNotAFitting("a weld mark, not a part"),
        ["Y-RØR-GML-v1"] = new PartIsProdukt(YRoer),
        ["Y-RØR-GLD"] = new PartIsProdukt(YRoer),
        ["Y-RØR-GLD-ISOPLUS"] = new PartIsProdukt(YRoer),
        ["Y-RØR-GLD-LOGSTOR"] = new PartIsProdukt(YRoer),
        ["Y-RØR-ALUPEX"] = new PartNotImplemented("Y-rør is published for steel only; the AluPex variant has no NDH counterpart", "Y-rør i AluPex findes ikke i NDH endnu"),
        ["Y-RØR"] = new PartIsProdukt(YRoer),
        ["BUEROR1"] = new PartIsProdukt(Bueroer),
        ["BUEROR2"] = new PartIsProdukt(Bueroer),
        ["STIKAFGRENING"] = new PartNotNeeded("stik are not modelled in NDH"),
        ["STIKTEE"] = new PartNotNeeded("stik are not modelled in NDH"),
        ["MATERIALESKIFT"] = new PartIsProdukt(Materialeskift),
        ["PRTFLX-TEE"] = new PartNotImplemented("PertFlextra tees have no NDH part yet (NorsynDrawingTools #318)", "PertFlextra-afgreninger findes ikke i NDH endnu (#318)"),
        ["PRTFLX-BØJN-90"] = new PartIsProdukt(PraeisoleretBoejningPertflextra),
        ["PRTFLX-BØJN-45"] = new PartIsProdukt(PraeisoleretBoejningPertflextra),
        ["PRTFLX-REDUKTION"] = new PartIsProdukt(Reduktion),
        ["PRESKOBLING-TEE-PRT"] = new PartNotImplemented("Preskobling T-stykke is published for AluPex only (NorsynDrawingTools #318)", "Preskobling T-stykke findes kun for AluPex (#318)"),
        ["PRT-PIPE-TEE"] = new PartNotImplemented("PertPIPE tees have no NDH part yet (NorsynDrawingTools #318)", "PertPIPE-afgreninger findes ikke i NDH endnu (#318)"),
        ["PRT-PIPE-BØJN-90"] = new PartIsProdukt(PraeisoleretBoejningPertpipe),
        ["PRT-PIPE-BØJN-45"] = new PartIsProdukt(PraeisoleretBoejningPertpipe),
        ["PRT-PIPE-REDUKTION"] = new PartIsProdukt(Reduktion),
        ["PRT-PIPE-PRESKOBLING-TEE"] = new PartNotImplemented("Preskobling T-stykke is published for AluPex only (NorsynDrawingTools #318)", "Preskobling T-stykke findes kun for AluPex (#318)"),
        ["PRT-PIPE-MATSKIFT"] = new PartNotNeeded("not used"),
        ["PRT-PIPE-VENTIL"] = new PartIsProdukt(Ventil),
        ["ALUPEX-PRÆ-TEE"] = new PartIsProdukt(PraeisoleretTStykke),
        ["ALUPEX-BØJN-90"] = new PartIsProdukt(PraeisoleretBoejningAlupex),
        ["ALUPEX-PRESKOBLING-TEE"] = new PartIsProdukt(PreskoblingTStykke),
        ["ALUPEX-REDUKTION"] = new PartIsProdukt(Reduktion),
    };

    /// <summary>
    /// What this block becomes. A block the register does not name is NOT a
    /// silent skip: the register covers the whole company register, so an
    /// unknown name is a block from somewhere else, and the drafter is told.
    /// </summary>
    public static LegacyVerdict Of(string navn) =>
        ByNavn.TryGetValue(navn, out LegacyVerdict? verdict)
            ? verdict
            : new PartNotImplemented(
                $"legacy block '{navn}' is not in the company register",
                $"den gamle blok '{navn}' står ikke i firmaets blokregister");

    /// <summary>Every block the register names, for the census and the tests.</summary>
    public static IReadOnlyDictionary<string, LegacyVerdict> All => ByNavn;
}
