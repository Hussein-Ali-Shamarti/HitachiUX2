using HitachiUX2;
using HitachiUX2.Exceptions;

namespace HitachiUX2GUI.Forms;

public class MainForm : Form
{
    private static readonly Color BgColor      = Color.FromArgb(26, 26, 46);
    private static readonly Color PanelColor   = Color.FromArgb(22, 33, 62);
    private static readonly Color AccentColor  = Color.FromArgb(15, 52, 96);
    private static readonly Color BlueColor    = Color.FromArgb(26, 106, 168);
    private static readonly Color GreenColor   = Color.FromArgb(0, 184, 148);
    private static readonly Color RedColor     = Color.FromArgb(214, 48, 49);
    private static readonly Color YellowColor  = Color.FromArgb(253, 203, 110);
    private static readonly Color TextColor    = Color.FromArgb(232, 234, 246);
    private static readonly Color SubtextColor = Color.FromArgb(144, 164, 174);

    private HitachiUX2Printer? _printer;
    private bool               _connected;

    private Label       _statusLabel = new();
    private Button      _connectBtn  = new();
    private Button      _sendBtn     = new();
    private RichTextBox _logBox      = new();
    private TabControl  _tabs        = new();

    private NumericUpDown  _date1Item    = new();
    private ComboBox       _date1Format  = new();
    private RadioButton    _date1Days    = new();
    private RadioButton    _date1Manual  = new();
    private NumericUpDown  _date1DaysSp  = new();
    private DateTimePicker _date1Picker  = new();
    private Label          _date1Preview = new();

    private CheckBox       _date2Enable  = new();
    private NumericUpDown  _date2Item    = new();
    private ComboBox       _date2Format  = new();
    private RadioButton    _date2Days    = new();
    private RadioButton    _date2Manual  = new();
    private NumericUpDown  _date2DaysSp  = new();
    private DateTimePicker _date2Picker  = new();
    private Label          _date2Preview = new();

    private readonly (NumericUpDown Item, TextBox Text)[] _extraFields = new (NumericUpDown, TextBox)[3];

    private NumericUpDown _jobNumber = new();

    private Label           _fetchStatusLabel = new();
    private FlowLayoutPanel _patternCards     = new();

    private static readonly string[] DateFormats =
    [
        "dd.MM.yy", "dd.MM.yyyy", "yyyy-MM-dd",
        "dd/MM/yyyy", "dd/MM/yy", "yyyyMMdd", "dd MMM yyyy"
    ];

    public MainForm()
    {
        Text          = "Hitachi UX2-D160W — Print Kontroll";
        Size          = new Size(700, 900);
        MinimumSize   = new Size(640, 750);
        BackColor     = BgColor;
        ForeColor     = TextColor;
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildUI();
    }

    private void BuildUI()
    {
        var header   = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = AccentColor };
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
        header.Controls.AddRange([titleLbl, subLbl]);
        header.Layout += (_, _) =>
        {
            titleLbl.Location = new Point((header.Width - titleLbl.Width) / 2, 10);
            subLbl.Location   = new Point((header.Width - subLbl.Width) / 2, 45);
        };

