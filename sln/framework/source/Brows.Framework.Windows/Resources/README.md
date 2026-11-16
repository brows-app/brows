# WPF resources

`Resources.xaml` loads the shared colors, fonts, and control styles. The application
loads it once, before the export resource dictionaries. Most WPF controls use implicit
styles. Numbered styles (`Brows.Style.TextBlock1` through `TextBlock5`, for example)
provide larger text and compact text respectively; semantic variants provide info,
success, warning, and danger treatments. Feature dictionaries supply data templates
and behavior or layout overrides based on the shared styles.

Color themes and the shared font scale are independent. `Themes/Light.FileExplorer.xaml`
and `Themes/Dark.FileExplorer.xaml` define the File Explorer palettes. They merge
`Base.FileExplorer.xaml` for theme metrics and `Base.xaml` for shared constant brushes.
Each palette defines frozen brushes using static references to its own colors.
Controls use dynamic references to these brushes so changing the theme replaces
the brushes on existing controls. Shared constant brushes, geometries, transforms,
and animation definitions are frozen where possible. Control-specific font resources
remain in `Fonts.xaml`; theme dictionaries provide component-specific brushes, including
for the command palette configuration editor and suggestion collection.

`Fonts.xaml` defines the font scale. Normal text and data use Consolas at 12 device
independent units. Buttons, labels, and combo boxes use Segoe UI. Shared font resources
can be overridden without modifying the color palette. Theme changes replace only the
theme dictionary and retain shared font resources, styles, and export dictionaries.

Use `theme FileExplorer light` or `theme FileExplorer dark` in the command palette.
The theme name and mode are saved in the commander theme configuration. The previous
`Base`, `BG`, and `FG` settings are superseded by `Name` and `Mode`; an existing
configuration without those settings starts with FileExplorer dark.
Invalid saved theme settings fall back to FileExplorer dark with a warning, while
export resources and the theme command remain available. Invalid interactive theme
requests report an error and leave the current theme unchanged.

To add another color theme, provide matching `Light.<Name>.xaml` and
`Dark.<Name>.xaml` dictionaries, then extend the supported-name validation in
`Brows.App.Windows/Windows/AppTheme.cs` and the command help. Keep resource keys
consistent between modes, and use dynamic brush references in views so existing
controls update when the palette changes.

Resources supplied by a theme dictionary use `Brows.Theme.*` keys (brushes, corner radii,
and similar values), with colors under `Brows.Theme.Color.*`. Shared styles, templates, and
font resources use `Brows.Style.*` keys, so a key's prefix shows whether a theme provides it.
Framework control styles live in
`Resources/Styles`, while shell styles and export-specific styles stay in their
own projects. Export styles use feature prefixes such as `Brows.Style.Git.*`,
`Brows.Style.Find.*`, and `Brows.Style.Bookmark.*`.

The operator control style belongs to Brows.Commander.Windows, which references
Brows.Operations.Windows. Brows.Framework.Windows supplies its shared style and
font resources without depending on the operations control package.

The old framework `Resource/Resource.xaml` entry point forwards to styles only.
It must not import Fonts.xaml again: doing so would override independently merged
font schemes. Specialized font sizes and weights are resources in Fonts.xaml,
including the operator headers, palette input, suggestions, and headings.

Localized footer and Find-result styles remain in culture dictionaries. Template
discovery keys such as `PanelProvider_*` and `CommandContextData_*` are unchanged.
