using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Services.Dtr;

/// <summary>
/// Turns a payer Questionnaire (FHIR) into a DtrForm: a flat list of simple questions the EHR can show without
/// knowing FHIR. Groups become groupLinkId / groupText on their questions; enableWhen becomes showWhen.
/// </summary>
public static class QuestionnaireReader
{
    /// <summary>The form as the EHR sees it, with the answers saved so far.</summary>
    public static DtrForm ToForm(DtrFormRecord record, Questionnaire questionnaire)
    {
        DtrForm form = new DtrForm();
        form.FormId = record.FormId;
        form.PayerName = record.PayerName;
        form.PatientId = record.PatientId;
        form.OrderIds = record.OrderIds;
        form.CoverageId = record.CoverageId;
        form.QuestionnaireUrl = record.QuestionnaireUrl;
        form.Title = questionnaire.Title ?? questionnaire.Name;
        form.Status = record.Status;
        form.QuestionnaireResponseId = record.QuestionnaireResponseId;
        form.Adaptive = record.Adaptive;
        form.MoreQuestionsExpected = record.Adaptive && !record.PayerFinished;

        Dictionary<string, List<string>> saved = new Dictionary<string, List<string>>();
        foreach (DtrAnswerInput answer in record.Answers)
        {
            saved[answer.LinkId] = answer.Values;
        }

        AddQuestions(questionnaire.Item, null, null, saved, form.Questions);
        return form;
    }

    /// <summary>Adds the items (and their children) in display order.</summary>
    private static void AddQuestions(List<QuestionnaireItem> items, string? groupLinkId, string? groupText,
        Dictionary<string, List<string>> saved, List<DtrQuestion> questions)
    {
        foreach (QuestionnaireItem item in items)
        {
            if (item.Type == "group")
            {
                // the group itself is not a question: its children carry its linkId / text
                string? text = item.Text ?? groupText;
                AddQuestions(item.Item, item.LinkId, text, saved, questions);
                continue;
            }

            questions.Add(ToQuestion(item, groupLinkId, groupText, saved));

            // sub-questions of a question (rare) are listed right after it
            if (item.Item.Count > 0)
            {
                AddQuestions(item.Item, groupLinkId, groupText, saved, questions);
            }
        }
    }

    /// <summary>One Questionnaire item -> DtrQuestion.</summary>
    private static DtrQuestion ToQuestion(QuestionnaireItem item, string? groupLinkId, string? groupText, Dictionary<string, List<string>> saved)
    {
        DtrQuestion question = new DtrQuestion();
        question.LinkId = item.LinkId;
        question.Text = string.IsNullOrEmpty(item.Prefix) ? (item.Text ?? item.LinkId) : item.Prefix + " " + item.Text;
        question.Type = item.Type;
        question.Required = item.Required == true;
        question.Repeats = item.Repeats == true;
        question.ReadOnly = item.ReadOnly == true;
        question.MaxLength = item.MaxLength;
        question.GroupLinkId = groupLinkId;
        question.GroupText = groupText;
        question.OptionsValueSet = item.AnswerValueSet;
        question.ShowWhenBehavior = string.IsNullOrEmpty(item.EnableBehavior) ? "all" : item.EnableBehavior;

        foreach (QuestionnaireAnswerOption option in item.AnswerOption)
        {
            DtrOption simple = ToOption(option);
            question.Options.Add(simple);
            if (option.InitialSelected == true)
            {
                question.DefaultValues.Add(simple.Code);
            }
        }

        foreach (QuestionnaireInitial initial in item.Initial)
        {
            string? value = AnswerText.FromInitial(initial);
            if (value != null)
            {
                question.DefaultValues.Add(value);
            }
        }

        foreach (QuestionnaireEnableWhen rule in item.EnableWhen)
        {
            DtrCondition condition = new DtrCondition();
            condition.QuestionLinkId = rule.Question;
            condition.Operator = rule.Operator;
            condition.Value = AnswerText.FromCondition(rule);
            question.ShowWhen.Add(condition);
        }

        List<string>? current;
        if (saved.TryGetValue(item.LinkId, out current))
        {
            question.CurrentValues.AddRange(current);
        }
        return question;
    }

    /// <summary>An answer option -> code / display / system.</summary>
    public static DtrOption ToOption(QuestionnaireAnswerOption option)
    {
        DtrOption simple = new DtrOption();
        if (option.ValueCoding != null)
        {
            simple.Code = option.ValueCoding.Code ?? string.Empty;
            simple.Display = option.ValueCoding.Display ?? simple.Code;
            simple.System = option.ValueCoding.System;
        }
        else if (option.ValueString != null)
        {
            simple.Code = option.ValueString;
            simple.Display = option.ValueString;
        }
        else if (option.ValueInteger != null)
        {
            simple.Code = option.ValueInteger.Value.ToString();
            simple.Display = simple.Code;
        }
        else if (option.ValueDate != null)
        {
            simple.Code = option.ValueDate;
            simple.Display = option.ValueDate;
        }
        return simple;
    }
}
