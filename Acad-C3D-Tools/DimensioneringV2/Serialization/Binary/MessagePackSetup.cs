using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

using Norsyn.Storage;

using NorsynHydraulicCalc.Rules;

namespace DimensioneringV2.Serialization.Binary;

internal static class MessagePackSetup
{
    internal static void Configure()
    {
        var resolver = CompositeResolver.Create(
            new IMessagePackFormatter[] { new PipeRuleFormatter() },
            new IFormatterResolver[] { StandardResolver.Instance }
        );

        NorsynStorage.Configure(
            MessagePackSerializerOptions.Standard
                .WithResolver(resolver)
                .WithCompression(MessagePackCompression.Lz4BlockArray));
    }
}
