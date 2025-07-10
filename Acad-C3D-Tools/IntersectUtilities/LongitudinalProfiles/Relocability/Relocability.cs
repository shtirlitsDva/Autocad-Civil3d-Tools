using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using Microsoft.Extensions.DependencyInjection;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LongitudinalProfiles.Relocability
{
    public readonly record struct FjvPipe(PipeSystemEnum System, int NominalDiameter);

    public readonly record struct LerKrydsning(LerTypeEnum Type, Spatial Spatial, int Diameter);

    public readonly struct RuleKey : IEquatable<RuleKey>
    {
        public LerTypeEnum Type { get; }
        public Spatial Spatial { get; }

        public RuleKey(LerTypeEnum type, Spatial spatial = Spatial.Unknown) =>
            (Type, Spatial) = (type, spatial);

        public bool Equals(RuleKey other) => Type == other.Type && Spatial == other.Spatial;

        public override int GetHashCode() => HashCode.Combine(Type, Spatial);

        public override string ToString() => $"{Type}-{Spatial}";
    }

    public static class RuleKeyExtensions
    {
        //We use this to convert LerKrydsning to RuleKey
        //This is because some rules care about spatial and others do not.
        //If the rule does not care about spatial, we use Spatial.Unknown
        public static RuleKey ToRuleKey(this LerKrydsning util)
        {
            switch (util.Type)
            {
                case LerTypeEnum.Afløb:
                    return new RuleKey(util.Type, util.Spatial);
                case LerTypeEnum.Damp:
                case LerTypeEnum.EL_HS:
                case LerTypeEnum.EL_LS:
                case LerTypeEnum.Gas:
                case LerTypeEnum.Luft:
                case LerTypeEnum.Oil:
                case LerTypeEnum.FJV:
                case LerTypeEnum.Vand:
                case LerTypeEnum.UAD:
                    return new RuleKey(util.Type);
                default:
                    return new RuleKey(util.Type, Spatial.Unknown);
            }
        }
    }

    // ------------------------- Contracts ------------------------
    public interface IRelocatabilityService
    {
        bool IsRelocatable(FjvPipe pipe, LerKrydsning util);
    }

    public interface IPipeRule
    {
        bool AppliesTo(FjvPipe pipe);
        bool IsRelocatable(LerKrydsning util);
    }

    public interface IThreshold
    {
        bool IsRelocatable(int diameter);
    }

    #region Thresholds
    public abstract record Threshold : IThreshold
    {
        public abstract bool IsRelocatable(int diameter);

        public static readonly IThreshold AlleRespekteres = new AlleRespekteresThreshold();
        public static IThreshold AlleOmlægges => new AlleOmlæggesThreshold();

        public static IThreshold KanOmlæggesUnder(int limit) =>
            new KanOmlæggesUnderThreshold(limit);

        private sealed record AlleRespekteresThreshold : Threshold
        {
            public override bool IsRelocatable(int _) => false;
        }

        private sealed record AlleOmlæggesThreshold : Threshold
        {
            public override bool IsRelocatable(int _) => true;
        }

        private sealed record KanOmlæggesUnderThreshold(int Limit) : Threshold
        {
            public override bool IsRelocatable(int d) => d <= Limit;

            public override string ToString() => $"<= {Limit}";
        }
    }
    #endregion

    // ------------------------ Rules -----------------------------
    public sealed class PipeRule : IPipeRule
    {
        private readonly ImmutableDictionary<RuleKey, IThreshold> _thresholds;

        public PipeSystemEnum System { get; }
        public int? DnMin { get; }
        public int? DnMax { get; }

        public PipeRule(
            PipeSystemEnum system,
            int? dnMin,
            int? dnMax,
            IDictionary<RuleKey, IThreshold> thresholds
        )
        {
            System = system;
            DnMin = dnMin;
            DnMax = dnMax;
            _thresholds = thresholds.ToImmutableDictionary();
        }

        public bool AppliesTo(FjvPipe pipe) =>
            pipe.System == System
            && pipe.NominalDiameter >= (DnMin ?? int.MinValue)
            && pipe.NominalDiameter <= (DnMax ?? int.MaxValue);

        public bool IsRelocatable(LerKrydsning util) =>
            _thresholds.TryGetValue(util.ToRuleKey(), out var th)
            && th.IsRelocatable(util.Diameter);
    }

    // -------------------- Service façade ------------------------
    public sealed class RelocatabilityService : IRelocatabilityService
    {
        private readonly ImmutableArray<IPipeRule> _rules;

        public RelocatabilityService(IEnumerable<IPipeRule> rules) =>
            _rules = rules.ToImmutableArray();

        public bool IsRelocatable(FjvPipe pipe, LerKrydsning util)
        {
            var rule =
                _rules.FirstOrDefault(r => r.AppliesTo(pipe))
                ?? throw new InvalidOperationException(
                    $"No rule for {pipe.System} DN{pipe.NominalDiameter}"
                );
            return rule.IsRelocatable(util);
        }
    }

    // ------------------- Rule factory ---------------------------
    public static class RuleFactory
    {
        public static IEnumerable<IPipeRule> DefaultRules { get; } =
            [
                // Twin steel DN32‒DN50
                new PipeRule(
                    PipeSystemEnum.Stål,
                    32,
                    50,
                    new Dictionary<RuleKey, IThreshold>
                    {
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.ThreeD),
                            Threshold.AlleRespekteres
                        },
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.TwoD),
                            Threshold.AlleOmlægges
                        },
                        { new RuleKey(LerTypeEnum.Damp), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.EL_LS), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.EL_HS), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.FJV), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Gas), Threshold.KanOmlæggesUnder(41) },
                        { new RuleKey(LerTypeEnum.Luft), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Oil), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand), Threshold.KanOmlæggesUnder(41) },
                        { new RuleKey(LerTypeEnum.UAD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Ignored), Threshold.AlleOmlægges },
                    }
                ),
                // Twin steel DN65‒DN80
                new PipeRule(
                    PipeSystemEnum.Stål,
                    65,
                    80,
                    new Dictionary<RuleKey, IThreshold>
                    {
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.ThreeD),
                            Threshold.AlleRespekteres
                        },
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.TwoD),
                            Threshold.AlleOmlægges
                        },
                        { new RuleKey(LerTypeEnum.Damp), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.EL_LS), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.EL_HS), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.FJV), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Gas), Threshold.KanOmlæggesUnder(64) },
                        { new RuleKey(LerTypeEnum.Luft), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Oil), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand), Threshold.KanOmlæggesUnder(64) },
                        { new RuleKey(LerTypeEnum.UAD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Ignored), Threshold.AlleOmlægges },
                    }
                ),
                // Twin steel DN100‒DN250
                new PipeRule(
                    PipeSystemEnum.Stål,
                    100,
                    600,
                    new Dictionary<RuleKey, IThreshold>
                    {
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.ThreeD),
                            Threshold.AlleRespekteres
                        },
                        {
                            new RuleKey(LerTypeEnum.Afløb, Spatial.TwoD),
                            Threshold.AlleOmlægges
                        },
                        { new RuleKey(LerTypeEnum.Damp), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.EL_LS), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.EL_HS), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.FJV), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Gas), Threshold.KanOmlæggesUnder(91) },
                        { new RuleKey(LerTypeEnum.Luft), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Oil), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand), Threshold.KanOmlæggesUnder(121) },
                        { new RuleKey(LerTypeEnum.UAD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Ignored), Threshold.AlleOmlægges },
                    }
                ),
                // PERT – all dimensions
                new PipeRule(
                    PipeSystemEnum.PertFlextra,
                    null,
                    null,
                    new Dictionary<RuleKey, IThreshold>
                    {
                        { new RuleKey(LerTypeEnum.Afløb, Spatial.ThreeD), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Afløb, Spatial.TwoD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Damp), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.EL_LS), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.EL_HS), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.FJV), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Gas), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Luft), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Oil), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand, Spatial.ThreeD), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand, Spatial.TwoD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.UAD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Ignored), Threshold.AlleOmlægges },
                    }
                ),
                //AquaTherm11 - all dimension
                new PipeRule(
                    PipeSystemEnum.AquaTherm11,
                    null,
                    null,
                    new Dictionary<RuleKey, IThreshold>
                    {
                        { new RuleKey(LerTypeEnum.Afløb, Spatial.ThreeD), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Afløb, Spatial.TwoD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Damp), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.EL_LS), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.EL_HS), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.FJV), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Gas), Threshold.KanOmlæggesUnder(62) },
                        { new RuleKey(LerTypeEnum.Luft), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Oil), Threshold.AlleRespekteres },
                        { new RuleKey(LerTypeEnum.Vand), Threshold.KanOmlæggesUnder(39) },
                        { new RuleKey(LerTypeEnum.UAD), Threshold.AlleOmlægges },
                        { new RuleKey(LerTypeEnum.Ignored), Threshold.AlleOmlægges },
                    }
                ),
            ];

        public static IRelocatabilityService CreateDefaultService() =>
            new RelocatabilityService(DefaultRules);
    }
}
