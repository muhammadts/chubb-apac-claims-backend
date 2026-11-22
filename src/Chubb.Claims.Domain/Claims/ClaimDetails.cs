namespace Chubb.Claims.Domain.Claims;

public abstract record ClaimDetails;

public sealed record MotorClaimDetails(
    string? VehicleRegistration = null,
    string? Make = null,
    string? Model = null,
    short? ModelYear = null,
    string? VehicleUse = null,
    bool? Drivable = null,
    bool? OtherPartyInvolved = null) : ClaimDetails;

public sealed record PropertyClaimDetails(
    string? PropertyType = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? Region = null,
    string? PostalCode = null,
    string? CountryCode = null,
    string? LossCause = null,
    string? AffectedAreaDescription = null,
    bool? Habitable = null) : ClaimDetails;
