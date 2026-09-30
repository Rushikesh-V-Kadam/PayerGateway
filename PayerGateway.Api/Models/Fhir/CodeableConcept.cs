namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR CodeableConcept: one or more codings plus optional text.</summary>
public class CodeableConcept
{
    public List<Coding> Coding { get; set; } = new();
    public string? Text { get; set; }

    /// <summary>Empty concept (used by the JSON serializer).</summary>
    public CodeableConcept()
    {
    }

    /// <summary>Creates a concept with one coding.</summary>
    public CodeableConcept(string? system, string? code, string? display = null)
    {
        Coding.Add(new Coding(system, code, display));
    }

    /// <summary>First coding, or null.</summary>
    public Coding? FirstCoding()
    {
        if (Coding.Count == 0)
        {
            return null;
        }
        return Coding[0];
    }

    /// <summary>First code, or null.</summary>
    public string? FirstCode()
    {
        Coding? first = FirstCoding();
        if (first == null)
        {
            return null;
        }
        return first.Code;
    }

    /// <summary>First coding with this system, or null.</summary>
    public Coding? CodingWithSystem(string system)
    {
        foreach (Coding coding in Coding)
        {
            if (coding.System == system)
            {
                return coding;
            }
        }
        return null;
    }

    /// <summary>True when any coding has this code.</summary>
    public bool HasCode(string code)
    {
        foreach (Coding coding in Coding)
        {
            if (coding.Code == code)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Text, else the first display, else the first code.</summary>
    public string? BestText()
    {
        if (!string.IsNullOrEmpty(Text))
        {
            return Text;
        }
        Coding? first = FirstCoding();
        if (first == null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(first.Display))
        {
            return first.Display;
        }
        return first.Code;
    }
}
