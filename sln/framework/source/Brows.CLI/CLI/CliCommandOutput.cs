using System;

namespace Brows.CLI;

internal sealed class CliCommandOutput {
    /*
     * CLI command output is output from a CLI program that should
     * be consistent, interpretable, and backwards compatible with
     * and for callers of the CLI program. This means that the out-
     * put is expected to be parsed by another program and not nec-
     * essarily read by a human being.
     * 
     * This behavior is similar to how the git CLI works, where data
     * for humans is sent to stderr, and data for machines is sent
     * to stdout.
     * 
     * We construct this data with and identify this data by a well-
     * known prefix. The prefix is added and removed in a mannger that
     * is invisible to the program and is only used to flag data as
     * informational or suited for machine interpretation.
     */

    private const string Prefix = "!#:";

    public string Data { get; }

    public CliCommandOutput(string data) {
        Data = data;
    }

    public sealed override string ToString() {
        return Prefix + Data;
    }

    public static bool TryGetData(string s, out string data) {
        if (s is null) {
            data = null;
            return false;
        }
        if (s.Length < Prefix.Length) {
            data = null;
            return false;
        }
        if (s.StartsWith(Prefix, StringComparison.Ordinal) == false) {
            data = null;
            return false;
        }
        data = s.Substring(Prefix.Length);
        return true;
    }
}

