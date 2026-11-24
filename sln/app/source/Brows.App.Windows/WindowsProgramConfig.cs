namespace Brows; 
public sealed class WindowsProgramConfig {
    public SplashConfig Splash {
        get => _Splash ??= new();
        set => _Splash = value;
    }
    private SplashConfig _Splash;

    /// <summary>
    /// Gets or sets the configuration of the agent bridge, which lets an agent drive the application.
    /// </summary>
    public AgentConfig Agent {
        get => field ??= new();
        set;
    }

    public sealed class SplashConfig {
        public bool Show { get; set; } = true;
    }

    /// <summary>
    /// Configures the agent bridge, set from the command line as <c>agent.pipe=&lt;name&gt;</c>.
    /// </summary>
    public sealed class AgentConfig {
        /// <summary>
        /// Gets or sets the name of the named pipe the bridge listens on. The bridge runs only in Debug
        /// builds, and only when a name is set.
        /// </summary>
        public string Pipe { get; set; }
    }
}
