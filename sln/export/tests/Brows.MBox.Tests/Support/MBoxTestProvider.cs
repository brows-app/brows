using Brows.Composition;
using Brows.Panels;
using Brows.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal static class MBoxTestProvider {
    private static IExport[] Exports(MBoxProviderFactory factory) {
        return [
            factory,
            new Brows.Data.MBoxEntryData.Subject(),
            new Brows.Data.MBoxEntryData.From(),
            new Brows.Data.MBoxEntryData.Date(),
            new Brows.Data.MBoxEntryData.To(),
            new Brows.Data.MBoxEntryData.MessageId(),
            new Brows.Data.MBoxEntryData.Labels(),
            new Brows.Data.MBoxEntryData.ThreadId(),
            new Brows.Data.MBoxEntryData.Size(),
        ];
    }

    public static async Task<MBoxProvider> Create(string path, MBoxConfig config = null) {
        var factory = config is null ? new MBoxProviderFactory() : new MBoxProviderFactory(config);
        MBoxProvider provider = null;
        await ImportSandbox.Enter(Exports(factory), async () => {
            provider = (MBoxProvider)await ((IProviderFactory)factory).CreateFor(
                path,
                new TestPanel(),
                CancellationToken.None);
        });
        return provider;
    }

    public static Task Refresh(MBoxProvider provider, CancellationToken token) {
        return ((IProvider)provider).Refresh(token);
    }
}
