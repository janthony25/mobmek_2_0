namespace MobmekApi.Entities;

/// <summary>
/// One partial payment recorded against an <see cref="Invoice"/> before it's fully settled.
/// Unlike the single payment snapshot on <see cref="Invoice"/> (<see cref="Invoice.AmountPaid"/>
/// etc., set once by <see cref="Services.InvoiceService.MarkPaidAsync"/>), an invoice can have
/// any number of these; the invoice's remaining balance is always computed as
/// <c>TotalAmount - Sum(PartialPayments.Amount)</c>, never stored. Record-keeping only for now —
/// it does not post a cash-flow ledger entry (see docs/features/feature-gaps.md), and it never
/// rewrites <see cref="Invoice.Notes"/> or the job's JobNotes/InvoiceNotes (those print on the
/// customer-facing PDF and stay purely free-text) — the history is surfaced only through this
/// entity's own rows.
/// </summary>
public class InvoicePartialPayment : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }

    /// <summary>"Cash", "Card", or "Bank Transfer" — free text, same shape as <see cref="Invoice.ModeOfPayment"/>.</summary>
    public string? ModeOfPayment { get; set; }

    /// <summary>Editable after creation, same as <see cref="ShowOnInvoice"/> (amount/date/mode are locked once recorded).</summary>
    public string? Notes { get; set; }

    /// <summary>When the payment was actually taken (no time component) — user-set, so a payment can be logged after the fact.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>
    /// Opt-in: this payment only appears on the generated invoice PDF (as a line after the Notes
    /// section) when explicitly toggled on. Defaults false — recording a payment never changes
    /// a customer-facing document on its own.
    /// </summary>
    public bool ShowOnInvoice { get; set; }
}
