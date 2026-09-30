namespace PayerGateway.Api.Constants;

/// <summary>Other X12 codes used on Claim.item.</summary>
public static class X12Codes
{
    // 1322 certification type
    public const string CertificationInitial = "I";
    public const string CertificationCancel = "3";
    public const string CertificationRenewal = "R";

    // 1525 request type
    public const string HealthServicesReview = "HS";

    // 1365 service types (Claim.item.category)
    public const string MedicalCare = "1";
    public const string Surgical = "2";
    public const string DiagnosticXRay = "4";
    public const string DmePurchase = "12";
    public const string Dialysis = "76";
    public const string Pharmacy = "88";
    public const string PhysicalTherapy = "PT";
}
