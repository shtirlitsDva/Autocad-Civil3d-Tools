using System.IO;
using System.Text.Json;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public static class SharedSequenceFileManager
{
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;
    private const int MaxBackups = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static List<SequenceDefinition> Read(string path)
    {
        if (!File.Exists(path))
            return new List<SequenceDefinition>();

        return WithRetry(() =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fs);
            string json = reader.ReadToEnd();
            var list = JsonSerializer.Deserialize<List<SequenceDefinition>>(json, _jsonOptions)
                       ?? new List<SequenceDefinition>();
            foreach (var seq in list)
                seq.StorageLevel = SequenceStorageLevel.Shared;
            return list;
        });
    }

    public static void Write(string path, SequenceDefinition sequence)
    {
        WithRetry(() =>
        {
            var sequences = ReadRaw(path);
            var existing = sequences.FindIndex(s => s.Id == sequence.Id);

            if (existing >= 0)
                sequences[existing] = sequence;
            else
                sequences.Add(sequence);

            WriteAtomic(path, sequences);
        });
    }

    public static void Delete(string path, string sequenceId)
    {
        WithRetry(() =>
        {
            var sequences = ReadRaw(path);
            sequences.RemoveAll(s => s.Id == sequenceId);
            WriteAtomic(path, sequences);
        });
    }

    private static List<SequenceDefinition> ReadRaw(string path)
    {
        if (!File.Exists(path))
            return new List<SequenceDefinition>();

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<SequenceDefinition>>(json, _jsonOptions)
               ?? new List<SequenceDefinition>();
    }

    private static void WriteAtomic(string path, List<SequenceDefinition> sequences)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        RotateBackup(path);

        string tmpPath = path + ".tmp";
        string json = JsonSerializer.Serialize(sequences, _jsonOptions);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, path, overwrite: true);
    }

    private static void RotateBackup(string path)
    {
        if (!File.Exists(path)) return;

        for (int i = MaxBackups; i >= 2; i--)
        {
            string older = $"{path}.bak.{i - 1:D3}";
            string newer = $"{path}.bak.{i:D3}";
            if (File.Exists(older))
            {
                if (File.Exists(newer)) File.Delete(newer);
                File.Move(older, newer);
            }
        }

        string first = $"{path}.bak.001";
        if (File.Exists(first)) File.Delete(first);
        File.Copy(path, first);
    }

    private static T WithRetry<T>(Func<T> action)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
        return action();
    }

    private static void WithRetry(Action action)
    {
        WithRetry<object?>(() => { action(); return null; });
    }
}
