using MessagePack;
using MessagePack.Formatters;

namespace NorsynHydraulicCalc.Rules
{
    /// <summary>
    /// MessagePack formatter for polymorphic IPipeRule serialization.
    /// Uses a type-tag byte to discriminate between concrete implementations.
    /// Tag 0 = ParentPipeRule.
    /// </summary>
    internal class PipeRuleFormatter : IMessagePackFormatter<IPipeRule>
    {
        private const byte TagParentPipeRule = 0;

        public void Serialize(
            ref MessagePackWriter writer,
            IPipeRule value,
            MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            switch (value)
            {
                case ParentPipeRule ppr:
                    writer.WriteArrayHeader(2);
                    writer.Write(TagParentPipeRule);
                    writer.Write((int)ppr.ParentPipeType);
                    break;
                default:
                    throw new MessagePackSerializationException(
                        $"Unknown IPipeRule implementation: {value.GetType().FullName}");
            }
        }

        public IPipeRule Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            var count = reader.ReadArrayHeader();
            if (count < 1)
                throw new MessagePackSerializationException("IPipeRule array must have at least 1 element (tag).");

            var tag = reader.ReadByte();

            switch (tag)
            {
                case TagParentPipeRule:
                    var pipeType = (PipeType)reader.ReadInt32();
                    return new ParentPipeRule(pipeType);
                default:
                    throw new MessagePackSerializationException(
                        $"Unknown IPipeRule type tag: {tag}");
            }
        }
    }
}
