using Autodesk.AutoCAD.ApplicationServices;

using Dreambuild.AutoCAD;

using DimensioneringV2.Models;
using DimensioneringV2.Serialization;

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

    internal static void Save(Document doc, HydraulicNetwork hn)
    {
        if (doc == null || hn.Id == null) return;
        using var docLock = doc.LockDocument();
        var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
        var dto = new HydraulicNetworkDto(hn);
        store.SetObject(KeyPrefix + hn.Id, dto, s_options);
        hn.IsSaved = true;
    }

    internal static HydraulicNetwork? Load(Document doc, string id)
    {
        if (doc == null) return null;
        try
        {
            using var docLock = doc.LockDocument();
            var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
            var key = KeyPrefix + id;
            if (!store.Has(key)) return null;
            var dto = store.GetObject<HydraulicNetworkDto>(key, s_options);
            return dto?.ToHydraulicNetwork();
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
            var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
            return store.GetKeys(KeyPrefix)
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
        var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
        store.RemoveEntry(KeyPrefix + id);
    }

    internal static void SaveCounter(Document doc, CalcCounter counter)
    {
        if (doc == null) return;
        using var docLock = doc.LockDocument();
        var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
        store.SetObject(CounterKey, counter);
    }

    internal static CalcCounter LoadCounter(Document doc)
    {
        if (doc == null) return new();
        try
        {
            using var docLock = doc.LockDocument();
            var store = FlexDataStoreExtensions.FlexDataStore(doc.Database);
            if (store.Has(CounterKey))
                return store.GetObject<CalcCounter>(CounterKey) ?? new();
            return new();
        }
        catch
        {
            return new();
        }
    }
}
