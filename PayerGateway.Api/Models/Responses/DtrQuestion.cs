namespace PayerGateway.Api.Models.Responses;

/// <summary>One question of a payer form, in plain JSON.</summary>
public class DtrQuestion
{
    /// <summary>Send this back as linkId with the answer.</summary>
    /// <example>egfr</example>
    public string LinkId { get; set; } = string.Empty;

    /// <example>Most recent eGFR (mL/min/1.73m2)</example>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// boolean | decimal | integer | date | dateTime | time | string | text | url | choice | open-choice | quantity |
    /// display (text only, no answer) | attachment | reference (not supported by the gateway; answer via the payer's process).
    /// </summary>
    /// <example>integer</example>
    public string Type { get; set; } = "string";

    /// <example>true</example>
    public bool Required { get; set; }

    /// <summary>true = several answers allowed.</summary>
    /// <example>false</example>
    public bool Repeats { get; set; }

    /// <summary>true = show only; the payer fills it.</summary>
    /// <example>false</example>
    public bool ReadOnly { get; set; }

    /// <summary>Maximum text length, when the payer set one.</summary>
    public int? MaxLength { get; set; }

    /// <summary>linkId of the group (section) this question belongs to, or null.</summary>
    /// <example>clinical</example>
    public string? GroupLinkId { get; set; }

    /// <summary>Title of the group (section), or null.</summary>
    /// <example>Clinical information</example>
    public string? GroupText { get; set; }

    /// <summary>Allowed answers for choice / open-choice questions. Send the option's code.</summary>
    public List<DtrOption> Options { get; set; } = new();

    /// <summary>Name of the payer ValueSet with the allowed answers, when the options are not listed.</summary>
    public string? OptionsValueSet { get; set; }

    /// <summary>Default answers set by the payer (already filled in if you send nothing else).</summary>
    /// <example>[]</example>
    public List<string> DefaultValues { get; set; } = new();

    /// <summary>Answers saved so far for this form.</summary>
    /// <example>[]</example>
    public List<string> CurrentValues { get; set; } = new();

    /// <summary>Show (and answer) this question only when these conditions match. Empty = always shown.</summary>
    public List<DtrCondition> ShowWhen { get; set; } = new();

    /// <summary>all | any: how several showWhen conditions combine.</summary>
    /// <example>all</example>
    public string ShowWhenBehavior { get; set; } = "all";
}
