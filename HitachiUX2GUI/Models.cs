using System.Text.Json;
using System.Text.Json.Serialization;

namespace HitachiUX2GUI;

// ── Felttyper tilgjengelig i layout-byggeren ───────────────────────────

public enum FieldType
{
    FastTekst,
    DatoMedDager,
    DatoManuell,
    Lotnummer,
    Tid,
    Teller
}

// ── Ett print-felt i en layout ─────────────────────────────────────────

public class PrintField
{
    public FieldType Type    { get; set; } = FieldType.FastTekst;
    public string    Value   { get; set; } = "";
    public int       Days    { get; set; } = 0;
    public string    Format  { get; set; } = "dd.MM.yy";

    /// <summary>
    /// Løser feltet til den faktiske tekststrengen som skal sendes til skriveren.
    /// </summary>
    public string Resolve()
    {
        return Type switch
        {
            FieldType.FastTekst    => Value,
            FieldType.Lotnummer    => Value,
            FieldType.Teller       => Value,
            FieldType.Tid          => DateTime.Now.ToString("HH:mm:ss"),
            FieldType.DatoMedDager => DateTime.Now.AddDays(Days).ToString(Format),
            FieldType.DatoManuell  => DateTime.TryParse(Value, out var dt)
                                        ? dt.ToString(Format)
                                        : Value,
            _                      => Value
        };
    }

    public override string ToString() => $"{Type} → {Resolve()}";
}

// ── En komplett layout med navn og liste av felt ───────────────────────

public class PrintLayout
{
    public string           Name   { get; set; } = "Min layout";
    public List<PrintField> Fields { get; set; } = [];
}

// ── Lagrer og laster layouts til/fra JSON-fil ─────────────────────────

public static class LayoutStorage
{
    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "layouts.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        Converters           = { new JsonStringEnumConverter() }
    };

    public static List<PrintLayout> Load()
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<PrintLayout>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(List<PrintLayout> layouts)
    {
        var json = JsonSerializer.Serialize(layouts, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
