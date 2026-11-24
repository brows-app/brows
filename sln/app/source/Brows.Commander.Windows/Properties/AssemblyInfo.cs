using System.Runtime.InteropServices;
using System.Windows.Markup;

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("Brows.App.Windows")]
[assembly: InternalsVisibleTo("Brows.App.Windows.Agent")]
[assembly: InternalsVisibleTo("Brows.App.Windows.Agent.Tests")]
[assembly: InternalsVisibleTo("Brows.App.Windows.Tests")]

[assembly: XmlnsDefinition("http://schemas.brows.app/winfx/2026/xaml/presentation", "Brows")]
[assembly: XmlnsPrefix("http://schemas.brows.app/winfx/2026/xaml/presentation", "brows")]
