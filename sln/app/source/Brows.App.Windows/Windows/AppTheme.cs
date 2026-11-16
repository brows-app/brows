using Brows.Gui;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Brows.Windows;

internal sealed class AppTheme {
    private readonly ResourceDictionary AppResources;
    private readonly ResourceDictionary ThemeResources = new();

    private bool ExportsLoaded { get; set; }

    private static ResourceDictionary Load(string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/Brows.Framework.Windows;component/Resources/{name}.xaml")
        };
    }

    internal AppTheme(ResourceDictionary appResources) {
        AppResources = appResources ?? throw new ArgumentNullException(nameof(appResources));
        ThemeResources.MergedDictionaries.Add(Load("Themes/Dark.FileExplorer"));
        AppResources.MergedDictionaries.Add(ThemeResources);
        AppResources.MergedDictionaries.Add(Load("Resources"));
    }

    internal void LoadExportResources(IEnumerable<ResourceDictionary> resourceDictionaries) {
        ArgumentNullException.ThrowIfNull(resourceDictionaries);
        if (ExportsLoaded) {
            return;
        }
        foreach (var dictionary in resourceDictionaries) {
            AppResources.MergedDictionaries.Add(dictionary);
        }
        ExportsLoaded = true;
    }

    internal void Apply(CommanderTheme theme) {
        if (theme is null) {
            throw new ArgumentNullException(nameof(theme));
        }
        var nameIsValid = string.Equals(theme.Name?.Trim(), "FileExplorer", StringComparison.OrdinalIgnoreCase);
        if (!nameIsValid) {
            throw new ArgumentException($"Unknown theme: {theme.Name}", nameof(theme));
        }
        if (!Enum.IsDefined(theme.Mode)) {
            throw new ArgumentOutOfRangeException(nameof(theme), theme.Mode, "Unknown theme mode.");
        }
        var resources = Load($"Themes/{theme.Mode}.FileExplorer");
        ThemeResources.MergedDictionaries[0] = resources;
    }

    private sealed class Service : ICommanderTheme {
        public void Apply(CommanderTheme theme) {
            var app = (WindowsApplication)Application.Current;
            app.Dispatcher.Invoke(() => app.Theme.Apply(theme));
        }
    }
}
