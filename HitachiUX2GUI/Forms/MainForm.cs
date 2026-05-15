using HitachiUX2;
using HitachiUX2.Exceptions;

namespace HitachiUX2GUI.Forms;

/// <summary>
/// Hoved-vinduet for Hitachi UX2-D160W Print Kontroll.
///
/// Fane 1: Send dato/tekst til eksisterende layout på skriveren.
/// Fane 2: Bygg ny layout fra bunnen av og send til skriveren.
/// </summary>
public class MainForm : Form
{
    // ── Fargepalett ────────────────────────────────────────────────
    private static readonly Color BgColor      = Color.FromArgb(26, 26, 46);
    private static readonly Color PanelColor   = Color.FromArgb(22, 33, 62);
    private static readonly Color AccentColor  = Color.FromArgb(15, 52, 96);
    private static readonly Color BlueColor    = Color.FromArgb(26, 106, 168);
    private static readonly Color GreenColor   = Color.FromArgb(0, 184, 148);
    private static readonly Color RedColor     = Color.FromArgb(214, 48, 49);
    private static readonly Color YellowColor  = Color.FromArgb(253, 203, 110);
    private static readonly Color TextColor    = Color.FromArgb(232, 234, 246);
    private static readonly Color SubtextColor = Color.FromArgb(144, 164, 174);

    // ── Skriver-tilstand ───────────────────────────────────────────
    private HitachiUX2Printer? _printer;
    private bool               _connected;

    // ── Layouts ────────────────────────────────────────────────────
    private List<PrintLayout> _layouts = LayoutStorage.Load();
    private List<PrintField>  _currentFields = [];

    // ── Felles kontroller ──────────────────────────────────────────
    private Label  _statusLabel  = new();
    private Button _connectBtn   = new();
    private Button _sendBtn      = new();
    private RichTextBox _logBox  = new();
    private TabControl _tabs     = new();

    // ── Fane 1: Send til layout ────────────────────────────────────
    private NumericUpDown _jobNumber    = new();
    private NumericUpDown _date1Item    = new();
    private ComboBox      _date1Format  = new();
    private RadioButton   _date1Days    = new();
    private RadioButton   _date1Manual  = new();
    private NumericUpDown _date1DaysSp  = new();
    private DateTimePicker _date1Picker = new();
    private Label         _date1Preview = new();

    private CheckBox      _date2Enable  = new();
    private NumericUpDown _date2Item    = new();
    private ComboBox      _date2Format  = new();
    private RadioButton   _date2Days    = new();
    private RadioButton   _date2Manual  = new();
    private NumericUpDown _date2DaysSp  = new();
    private DateTimePicker _date2Picker = new();
    private Label         _date2Preview = new();

    private readonly (NumericUpDown Item, TextBox Text)[] _extraFields = new (NumericUpDown, TextBox)[3];

    // ── Fane 2: Bygg ny layout ─────────────────────────────────────
    private TextBox   _layoutName   = new();
    private ComboBox  _loadCombo    = new();
    private FlowLayoutPanel _fieldList = new();

    private static readonly string[] DateFormats =
    [
        "dd.MM.yy", "dd.MM.yyyy", "yyyy-MM-dd",
        "dd/MM/yyyy", "dd/MM/yy", "yyyyMMdd", "dd MMM yyyy"
    ];

    public MainForm()
    {
        Text            = "Hitachi UX2-D160W — Print Kontroll";
        Size            = new Size(700, 900);
        MinimumSize     = new Size(640, 750);
        BackColor       = BgColor;
        ForeColor       = TextColor;
        StartPosition   = FormStartPosition.CenterScreen;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildUI();
    }

    // ── Bygg brukergrensesnitt ─────────────────────────────────────

    private void BuildUI()
    {
        // Topptekst
        var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = AccentColor };
        var titleLbl = new Label
        {
            Text      = "HITACHI UX2-D160W",
            Font      = new Font("Courier New", 18, FontStyle.Bold),
            ForeColor = YellowColor,
            AutoSize  = true,
        };
        var subLbl = new Label
        {
            Text      = "Print Kontrollpanel",
            Font      = new Font("Courier New", 9),
            ForeColor = SubtextColor,
            AutoSize  = true,
        };
        header.Controls.Add(titleLbl);
        header.Controls.Add(subLbl);
        header.Layout += (_, _) =>
        {
            titleLbl.Location = new Point((header.Width - titleLbl.Width) / 2, 10);
            subLbl.Location   = new Point((header.Width - subLbl.Width) / 2, 45);
        };

