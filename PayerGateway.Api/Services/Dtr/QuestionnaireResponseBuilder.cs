using System.Globalization;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Services.Dtr;

/// <summary>
/// Builds the items of a FHIR QuestionnaireResponse from the EHR's plain answers, following the payer Questionnaire:
///  - same tree as the Questionnaire (groups contain their questions)
///  - each text value is checked and converted to the right FHIR type (boolean, integer, date, coding ...)
///  - questions hidden by enableWhen are left out; payer default values are used when nothing was sent
///  - required questions without an answer are listed in MissingRequired
/// Create one builder per save; read Errors / Warnings / MissingRequired after BuildItems().
/// </summary>
public class QuestionnaireResponseBuilder
{
    private readonly Questionnaire _questionnaire;
    private readonly Dictionary<string, QuestionnaireItem> _items = new Dictionary<string, QuestionnaireItem>();
    private readonly Dictionary<string, List<string>> _sent = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, List<string>> _effective = new Dictionary<string, List<string>>();

    /// <summary>Wrong values (e.g. "abc" for an integer). Any error means nothing is saved (400).</summary>
    public List<string> Errors { get; } = new List<string>();

    /// <summary>Answers that were ignored (unknown linkId, hidden question, unsupported type).</summary>
    public List<string> Warnings { get; } = new List<string>();

    /// <summary>Required questions (that are shown) without an answer.</summary>
    public List<string> MissingRequired { get; } = new List<string>();

    /// <summary>Number of questions answered.</summary>
    public int AnsweredCount { get; private set; }

    /// <summary>Prepares the lookups: every item by linkId, the sent answers, and sent-or-default values.</summary>
    public QuestionnaireResponseBuilder(Questionnaire questionnaire, List<DtrAnswerInput> answers)
    {
        _questionnaire = questionnaire;
        IndexItems(questionnaire.Item);

        foreach (DtrAnswerInput answer in answers)
        {
            List<string> values = new List<string>();
            foreach (string value in answer.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
            if (values.Count > 0)
            {
                _sent[answer.LinkId] = values;
            }
        }

        foreach (KeyValuePair<string, QuestionnaireItem> entry in _items)
        {
            List<string>? sentValues;
            if (_sent.TryGetValue(entry.Key, out sentValues))
            {
                _effective[entry.Key] = sentValues;
            }
            else
            {
                List<string> defaults = DefaultValues(entry.Value);
                if (defaults.Count > 0)
                {
                    _effective[entry.Key] = defaults;
                }
            }
        }
    }

    /// <summary>Builds the QuestionnaireResponse items. Check Errors afterwards.</summary>
    public List<QuestionnaireResponseItem> BuildItems()
    {
        List<QuestionnaireResponseItem> items = BuildLevel(_questionnaire.Item);

        foreach (string linkId in _sent.Keys)
        {
            QuestionnaireItem? item;
            if (!_items.TryGetValue(linkId, out item))
            {
                Warnings.Add(linkId + ": no such question in this form; ignored.");
            }
            else if (item.Type == "group" || item.Type == "display")
            {
                Warnings.Add(linkId + ": is a " + item.Type + ", not a question; ignored.");
            }
        }
        return items;
    }

    // ================================================================== tree

    /// <summary>Builds one level of the tree (and, through recursion, everything below it).</summary>
    private List<QuestionnaireResponseItem> BuildLevel(List<QuestionnaireItem> items)
    {
        List<QuestionnaireResponseItem> result = new List<QuestionnaireResponseItem>();
        foreach (QuestionnaireItem item in items)
        {
            if (!IsEnabled(item))
            {
                if (HasSentAnswers(item))
                {
                    Warnings.Add(item.LinkId + ": not shown for these answers (enableWhen); its answers were ignored.");
                }
                continue;
            }

            if (item.Type == "group")
            {
                List<QuestionnaireResponseItem> children = BuildLevel(item.Item);
                if (children.Count > 0)
                {
                    QuestionnaireResponseItem group = new QuestionnaireResponseItem();
                    group.LinkId = item.LinkId;
                    group.Text = item.Text;
                    group.Item = children;
                    result.Add(group);
                }
                continue;
            }

            if (item.Type == "display")
            {
                continue;
            }

            QuestionnaireResponseItem? question = BuildQuestion(item);
            if (question != null)
            {
                result.Add(question);
            }
        }
        return result;
    }

    /// <summary>One answered question, or null when it has no (valid) answer.</summary>
    private QuestionnaireResponseItem? BuildQuestion(QuestionnaireItem item)
    {
        List<string>? values;
        if (!_effective.TryGetValue(item.LinkId, out values) || values.Count == 0)
        {
            if (item.Required == true)
            {
                MissingRequired.Add(item.LinkId + " (" + (item.Text ?? item.LinkId) + ")");
            }
            return null;
        }

        if (item.Repeats != true && values.Count > 1)
        {
            Errors.Add(item.LinkId + ": only one answer is allowed.");
            return null;
        }

        bool fromDefault = !_sent.ContainsKey(item.LinkId);
        List<QuestionnaireResponseAnswer> answers = new List<QuestionnaireResponseAnswer>();
        foreach (string value in values)
        {
            QuestionnaireResponseAnswer? answer = Convert(item, value);
            if (answer != null)
            {
                answer.Extension.Add(InformationOrigin(fromDefault ? "auto" : "manual"));
                answers.Add(answer);
            }
        }
        if (answers.Count == 0)
        {
            return null;
        }

        // sub-questions of a question sit under its first answer
        if (item.Item.Count > 0)
        {
            answers[0].Item = BuildLevel(item.Item);
        }

        QuestionnaireResponseItem question = new QuestionnaireResponseItem();
        question.LinkId = item.LinkId;
        question.Text = item.Text;
        question.Answer = answers;
        AnsweredCount = AnsweredCount + 1;
        return question;
    }

    // ================================================================== values

    /// <summary>Text -> the FHIR answer for the question type. Null (and an error or warning) when it does not fit.</summary>
    private QuestionnaireResponseAnswer? Convert(QuestionnaireItem item, string value)
    {
        QuestionnaireResponseAnswer answer = new QuestionnaireResponseAnswer();
        string type = item.Type;

        if (type == "boolean")
        {
            string lower = value.ToLowerInvariant();
            if (lower == "true" || lower == "yes") { answer.ValueBoolean = true; return answer; }
            if (lower == "false" || lower == "no") { answer.ValueBoolean = false; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not true / false.");
            return null;
        }

        if (type == "integer")
        {
            int number;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) { answer.ValueInteger = number; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a whole number.");
            return null;
        }

        if (type == "decimal")
        {
            decimal number;
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) { answer.ValueDecimal = number; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a number.");
            return null;
        }

        if (type == "date")
        {
            DateTime date;
            string[] formats = new string[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) { answer.ValueDate = value; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a date (yyyy-MM-dd).");
            return null;
        }

        if (type == "dateTime")
        {
            DateTimeOffset dateTime;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime)) { answer.ValueDateTime = value; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a date and time (yyyy-MM-ddTHH:mm:ssZ).");
            return null;
        }

        if (type == "time")
        {
            TimeSpan time;
            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time)) { answer.ValueTime = value; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a time (HH:mm:ss).");
            return null;
        }

        if (type == "string" || type == "text")
        {
            if (item.MaxLength != null && value.Length > item.MaxLength.Value)
            {
                Errors.Add(item.LinkId + ": the answer is longer than " + item.MaxLength.Value + " characters.");
                return null;
            }
            answer.ValueString = value;
            return answer;
        }

        if (type == "url")
        {
            Uri? uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri)) { answer.ValueUri = value; return answer; }
            Errors.Add(item.LinkId + ": '" + value + "' is not a web address.");
            return null;
        }

