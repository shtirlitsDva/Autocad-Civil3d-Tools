using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>ONE PLACE A RULE ROW CAN SPEAK FOR: a pipe system and a situation.</summary>
internal readonly record struct FittingPlace(string SystemToken, string SituationToken)
{
    public override string ToString() => $"{SystemToken}/{SituationToken}";
}

/// <summary>
/// WHAT THE OLD DRAWING DREW, place by place - the count the drawing's policy
/// is fitted to.
///
/// It counts only what it can both PLACE and NAME: a block whose situation the
/// rule sheet decides (Elbow and Reducer), whose part translates to a Produkt,
/// and whose pipe system the block itself states. Everything else is tallied as
/// a number and reported, never as a complaint - the part register already
/// tells the drafter about a block NDH cannot place, and a second message about
/// a Y-rør that translated perfectly well would be noise.
///
/// THE COUNT IS WHAT THE DRAWING DID, not what it should have done. Nothing
/// here prefers a part because it is newer or better; the majority is policy
/// because it is the majority.
///
/// AND A COUNT HAS TO BE ABLE TO SEE BOTH ANSWERS. Arc is on the sheet and is
/// still not counted, because an elastic bend is a polyline ARC and not a
/// block: counting blocks finds every buerør in the drawing and not one of the
/// elastic bends beside them, so the sample is 100% buerør however the drawing
/// was actually drawn, and the row fitted from it made EVERY curve a buerør
/// (owner 2026-09-21). A situation whose two answers are not both blocks is
/// classified NOT_IN_BLOCKS in the register's generator and never reaches here.
/// </summary>
internal sealed class FittingCensus
{
    private readonly Dictionary<FittingPlace, Dictionary<string, int>> drawn = new();

