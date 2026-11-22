namespace Chubb.Claims.Domain.Claims;

public sealed record IncidentFacts
{
    public IncidentFacts(DateTimeOffset occurredAt, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Incident description is required.", nameof(description));
        }

        OccurredAt = occurredAt;
        Description = description.Trim();
    }

    public DateTimeOffset OccurredAt { get; }
    public string Description { get; }
}
