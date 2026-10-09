using System;
using System.Collections.Generic;

using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;

namespace LERImporter.Host.Brx;

// The two DwgFilers LERImporter hands BricsCAD's AEC objects. Both say they are file filers:
// MEASURED (V26.2, 2026-10-10) a copy filer's stream carries raw memory addresses
// (WriteAddress) and cannot be replayed, while a file filer's carries only values and ids.
// A filer must never throw into the host, so a call the stream does not hold is noted and
// answered with a neutral value; the caller reads Error afterwards.

/// <summary>Records what an object files: its DwgOut as tokens, its ids as slots.</summary>
internal sealed class RecordingFiler : DwgFiler
{
    private readonly List<AecToken> _tokens = new();
    private readonly List<ObjectId> _ids = new();
    private ErrorStatus _status = ErrorStatus.OK;

    public IReadOnlyList<AecToken> Tokens => _tokens;
    public IReadOnlyList<ObjectId> Ids => _ids;

    /// <summary>The first call the token stream has no kind for, or "" when there was none.</summary>
    public string Error { get; private set; } = "";

    public override long Position => _tokens.Count;
    public override FilerType FilerType => FilerType.FileFiler;
    public override ErrorStatus FilerStatus { get => _status; set => _status = value; }
    public override void ResetFilerStatus() => _status = ErrorStatus.OK;
    public override void Seek(long offset, int method) { }

    private void Unexpected(string call)
    {
        if (Error.Length == 0) Error = $"{call} at token {_tokens.Count}";
    }

    private int Slot(ObjectId id)
    {
        _ids.Add(id);
        return _ids.Count - 1;
    }

    public override void WriteBoolean(bool value) => _tokens.Add(AecToken.Bool(value));
    public override void WriteByte(byte value) => _tokens.Add(AecToken.Byte(value));
    public override void WriteInt16(short value) => _tokens.Add(AecToken.Int16(value));
    public override void WriteInt32(int value) => _tokens.Add(AecToken.Int32(value));
    public override void WriteDouble(double value) => _tokens.Add(AecToken.Double(value));
    public override void WriteString(string value) => _tokens.Add(AecToken.String(value ?? ""));
    public override void WriteSoftPointerId(ObjectId value) => _tokens.Add(AecToken.Soft(Slot(value)));
    public override void WriteHardPointerId(ObjectId value) => _tokens.Add(AecToken.Hard(Slot(value)));

    public override void WriteAddress(IntPtr value) => Unexpected("WriteAddress");
    public override void WriteBinaryChunk(byte[] chunk) => Unexpected("WriteBinaryChunk");
    public override void WriteBytes(byte[] value) => Unexpected("WriteBytes");
    public override void WriteHandle(Handle handle) => Unexpected("WriteHandle");
    public override void WriteHardOwnershipId(ObjectId value) => Unexpected("WriteHardOwnershipId");
    public override void WriteSoftOwnershipId(ObjectId value) => Unexpected("WriteSoftOwnershipId");
    public override void WriteInt64(long value) => Unexpected("WriteInt64");
    public override void WriteUInt16(ushort value) => Unexpected("WriteUInt16");
    public override void WriteUInt32(uint value) => Unexpected("WriteUInt32");
    public override void WriteUInt64(ulong value) => Unexpected("WriteUInt64");
    public override void WritePoint2d(Point2d value) => Unexpected("WritePoint2d");
    public override void WritePoint3d(Point3d value) => Unexpected("WritePoint3d");
    public override void WriteScale3d(Scale3d value) => Unexpected("WriteScale3d");
    public override void WriteVector2d(Vector2d value) => Unexpected("WriteVector2d");
    public override void WriteVector3d(Vector3d value) => Unexpected("WriteVector3d");

