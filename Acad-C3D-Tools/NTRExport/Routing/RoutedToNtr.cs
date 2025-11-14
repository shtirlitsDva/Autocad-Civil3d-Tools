using System;
using System.Collections.Generic;

using NTRExport.Enums;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;
using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Routing
{
    internal static class RoutedToNtrExtensions
    {
        public static IEnumerable<string> ToNtr(this RoutedMember member, INtrSoilAdapter soil, ConfigurationData conf) =>
            member switch
            {
                RoutedStraight straight => straight.ToNtrInternal(soil, conf),
                RoutedBend bend => bend.ToNtrInternal(conf),
                RoutedReducer reducer => reducer.ToNtrInternal(conf),
                RoutedTee tee => tee.ToNtrInternal(conf),
                RoutedValve instrument => instrument.ToNtrInternal(conf),
                _ => Array.Empty<string>(),
            };

        private static IEnumerable<string> ToNtrInternal(this RoutedStraight straight, INtrSoilAdapter soil, ConfigurationData conf)
        {
            var soilTokens = NtrFormat.SoilTokens(straight.Soil);
            var soilRef = soil.RefToken(straight.Soil);
            if (!string.IsNullOrWhiteSpace(soilRef)) soilTokens += $" {soilRef}";

            var seriesSuffix = straight.Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };

            yield return
                "RO " +
                $"P1={NtrFormat.Pt(straight.A)} " +
                $"P2={NtrFormat.Pt(straight.B)} " +
                $"DN=DN{straight.DN}.{straight.DnSuffix}{seriesSuffix}" +
                FormatMaterial(straight.Material) +
                LastToken(conf, straight.FlowRole) +
                straight.PipelineToken() +
                soilTokens;
        }

        private static IEnumerable<string> ToNtrInternal(this RoutedBend bend, ConfigurationData conf)
        {
            var seriesSuffix = bend.Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };

            yield return
                "BOG " +
                $"P1={NtrFormat.Pt(bend.A)} " +
                $"P2={NtrFormat.Pt(bend.B)} " +
                $"PT={NtrFormat.Pt(bend.T)} " +
                $"DN=DN{bend.DN}.{bend.DnSuffix}{seriesSuffix}" +
                FormatMaterial(bend.Material) +
                LastToken(conf, bend.FlowRole) +
                bend.PipelineToken() +
                NtrFormat.SoilTokens(null);
        }

        private static IEnumerable<string> ToNtrInternal(this RoutedReducer reducer, ConfigurationData conf)
        {
            var seriesSuffix = reducer.Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };

            yield return
                "RED " +
                $"P1={NtrFormat.Pt(reducer.P1)} " +
                $"P2={NtrFormat.Pt(reducer.P2)} " +
                $"DN1=DN{reducer.Dn1}.{reducer.Dn1Suffix}{seriesSuffix} " +
                $"DN2=DN{reducer.Dn2}.{reducer.Dn2Suffix}{seriesSuffix}" +
                FormatMaterial(reducer.Material) +
                LastToken(conf, reducer.FlowRole) +
                reducer.PipelineToken() +
                NtrFormat.SoilTokens(null);
        }

        private static IEnumerable<string> ToNtrInternal(this RoutedTee tee, ConfigurationData conf)
        {
            var seriesSuffix = tee.Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };

            yield return
                "TEE " +
                $"PH1={NtrFormat.Pt(tee.Ph1)} " +
                $"PH2={NtrFormat.Pt(tee.Ph2)} " +
                $"PA1={NtrFormat.Pt(tee.Pa1)} " +
                $"PA2={NtrFormat.Pt(tee.Pa2)} " +
                $"DNH=DN{tee.DN}.{tee.DnMainSuffix}{seriesSuffix} " +
                $"DNA=DN{tee.DnBranch}.{tee.DnBranchSuffix}{seriesSuffix}" +
                FormatMaterial(tee.Material) +
                LastToken(conf, tee.FlowRole) +
                tee.PipelineToken() +
                NtrFormat.SoilTokens(null);
        }

        private static IEnumerable<string> ToNtrInternal(this RoutedValve instrument, ConfigurationData conf)
        {
            var seriesSuffix = instrument.Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };

            yield return
                "ARM " +
                $"P1={NtrFormat.Pt(instrument.P1)} " +
                $"P2={NtrFormat.Pt(instrument.P2)} " +
                $"PM={NtrFormat.Pt(instrument.Pm)} " +
                $"DN1=DN{instrument.DN}.{instrument.DnSuffix}{seriesSuffix} " +
                $"DN2=DN{instrument.Dn2}.{instrument.Dn2Suffix}{seriesSuffix}" +
                FormatMaterial(instrument.Material) +
                LastToken(conf, instrument.FlowRole) +
                instrument.PipelineToken() +
                NtrFormat.SoilTokens(null);
        }

        private static string PipelineToken(this RoutedMember member) =>
            string.IsNullOrWhiteSpace(member.LTG) ? string.Empty : $" LTG={member.LTG}";

        private static string FormatMaterial(string? material) =>
            material is { Length: > 0 } ? $" MAT={material}" : string.Empty;

        private static string LastToken(ConfigurationData conf, FlowRole flow)
        {
            if (conf == null) return string.Empty;
            var last = flow switch
            {
                FlowRole.Supply => conf.SupplyLast,
                FlowRole.Return => conf.ReturnLast,
                _ => null,
            };

            return last != null ? " " + last.EmitRecord() : string.Empty;
        }
    }
}
