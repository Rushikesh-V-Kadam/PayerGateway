namespace PayerGateway.Api.Constants;

/// <summary>X12 306 action codes the payer puts on each ClaimResponse item.</summary>
public static class X12ReviewActions
{
    public const string Approved = "A1";          // certified in total
    public const string PartiallyApproved = "A2"; // certified - partial
    public const string Denied = "A3";            // not certified
    public const string Pended = "A4";            // waiting (more info or clinical review)
    public const string Modified = "A6";
    public const string Cancelled = "C";
    public const string ContactPayer = "CT";
    public const string NotRequired = "NA";       // no action required
}
