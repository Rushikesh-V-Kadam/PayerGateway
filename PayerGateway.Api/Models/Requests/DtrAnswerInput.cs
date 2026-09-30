using System.ComponentModel.DataAnnotations;

namespace PayerGateway.Api.Models.Requests;

/// <summary>
/// The answer to one question, as text. Write values like this (the question "type" in GET /api/v1/dtr/forms/{formId}):
///   boolean: "true" / "false"          integer: "12"            decimal: "12.5"
///   date: "2026-08-10"                 dateTime: "2026-08-10T09:30:00Z"   time: "09:30:00"
///   string / text / url: any text      choice / open-choice: the option code (open-choice also free text)
///   quantity: "12 mL/min" (number, space, unit)
/// </summary>
public class DtrAnswerInput
{
    /// <summary>linkId of the question.</summary>
    /// <example>egfr</example>
    [Required]
    public string LinkId { get; set; } = string.Empty;

    /// <summary>The answer(s). Normally one value; several only when the question has repeats = true.</summary>
    /// <example>["12"]</example>
    [Required]
    public List<string> Values { get; set; } = new();
}