    public override IntPtr ReadAddress() { Unexpected("ReadAddress"); return IntPtr.Zero; }
    public override byte[] ReadBinaryChunk() { Unexpected("ReadBinaryChunk"); return Array.Empty<byte>(); }
    public override bool ReadBoolean() { Unexpected("ReadBoolean"); return false; }
    public override byte ReadByte() { Unexpected("ReadByte"); return 0; }
    public override void ReadBytes(byte[] value) => Unexpected("ReadBytes");
    public override double ReadDouble() { Unexpected("ReadDouble"); return 0; }
    public override Handle ReadHandle() { Unexpected("ReadHandle"); return new Handle(0); }
    public override ObjectId ReadHardOwnershipId() { Unexpected("ReadHardOwnershipId"); return ObjectId.Null; }
    public override ObjectId ReadHardPointerId() { Unexpected("ReadHardPointerId"); return ObjectId.Null; }
    public override short ReadInt16() { Unexpected("ReadInt16"); return 0; }
    public override int ReadInt32() { Unexpected("ReadInt32"); return 0; }
    public override long ReadInt64() { Unexpected("ReadInt64"); return 0; }
    public override Point2d ReadPoint2d() { Unexpected("ReadPoint2d"); return Point2d.Origin; }
    public override Point3d ReadPoint3d() { Unexpected("ReadPoint3d"); return Point3d.Origin; }
    public override Scale3d ReadScale3d() { Unexpected("ReadScale3d"); return new Scale3d(1); }
    public override ObjectId ReadSoftOwnershipId() { Unexpected("ReadSoftOwnershipId"); return ObjectId.Null; }
    public override ObjectId ReadSoftPointerId() { Unexpected("ReadSoftPointerId"); return ObjectId.Null; }
    public override string ReadString() { Unexpected("ReadString"); return ""; }
    public override ushort ReadUInt16() { Unexpected("ReadUInt16"); return 0; }
    public override uint ReadUInt32() { Unexpected("ReadUInt32"); return 0; }
    public override ulong ReadUInt64() { Unexpected("ReadUInt64"); return 0; }
    public override Vector2d ReadVector2d() { Unexpected("ReadVector2d"); return new Vector2d(); }
    public override Vector3d ReadVector3d() { Unexpected("ReadVector3d"); return new Vector3d(); }
}

/// <summary>
/// Hands an object a token stream through DwgIn. Every read must find the token kind it
/// asks for; the first that does not is the Error, and the rest of the reads get neutral
/// values. Pointer tokens resolve through the id table.
/// </summary>
internal sealed class ReplayFiler : DwgFiler
{
    private readonly IReadOnlyList<AecToken> _tokens;
    private readonly IReadOnlyList<ObjectId> _ids;
    private int _next;
    private ErrorStatus _status = ErrorStatus.OK;

    public ReplayFiler(IReadOnlyList<AecToken> tokens, IReadOnlyList<ObjectId> ids)
    {
        _tokens = tokens;
        _ids = ids;
    }

    /// <summary>The first read the stream could not answer, or "" when there was none.</summary>
    public string Error { get; private set; } = "";
    public int Consumed => _next;

    public override long Position => _next;
    public override FilerType FilerType => FilerType.FileFiler;
    public override ErrorStatus FilerStatus { get => _status; set => _status = value; }
    public override void ResetFilerStatus() => _status = ErrorStatus.OK;
    public override void Seek(long offset, int method) { }

    private Option<AecToken> Take(AecTokenKind kind)
    {
        if (Error.Length > 0) return Option<AecToken>.Nothing;
        if (_next >= _tokens.Count)
        {
            Error = $"read {kind} past the end ({_tokens.Count} tokens)";
            return Option<AecToken>.Nothing;
        }
        AecToken token = _tokens[_next++];
        if (token.Kind == kind) return Option<AecToken>.Of(token);
        Error = $"read {kind} at token {_next - 1}, which is {token}";
        return Option<AecToken>.Nothing;
    }

    private ObjectId Id(AecTokenKind kind) => Take(kind).Match(
        token =>
        {
            if (token.Integer >= 0 && token.Integer < _ids.Count) return _ids[(int)token.Integer];
            Error = $"slot {token.Integer} at token {_next - 1} is not in the id table";
            return ObjectId.Null;
        },
        () => ObjectId.Null);

    private void Unexpected(string call)
    {
        if (Error.Length == 0) Error = $"{call} at token {_next}";
    }

