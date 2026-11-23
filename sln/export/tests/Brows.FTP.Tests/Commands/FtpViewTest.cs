using Brows.Commands;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Reflection;

namespace Brows.Commands;

[TestFixture]
internal sealed class FtpViewTest {
    [TestCase("Brows.FTP", "FtpProviderFactory", "FtpProvider", "ftp://example.test/root/")]
    [TestCase("Brows.FTPS", "FtpsProviderFactory", "FtpsProvider", "ftps://example.test/root/")]
    public void View_TriggeredWork_TogglesPreviewWithoutPaletteParameters(string assemblyName,
                                                                          string factoryName,
                                                                          string providerName,
                                                                          string id) {
        using var provider = CreateProvider(assemblyName, factoryName, providerName, id);

        Assert.That(Work(assemblyName, provider, parameter: null, input: "view"), Is.True);
        Assert.That(HasPreview(provider), Is.True);
        Assert.That(Work(assemblyName, provider, parameter: null, input: "view"), Is.True);
        Assert.That(HasPreview(provider), Is.False);
    }

    [TestCase("Brows.FTP", "FtpProviderFactory", "FtpProvider", "ftp://example.test/root/")]
    [TestCase("Brows.FTPS", "FtpsProviderFactory", "FtpsProvider", "ftps://example.test/root/")]
    public void View_TriggeredWork_TogglesPreviewWithAltPConfiguredParameter(string assemblyName,
                                                                             string factoryName,
                                                                             string providerName,
                                                                             string id) {
        using var provider = CreateProvider(assemblyName, factoryName, providerName, id);

        Assert.That(Work(assemblyName, provider, parameter: "toggle=preview", input: "alt+p"), Is.True);
        Assert.That(HasPreview(provider), Is.True);
        Assert.That(Work(assemblyName, provider, parameter: "toggle=preview", input: "alt+p"), Is.True);
        Assert.That(HasPreview(provider), Is.False);
    }

    private static ICommand CreateCommand(string assemblyName) {
        var assembly = Assembly.Load(assemblyName);
        var commandType = assembly.GetType("Brows.Commands.View", throwOnError: true);
        var command = (ICommand)Activator.CreateInstance(commandType, nonPublic: true);
        var framework = typeof(ICommand).Assembly;
        var cliProviderType = framework.GetType("Brows.Commands.CommandCliProvider", throwOnError: true);
        var cliProvider = Activator.CreateInstance(cliProviderType, nonPublic: true);
        var baseCommandType = framework.GetType("Brows.Commands.Command", throwOnError: true);
        var property = baseCommandType.GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        property.SetValue(command, cliProvider);
        return command;
    }

    private static Provider CreateProvider(string assemblyName,
                                           string factoryName,
                                           string providerName,
                                           string id) {
        var assembly = Assembly.Load(assemblyName);
        var factoryType = assembly.GetType($"Brows.{factoryName}", throwOnError: true);
        var factory = Activator.CreateInstance(factoryType, nonPublic: true);
        var providerType = assembly.GetType($"Brows.{providerName}", throwOnError: true);
        var provider = Activator.CreateInstance(providerType,
                                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                binder: null,
                                                args: [factory, new Uri(id), null],
                                                culture: null);
        return (Provider)provider;
    }

    private static bool HasPreview(Provider provider) {
        return provider.Detail.Active is not null;
    }

    private static bool Work(string assemblyName, Provider provider, string parameter, string input) {
        var command = CreateCommand(assemblyName);
        var context = new TestCommandContext {
            Panels = { Panels = { new TestPanel { Column = 0, Provider = provider } } },
            Source = new TestCommandSource { Items = { ((IEntryProvider)provider).Observation } },
            Line = new TestCommandLine {
                Input = input,
                Command = ViewCommandName(assemblyName),
                Trigger = input == "alt+p" ? "ALT+P" : null,
                Parameter = parameter
            }
        };
        return command.TriggeredWork(context);
    }

    private static string ViewCommandName(string assemblyName) {
        return assemblyName == "Brows.FTPS" ? "Ftps_View" : "Ftp_View";
    }
}
