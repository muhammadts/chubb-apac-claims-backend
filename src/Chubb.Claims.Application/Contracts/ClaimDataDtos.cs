using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Contracts;

public sealed record ClaimantContactInput(string Name, string Email, string? Phone);
public sealed record IncidentInput(DateTimeOffset OccurredAt, string Description);
public abstract record ClaimDetailsInput;
public sealed record MotorClaimDetailsInput(string? VehicleRegistration = null, string? Make = null,
    string? Model = null, short? ModelYear = null, string? VehicleUse = null, bool? Drivable = null,
    bool? OtherPartyInvolved = null) : ClaimDetailsInput;
public sealed record PropertyClaimDetailsInput(string? PropertyType = null, string? AddressLine1 = null,
    string? AddressLine2 = null, string? City = null, string? Region = null, string? PostalCode = null,
    string? CountryCode = null, string? LossCause = null, string? AffectedAreaDescription = null,
    bool? Habitable = null) : ClaimDetailsInput;
public sealed record MoneyEstimateInput(decimal Amount, string Currency)
{
    public MoneyEstimate ToDomain() => new(Amount, Currency);
}

public sealed record ClaimantContactDto(string Name, string Email, string? Phone);
public sealed record IncidentDto(DateTimeOffset OccurredAt, string Description);
public abstract record ClaimDetailsDto;
public sealed record MotorClaimDetailsDto(string? VehicleRegistration, string? Make, string? Model,
    short? ModelYear, string? VehicleUse, bool? Drivable, bool? OtherPartyInvolved) : ClaimDetailsDto;
public sealed record PropertyClaimDetailsDto(string? PropertyType, string? AddressLine1, string? AddressLine2,
    string? City, string? Region, string? PostalCode, string? CountryCode, string? LossCause,
    string? AffectedAreaDescription, bool? Habitable) : ClaimDetailsDto;
public sealed record MoneyEstimateDto(decimal Amount, string Currency)
{
    public static MoneyEstimateDto? FromDomain(MoneyEstimate? value) => value is null ? null : new(value.Amount, value.Currency);
}
public sealed record StaffDecisionDto(DecisionType Type, string Reason, string DecidedBySubjectId,
    DateTimeOffset DecidedAt, MoneyEstimateDto? Amount);

public static class ClaimDataMapping
{
    public static ClaimantContactSnapshot ToDomain(this ClaimantContactInput value) => new(value.Name, value.Email, value.Phone);
    public static IncidentFacts ToDomain(this IncidentInput value) => new(value.OccurredAt, value.Description);
    public static ClaimDetails ToDomain(this ClaimDetailsInput value) => value switch
    {
        MotorClaimDetailsInput motor => new MotorClaimDetails(motor.VehicleRegistration, motor.Make, motor.Model,
            motor.ModelYear, motor.VehicleUse, motor.Drivable, motor.OtherPartyInvolved),
        PropertyClaimDetailsInput property => new PropertyClaimDetails(property.PropertyType, property.AddressLine1,
            property.AddressLine2, property.City, property.Region, property.PostalCode, property.CountryCode,
            property.LossCause, property.AffectedAreaDescription, property.Habitable),
        _ => throw new ArgumentOutOfRangeException(nameof(value), "Unsupported claim details input.")
    };
    public static ClaimantContactDto ToDto(this ClaimantContactSnapshot value) => new(value.Name, value.Email, value.Phone);
    public static IncidentDto ToDto(this IncidentFacts value) => new(value.OccurredAt, value.Description);
    public static ClaimDetailsDto ToDto(this ClaimDetails value) => value switch
    {
        MotorClaimDetails motor => new MotorClaimDetailsDto(motor.VehicleRegistration, motor.Make, motor.Model,
            motor.ModelYear, motor.VehicleUse, motor.Drivable, motor.OtherPartyInvolved),
        PropertyClaimDetails property => new PropertyClaimDetailsDto(property.PropertyType, property.AddressLine1,
            property.AddressLine2, property.City, property.Region, property.PostalCode, property.CountryCode,
            property.LossCause, property.AffectedAreaDescription, property.Habitable),
        _ => throw new ArgumentOutOfRangeException(nameof(value), "Unsupported claim details type.")
    };
}
