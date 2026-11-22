namespace Chubb.Claims.Domain.Claims;

/// <summary>A non-negative estimate with its own currency; it is not an accounting liability.</summary>
public sealed record MoneyEstimate
{
    public MoneyEstimate(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "An estimate cannot be negative.");
        }

        if (amount > 999_999_999_999_999.9999m || decimal.Round(amount, 4) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must fit decimal(19,4).");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3 || currency.Trim().Any(c => c is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must be a three-letter uppercase code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.Trim();
    }

    public decimal Amount { get; }
    public string Currency { get; }
}
