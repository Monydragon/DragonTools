using DragonTools.Enums;

namespace DragonTools.Models.Symptom_Tracker;

public class SymptomHandler
{
    public Symptoms Symptom { get; set; }
    public DateTime Date { get; set; }
    public SymptomSeverity Severity { get; set; }
    public string Notes { get; set; }
    
    public SymptomHandler(Symptoms symptom, DateTime date, SymptomSeverity severity, string notes)
    {
        Symptom = symptom;
        Date = date;
        Severity = severity;
        Notes = notes;
    }
    
    public override string ToString()
    {
        return $"{Date.ToShortDateString()} - {Symptom} ({Severity}): {Notes}";
    }
    
    public string ToDetailedString()
    {
        return $"Date: {Date.ToShortDateString()}\nSymptom: {Symptom}\nSeverity: {Severity}\nNotes: {Notes}";
    }
}