        if (type == "choice" || type == "open-choice")
        {
            return ConvertChoice(item, value, answer);
        }

        if (type == "quantity")
        {
            return ConvertQuantity(item, value, answer);
        }

        Warnings.Add(item.LinkId + ": questions of type '" + type + "' cannot be answered through the gateway; ignored.");
        return null;
    }

    /// <summary>choice: the value must be an option code (or display). open-choice: other text is kept as free text.</summary>
    private QuestionnaireResponseAnswer? ConvertChoice(QuestionnaireItem item, string value, QuestionnaireResponseAnswer answer)
    {
        foreach (QuestionnaireAnswerOption option in item.AnswerOption)
        {
            DtrOption simple = QuestionnaireReader.ToOption(option);
            bool matches = string.Equals(simple.Code, value, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(simple.Display, value, StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                continue;
            }
            if (option.ValueCoding != null)
            {
                answer.ValueCoding = new Coding(option.ValueCoding.System, option.ValueCoding.Code, option.ValueCoding.Display);
            }
            else if (option.ValueInteger != null)
            {
                answer.ValueInteger = option.ValueInteger;
            }
            else if (option.ValueDate != null)
            {
                answer.ValueDate = option.ValueDate;
            }
            else
            {
                answer.ValueString = option.ValueString;
            }
            return answer;
        }

        if (item.Type == "open-choice")
        {
            answer.ValueString = value;
            return answer;
        }

        if (item.AnswerOption.Count == 0)
        {
            // options come from a payer ValueSet the gateway does not expand: send the code as given
            answer.ValueCoding = new Coding(null, value, null);
            return answer;
        }

        List<string> codes = new List<string>();
        foreach (QuestionnaireAnswerOption option in item.AnswerOption)
        {
            codes.Add(QuestionnaireReader.ToOption(option).Code);
        }
        Errors.Add(item.LinkId + ": '" + value + "' is not one of the options (" + string.Join(", ", codes) + ").");
        return null;
    }

    /// <summary>"12 mL/min" -> value 12, unit "mL/min".</summary>
    private QuestionnaireResponseAnswer? ConvertQuantity(QuestionnaireItem item, string value, QuestionnaireResponseAnswer answer)
    {
        string numberPart = value;
        string? unit = null;
        int space = value.IndexOf(' ');
        if (space > 0)
        {
            numberPart = value.Substring(0, space);
            unit = value.Substring(space + 1).Trim();
        }

        decimal number;
        if (!decimal.TryParse(numberPart, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            Errors.Add(item.LinkId + ": '" + value + "' is not a quantity (number, space, unit).");
            return null;
        }
        answer.ValueQuantity = new Quantity();
        answer.ValueQuantity.Value = number;
        answer.ValueQuantity.Unit = unit;
        return answer;
    }

    /// <summary>DTR information-origin extension: auto (payer default) or manual (entered by the provider).</summary>
    private static Extension InformationOrigin(string source)
    {
        Extension origin = new Extension(DtrCodes.InformationOriginUrl);
        Extension part = new Extension("source");
        part.ValueCode = source;
        origin.Children.Add(part);
        return origin;
    }

    // ================================================================== enableWhen

    /// <summary>True when the item has no enableWhen, or its conditions match (all / any).</summary>
    private bool IsEnabled(QuestionnaireItem item)
    {
        if (item.EnableWhen.Count == 0)
        {
            return true;
        }

        bool any = item.EnableBehavior == "any";
        foreach (QuestionnaireEnableWhen rule in item.EnableWhen)
        {
            bool matches = ConditionMatches(rule);
            if (any && matches)
            {
                return true;
            }
            if (!any && !matches)
            {
                return false;
            }
        }
        return !any;
    }

    /// <summary>Checks one enableWhen rule against the current answers.</summary>
    private bool ConditionMatches(QuestionnaireEnableWhen rule)
    {
        List<string>? values;
        if (!_effective.TryGetValue(rule.Question, out values))
        {
            values = new List<string>();
        }

        if (rule.Operator == "exists")
        {
            bool expectedExists = rule.AnswerBoolean != false;
            return (values.Count > 0) == expectedExists;
        }

        string expected = AnswerText.FromCondition(rule);
        foreach (string value in values)
        {
            if (Compare(value, expected, rule.Operator))
            {
                return true;
            }
        }
        // "!=" with no answer at all counts as "not equal"
        return rule.Operator == "!=" && values.Count == 0;
    }

    /// <summary>Compares numbers as numbers, everything else as text (ISO dates sort correctly as text).</summary>
    private static bool Compare(string actual, string expected, string op)
    {
        int result;
        decimal left;
        decimal right;
        if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out left)
            && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out right))
        {
            result = left.CompareTo(right);
        }
        else if (op == "=" || op == "!=")
        {
            result = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }
        else
        {
            result = string.CompareOrdinal(actual, expected);
        }

        if (op == "=") return result == 0;
        if (op == "!=") return result != 0;
        if (op == ">") return result > 0;
        if (op == "<") return result < 0;
        if (op == ">=") return result >= 0;
        if (op == "<=") return result <= 0;
        return false;
    }

    // ================================================================== lookups

    /// <summary>Puts every item (at any depth) in the linkId lookup.</summary>
    private void IndexItems(List<QuestionnaireItem> items)
    {
        foreach (QuestionnaireItem item in items)
        {
            _items[item.LinkId] = item;
            IndexItems(item.Item);
        }
    }

    /// <summary>True when the EHR sent answers for this item or anything below it.</summary>
    private bool HasSentAnswers(QuestionnaireItem item)
    {
        if (_sent.ContainsKey(item.LinkId))
        {
            return true;
        }
        foreach (QuestionnaireItem child in item.Item)
        {
            if (HasSentAnswers(child))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The payer's default answers of a question (initial values and pre-selected options).</summary>
    private static List<string> DefaultValues(QuestionnaireItem item)
    {
        List<string> defaults = new List<string>();
        foreach (QuestionnaireInitial initial in item.Initial)
        {
            string? value = AnswerText.FromInitial(initial);
            if (value != null)
            {
                defaults.Add(value);
            }
        }
        foreach (QuestionnaireAnswerOption option in item.AnswerOption)
        {
            if (option.InitialSelected == true)
            {
                defaults.Add(QuestionnaireReader.ToOption(option).Code);
            }
        }
        return defaults;
    }
}
