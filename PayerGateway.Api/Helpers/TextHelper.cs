namespace PayerGateway.Api.Helpers;

/// <summary>Small string helpers.</summary>
public static class TextHelper
{
    /// <summary>Cuts text to a maximum length, adding "..." when it was cut.</summary>
    public static string Shorten(string? text, int maxLength)
    {
        if (text == null)
        {
            return string.Empty;
        }
        if (text.Length <= maxLength)
        {
            return text;
        }
        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>The id part of "Type/id" (or the whole text when there is no slash).</summary>
    public static string LastSegment(string reference)
    {
        int slash = reference.LastIndexOf('/');
        if (slash < 0)
        {
            return reference;
        }
        return reference.Substring(slash + 1);
    }

    /// <summary>The type part of "Type/id", or an empty string.</summary>
    public static string FirstSegment(string reference)
    {
        int slash = reference.IndexOf('/');
        if (slash < 0)
        {
            return string.Empty;
        }
        return reference.Substring(0, slash);
    }

    /// <summary>A list without empty entries and without duplicates, in the original order.</summary>
    public static List<string> Distinct(List<string> values)
    {
        List<string> result = new List<string>();
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value))
            {
                result.Add(value);
            }
        }
        return result;
    }
}
