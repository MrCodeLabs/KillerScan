namespace KillerScan.Engine;

/// <summary>Read-only advice, never proof of compatibility or permission to configure HA.</summary>
public sealed record HomeAssistantCandidate
{
    public string Domain { get; init; } = "";
    public string Basis { get; init; } = "Observed";
    public RecognitionEvidence[] Evidence { get; init; } = [];
    public string[] Requirements { get; init; } = [];
    public HomeAssistantCandidate Snapshot() => this with { Evidence = [.. Evidence], Requirements = [.. Requirements] };
}

public sealed record HomeAssistantAdvice
{
    public int Version { get; init; } = 2;
    public string CatalogueRevision { get; init; } = HomeAssistantAdvisor.CatalogueRevision;
    public string DocumentationReviewedOn { get; init; } = "2026-10-23";
    public string Status { get; init; } = "Unknown";
    public string? RecommendedDomain { get; init; }
    public string RecommendationReason { get; init; } = "Missing";
    public HomeAssistantCandidate[] Candidates { get; init; } = [];
    public string[] Steps { get; init; } = [];
    public HomeAssistantAdvice Snapshot() => this with
    { Candidates = Candidates.Select(candidate => candidate.Snapshot()).ToArray(), Steps = [.. Steps] };
}

/// <summary>Optional, ephemeral user leads. They do not become captured evidence or saved identity.</summary>
public sealed record HomeAssistantContext
{
    public string ManufacturerModel { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string Goal { get; init; } = "Monitor";
}

public sealed record HomeAssistantIntegration(string Domain, string Name, string DocumentationUrl, string[] Requirements);
