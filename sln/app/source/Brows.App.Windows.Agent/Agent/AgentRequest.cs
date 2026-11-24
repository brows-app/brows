namespace Brows.Agent;

/*
 * A request from an agent, read as one line of JSON. See the agent-driving plan for the meaning of each field.
 */
internal sealed record AgentRequest {
    public string Op { get; init; }
    public long? Window { get; init; }
    public string Text { get; init; }
    public string Keys { get; init; }
    public string Expect { get; init; }
    public bool RequirePalette { get; init; }
    public bool Absent { get; init; }
    public bool? Settle { get; init; }
    public int? Quiet { get; init; }
    public int? Timeout { get; init; }
    public string Path { get; init; }
    public int? MaxLines { get; init; }
    public long Since { get; init; }
}
