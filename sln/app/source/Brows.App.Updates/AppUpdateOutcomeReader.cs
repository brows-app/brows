using Brows.Updates;
using Domore.Logs;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Brows;

internal sealed class AppUpdateOutcomeReader : IAppUpdateOutcome {
    private static readonly ILog Log = Logging.For(typeof(AppUpdateOutcomeReader));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter<AppUpdateOutcomeKind>(allowIntegerValues: false) }
    };

    internal UpdateInstallation Installation { get => field ??= UpdateInstallation.Current; init; }

    async Task<AppUpdateOutcome> IAppUpdateOutcome.Get(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(Installation.UpdateDirectory, "outcome.json");
        // Claim the record before reading it so another reader cannot consume or delete the same outcome.
        var claimed = $"{path}.{Guid.NewGuid():N}.tmp";
        try {
            File.Move(path, claimed);
        }
        catch (FileNotFoundException) {
            return null;
        }
        catch (DirectoryNotFoundException) {
            return null;
        }

        try {
            using (var stream = File.OpenRead(claimed)) {
                var outcome = await JsonSerializer.DeserializeAsync<AppUpdateOutcome>(stream, Json, token)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(outcome?.Version)) {
                    throw new JsonException("The update outcome has no version.");
                }
                return outcome;
            }
        }
        catch (JsonException ex) {
            if (Log.Warn()) {
                Log.Warn("Discarding an invalid Brows update outcome.", ex);
            }
            return null;
        }
        finally {
            File.Delete(claimed);
        }
    }
}
