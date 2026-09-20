using Newtonsoft.Json;
using Brows.Composition;
using System;

namespace Brows.Json;

public interface IJsonSerializerSettingsFactory : IExport {
    JsonSerializerSettings Create(Action<JsonSerializerSettings> callback = null, IExportContext context = null);
}
