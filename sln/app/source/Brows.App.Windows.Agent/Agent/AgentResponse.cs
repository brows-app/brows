using System;
using System.Collections.Generic;

namespace Brows.Agent;

/*
 * The response to an agent's request, written as one line of JSON. Every field is written, null when it does
 * not apply, so that clients can read any field without first checking that it exists.
 */
internal sealed record AgentResponse {
    public bool Ok { get; init; }
    public string Error { get; init; }
    public int Pid { get; init; } = Environment.ProcessId;
    public AgentWindowInfo Window { get; init; }
    public AgentFocus Focus { get; init; }
    public AgentPalette Palette { get; init; }
    public bool? Settled { get; init; }
    public string Ui { get; init; }
    public IReadOnlyList<AgentWindowInfo> Windows { get; init; }
    public string Path { get; init; }
    public bool? Empty { get; init; }
    public IReadOnlyList<AgentLogEntry> Log { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public int? Delivered { get; init; }
    public int? Requested { get; init; }

    internal static AgentResponse Failure(string error) {
        return new AgentResponse { Ok = false, Error = error };
    }
}
