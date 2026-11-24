namespace Brows.Agent;

/*
 * Describes a commander window. The id is the window's handle, which does not change while the window is open.
 */
internal sealed record AgentWindowInfo(long Id, int Index, string Title, bool Active, int Width, int Height);
