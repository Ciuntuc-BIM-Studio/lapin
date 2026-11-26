namespace Lapin.Platform;

/// <summary>
/// A certificate of work actually done (a monthly statement of works, „situație de lucrări”): its date and the value it
/// certifies in the contract's terms (before retention, advance recovery and VAT). The open cost module's cash flow runs on
/// the planned schedule; a module that measures progress may give these instead (docs/plan-4d5d.md, cash flow).
/// </summary>
public sealed record CertifiedValue(DateOnly Date, double Value, string? Name = null);

/// <summary>
/// The hook the cash flow (cost module) reads certificates of actual progress through, when an edition has a module that
/// makes them. Null or empty: the cash flow is the planned one. The certificates given replace the planned ones up to the
/// last one's date; the planned work after it carries the rest of the contract's value.
/// </summary>
public interface ICertifiedProgress
{
    IReadOnlyList<CertifiedValue>? Certificates(IQueryContext query, string estimateId);
}
