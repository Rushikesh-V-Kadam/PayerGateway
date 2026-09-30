namespace PayerGateway.Api.Constants;

/// <summary>Plain status values the EHR receives for a prior authorization and its lines.</summary>
public static class DecisionStatus
{
    public const string Approved = "approved";
    public const string PartiallyApproved = "partially-approved";
    public const string Denied = "denied";
    public const string Pended = "pended";
    public const string NotRequired = "not-required";
    public const string Cancelled = "cancelled";
    public const string ContactPayer = "contact-payer";
    public const string Modified = "modified";
    public const string Error = "error";
}
