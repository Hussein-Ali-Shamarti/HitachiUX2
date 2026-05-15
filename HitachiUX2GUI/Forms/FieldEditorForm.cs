namespace HitachiUX2GUI.Forms;

/// <summary>
/// Dialog for å opprette eller redigere ett print-felt.
/// Viser/skjuler kontroller basert på valgt felttype.
/// </summary>
public class FieldEditorForm : Form
{
    // ── Resultatet etter at brukeren trykker OK ────────────────────
    public PrintField Result { get; private set; } = new();

    // ── Kontroller ─────────────────────────────────────────────────
    private readonly ComboBox    _typeCombo    = new();
    private readonly TextBox     _valueBox     = new();
    private readonly NumericUpDown _daysSpinner = new();
    private readonly ComboBox    _formatCombo  = new();
    private Label                _valueLbl     = new();
    private Label                _daysLbl      = new();
    private Label                _formatLbl    = new();
    private readonly Label       _previewLbl   = new();

    private static readonly string[] DateFormats =
    [
        "dd.MM.yy", "dd.MM.yyyy", "yyyy-MM-dd",
        "dd/MM/yyyy", "dd/MM/yy", "yyyyMMdd", "dd MMM yyyy"
    ];

    public FieldEditorForm(PrintField? existing = null)
    {
        Text            = "Rediger felt";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MinimizeBox     = false;
        MaximizeBox     = false;
        Size            = new Size(400, 320);
        BackColor       = Color.FromArgb(26, 26, 46);
        ForeColor       = Color.FromArgb(232, 234, 246);

        BuildControls();

        // Fyll inn eksisterende verdier hvis vi redigerer
        if (existing is not null)
            PopulateFrom(existing);

        UpdateVisibility();
    }

