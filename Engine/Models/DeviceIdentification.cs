using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KillerScan.Engine;

public sealed record RecognitionEvidence
{
    public string Source { get; init; } = "";
    public string Value { get; init; } = "";
    public string Origin { get; init; } = "";
    public DateTimeOffset? ObservedAtUtc { get; init; }
    public bool Reused { get; init; }
}

public sealed record RecognitionRule(string Candidate, int Weight, string[] Sources);
public sealed record RecognitionCandidate(string Type, int Score, string[] Sources)
{
    public RecognitionCandidate() : this("", 0, []) { }
}

/// <summary>Heuristic assessment, not a calibrated probability or authenticated hardware identity.</summary>
public sealed record DeviceIdentification
{
    public int Version { get; init; } = 1;
    public string AutomaticType { get; init; } = "Unknown";
    public string Confidence { get; init; } = "None";
    public string? ManualType { get; init; }
    public string? ReportedModel { get; init; }
    public bool ModelConflict { get; init; }
    public bool ConflictingSignals { get; init; }
    public DateTimeOffset AssessedAtUtc { get; init; }
    public DateTimeOffset? ObservationStartedAtUtc { get; init; }
    public DateTimeOffset? ObservationCompletedAtUtc { get; init; }
    public string Scope { get; init; } = "external";
    public string[] DisabledFeatures { get; init; } = [];
    public RecognitionEvidence[] Evidence { get; init; } = [];
    public RecognitionRule[] Rules { get; init; } = [];
    public RecognitionCandidate[] Candidates { get; init; } = [];
    [JsonIgnore] public string EffectiveConfidence => ManualType == null ? Confidence : "Manual";

    public DeviceIdentification Snapshot() => this with
    {
        DisabledFeatures = [.. DisabledFeatures], Evidence = [.. Evidence],
        Rules = Rules.Select(rule => rule with { Sources = [.. rule.Sources] }).ToArray(),
        Candidates = Candidates.Select(candidate => candidate with { Sources = [.. candidate.Sources] }).ToArray()
    };

    public static void ApplyManualType(NetworkDevice device, string? type)
    {
        var result = device.Identification ?? new DeviceIdentification { Scope = "manual", AssessedAtUtc = DateTimeOffset.UtcNow };
        device.Identification = result with { ManualType = type };
        device.DeviceType = type ?? result.AutomaticType;
    }
}
