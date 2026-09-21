using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// WHICH NDH SITUATION EACH LEGACY BLOCK STANDS IN - the question the
/// census asks that <see cref="LegacyPartRegister"/> cannot answer. The
/// part register says what a block BECOMES; this says what KIND of place it
/// stands in, and a rule row needs both.
///
/// GENERATED from the same table, keyed on its Type column - edit
/// <c>tools/gen-legacy-register/gen_register.py</c>&apos;s SITUATIONS map and
/// re-run. A legacy type nobody has classified refuses the generation: an
/// unclassified block is one the census would pass over in silence.
///
/// Only three situations can be FITTED as policy - Elbow, Reducer and Arc,
/// and each states the CAUSE KIND of the component it becomes, so an
/// override written for it names the component in full without anything
/// having to translate a situation into a cause.
/// Everything else states why it is somebody else&apos;s: the afgreningsmatrix
/// decides afgreninger, three roles take the only Produkt they publish and
/// never read the sheet, a ventil has no component at all, a stik is not
/// modelled, and a weld mark is not a part.
/// </summary>
internal static class LegacySituationRegister
{
    private static readonly Dictionary<string, LegacySituation> ByNavn =
        new(StringComparer.Ordinal)
    {
        ["T ENKELT S2"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T ENKELT S3"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T PARALLEL S3 E"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T PARALLEL S3 E VARIABEL"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["AFGRSTUDS"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["ENDEBUND"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["VENTIL E"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["F MODEL"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["F-MODEL-GLD"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["F-MODEL-ISOPLUS"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["F-MODEL-LOGSTOR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["H-MODEL-ISOPLUS-ALUPEX"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["BØJN KDLR"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["BØJN KDLR SIMPEL"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["BØJN KDLR v2"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["AT-ELBW-90D"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["AT-ELBW-45D"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["AT-ELBW-30D"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["AT-ELBW-15D"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["BØJN KDLR VERT 45"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["BØJN KDLR VERT"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["BØJN KDLR VERT 5D"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["T TWIN S2"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T TWIN S3"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T-TWIN-S2-ISOPLUS"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T-TWIN-S3-ISOPLUS"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T-TWIN-S2-LOGSTOR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["T-TWIN-S3-LOGSTOR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["TEE KDLR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["AT11-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PA TWIN S3"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PRÆBØJN 90GR ENKELT"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRÆBØJN 90GR TWIN"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRÆBØJN-90GR-TWIN-GLD"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRÆBØJN 90GR VARIABEL"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRÆBØJN-90GR-VARIABEL-V1"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRÆBØJN-90GR-VARIABEL-GLD"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["VENTIL TWIN"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["VENTIL-TWIN-GLD"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["VENTIL-TWIN-ISOPLUS"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["VENTIL-TWIN-LOGSTOR"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["VENTIL-ENKELT-GLD"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["PRÆRED LOGSTOR"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["PRÆRED ISOPLUS"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["RED KDLR"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["RED KDLR x2"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["AT11-REDUCER"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["SH LIGE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["SH VINKLET"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["SVEJSEPUNKT"] = new DecidedElsewhere("not a part"),
        ["SVEJSEPUNKT-NOTXT"] = new DecidedElsewhere("not a part"),
        ["SVEJSEPUNKT-V2"] = new DecidedElsewhere("not a part"),
        ["Y-RØR-GML-v1"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["Y-RØR-GLD"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["Y-RØR-GLD-ISOPLUS"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["Y-RØR-GLD-LOGSTOR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["Y-RØR-ALUPEX"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["Y-RØR"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["BUEROR1"] = new SheetDecides(NdhSituation.Arc, NdhCause.Arc),
        ["BUEROR2"] = new SheetDecides(NdhSituation.Arc, NdhCause.Arc),
        ["STIKAFGRENING"] = new DecidedElsewhere("a stik is not modelled in NDH, so there is no place to stand in"),
        ["STIKTEE"] = new DecidedElsewhere("a stik is not modelled in NDH, so there is no place to stand in"),
        ["MATERIALESKIFT"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["PRTFLX-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PRTFLX-BØJN-90"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRTFLX-BØJN-45"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRTFLX-REDUKTION"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["PRESKOBLING-TEE-PRT"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PRT-PIPE-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PRT-PIPE-BØJN-90"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRT-PIPE-BØJN-45"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["PRT-PIPE-REDUKTION"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
        ["PRT-PIPE-PRESKOBLING-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["PRT-PIPE-MATSKIFT"] = new DecidedElsewhere("the planner takes the only Produkt this role publishes and never reads the sheet"),
        ["PRT-PIPE-VENTIL"] = new DecidedElsewhere("no valve component is ever planned onto a run, so there is no part to select"),
        ["ALUPEX-PRÆ-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["ALUPEX-BØJN-90"] = new SheetDecides(NdhSituation.Elbow, NdhCause.Elbow),
        ["ALUPEX-PRESKOBLING-TEE"] = new DecidedElsewhere("the afgreningsmatrix decides afgreninger, never a rule row"),
        ["ALUPEX-REDUKTION"] = new SheetDecides(NdhSituation.Reducer, NdhCause.Reducer),
    };

    /// <summary>
    /// The situation this block stands in. A block the register does not
    /// name is not counted and says why - the part register tells the
    /// drafter about it, and the census has nothing to add.
    /// </summary>
    public static LegacySituation Of(string navn) =>
        ByNavn.TryGetValue(navn, out LegacySituation? situation)
            ? situation
            : new DecidedElsewhere(
                $"the block '{navn}' is not in the company register, so what kind of " +
                "place it stands in is not known either");
}
