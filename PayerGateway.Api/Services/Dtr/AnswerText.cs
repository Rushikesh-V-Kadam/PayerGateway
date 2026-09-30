using System.Globalization;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Services.Dtr;

/// <summary>
/// Turns FHIR answer values into the plain text the EHR sees (defaults, conditions),
/// using the same format the EHR sends answers in ("true", "12", "2026-08-10", option code ...).
/// </summary>
public static class AnswerText
{
    /// <summary>A default (initial) value as text, or null.</summary>
    public static string? FromInitial(QuestionnaireInitial initial)
    {
        if (initial.ValueBoolean != null) return initial.ValueBoolean.Value ? "true" : "false";
        if (initial.ValueDecimal != null) return initial.ValueDecimal.Value.ToString(CultureInfo.InvariantCulture);
        if (initial.ValueInteger != null) return initial.ValueInteger.Value.ToString(CultureInfo.InvariantCulture);
        if (initial.ValueDate != null) return initial.ValueDate;
        if (initial.ValueDateTime != null) return initial.ValueDateTime;
        if (initial.ValueString != null) return initial.ValueString;
        if (initial.ValueCoding != null) return initial.ValueCoding.Code;
        if (initial.ValueQuantity != null) return FromQuantity(initial.ValueQuantity);
        return null;
    }

    /// <summary>The value an enableWhen condition compares with, as text.</summary>
    public static string FromCondition(QuestionnaireEnableWhen condition)
    {
        if (condition.AnswerBoolean != null) return condition.AnswerBoolean.Value ? "true" : "false";
        if (condition.AnswerDecimal != null) return condition.AnswerDecimal.Value.ToString(CultureInfo.InvariantCulture);
        if (condition.AnswerInteger != null) return condition.AnswerInteger.Value.ToString(CultureInfo.InvariantCulture);
        if (condition.AnswerDate != null) return condition.AnswerDate;
        if (condition.AnswerDateTime != null) return condition.AnswerDateTime;
        if (condition.AnswerString != null) return condition.AnswerString;
        if (condition.AnswerCoding != null && condition.AnswerCoding.Code != null) return condition.AnswerCoding.Code;
        if (condition.AnswerQuantity != null) return FromQuantity(condition.AnswerQuantity);
        return string.Empty;
    }

    /// <summary>"12 mL/min" (or just "12" without a unit).</summary>
    public static string FromQuantity(Quantity quantity)
    {
        string number = quantity.Value == null ? string.Empty : quantity.Value.Value.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(quantity.Unit))
        {
            return number;
        }
        return number + " " + quantity.Unit;
    }
}
