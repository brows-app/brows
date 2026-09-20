using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Brows.Json.ConverterProviders;

internal sealed class StringEnumConverterProvider : JsonConverterProvider {
    protected internal override IEnumerable<JsonConverter> JsonConverters { get; } =
        [new StringEnumConverter()];
}