        // Statuslinje
        var statusPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = PanelColor };
        var statusLblStatic = new Label
        {
            Text      = "STATUS:",
            Font      = new Font("Courier New", 9, FontStyle.Bold),
            ForeColor = SubtextColor,
            Location  = new Point(14, 13),
            AutoSize  = true,
        };
        _statusLabel = new Label
        {
            Text      = "  ● Ikke tilkoblet",
            Font      = new Font("Courier New", 10, FontStyle.Bold),
            ForeColor = RedColor,
            Location  = new Point(80, 12),
            AutoSize  = true,
        };
        _connectBtn = StyledButton("KOBLE TIL", BlueColor, BgColor);
        _connectBtn.Dock   = DockStyle.Right;
        _connectBtn.Width  = 120;
        _connectBtn.Click += ConnectBtn_Click;
        statusPanel.Controls.AddRange([statusLblStatic, _statusLabel, _connectBtn]);

        // Send-knapp (alltid synlig)
        _sendBtn = new Button
        {
            Text      = "⬆  SEND TIL SKRIVER",
            Font      = new Font("Courier New", 13, FontStyle.Bold),
            BackColor = GreenColor,
            ForeColor = BgColor,
            Dock      = DockStyle.Top,
            Height    = 50,
            FlatStyle = FlatStyle.Flat,
            Enabled   = false,
            Cursor    = Cursors.Hand,
        };
        _sendBtn.FlatAppearance.BorderSize = 0;
        _sendBtn.Click += SendBtn_Click;

        // Fane-system
        _tabs = new TabControl
        {
            Dock      = DockStyle.Fill,
            BackColor = BgColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
        };
        _tabs.SelectedIndexChanged += (_, _) =>
            _sendBtn.Text = _tabs.SelectedIndex == 0
                ? "⬆  SEND TIL SKRIVER"
                : "⬆  SEND NY LAYOUT TIL SKRIVER";

        var tab1 = new TabPage("  Send til layout  ") { BackColor = BgColor };
        var tab2 = new TabPage("  Bygg ny layout  ")  { BackColor = BgColor };
        BuildTab1(tab1);
        BuildTab2(tab2);
        _tabs.TabPages.AddRange([tab1, tab2]);

        // Loggvisning
        var logPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = PanelColor };
        _logBox = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 27, 42),
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 8),
            ReadOnly  = true,
            BorderStyle = BorderStyle.None,
        };
        logPanel.Controls.Add(_logBox);

        // Separator-linjer
        var sep1 = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = AccentColor };
        var sep2 = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = AccentColor };
        var sep3 = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = AccentColor };

        // Legg til i riktig rekkefølge (Dock=Top bygger nedover)
        Controls.Add(_tabs);
        Controls.Add(sep2);
        Controls.Add(_sendBtn);
        Controls.Add(sep1);
        Controls.Add(statusPanel);
        Controls.Add(header);
        Controls.Add(sep3);
        Controls.Add(logPanel);
    }

    // ── Fane 1: Send til eksisterende layout ──────────────────────

    private void BuildTab1(TabPage page)
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgColor };
        page.Controls.Add(scroll);

        int y = 10;

        // Job-seksjon
        AddSectionHeader(scroll, "JOB / LAYOUT", ref y);
        var jobPanel = AddPanel(scroll, ref y, 60);
        AddLabel(jobPanel, "Job-nummer (1–2000):", 10, 16);
        _jobNumber = new NumericUpDown
        {
            Location  = new Point(200, 14),
            Size      = new Size(80, 28),
            Minimum   = 1, Maximum = 2000, Value = 1,
            BackColor = Color.FromArgb(13, 27, 42),
            ForeColor = YellowColor,
            Font      = new Font("Courier New", 11, FontStyle.Bold),
        };
        var recallBtn = StyledButton("Last inn job →", BlueColor, TextColor);
        recallBtn.Location = new Point(290, 12);
        recallBtn.Size     = new Size(140, 32);
        recallBtn.Click   += RecallBtn_Click;
        jobPanel.Controls.AddRange([_jobNumber, recallBtn]);

        // Dato 1
        AddSectionHeader(scroll, "DATO 1", ref y);
        BuildDateBlock(scroll, ref y, "d1",
            ref _date1Item, ref _date1Format, ref _date1Days,
            ref _date1Manual, ref _date1DaysSp, ref _date1Picker, ref _date1Preview,
            optional: false);

        // Dato 2
        AddSectionHeader(scroll, "DATO 2  (valgfritt)", ref y);
        var d2Panel = AddPanel(scroll, ref y, 20);
        _date2Enable = new CheckBox
        {
            Text      = "Aktiver dato 2",
            Location  = new Point(10, 0),
            AutoSize  = true,
            ForeColor = TextColor,
            Font      = new Font("Courier New", 10),
        };
        _date2Enable.CheckedChanged += (_, _) => UpdateDate2Enabled();
        d2Panel.Controls.Add(_date2Enable);
        BuildDateBlock(scroll, ref y, "d2",
            ref _date2Item, ref _date2Format, ref _date2Days,
            ref _date2Manual, ref _date2DaysSp, ref _date2Picker, ref _date2Preview,
            optional: true);

        // Ekstra tekst-felt
        AddSectionHeader(scroll, "EKSTRA TEKST-FELT  (valgfritt)", ref y);
        var extraPanel = AddPanel(scroll, ref y, 100);
        for (int i = 0; i < 3; i++)
        {
            int yOff = 8 + i * 30;
            AddLabel(extraPanel, "Item:", 10, yOff + 5);
            var itemSp = new NumericUpDown
            {
                Location  = new Point(55, yOff),
                Size      = new Size(60, 28),
                Minimum   = 1, Maximum = 50,
                Value     = i + 3,
                BackColor = Color.FromArgb(13, 27, 42),
                ForeColor = YellowColor,
                Font      = new Font("Courier New", 10, FontStyle.Bold),
            };
            AddLabel(extraPanel, "Tekst:", 125, yOff + 5);
            var textBox = new TextBox
            {
                Location    = new Point(175, yOff),
                Size        = new Size(220, 28),
                BackColor   = Color.FromArgb(13, 27, 42),
                ForeColor   = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Courier New", 10),
            };
            extraPanel.Controls.AddRange([itemSp, textBox]);
            _extraFields[i] = (itemSp, textBox);
        }
    }

    private void BuildDateBlock(
        Panel parent, ref int y, string prefix,
        ref NumericUpDown itemSp, ref ComboBox formatCb,
        ref RadioButton daysRb, ref RadioButton manualRb,
        ref NumericUpDown daysSp, ref DateTimePicker picker,
        ref Label preview, bool optional)
    {
        var panel = AddPanel(parent, ref y, optional ? 210 : 185);

        AddLabel(panel, "Print item nr:", 10, 14);
        itemSp = new NumericUpDown
        {
            Location  = new Point(160, 12),
            Size      = new Size(80, 28),
            Minimum   = 1, Maximum = 50,
            Value     = prefix == "d1" ? 2 : 3,
            BackColor = Color.FromArgb(13, 27, 42),
            ForeColor = YellowColor,
            Font      = new Font("Courier New", 11, FontStyle.Bold),
        };

        AddLabel(panel, "Datoformat:", 10, 52);
        formatCb = new ComboBox
        {
            Location      = new Point(160, 50),
            Size          = new Size(200, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.FromArgb(13, 27, 42),
            ForeColor     = YellowColor,
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Courier New", 10),
        };
        formatCb.Items.AddRange(DateFormats);
        formatCb.SelectedIndex = 0;
        formatCb.SelectedIndexChanged += (_, _) => UpdatePreview(prefix);

        AddLabel(panel, "Modus:", 10, 90);
        daysRb = new RadioButton
        {
            Text      = "Legg til dager fra i dag",
            Location  = new Point(160, 88),
            AutoSize  = true,
            Checked   = true,
            ForeColor = TextColor,
            Font      = new Font("Courier New", 9),
        };
        manualRb = new RadioButton
        {
            Text      = "Manuell dato",
            Location  = new Point(160, 112),
            AutoSize  = true,
            ForeColor = TextColor,
            Font      = new Font("Courier New", 9),
        };
        daysRb.CheckedChanged   += (_, _) => { ToggleDateMode(prefix); UpdatePreview(prefix); };
        manualRb.CheckedChanged += (_, _) => { ToggleDateMode(prefix); UpdatePreview(prefix); };

        AddLabel(panel, "Antall dager:", 10, 137);
        daysSp = new NumericUpDown
        {
            Location  = new Point(160, 135),
            Size      = new Size(100, 28),
            Minimum   = 0, Maximum = 9999,
            BackColor = Color.FromArgb(13, 27, 42),
            ForeColor = YellowColor,
            Font      = new Font("Courier New", 11, FontStyle.Bold),
        };
        daysSp.ValueChanged += (_, _) => UpdatePreview(prefix);

        picker = new DateTimePicker
        {
            Location   = new Point(160, 135),
            Size       = new Size(200, 28),
            Format     = DateTimePickerFormat.Short,
            CalendarForeColor = YellowColor,
            Visible    = false,
        };
        picker.ValueChanged += (_, _) => UpdatePreview(prefix);

        // Forhåndsvisning
        var prevLbl = new Label
        {
            Text      = "Forhåndsvisning:",
            Location  = new Point(10, 172),
            AutoSize  = true,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 8),
        };
        preview = new Label
        {
            Location  = new Point(160, 168),
            Size      = new Size(200, 28),
            Font      = new Font("Courier New", 13, FontStyle.Bold),
            ForeColor = GreenColor,
        };

        panel.Controls.AddRange([itemSp, formatCb, daysRb, manualRb,
                                  daysSp, picker, prevLbl, preview]);

        if (optional)
        {
            // Flytt alle kontroller litt ned for checkbox-plass
            foreach (Control c in panel.Controls)
                c.Top += 20;
        }

        UpdatePreview(prefix);
    }

    // ── Fane 2: Bygg ny layout ─────────────────────────────────────

    private void BuildTab2(TabPage page)
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
        page.Controls.Add(main);

        // Topp: navn + lagre/last inn
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 94, BackColor = BgColor };
        AddLabel(topPanel, "Layout-navn:", 16, 16);
        _layoutName = new TextBox
        {
            Location    = new Point(140, 14),
            Size        = new Size(200, 28),
            Text        = "Min layout",
            BackColor   = Color.FromArgb(13, 27, 42),
            ForeColor   = YellowColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Courier New", 10),
        };
        var saveBtn = StyledButton("💾 Lagre", BlueColor, TextColor);
        saveBtn.Location = new Point(350, 12);
        saveBtn.Size     = new Size(100, 30);
        saveBtn.Click   += SaveLayout_Click;

        AddLabel(topPanel, "Last inn:", 16, 54);
        _loadCombo = new ComboBox
        {
            Location      = new Point(140, 52),
            Size          = new Size(200, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.FromArgb(13, 27, 42),
            ForeColor     = YellowColor,
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Courier New", 10),
        };
        RefreshLoadCombo();
        var loadBtn = StyledButton("Last inn →", AccentColor, TextColor);
        loadBtn.Location = new Point(350, 50);
        loadBtn.Size     = new Size(100, 30);
        loadBtn.Click   += LoadLayout_Click;
        topPanel.Controls.AddRange([_layoutName, saveBtn, _loadCombo, loadBtn]);

        // Seksjonstittel
        var fieldHeader = new Label
        {
            Text      = "  FELT I LAYOUTEN",
            Dock      = DockStyle.Top,
            Height    = 28,
            BackColor = AccentColor,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // Bunn: knapper
        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = BgColor };
        var addBtn = StyledButton("➕ Legg til felt", BlueColor, TextColor);
        addBtn.Location = new Point(16, 6);
        addBtn.Size     = new Size(150, 34);
        addBtn.Click   += AddField_Click;
        var clearBtn = StyledButton("🗑 Tøm alle", RedColor, TextColor);
        clearBtn.Location = new Point(176, 6);
        clearBtn.Size     = new Size(120, 34);
        clearBtn.Click   += (_, _) =>
        {
            if (MessageBox.Show("Fjerne alle felt?", "Tøm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _currentFields.Clear();
                RefreshFieldList();
            }
        };
        btnPanel.Controls.AddRange([addBtn, clearBtn]);

        // Felt-liste fyller resten
        _fieldList = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = PanelColor,
        };

        // Rekkefølge for Dock: Fill først, Bottom, deretter Top (siste Top = øverst)
        main.Controls.Add(_fieldList);
        main.Controls.Add(btnPanel);
        main.Controls.Add(fieldHeader);
        main.Controls.Add(topPanel);
    }

    // ── Oppdater felt-listen ───────────────────────────────────────

    private void RefreshFieldList()
    {
        _fieldList.Controls.Clear();

        if (_currentFields.Count == 0)
        {
            _fieldList.Controls.Add(new Label
            {
                Text      = "Ingen felt ennå — trykk 'Legg til felt'",
                ForeColor = SubtextColor,
                Font      = new Font("Courier New", 10),
                AutoSize  = true,
                Margin    = new Padding(10, 16, 0, 0),
            });
            return;
        }

        for (int i = 0; i < _currentFields.Count; i++)
        {
            var field = _currentFields[i];
            var idx   = i;

            var row = new Panel
            {
                Size      = new Size(610, 38),
                BackColor = idx % 2 == 0 ? AccentColor : PanelColor,
                Margin    = new Padding(0, 1, 0, 0),
            };

            row.Controls.Add(new Label
            {
                Text      = $"Item {idx + 1}:",
                Location  = new Point(6, 10),
                Size      = new Size(70, 20),
                ForeColor = YellowColor,
                Font      = new Font("Courier New", 9, FontStyle.Bold),
            });
            row.Controls.Add(new Label
            {
                Text      = field.Type.ToString(),
                Location  = new Point(80, 10),
                Size      = new Size(160, 20),
                ForeColor = SubtextColor,
                Font      = new Font("Courier New", 9),
            });
            row.Controls.Add(new Label
            {
                Text      = $"→ {field.Resolve()}",
                Location  = new Point(245, 10),
                Size      = new Size(180, 20),
                ForeColor = GreenColor,
                Font      = new Font("Courier New", 9, FontStyle.Bold),
            });

            // Handlingsknapper
            var editBtn = SmallButton("✏", BlueColor);
            editBtn.Location = new Point(540, 6);
            editBtn.Click   += (_, _) => EditField(idx);

            var delBtn = SmallButton("🗑", RedColor);
            delBtn.Location = new Point(572, 6);
            delBtn.Click   += (_, _) => { _currentFields.RemoveAt(idx); RefreshFieldList(); };

            var upBtn = SmallButton("▲", AccentColor);
            upBtn.Location = new Point(500, 6);
            upBtn.Enabled  = idx > 0;
            upBtn.Click   += (_, _) => MoveField(idx, -1);

            var downBtn = SmallButton("▼", AccentColor);
            downBtn.Location = new Point(520, 6);
            downBtn.Enabled  = idx < _currentFields.Count - 1;
            downBtn.Click   += (_, _) => MoveField(idx, 1);

            row.Controls.AddRange([editBtn, delBtn, upBtn, downBtn]);
            _fieldList.Controls.Add(row);
        }
    }

    private void MoveField(int idx, int dir)
    {
        var ni = idx + dir;
        (_currentFields[idx], _currentFields[ni]) = (_currentFields[ni], _currentFields[idx]);
        RefreshFieldList();
    }

    private void EditField(int idx)
    {
        using var dlg = new FieldEditorForm(_currentFields[idx]);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _currentFields[idx] = dlg.Result;
            RefreshFieldList();
        }
    }

    // ── Event handlers ─────────────────────────────────────────────

    private void ConnectBtn_Click(object? sender, EventArgs e)
    {
        if (_connected)
            Disconnect();
        else
            Connect();
    }

    private void Connect()
    {
        try
        {
            _printer = new HitachiUX2Printer("192.168.0.250", useEnq: false);
            _printer.Connect();
            _connected = true;
            _statusLabel.Text      = "  ● Tilkoblet (192.168.0.250)";
            _statusLabel.ForeColor = GreenColor;
            _connectBtn.Text       = "KOBLE FRA";
            _connectBtn.BackColor  = RedColor;
            _sendBtn.Enabled       = true;
            Log("Tilkoblet 192.168.0.250:1024");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Tilkoblingsfeil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log($"FEIL: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        _printer?.Disconnect();
        _printer   = null;
        _connected = false;
        _statusLabel.Text      = "  ● Ikke tilkoblet";
        _statusLabel.ForeColor = RedColor;
        _connectBtn.Text       = "KOBLE TIL";
        _connectBtn.BackColor  = BlueColor;
        _sendBtn.Enabled       = false;
        Log("Frakoblet");
    }

    private void RecallBtn_Click(object? sender, EventArgs e)
    {
        if (!_connected) { ShowNotConnected(); return; }
        try
        {
            _printer!.RecallPrintData((int)_jobNumber.Value);
            Log($"Job #{_jobNumber.Value} lastet inn");
            MessageBox.Show($"Job #{_jobNumber.Value} lastet inn!", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Feil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log($"FEIL: {ex.Message}");
        }
    }

    private void SendBtn_Click(object? sender, EventArgs e)
    {
        if (!_connected) { ShowNotConnected(); return; }

        try
        {
            var items = _tabs.SelectedIndex == 0
                ? BuildItemsFromTab1()
                : BuildItemsFromTab2();

            if (items.Count == 0)
            {
                MessageBox.Show("Ingen felt å sende!", "Tomt");
                return;
            }

            _printer!.SendPrintContent(items);
            Log($"Sendt: {string.Join(", ", items.Select(x => $"Item {x.Key}: {x.Value}"))}");

            var summary = string.Join("\n", items.OrderBy(x => x.Key).Select(x => $"Item {x.Key}: {x.Value}"));
            MessageBox.Show($"Skriveren ble oppdatert:\n\n{summary}", "✅ Sendt!");
        }
        catch (PrinterNakException ex)
        {
            MessageBox.Show(ex.Message, "Skriverfeil (NAK)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log($"NAK: {ex.Message}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Feil", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log($"FEIL: {ex.Message}");
        }
    }

    private Dictionary<int, string> BuildItemsFromTab1()
    {
        var items = new Dictionary<int, string>();

        // Dato 1
        items[(int)_date1Item.Value] = GetDateString("d1");

        // Dato 2 (valgfri)
        if (_date2Enable.Checked)
            items[(int)_date2Item.Value] = GetDateString("d2");

        // Ekstra felt
        foreach (var (itemSp, textBox) in _extraFields)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                items[(int)itemSp.Value] = textBox.Text.Trim();
        }

        return items;
    }

    private Dictionary<int, string> BuildItemsFromTab2()
    {
        if (_currentFields.Count == 0)
            throw new InvalidOperationException("Ingen felt i layouten.");

        return _currentFields
            .Select((field, idx) => (idx + 1, field.Resolve()))
            .ToDictionary(x => x.Item1, x => x.Item2);
    }

    private void AddField_Click(object? sender, EventArgs e)
    {
        using var dlg = new FieldEditorForm();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _currentFields.Add(dlg.Result);
            RefreshFieldList();
        }
    }

    private void SaveLayout_Click(object? sender, EventArgs e)
    {
        var name = _layoutName.Text.Trim();
        if (string.IsNullOrEmpty(name)) { MessageBox.Show("Skriv inn layout-navn!"); return; }

        var existing = _layouts.FirstOrDefault(l => l.Name == name);
        if (existing is not null)
            existing.Fields = [.. _currentFields];
        else
            _layouts.Add(new PrintLayout { Name = name, Fields = [.. _currentFields] });

        LayoutStorage.Save(_layouts);
        RefreshLoadCombo();
        Log($"Layout '{name}' lagret ({_currentFields.Count} felt)");
        MessageBox.Show($"Layout '{name}' er lagret.", "Lagret!");
    }

    private void LoadLayout_Click(object? sender, EventArgs e)
    {
        var name = _loadCombo.SelectedItem?.ToString();
        if (name is null) return;
        var layout = _layouts.FirstOrDefault(l => l.Name == name);
        if (layout is null) return;
        _currentFields = [.. layout.Fields];
        _layoutName.Text = name;
        RefreshFieldList();
        Log($"Layout '{name}' lastet inn");
    }

    // ── Dato-hjelpemetoder ─────────────────────────────────────────

    private string GetDateString(string prefix)
    {
        var isD1   = prefix == "d1";
        var days   = isD1 ? _date1DaysSp  : _date2DaysSp;
        var manual = isD1 ? _date1Manual  : _date2Manual;
        var picker = isD1 ? _date1Picker  : _date2Picker;
        var format = isD1 ? _date1Format  : _date2Format;

        var fmt = format.SelectedItem?.ToString() ?? "dd.MM.yy";
        return manual.Checked
            ? picker.Value.ToString(fmt)
            : DateTime.Now.AddDays((double)days.Value).ToString(fmt);
    }

    private void UpdatePreview(string prefix)
    {
        try
        {
            var value   = GetDateString(prefix);
            var preview = prefix == "d1" ? _date1Preview : _date2Preview;
            if (preview is not null)
            {
                preview.Text      = value;
                preview.ForeColor = GreenColor;
            }
        }
        catch
        {
            var preview = prefix == "d1" ? _date1Preview : _date2Preview;
            if (preview is not null)
            {
                preview.Text      = "Ugyldig dato!";
                preview.ForeColor = RedColor;
            }
        }
    }

    private void ToggleDateMode(string prefix)
    {
        var isD1   = prefix == "d1";
        var days   = isD1 ? _date1DaysSp  : _date2DaysSp;
        var picker = isD1 ? _date1Picker  : _date2Picker;
        var manual = isD1 ? _date1Manual  : _date2Manual;

        days.Visible   = !manual.Checked;
        picker.Visible = manual.Checked;
    }

    private void UpdateDate2Enabled()
    {
        var enabled = _date2Enable.Checked;
        foreach (Control c in _date2Item.Parent?.Controls ?? new Control.ControlCollection(null))
            if (c != _date2Enable)
                c.Enabled = enabled;
    }

    // ── Layout-hjelp ───────────────────────────────────────────────

    private void RefreshLoadCombo()
    {
        _loadCombo.Items.Clear();
        foreach (var l in _layouts)
            _loadCombo.Items.Add(l.Name);
    }

    // ── UI-hjelpemetoder ───────────────────────────────────────────

    private void Log(string message)
    {
        if (_logBox.IsDisposed) return;
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        _logBox.ScrollToCaret();
    }

    private void ShowNotConnected() =>
        MessageBox.Show("Koble til skriveren først!", "Ikke tilkoblet");

    private Panel AddPanel(Panel parent, ref int y, int height)
    {
        var panel = new Panel
        {
            Location  = new Point(16, y),
            Size      = new Size(parent.Width - 32, height),
            BackColor = PanelColor,
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        parent.Controls.Add(panel);
        y += height + 4;
        return panel;
    }

    private static void AddSectionHeader(Panel parent, string title, ref int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = $"  {title}",
            Location  = new Point(0, y),
            Size      = new Size(parent.Width, 26),
            BackColor = AccentColor,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        y += 30;
    }

    private static void AddLabel(Control parent, string text, int x, int y) =>
        parent.Controls.Add(new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            ForeColor = TextColor,
            Font      = new Font("Courier New", 9),
        });

    private static Button StyledButton(string text, Color bg, Color fg) => new()
    {
        Text      = text,
        BackColor = bg,
        ForeColor = fg,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Courier New", 9, FontStyle.Bold),
        Cursor    = Cursors.Hand,
        Height    = 32,
    };

    private static Button SmallButton(string text, Color bg) => new()
    {
        Text      = text,
        Size      = new Size(28, 26),
        BackColor = bg,
        ForeColor = TextColor,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Courier New", 9),
        Cursor    = Cursors.Hand,
    };

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _printer?.Disconnect();
        base.OnFormClosed(e);
    }
}
