namespace Brows.Agent;

/*
 * Whether a window shows the command palette, whether its input has keyboard focus, and the input's text.
 */
internal sealed record AgentPalette(bool Open, bool Focused, string Text);
