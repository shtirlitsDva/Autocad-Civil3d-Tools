using System.IO;
using System.Text.Json;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public class SequenceStorageService
{
    private static readonly Lazy<SequenceStorageService> _instance = new(() => new SequenceStorageService());
    public static SequenceStorageService Instance => _instance.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string UserSequencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Autodesk", "ApplicationPlugins", "IntersectUtilities",
        "bpv2_user_sequences.json");

    private static readonly string SharedSequencesPath =
        @"X:\AutoCAD DRI - 01 Civil 3D\BPv2\shared_sequences.json";

    public bool IsSharedStorageAvailable { get; }

    private SequenceStorageService()
    {
        string? dir = Path.GetDirectoryName(SharedSequencesPath);
        IsSharedStorageAvailable = !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
    }

    public List<SequenceDefinition> LoadAll()
    {
        var all = new List<SequenceDefinition>();

        foreach (var seq in PredefinedSequences.GetAll())
            all.Add(seq);

        foreach (var seq in LoadUserSequences())
            all.Add(seq);

        if (IsSharedStorageAvailable)
        {
            foreach (var seq in LoadSharedSequences())
                all.Add(seq);
        }

        return all;
    }

    public List<SequenceDefinition> LoadUserSequences()
    {
        return LoadFromFile(UserSequencesPath, SequenceStorageLevel.User);
    }

    public List<SequenceDefinition> LoadSharedSequences()
    {
        if (!IsSharedStorageAvailable)
            return new List<SequenceDefinition>();

        return SharedSequenceFileManager.Read(SharedSequencesPath);
    }

    public void SaveUserSequence(SequenceDefinition sequence)
    {
        sequence.StorageLevel = SequenceStorageLevel.User;
        sequence.ModifiedAt = DateTime.UtcNow;

        var sequences = LoadFromFile(UserSequencesPath, SequenceStorageLevel.User);
        var existing = sequences.FindIndex(s => s.Id == sequence.Id);

        if (existing >= 0)
            sequences[existing] = sequence;
        else
            sequences.Add(sequence);

        SaveToFile(UserSequencesPath, sequences);
    }

    public void DeleteUserSequence(string sequenceId)
    {
        var sequences = LoadFromFile(UserSequencesPath, SequenceStorageLevel.User);
        sequences.RemoveAll(s => s.Id == sequenceId);
        SaveToFile(UserSequencesPath, sequences);
    }

    public void SaveSharedSequence(SequenceDefinition sequence)
    {
        if (!IsSharedStorageAvailable) return;

        sequence.StorageLevel = SequenceStorageLevel.Shared;
        sequence.ModifiedAt = DateTime.UtcNow;
        SharedSequenceFileManager.Write(SharedSequencesPath, sequence);
    }

    public void DeleteSharedSequence(string sequenceId)
    {
        if (!IsSharedStorageAvailable) return;

        SharedSequenceFileManager.Delete(SharedSequencesPath, sequenceId);
    }

    private static List<SequenceDefinition> LoadFromFile(string path, SequenceStorageLevel level)
    {
        if (!File.Exists(path))
            return new List<SequenceDefinition>();

        try
        {
            string json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<SequenceDefinition>>(json, _jsonOptions)
                       ?? new List<SequenceDefinition>();
            foreach (var seq in list)
                seq.StorageLevel = level;
            return list;
        }
        catch
        {
            return new List<SequenceDefinition>();
        }
    }

    private static void SaveToFile(string path, List<SequenceDefinition> sequences)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(sequences, _jsonOptions);
        File.WriteAllText(path, json);
    }
}