    public override bool ReadBoolean() => Take(AecTokenKind.Bool).Match(t => t.Integer != 0, () => false);
    public override byte ReadByte() => Take(AecTokenKind.Byte).Match(t => (byte)t.Integer, () => (byte)0);
    public override short ReadInt16() => Take(AecTokenKind.Int16).Match(t => (short)t.Integer, () => (short)0);
    public override int ReadInt32() => Take(AecTokenKind.Int32).Match(t => (int)t.Integer, () => 0);
    public override double ReadDouble() => Take(AecTokenKind.Double).Match(t => t.Real, () => 0.0);
    public override string ReadString() => Take(AecTokenKind.String).Match(t => t.Text, () => "");
    public override ObjectId ReadSoftPointerId() => Id(AecTokenKind.SoftPointer);
    public override ObjectId ReadHardPointerId() => Id(AecTokenKind.HardPointer);

    public override IntPtr ReadAddress() { Unexpected("ReadAddress"); return IntPtr.Zero; }
    public override byte[] ReadBinaryChunk() { Unexpected("ReadBinaryChunk"); return Array.Empty<byte>(); }
    public override void ReadBytes(byte[] value) => Unexpected("ReadBytes");
    public override Handle ReadHandle() { Unexpected("ReadHandle"); return new Handle(0); }
    public override ObjectId ReadHardOwnershipId() { Unexpected("ReadHardOwnershipId"); return ObjectId.Null; }
    public override ObjectId ReadSoftOwnershipId() { Unexpected("ReadSoftOwnershipId"); return ObjectId.Null; }
    public override long ReadInt64() { Unexpected("ReadInt64"); return 0; }
    public override ushort ReadUInt16() { Unexpected("ReadUInt16"); return 0; }
    public override uint ReadUInt32() { Unexpected("ReadUInt32"); return 0; }
    public override ulong ReadUInt64() { Unexpected("ReadUInt64"); return 0; }
    public override Point2d ReadPoint2d() { Unexpected("ReadPoint2d"); return Point2d.Origin; }
    public override Point3d ReadPoint3d() { Unexpected("ReadPoint3d"); return Point3d.Origin; }
    public override Scale3d ReadScale3d() { Unexpected("ReadScale3d"); return new Scale3d(1); }
    public override Vector2d ReadVector2d() { Unexpected("ReadVector2d"); return new Vector2d(); }
    public override Vector3d ReadVector3d() { Unexpected("ReadVector3d"); return new Vector3d(); }

    // DwgIn only reads; a write here would be the host filing back into a reading filer.
    public override void WriteAddress(IntPtr value) => Unexpected("WriteAddress");
    public override void WriteBinaryChunk(byte[] chunk) => Unexpected("WriteBinaryChunk");
    public override void WriteBoolean(bool value) => Unexpected("WriteBoolean");
    public override void WriteByte(byte value) => Unexpected("WriteByte");
    public override void WriteBytes(byte[] value) => Unexpected("WriteBytes");
    public override void WriteDouble(double value) => Unexpected("WriteDouble");
    public override void WriteHandle(Handle handle) => Unexpected("WriteHandle");
    public override void WriteHardOwnershipId(ObjectId value) => Unexpected("WriteHardOwnershipId");
    public override void WriteHardPointerId(ObjectId value) => Unexpected("WriteHardPointerId");
    public override void WriteInt16(short value) => Unexpected("WriteInt16");
    public override void WriteInt32(int value) => Unexpected("WriteInt32");
    public override void WriteInt64(long value) => Unexpected("WriteInt64");
    public override void WritePoint2d(Point2d value) => Unexpected("WritePoint2d");
    public override void WritePoint3d(Point3d value) => Unexpected("WritePoint3d");
    public override void WriteScale3d(Scale3d value) => Unexpected("WriteScale3d");
    public override void WriteSoftOwnershipId(ObjectId value) => Unexpected("WriteSoftOwnershipId");
    public override void WriteSoftPointerId(ObjectId value) => Unexpected("WriteSoftPointerId");
    public override void WriteString(string value) => Unexpected("WriteString");
    public override void WriteUInt16(ushort value) => Unexpected("WriteUInt16");
    public override void WriteUInt32(uint value) => Unexpected("WriteUInt32");
    public override void WriteUInt64(ulong value) => Unexpected("WriteUInt64");
    public override void WriteVector2d(Vector2d value) => Unexpected("WriteVector2d");
    public override void WriteVector3d(Vector3d value) => Unexpected("WriteVector3d");
}
