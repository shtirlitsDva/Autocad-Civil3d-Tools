using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using Microsoft.Extensions.DependencyInjection;

namespace IntersectUtilities.LongitudinalProfiles.Relocability
{
    public readonly record struct FjvPipe(PipeSystemEnum System, int NominalDiameter);
    public readonly record struct LerKrydsning(LerTypeEnum Type, int Diameter);

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

    // --------------------- Threshold logic ----------------------
    public abstract record Threshold : IThreshold
    {
        public abstract bool IsRelocatable(int diameter);

        public static readonly IThreshold AlleLedningerRespekteres = new AlleRespekteresThreshold();
        public static IThreshold AlleLedningerOmlægges => new AlleOmlæggesThreshold();
        public static IThreshold KanOmlæggesUnder(int limit) => new KanOmlæggesUnderThreshold(limit);
        

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

    // ------------------------ Rules -----------------------------
    public sealed class PipeRule : IPipeRule
    {
        private readonly ImmutableDictionary<LerTypeEnum, IThreshold> _thresholds;

        public PipeSystemEnum System { get; }
        public int? DnMin { get; }
        public int? DnMax { get; }

        public PipeRule(
            PipeSystemEnum system,
            int? dnMin,
            int? dnMax,
            IDictionary<LerTypeEnum, IThreshold> thresholds)
        {
            System = system;
            DnMin = dnMin;
            DnMax = dnMax;
            _thresholds = thresholds.ToImmutableDictionary();
        }

        public bool AppliesTo(FjvPipe pipe) =>
            pipe.System == System &&
            pipe.NominalDiameter >= (DnMin ?? int.MinValue) &&
            pipe.NominalDiameter <= (DnMax ?? int.MaxValue);

        public bool IsRelocatable(LerKrydsning util) =>
            _thresholds.TryGetValue(util.Type, out var th) && th.IsRelocatable(util.Diameter);
    }

    // -------------------- Service façade ------------------------
    public sealed class RelocatabilityService : IRelocatabilityService
    {
        private readonly ImmutableArray<IPipeRule> _rules;
        public RelocatabilityService(IEnumerable<IPipeRule> rules) =>
            _rules = rules.ToImmutableArray();

        public bool IsRelocatable(FjvPipe pipe, LerKrydsning util)
        {
            var rule = _rules.FirstOrDefault(r => r.AppliesTo(pipe))
                       ?? throw new InvalidOperationException(
                            $"No rule for {pipe.System} DN{pipe.NominalDiameter}");
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
                PipeSystemEnum.Stål, 32, 50,
                new Dictionary<LerTypeEnum, IThreshold>
                {
                    { LerTypeEnum.Afløb3D,       Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.Afløb2D,       Threshold.AlleLedningerOmlægges },
                    { LerTypeEnum.Gas,           Threshold.KanOmlæggesUnder(41) },
                    { LerTypeEnum.Vand,          Threshold.KanOmlæggesUnder(41) },
                    { LerTypeEnum.ElHøjSpænding, Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.ElLavSpænding, Threshold.AlleLedningerOmlægges }
                }),

            // Twin steel DN65‒DN80
            new PipeRule(
                PipeSystemEnum.Stål, 65, 80,
                new Dictionary<LerTypeEnum, IThreshold>
                {
                    { LerTypeEnum.Afløb3D,       Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.Afløb2D,       Threshold.AlleLedningerOmlægges },
                    { LerTypeEnum.Gas,           Threshold.KanOmlæggesUnder(64) },
                    { LerTypeEnum.Vand,          Threshold.KanOmlæggesUnder(64) },
                    { LerTypeEnum.ElHøjSpænding, Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.ElLavSpænding, Threshold.AlleLedningerOmlægges }
                }),

            // Twin steel DN100‒DN250
            new PipeRule(
                PipeSystemEnum.Stål, 100, 250,
                new Dictionary<LerTypeEnum, IThreshold>
                {
                    { LerTypeEnum.Afløb3D,       Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.Afløb2D,       Threshold.AlleLedningerOmlægges },
                    { LerTypeEnum.Gas,           Threshold.KanOmlæggesUnder(91) },
                    { LerTypeEnum.Vand,          Threshold.KanOmlæggesUnder(121) },
                    { LerTypeEnum.ElHøjSpænding, Threshold.AlleLedningerRespekteres },
                    { LerTypeEnum.ElLavSpænding, Threshold.AlleLedningerOmlægges }
                }),

            // PERT – all dimensions
            new PipeRule(
                PipeSystemEnum.PertFlextra, null, null,
                Enum.GetValues<LerTypeEnum>()
                    .ToDictionary(t => t, _ => Threshold.AlleLedningerRespekteres))
        ];

        public static IRelocatabilityService CreateDefaultService() =>
            new RelocatabilityService(DefaultRules);
    }

    // -------------- DI extension (optional) ---------------------
    public static class RelocatabilityExtensions
    {
        public static IServiceCollection AddRelocatability(this IServiceCollection services)
        {
            services.AddSingleton<IEnumerable<IPipeRule>>(_ => RuleFactory.DefaultRules);
            services.AddSingleton<IRelocatabilityService, RelocatabilityService>();
            return services;
        }
    }
}