        var statusPanel     = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = PanelColor };
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
        _connectBtn       = StyledButton("KOBLE TIL", BlueColor, BgColor);
        _connectBtn.Dock  = DockStyle.Right;
        _connectBtn.Width = 120;
        _connectBtn.Click += ConnectBtn_Click;
        statusPanel.Controls.AddRange([statusLblStatic, _statusLabel, _connectBtn]);

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

        _tabs = new TabControl
        {
            Dock      = DockStyle.Fill,
            BackColor = BgColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
        };
        _tabs.SelectedIndexChanged += (_, _) =>
            _sendBtn.Enabled = _tabs.SelectedIndex != 1 && _connected;

        var tab1 = new TabPage("  Send til layout  ") { BackColor = BgColor };
        var tab2 = new TabPage("  Mønstre  ")          { BackColor = BgColor };
        BuildTab1(tab1);
        BuildTab2(tab2);
        _tabs.TabPages.AddRange([tab1, tab2]);

        var logPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = PanelColor };
        _logBox = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = Color.FromArgb(13, 27, 42),
            ForeColor   = SubtextColor,
            Font        = new Font("Courier New", 8),
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
        };
        logPanel.Controls.Add(_logBox);

        var jobPanel = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = PanelColor };
        var jobLbl = new Label
        {
            Text      = "Jobb nr:",
            Location  = new Point(14, 12),
            AutoSize  = true,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
        };
        _jobNumber = new NumericUpDown
        {
            Location  = new Point(90, 8),
            Size      = new Size(80, 28),
            Minimum   = 1, Maximum = 2000,
            Value     = 1,
            BackColor = Color.FromArgb(13, 27, 42),
            ForeColor = YellowColor,
            Font      = new Font("Courier New", 13, FontStyle.Bold),
        };
        var jobHint = new Label
        {
            Text      = "Velg jobb/layout som skal oppdateres på skriveren",
            Location  = new Point(185, 12),
            AutoSize  = true,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 8),
        };
        jobPanel.Controls.AddRange([jobLbl, _jobNumber, jobHint]);

        var sep1 = new Panel { Dock = DockStyle.Top,    Height = 2, BackColor = AccentColor };
        var sep2 = new Panel { Dock = DockStyle.Top,    Height = 2, BackColor = AccentColor };
        var sep3 = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = AccentColor };

        // Dock=Top stacks downward, so controls must be added in reverse display order
        Controls.Add(_tabs);
        Controls.Add(sep2);
        Controls.Add(_sendBtn);
        Controls.Add(sep1);
        Controls.Add(jobPanel);
        Controls.Add(statusPanel);
        Controls.Add(header);
        Controls.Add(sep3);
        Controls.Add(logPanel);
    }

    private void BuildTab1(TabPage page)
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgColor };
        page.Controls.Add(scroll);

        int y = 10;

        AddSectionHeader(scroll, "SKRIVER-LAYOUT", ref y);
        var layoutInfo = AddPanel(scroll, ref y, 90);
        var layoutItems = new[]
        {
            ("Item 1", "Produksjonsdato",   "dd.MM.yy"),
            ("Item 2", "Best before-dato",  "dd.MM.yy"),
            ("Item 3", "Fri tekst",         "f.eks. «Siste forbruksdag»"),
            ("Item 4", "EFTA-logo",         "ikke send — fast innhold"),
        };
        for (int i = 0; i < layoutItems.Length; i++)
        {
            var (item, label, hint) = layoutItems[i];
            int xOff = (i % 2) * 300 + 10;
            int yOff = (i / 2) * 38 + 8;
            layoutInfo.Controls.Add(new Label
            {
                Text      = $"{item}:",
                Location  = new Point(xOff, yOff),
                Size      = new Size(55, 18),
                ForeColor = YellowColor,
                Font      = new Font("Courier New", 8, FontStyle.Bold),
            });
            layoutInfo.Controls.Add(new Label
            {
                Text      = label,
                Location  = new Point(xOff + 58, yOff),
                Size      = new Size(200, 18),
                ForeColor = GreenColor,
                Font      = new Font("Courier New", 8, FontStyle.Bold),
            });
            layoutInfo.Controls.Add(new Label
            {
                Text      = hint,
                Location  = new Point(xOff, yOff + 18),
                Size      = new Size(280, 16),
                ForeColor = SubtextColor,
                Font      = new Font("Courier New", 7),
            });
        }

        AddSectionHeader(scroll, "DATO 1  →  Item 1  (produksjonsdato)", ref y);
        BuildDateBlock(scroll, ref y, "d1",
            ref _date1Item, ref _date1Format, ref _date1Days,
            ref _date1Manual, ref _date1DaysSp, ref _date1Picker, ref _date1Preview,
            optional: false);

        AddSectionHeader(scroll, "DATO 2  →  Item 2  (best before)", ref y);
        var d2Panel = AddPanel(scroll, ref y, 20);
        _date2Enable = new CheckBox
        {
            Text      = "Send dato 2",
            Location  = new Point(10, 0),
            AutoSize  = true,
            Checked   = true,
            ForeColor = TextColor,
            Font      = new Font("Courier New", 10),
        };
        _date2Enable.CheckedChanged += (_, _) => UpdateDate2Enabled();
        d2Panel.Controls.Add(_date2Enable);
        BuildDateBlock(scroll, ref y, "d2",
            ref _date2Item, ref _date2Format, ref _date2Days,
            ref _date2Manual, ref _date2DaysSp, ref _date2Picker, ref _date2Preview,
            optional: true);

        AddSectionHeader(scroll, "FRI TEKST  →  Item 3", ref y);
        var extraPanel   = AddPanel(scroll, ref y, 100);
        var extraDefaults = new (int item, string text)[] { (3, ""), (5, ""), (6, "") };
        for (int i = 0; i < 3; i++)
        {
            int yOff = 8 + i * 30;
            AddLabel(extraPanel, "Item:", 10, yOff + 5);
            var itemSp = new NumericUpDown
            {
                Location  = new Point(55, yOff),
                Size      = new Size(60, 28),
                Minimum   = 1, Maximum = 50,
                Value     = extraDefaults[i].item,
                BackColor = Color.FromArgb(13, 27, 42),
                ForeColor = YellowColor,
                Font      = new Font("Courier New", 10, FontStyle.Bold),
            };
            AddLabel(extraPanel, "Tekst:", 125, yOff + 5);
            var textBox = new TextBox
            {
                Location    = new Point(175, yOff),
                Size        = new Size(220, 28),
                Text        = extraDefaults[i].text,
                BackColor   = Color.FromArgb(13, 27, 42),
                ForeColor   = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Courier New", 10),
            };
            extraPanel.Controls.AddRange([itemSp, textBox]);
            _extraFields[i] = (itemSp, textBox);
        }

        scroll.Controls.Add(new Label
        {
            Text      = "  Tip: skriv {Z/0}, {X/1} osv. for å referere til brukermønstre fra skriveren",
            Location  = new Point(16, y),
            Size      = new Size(560, 18),
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 8),
        });
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
            Value     = prefix == "d1" ? 1 : 2,
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
            Location          = new Point(160, 135),
            Size              = new Size(200, 28),
            Format            = DateTimePickerFormat.Short,
            CalendarForeColor = YellowColor,
            Visible           = false,
        };
        picker.ValueChanged += (_, _) => UpdatePreview(prefix);

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

        panel.Controls.AddRange([itemSp, formatCb, daysRb, manualRb, daysSp, picker, prevLbl, preview]);

        if (optional)
        {
            // Shift everything down to make room for the "Send dato 2" checkbox above
            foreach (Control c in panel.Controls)
                c.Top += 20;
        }

        UpdatePreview(prefix);
    }

    private void BuildTab2(TabPage page)
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };
        page.Controls.Add(main);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgColor };
        var fetchBtn = StyledButton("Hent mønstre fra skriver", BlueColor, TextColor);
        fetchBtn.Location = new Point(16, 11);
        fetchBtn.Size     = new Size(220, 34);
        fetchBtn.Click   += FetchPatterns_Click;
        _fetchStatusLabel = new Label
        {
            Text      = "Trykk for å hente mønstre lagret på skriveren",
            Location  = new Point(248, 20),
            AutoSize  = true,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 9),
        };
        topPanel.Controls.AddRange([fetchBtn, _fetchStatusLabel]);

        var listHeader = new Label
        {
            Text      = "  FUNNET MØNSTRE  —  klikk «Kopier» for å kopiere referansen",
            Dock      = DockStyle.Top,
            Height    = 26,
            BackColor = AccentColor,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _patternCards = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = PanelColor,
            Padding       = new Padding(4),
        };

        main.Controls.Add(_patternCards);
        main.Controls.Add(listHeader);
        main.Controls.Add(topPanel);
    }

    private async void FetchPatterns_Click(object? sender, EventArgs e)
    {
        if (!_connected) { ShowNotConnected(); return; }

        _fetchStatusLabel.Text = "Henter mønstre 0-19 (fast og fri) ...";
        foreach (Panel p in _patternCards.Controls.OfType<Panel>())
            foreach (PictureBox pb in p.Controls.OfType<PictureBox>())
                pb.Image?.Dispose();
        _patternCards.Controls.Clear();

        var fixedResults = new List<(byte num, byte[] data)>();
        var freeResults  = new List<(byte num, byte[] data)>();

        await Task.Run(() =>
        {
            for (byte i = 0; i < 20; i++)
            {
                try
                {
                    var data = _printer!.GetUserPattern(i);
                    if (data is { Length: > 0 })
                        fixedResults.Add((i, data));
                }
                catch { }
            }
            for (byte i = 0; i < 20; i++)
            {
                try
                {
                    var data = _printer!.GetFreePattern(i);
                    if (data is { Length: > 0 })
                        freeResults.Add((i, data));
                }
                catch { }
            }
        });

        if (fixedResults.Count > 0)
        {
            AddSectionDivider("FAST BRUKERMØNSTER  {X/N}  (attr 0x64)");
            foreach (var (num, data) in fixedResults)
                AddPatternCard(num, data, 'X');
        }
        if (freeResults.Count > 0)
        {
            AddSectionDivider("FRI BRUKERMØNSTER  {Z/N}  (attr 0x65)");
            foreach (var (num, data) in freeResults)
                AddPatternCard(num, data, 'Z');
        }

        int total = fixedResults.Count + freeResults.Count;
        _fetchStatusLabel.Text = total > 0
            ? $"{fixedResults.Count} fast  +  {freeResults.Count} fri  mønster(e) funnet"
            : "Ingen mønstre funnet på skriveren";
    }

    private void AddSectionDivider(string text)
    {
        _patternCards.Controls.Add(new Label
        {
            Text      = $"  {text}",
            Size      = new Size(560, 24),
            BackColor = AccentColor,
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 8, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 4, 0, 0),
        });
    }

    private void AddPatternCard(byte patternNum, byte[] data, char typeCode)
    {
        // Some printer responses include a 3-byte header [height, cols, patternNum] before the bitmap.
        // Strip it when the length matches exactly so the preview renders correctly.
        byte[] previewData = data;
        if (data.Length >= 5 && data[0] is >= 7 and <= 16 && data[1] >= 1
            && data.Length == 3 + data[1] * 2)
            previewData = data[3..];
        if (previewData.Length % 2 != 0)
            previewData = data;

        Bitmap bmp;
        try   { bmp = PatternEncoder.Preview(previewData, scale: 5); }
        catch { bmp = new Bitmap(1, PatternEncoder.DotHeight * 5); }

        int cardH   = PatternEncoder.DotHeight * 5 + 42;
        var card    = new Panel
        {
            Size      = new Size(560, cardH),
            BackColor = patternNum % 2 == 0 ? AccentColor : PanelColor,
            Margin    = new Padding(0, 1, 0, 0),
        };
        var refText = $"{{{typeCode}/{patternNum}}}";

        card.Controls.Add(new Label
        {
            Text      = refText,
            Location  = new Point(8, (cardH - 20) / 2),
            Size      = new Size(72, 20),
            ForeColor = YellowColor,
            Font      = new Font("Courier New", 10, FontStyle.Bold),
        });
        card.Controls.Add(new PictureBox
        {
            Location    = new Point(86, 8),
            Size        = new Size(Math.Clamp(previewData.Length / 2 * 5, 20, 340), PatternEncoder.DotHeight * 5),
            BackColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode    = PictureBoxSizeMode.Zoom,
            Image       = bmp,
        });

        var copyBtn = StyledButton($"Kopier {refText}", BlueColor, TextColor);
        copyBtn.Location = new Point(440, 8);
        copyBtn.Size     = new Size(110, 30);
        copyBtn.Click   += (_, _) => { Clipboard.SetText(refText); Log($"Kopiert: {refText}"); };
        card.Controls.Add(copyBtn);

        card.Controls.Add(new Label
        {
            Text      = "Lim inn i tekst-felt\nunder 'Send til layout'",
            Location  = new Point(440, 42),
            Size      = new Size(110, 34),
            ForeColor = SubtextColor,
            Font      = new Font("Courier New", 7),
        });

        _patternCards.Controls.Add(card);
    }

    private void ConnectBtn_Click(object? sender, EventArgs e)
    {
        if (_connected) Disconnect();
        else Connect();
    }

    private void Connect()
    {
        try
        {
            _printer = new HitachiUX2Printer("192.168.0.250", useEnq: false);
            _printer.Connect();
            _connected             = true;
            _statusLabel.Text      = "  ● Tilkoblet (192.168.0.250)";
            _statusLabel.ForeColor = GreenColor;
            _connectBtn.Text       = "KOBLE FRA";
            _connectBtn.BackColor  = RedColor;
            _sendBtn.Enabled       = true;
            Log("Tilkoblet 192.168.0.250:44818");
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
        _printer               = null;
        _connected             = false;
        _statusLabel.Text      = "  ● Ikke tilkoblet";
        _statusLabel.ForeColor = RedColor;
        _connectBtn.Text       = "KOBLE TIL";
        _connectBtn.BackColor  = BlueColor;
        _sendBtn.Enabled       = false;
        Log("Frakoblet");
    }

    // Reconnects automatically if the printer closed the TCP session (e.g. after a pattern fetch).
    private void RunWithAutoReconnect(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Log("Tilkobling tapt — gjenoppretter ...");
            try
            {
                _printer!.Reconnect();
                Log("Gjenoppkoblet");
            }
            catch (Exception rex)
            {
                throw new IOException($"Gjenoppkobling feilet: {rex.Message}", rex);
            }
            action();
        }
    }

    private void SendBtn_Click(object? sender, EventArgs e)
    {
        if (!_connected) { ShowNotConnected(); return; }

        try
        {
            var items = BuildItemsFromTab1();
            if (items.Count == 0)
            {
                MessageBox.Show("Ingen felt å sende!", "Tomt");
                return;
            }

            int job = (int)_jobNumber.Value;
            RunWithAutoReconnect(() =>
            {
                _printer!.RecallPrintData(job);
                _printer!.SendPrintContent(items);
            });
            Log($"Jobb {job} sendt: {string.Join(", ", items.Select(x => $"Item {x.Key}: {x.Value}"))}");

            var summary = string.Join("\n", items.OrderBy(x => x.Key).Select(x => $"Item {x.Key}: {x.Value}"));
            MessageBox.Show($"Jobb {job} ble oppdatert:\n\n{summary}", "✅ Sendt!");
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

        items[(int)_date1Item.Value] = GetDateString("d1");

        if (_date2Enable.Checked)
            items[(int)_date2Item.Value] = GetDateString("d2");

        foreach (var (itemSp, textBox) in _extraFields)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                items[(int)itemSp.Value] = textBox.Text.Trim();
        }

        return items;
    }

    private string GetDateString(string prefix)
    {
        var isD1   = prefix == "d1";
        var days   = isD1 ? _date1DaysSp : _date2DaysSp;
        var manual = isD1 ? _date1Manual : _date2Manual;
        var picker = isD1 ? _date1Picker : _date2Picker;
        var format = isD1 ? _date1Format : _date2Format;

        var fmt = format.SelectedItem?.ToString() ?? "dd.MM.yy";
        return manual.Checked
            ? picker.Value.ToString(fmt)
            : DateTime.Now.AddDays((double)days.Value).ToString(fmt);
    }

    private void UpdatePreview(string prefix)
    {
        var preview = prefix == "d1" ? _date1Preview : _date2Preview;
        if (preview is null) return;
        try
        {
            preview.Text      = GetDateString(prefix);
            preview.ForeColor = GreenColor;
        }
        catch
        {
            preview.Text      = "Ugyldig dato!";
            preview.ForeColor = RedColor;
        }
    }

    private void ToggleDateMode(string prefix)
    {
        var isD1   = prefix == "d1";
        var days   = isD1 ? _date1DaysSp : _date2DaysSp;
        var picker = isD1 ? _date1Picker : _date2Picker;
        var manual = isD1 ? _date1Manual : _date2Manual;

        days.Visible   = !manual.Checked;
        picker.Visible = manual.Checked;
    }

    private void UpdateDate2Enabled()
    {
        var enabled  = _date2Enable.Checked;
        var controls = _date2Item.Parent?.Controls;
        if (controls is null) return;
        foreach (Control c in controls)
            if (c != _date2Enable)
                c.Enabled = enabled;
    }

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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _printer?.Disconnect();
        base.OnFormClosed(e);
    }
}
