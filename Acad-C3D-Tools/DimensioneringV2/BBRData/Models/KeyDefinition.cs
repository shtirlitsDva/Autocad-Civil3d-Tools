using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DimensioneringV2.BBRData.Models
{
    internal enum KeyJoinMode
    {
        DirectConcat,
        SpaceSeparated
    }

    /// <summary>
    /// Abstract base for match keys. Both BBR and CSV keys share
    /// order, join mode, and description display logic.
    /// Matching happens by order: BBR key #1 ↔ CSV key #1, etc.
    /// </summary>
    internal abstract partial class MatchKeyBase : ObservableObject
    {
        [ObservableProperty]
        private int _order;

        [ObservableProperty]
        private KeyJoinMode _joinMode = KeyJoinMode.SpaceSeparated;

        public abstract int PartCount { get; }
        public bool IsCompound => PartCount > 1;
        public bool HasParts => PartCount > 0;

        /// <summary>Human-readable description for display in the UI.</summary>
        public abstract string Description { get; }

        /// <summary>
        /// Call after modifying Parts to refresh bindings.
        /// </summary>
        public void NotifyDescriptionChanged()
        {
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(PartCount));
            OnPropertyChanged(nameof(IsCompound));
            OnPropertyChanged(nameof(HasParts));
        }
    }

    /// <summary>
    /// A match key on the BBR side, composed of one or more BBR property descriptors.
    /// </summary>
    internal class BbrMatchKey : MatchKeyBase
    {
        public List<BbrPropertyDescriptor> Parts { get; } = new();

        public override int PartCount => Parts.Count;

        public override string Description
        {
            get
            {
                if (Parts.Count == 0) return "(not set)";
                var names = Parts.Select(p => p.Name);
                return IsCompound
                    ? $"[{string.Join(" + ", names)}]"
                    : names.First();
            }
        }

        /// <summary>
        /// Compute the key value for a BBR row by extracting and joining part values.
        /// </summary>
        public string ComputeKeyValue(BbrRowData row)
        {
            var values = Parts.Select(p => row.GetDisplayValue(p.Name));
            return JoinMode == KeyJoinMode.SpaceSeparated
                ? string.Join(" ", values)
                : string.Concat(values);
        }

        /// <summary>
        /// Returns the data types of each part in order,
        /// for type-compatibility checks against the paired CSV key.
        /// </summary>
        public IEnumerable<BbrDataType> GetPartTypes() =>
            Parts.Select(p => p.DataType);
    }

    /// <summary>
    /// A match key on the CSV side, composed of one or more CSV column references.
    /// </summary>
    internal class CsvMatchKey : MatchKeyBase
    {
        public List<CsvKeyPart> Parts { get; } = new();

        public override int PartCount => Parts.Count;

        public override string Description
        {
            get
            {
                if (Parts.Count == 0) return "(not set)";
                var names = Parts.Select(p => p.ColumnName);
                return IsCompound
                    ? $"[{string.Join(" + ", names)}]"
                    : names.First();
            }
        }

        /// <summary>
        /// Compute the key value for a CSV row by extracting and joining part values.
        /// </summary>
        public string ComputeKeyValue(CsvRowData row)
        {
            var values = Parts.Select(p => row.GetDisplayValue(p.ColumnName));
            return JoinMode == KeyJoinMode.SpaceSeparated
                ? string.Join(" ", values)
                : string.Concat(values);
        }

        /// <summary>
        /// Returns the data types of each part in order,
        /// for type-compatibility checks against the paired BBR key.
        /// </summary>
        public IEnumerable<BbrDataType> GetPartTypes() =>
            Parts.Select(p => p.DataType);
    }

    /// <summary>
    /// Represents one CSV column reference within a key, with user-assigned type.
    /// </summary>
    internal class CsvKeyPart
    {
        public string ColumnName { get; set; } = string.Empty;
        public BbrDataType DataType { get; set; } = BbrDataType.String;

        public CsvKeyPart() { }

        public CsvKeyPart(string columnName, BbrDataType dataType)
        {
            ColumnName = columnName;
            DataType = dataType;
        }

        public override string ToString() => $"{ColumnName} ({DataType})";
    }
}