    //WHAT EACH COUNTED BLOCK SAID, by its legacy handle. The fit needs only the
    //tally; naming the components that DISAGREE with the fit needs to get back
    //to the individual block, and a handle is what the route carries forward.
    private readonly Dictionary<string, CountedBlock> counted =
        new Dictionary<string, CountedBlock>(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> passedOver =
        new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Blocks whose own SysNavn names no pipe system NDH knows.</summary>
    public int UnplacedBlocks { get; private set; }

    /// <summary>Blocks counted toward the drawing's policy.</summary>
    public int CountedBlocks { get; private set; }

    public void Read(IEnumerable<LegacyComponent> parts)
    {
        foreach (LegacyComponent part in parts)
            LegacySituationRegister.Of(part.Navn).Tell(new Counting(this, part));
    }

    /// <summary>
    /// THE POLICY THE COUNT IMPLIES: one unbanded row per place, selecting the
    /// part drawn there most often.
    ///
    /// UNBANDED ON PURPOSE. A band narrows a row to part of a situation, and
    /// the count says nothing about where a boundary between two parts would
    /// lie - inventing one would be writing a policy the old drawing never
    /// expressed. The whole situation takes the majority, and every component
    /// that disagrees is named individually.
    ///
    /// A TIE IS NOT A POLICY. Where two parts are drawn equally often the place
    /// is left out: it keeps whatever row the drawing already has, and every
    /// component there is overridden instead. A coin-flip written into a
    /// drawing's policy is worse than a long override list, because the drafter
    /// cannot see it happened.
    /// </summary>
    public FittedSheet Fit()
    {
        List<NdhFittingRule> rows = new List<NdhFittingRule>();
        Dictionary<FittingPlace, string> majority = new Dictionary<FittingPlace, string>();
        List<string> fitted = new List<string>();
        List<string> tied = new List<string>();

        foreach ((FittingPlace place, Dictionary<string, int> counts) in
                 drawn.OrderBy(x => x.Key.SystemToken, StringComparer.Ordinal)
                      .ThenBy(x => x.Key.SituationToken, StringComparer.Ordinal))
        {
            int most = counts.Values.Max();
            List<string> leaders = counts.Where(c => c.Value == most)
                                         .Select(c => c.Key)
                                         .OrderBy(p => p, StringComparer.Ordinal)
                                         .ToList();
            string spread = string.Join(", ", counts.OrderByDescending(c => c.Value)
                                                    .ThenBy(c => c.Key, StringComparer.Ordinal)
                                                    .Select(c => $"{c.Key} {c.Value}"));
            if (leaders.Count > 1)
            {
                tied.Add($"{place}: {spread} - uafgjort, reglen står urørt og hver komponent " +
                         "sættes enkeltvis.");
                continue;
            }

            majority[place] = leaders[0];
            rows.Add(NdhFittingRule.Whole(place.SystemToken, place.SituationToken,
                                          new NdhSelectsProdukt(leaders[0])));
            int total = counts.Values.Sum();
            fitted.Add(total == most
                ? $"{place}: {leaders[0]} ({most})."
                : $"{place}: {leaders[0]} ({most} af {total}) - {spread}.");
        }

        List<string> report = new List<string>();
        report.AddRange(fitted);
        report.AddRange(tied);
        foreach ((string why, int n) in passedOver)
            report.Add($"{n} blok(ke) talt fra: {why}");
        if (UnplacedBlocks > 0)
            report.Add($"{UnplacedBlocks} blok(ke) oplyser intet rørsystem og kunne ikke placeres.");

        return new FittedSheet(rows, majority, counted, report);
    }

    private void Add(string handle, FittingPlace place, NdhCause cause, string produkt)
    {
        if (!drawn.TryGetValue(place, out Dictionary<string, int>? counts))
            drawn[place] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
        counts[produkt] = counts.TryGetValue(produkt, out int n) ? n + 1 : 1;
        counted[handle] = new CountedBlock(place, cause, produkt);
        CountedBlocks++;
    }

    private void PassOver(string why) =>
        passedOver[why] = passedOver.TryGetValue(why, out int n) ? n + 1 : 1;

    /// <summary>
    /// ONE BLOCK, ASKED BOTH QUESTIONS IN ORDER. The nesting is the ordering:
    /// only a block whose situation counts is asked what it becomes, so there
    /// is no flag to read back and no state that can be half-answered.
    /// </summary>
    private sealed class Counting : ISituationAudience, ILegacyVerdictAudience
    {
        private readonly FittingCensus census;
        private readonly LegacyComponent part;
        private string situation = "";
        private NdhCause cause;

        public Counting(FittingCensus census, LegacyComponent part)
        {
            this.census = census;
            this.part = part;
        }

        public void Counts(string situationToken, NdhCause causeKind)
        {
            situation = situationToken;
            cause = causeKind;
            LegacyPartRegister.Of(part.Navn).Tell(part.Navn, this);
        }

        public void PassedOver(string why) => census.PassOver(why);

        public void Translates(string navn, string produkt)
        {
            //THE BLOCK STATES ITS OWN SYSTEM. SysNavn is the pipe system's own
            //name, so the enum parses it; the reference table's System column
            //is often a dynamic placeholder and would not.
            if (!Enum.TryParse(part.SysNavn, out PipeSystemEnum system)
                || system == PipeSystemEnum.Ukendt)
            {
                census.UnplacedBlocks++;
                return;
            }
            census.Add(part.Handle, new FittingPlace(NsDhModule.SystemToken(system), situation),
                       cause, produkt);
        }

        //A part NDH cannot place is the part register's message to tell, and it
        //tells it. The census only counts, so it says nothing twice.
        public void Missing(string navn, string note, string reason) =>
            census.PassOver("delen findes ikke i NDH endnu");

        public void Skipped(string navn, string why) => census.PassOver(why);
    }
}

/// <summary>
/// The policy the census implies, and what a component has to agree with to be
/// left alone.
/// </summary>
/// <summary>One legacy block the census counted, and everything naming its component needs.</summary>
internal readonly record struct CountedBlock(FittingPlace Place, NdhCause Cause, string Produkt);

internal sealed record FittedSheet(
    IReadOnlyList<NdhFittingRule> Rows,
    IReadOnlyDictionary<FittingPlace, string> Majority,
    IReadOnlyDictionary<string, CountedBlock> Counted,
    IReadOnlyList<string> Report)
{
    /// <summary>
    /// TRUE WHEN THE SHEET ALREADY ANSWERS THIS COMPONENT, so no override is
    /// owed. False both when the place took a different majority AND when the
    /// place was left unfitted - an unfitted place answers with whatever row
    /// the drawing already had, which is not this block's word, so every
    /// component there is named individually.
    /// </summary>
    public bool Answers(FittingPlace place, string produkt) =>
        Majority.TryGetValue(place, out string? chosen) && chosen == produkt;

    /// <summary>
    /// WHETHER THE COMPONENT THIS BLOCK BECOMES NEEDS NAMING, and which part it
    /// should be given. Three outcomes and all three are stated: the block was
    /// never counted, the fitted policy already says what it said, or the
    /// policy says something else and this one place has to be overruled.
    /// </summary>
    public void TellDeviation(string legacyHandle, IDeviationAudience audience)
    {
        if (!Counted.TryGetValue(legacyHandle, out CountedBlock said))
        {
            audience.NotCounted();
            return;
        }
        if (Answers(said.Place, said.Produkt))
        {
            audience.Agrees();
            return;
        }
        audience.Overrides(said.Cause, said.Produkt);
    }
}

/// <summary>What a caller does about one block, once the sheet has been fitted.</summary>
internal interface IDeviationAudience
{
    /// <summary>The census never counted this block; there is nothing to say about it.</summary>
    void NotCounted();

    /// <summary>The fitted policy already picks what this block drew. Leave it alone.</summary>
    void Agrees();

    /// <summary>
    /// The policy says otherwise here, so this one component is named. The
    /// cause kind comes with the part because an override names both.
    /// </summary>
    void Overrides(NdhCause cause, string produkt);
}