    private void BuildControls()
    {
        // Felttype
        var typeLbl = StyledLabel("Felttype:", 20, 20);

        _typeCombo.Items.AddRange(["Fast tekst", "Dato (+ dager)", "Dato (manuell)",
                                    "Lotnummer", "Tid (HH:mm:ss)", "Teller"]);
        _typeCombo.SelectedIndex  = 0;
        _typeCombo.DropDownStyle  = ComboBoxStyle.DropDownList;
        _typeCombo.Location       = new Point(160, 18);
        _typeCombo.Size           = new Size(200, 28);
        _typeCombo.BackColor      = Color.FromArgb(13, 27, 42);
        _typeCombo.ForeColor      = Color.FromArgb(253, 203, 110);
        _typeCombo.FlatStyle      = FlatStyle.Flat;
        _typeCombo.SelectedIndexChanged += (_, _) => UpdateVisibility();

        // Verdi
        _valueLbl = StyledLabel("Verdi:", 20, 60);
        _valueBox.Location   = new Point(160, 58);
        _valueBox.Size       = new Size(200, 28);
        _valueBox.BackColor  = Color.FromArgb(13, 27, 42);
        _valueBox.ForeColor  = Color.FromArgb(253, 203, 110);
        _valueBox.BorderStyle = BorderStyle.FixedSingle;

        // Antall dager
        _daysLbl = StyledLabel("Antall dager:", 20, 100);
        _daysSpinner.Location  = new Point(160, 98);
        _daysSpinner.Size      = new Size(100, 28);
        _daysSpinner.Minimum   = 0;
        _daysSpinner.Maximum   = 9999;
        _daysSpinner.BackColor = Color.FromArgb(13, 27, 42);
        _daysSpinner.ForeColor = Color.FromArgb(253, 203, 110);
        _daysSpinner.ValueChanged += (_, _) => UpdatePreview();

        // Datoformat
        _formatLbl = StyledLabel("Datoformat:", 20, 140);
        _formatCombo.Items.AddRange(DateFormats);
        _formatCombo.SelectedIndex = 0;
        _formatCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _formatCombo.Location      = new Point(160, 138);
        _formatCombo.Size          = new Size(200, 28);
        _formatCombo.BackColor     = Color.FromArgb(13, 27, 42);
        _formatCombo.ForeColor     = Color.FromArgb(253, 203, 110);
        _formatCombo.FlatStyle     = FlatStyle.Flat;
        _formatCombo.SelectedIndexChanged += (_, _) => UpdatePreview();

        // Forhåndsvisning
        var prevLbl = StyledLabel("Forhåndsvisning:", 20, 185);
        prevLbl.ForeColor = Color.FromArgb(144, 164, 174);

        _previewLbl.Location  = new Point(160, 183);
        _previewLbl.Size      = new Size(200, 28);
        _previewLbl.Font      = new Font("Courier New", 12, FontStyle.Bold);
        _previewLbl.ForeColor = Color.FromArgb(0, 184, 148);

        // OK / Avbryt
        var okBtn = new Button
        {
            Text      = "OK",
            Location  = new Point(120, 230),
            Size      = new Size(80, 34),
            BackColor = Color.FromArgb(0, 184, 148),
            ForeColor = Color.FromArgb(26, 26, 46),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
        };
        okBtn.FlatAppearance.BorderSize = 0;
        okBtn.Click += OkBtn_Click;

        var cancelBtn = new Button
        {
            Text      = "Avbryt",
            Location  = new Point(210, 230),
            Size      = new Size(80, 34),
            BackColor = Color.FromArgb(15, 52, 96),
            ForeColor = Color.FromArgb(232, 234, 246),
            FlatStyle = FlatStyle.Flat,
        };
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange([typeLbl, _typeCombo, _valueLbl, _valueBox,
                           _daysLbl, _daysSpinner, _formatLbl, _formatCombo,
                           prevLbl, _previewLbl, okBtn, cancelBtn]);

        _valueBox.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private Label StyledLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(135, 28),
        ForeColor = Color.FromArgb(232, 234, 246),
        Font      = new Font("Courier New", 9),
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private void UpdateVisibility()
    {
        var type = _typeCombo.SelectedIndex;

        // Fast tekst, Lotnummer, Teller, Dato manuell — vis verdi-felt
        _valueLbl.Visible  = type is 0 or 2 or 3 or 5;
        _valueBox.Visible  = type is 0 or 2 or 3 or 5;

        // Dato med dager — vis dager-spinner
        _daysLbl.Visible   = type == 1;
        _daysSpinner.Visible = type == 1;

        // Dato-typer — vis format-combo
        _formatLbl.Visible  = type is 1 or 2;
        _formatCombo.Visible = type is 1 or 2;

        _valueLbl.Text = type == 2 ? "Dato (YYYY-MM-DD):" : "Verdi:";

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            _previewLbl.Text      = BuildField().Resolve();
            _previewLbl.ForeColor = Color.FromArgb(0, 184, 148);
        }
        catch
        {
            _previewLbl.Text      = "Ugyldig!";
            _previewLbl.ForeColor = Color.FromArgb(214, 48, 49);
        }
    }

    private PrintField BuildField() => new()
    {
        Type   = (FieldType)_typeCombo.SelectedIndex,
        Value  = _valueBox.Text,
        Days   = (int)_daysSpinner.Value,
        Format = _formatCombo.SelectedItem?.ToString() ?? "dd.MM.yy",
    };

    private void PopulateFrom(PrintField field)
    {
        _typeCombo.SelectedIndex = (int)field.Type;
        _valueBox.Text           = field.Value;
        _daysSpinner.Value       = field.Days;
        var fmtIdx = Array.IndexOf(DateFormats, field.Format);
        _formatCombo.SelectedIndex = fmtIdx >= 0 ? fmtIdx : 0;
    }

    private void OkBtn_Click(object? sender, EventArgs e)
    {
        Result       = BuildField();
        DialogResult = DialogResult.OK;
        Close();
    }
}
