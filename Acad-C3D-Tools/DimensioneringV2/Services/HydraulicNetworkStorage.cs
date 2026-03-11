using Autodesk.AutoCAD.ApplicationServices;

using DimensioneringV2.Models;
using DimensioneringV2.Serialization;
using DimensioneringV2.Serialization.Binary;

using Norsyn.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DimensioneringV2.Services;

internal static class HydraulicNetworkStorage
{
    private const string KeyPrefix = "dimv2:hn:";
    private const string CounterKey = "dimv2:calc_counter";

    // JSON serialization infrastructure kept for .d2r export elsewhere
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            ReferenceHandler = ReferenceHandler.Preserve,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new AnalysisFeatureJsonConverter());
        options.Converters.Add(new UndirectedGraphJsonConverter());
        options.Converters.Add(new DimJsonConverter());
        return options;
    }

    internal static JsonSerializerOptions JsonOptions => s_options;

    internal static void Save(Document doc, HydraulicNetwork hn)
    {
        if (doc == null || hn.Id == null) return;
        using var docLock = doc.LockDocument();
        var dto = HydraulicNetworkMsgDto.FromDomain(hn);
        NorsynStorage.Put(KeyPrefix + hn.Id, dto);
        hn.IsSaved = true;
    }

    internal static HydraulicNetwork? Load(Document doc, string id)
    {
        if (doc == null) return null;
        try
        {
            using var docLock = doc.LockDocument();
            var key = KeyPrefix + id;
            if (!NorsynStorage.Exists(key)) return null;
            var dto = NorsynStorage.Get<HydraulicNetworkMsgDto>(key);
            return dto?.ToDomain();
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error loading HN '{id}': {ex.Message}");
            return null;
        }
    }

    internal static List<string> GetSavedIds(Document doc)
    {
        if (doc == null) return new();
        try
        {
            using var docLock = doc.LockDocument();
            return NorsynStorage.GetKeys()
                .Where(k => k.StartsWith(KeyPrefix))
                .Select(k => k.Substring(KeyPrefix.Length))
                .ToList();
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error listing saved HNs: {ex.Message}");
            return new();
        }
    }

    internal static void Delete(Document doc, string id)
    {
        if (doc == null) return;
        using var docLock = doc.LockDocument();
        NorsynStorage.Remove(KeyPrefix + id);
    }

    internal static void SaveCounter(Document doc, CalcCounter counter)
    {
        if (doc == null) return;
        using var docLock = doc.LockDocument();
        NorsynStorage.Put(CounterKey, counter);
    }

    internal static CalcCounter LoadCounter(Document doc)
    {
        if (doc == null) return new();
        try
        {
            using var docLock = doc.LockDocument();
            if (NorsynStorage.Exists(CounterKey))
                return NorsynStorage.Get<CalcCounter>(CounterKey) ?? new();
            return new();
        }
        catch
        {
            return new();
        }
    }
}
