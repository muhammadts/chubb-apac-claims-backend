namespace Chubb.Claims.Domain.Claims;

public sealed record ClaimantContactSnapshot
{
    public ClaimantContactSnapshot(string name, string email, string? phone)
    {
        Name = Required(name, nameof(name));
        Email = Required(email, nameof(email));
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    public string Name { get; }
    public string Email { get; }
    public string? Phone { get; }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
