using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Collects the drawing-wide facts of the legacy drawing (#319 settings):
/// - Producer: the producer a legacy part's block name ends in
///   (T-TWIN-S3-LOGSTOR, Y-RØR-GLD-ISOPLUS, PRÆRED LOGSTOR, ...). NDH's producer
///   is the STEEL producer, so only steel parts count; an AluPex part naming a
///   producer (H-MODEL-ISOPLUS-ALUPEX) is listed as ignored. GLD and unsuffixed
///   parts name none.
/// - Series: every legacy pipe of an imported pipeline, by system, Twin or the
///   bonded pair, and size, read from the pipe's width as the pipe schedule
///   reads it (PipeScheduleV2.GetPipeSeriesV2).
/// </summary>
internal static class LegacySettingsReader
{
    //NDH producer tokens, as the block names spell them.
    private static readonly (string Word, string Token)[] ProducerWords =
    [
        ("LOGSTOR", "Logstor"),
        ("ISOPLUS", "Isoplus"),
    ];

    private const string SteelSysNavn = "Stål";

    public static void Read(
        IReadOnlyList<LegacyComponent> parts, IReadOnlyDictionary<string, List<Entity>> groups,
        LegacySettingsFacts facts)
    {
        foreach (LegacyComponent part in parts)
        {
            string? token = ProducerIn(part.Navn);
            if (token == null) continue;
            if (part.SysNavn == SteelSysNavn) facts.AddProducer(token, part.Navn);
            else facts.IgnoredProducerParts[part.Navn] =
                facts.IgnoredProducerParts.TryGetValue(part.Navn, out int n) ? n + 1 : 1;
        }

        foreach (Polyline pipe in groups.Values.SelectMany(x => x).OfType<Polyline>())
        {
            PipeTypeEnum type = GetPipeType(pipe);
            if (type is not (PipeTypeEnum.Twin or PipeTypeEnum.Frem or PipeTypeEnum.Retur)) continue;
            PipeSystemEnum system = GetPipeSystem(pipe);
            if (system == PipeSystemEnum.Ukendt) continue;

            facts.AddPipe(
                new SeriesKey(system, type == PipeTypeEnum.Twin, GetPipeDN(pipe)),
                GetPipeSeriesV2(pipe, false),
                pipe.Length);
        }
    }

    private static string? ProducerIn(string navn)
    {
        string[] words = navn.ToUpperInvariant().Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach ((string word, string token) in ProducerWords)
            if (words.Contains(word)) return token;
        return null;
    }
}
