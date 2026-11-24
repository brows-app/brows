namespace Brows.Agent;

/*
 * The element with keyboard focus in a window: its type, its name, and the type of the Brows view that holds it.
 */
internal sealed record AgentFocus(string Type, string Name, string Owner);
