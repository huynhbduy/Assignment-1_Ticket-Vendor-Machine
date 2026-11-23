using System.Drawing;
using QRCoder;

namespace SmartTicketing;

public sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(25, 51, 84);
    private static readonly Color Blue = Color.FromArgb(37, 108, 235);
    private static readonly Color Page = Color.FromArgb(232, 238, 246);
    private static readonly Color Ink = Color.FromArgb(39, 58, 82);
    private static readonly Color Muted = Color.FromArgb(88, 108, 132);
    private static readonly Color Field = Color.FromArgb(244, 247, 251);
    private static readonly Color Border = Color.FromArgb(158, 178, 203);

    private readonly Panel _card = new();
    private readonly Panel _content = new();
    private readonly Panel _footer = new();
    private readonly Label _heading = new();
    private readonly Label _subtitle = new();
    private readonly Label _status = new();
    private readonly ComboBox _destinations = new();
    private readonly NumericUpDown _quantity = new();
    private readonly RadioButton _cardPayment = new();
    private readonly RadioButton _qrPayment = new();
    private readonly ComboBox _simulation = new();
    private readonly Button _primary = new();
    private readonly Button _secondary = new();
    private readonly System.Windows.Forms.Timer _qrTimer = new() { Interval = 1000 };
    private readonly SqlOrderStore? _store;
    private List<Destination> _routeList = [];
    private Screen _screen = Screen.Welcome;
    private PurchaseResult? _lastPurchase;
    private Guid? _pendingOrderId;
    private DateTime _qrExpiresAt;
    private PictureBox? _qrPicture;

    private enum Screen { Welcome, Routes, Payment, Qr, Result }

    public MainForm()
    {
        Text = "Smart Ticketing System";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(900, 680);
        BackColor = Page;
        Font = new Font("Segoe UI", 12F);

        var configuredConnection = Environment.GetEnvironmentVariable("SMARTTICKETING_SQL");
        if (!string.Equals(configuredConnection, "offline", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = string.IsNullOrWhiteSpace(configuredConnection)
                ? @"Server=(localdb)\MSSQLLocalDB;Database=SmartTicketing_VSRun;Trusted_Connection=True;TrustServerCertificate=True"
                : configuredConnection;
            _store = new SqlOrderStore(connectionString);
        }

        var page = new Panel { Dock = DockStyle.Fill, BackColor = Page, Padding = new Padding(48, 34, 48, 34) };
        _card.Dock = DockStyle.Fill;
        _card.BackColor = Color.White;
        _card.Padding = new Padding(0);
        _card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(101, 122, 146), 3);
            var rect = _card.ClientRectangle;
            rect.Width -= 2;
            rect.Height -= 2;
            e.Graphics.DrawRectangle(pen, rect);
        };

        var banner = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Navy, Padding = new Padding(40, 0, 20, 0) };
        var brand = new Label
        {
            Text = "SMART TICKETING", Dock = DockStyle.Fill, ForeColor = Color.White,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
        };
        banner.Controls.Add(brand);

        _footer.Dock = DockStyle.Bottom;
        _footer.Height = 118;
        _footer.Padding = new Padding(48, 18, 48, 28);
        _footer.BackColor = Color.White;
        _primary.Dock = DockStyle.Fill;
        _primary.Height = 56;
        _primary.FlatStyle = FlatStyle.Flat;
        _primary.FlatAppearance.BorderSize = 0;
        _primary.BackColor = Blue;
        _primary.ForeColor = Color.White;
        _primary.Font = new Font("Segoe UI", 15F, FontStyle.Regular);
        _primary.Cursor = Cursors.Hand;
        _primary.Click += async (_, _) => await PrimaryActionAsync();
        _secondary.Click += (_, _) =>
        {
            if (_screen == Screen.Qr)
            {
                _qrTimer.Stop();
                _pendingOrderId = null;
                ShowScreen(Screen.Payment);
            }
        };
        _footer.Controls.Add(_primary);

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(48, 25, 48, 12);
        _content.BackColor = Color.White;
        _heading.AutoSize = true;
        _heading.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
        _heading.ForeColor = Navy;
        _heading.Location = new Point(0, 0);
        _subtitle.AutoSize = true;
        _subtitle.Font = new Font("Segoe UI", 12F);
        _subtitle.ForeColor = Muted;
        _subtitle.Location = new Point(0, 48);
        _status.AutoSize = true;
        _status.ForeColor = Muted;
        _status.Font = new Font("Segoe UI", 10.5F);
        _status.Location = new Point(0, 405);
        _content.Controls.AddRange([_heading, _subtitle, _status]);

        _destinations.DropDownStyle = ComboBoxStyle.DropDownList;
        _destinations.Font = new Font("Segoe UI", 14F);
        _quantity.Minimum = 1;
        _quantity.Maximum = 10;
        _quantity.Value = 1;
        _quantity.Font = new Font("Segoe UI", 13F);
        _quantity.Width = 100;
        _cardPayment.Text = "Credit / debit card";
        _qrPayment.Text = "QR wallet (MoMo / VNPay / ZaloPay)";
        _cardPayment.AutoSize = _qrPayment.AutoSize = true;
        _cardPayment.Font = _qrPayment.Font = new Font("Segoe UI", 14F);
        _cardPayment.ForeColor = _qrPayment.ForeColor = Ink;
        _cardPayment.Checked = true;
        _simulation.DropDownStyle = ComboBoxStyle.DropDownList;
        _simulation.Items.AddRange(["Approved", "Declined", "Unknown / timeout"]);
        _simulation.SelectedIndex = 0;
        _qrTimer.Tick += (_, _) => UpdateQrCountdown();

        page.Controls.Add(_card);
        _card.Controls.Add(_content);
        _card.Controls.Add(_footer);
        _card.Controls.Add(banner);
        Controls.Add(page);
        Resize += (_, _) => LayoutScreen();
        ShowScreen(Screen.Welcome);
    }

    private void ShowScreen(Screen screen)
    {
        if (_qrPicture is not null)
        {
            _qrPicture.Image?.Dispose();
            _qrPicture = null;
        }
        _screen = screen;
        _content.Controls.Clear();
        _content.Controls.AddRange([_heading, _subtitle, _status]);
        _status.Text = string.Empty;
        _secondary.Visible = false;
        _footer.Controls.Clear();

        switch (screen)
        {
            case Screen.Welcome:
                _heading.Text = "Welcome";
                _subtitle.Text = "English interface";
                AddField("Language: English", 114, 56);
                AddField("Tap Start to buy a ticket", 190, 56);
                SetPrimary("START");
                break;
            case Screen.Routes:
                _heading.Text = "Choose destination";
                _subtitle.Text = _store is null
                    ? "Select a route and quantity • Offline demo, purchases are not saved"
                    : "Select a route and quantity";
                Place(_destinations, 114, 56);
                AddQuantityRow();
                var total = new Label { Name = "routeTotal", Text = "Total: calculated", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", 14F), Location = new Point(330, 207) };
                _content.Controls.Add(total);
                _quantity.ValueChanged += (_, _) => UpdateRouteTotal();
                _destinations.SelectedIndexChanged += (_, _) => UpdateRouteTotal();
                SetPrimary("CONTINUE");
                break;
            case Screen.Payment:
                _heading.Text = "Select payment";
                _subtitle.Text = _store is null
                    ? $"Order total: {CurrentTotal():N0} VND • Offline demo, not saved"
                    : $"Order total: {CurrentTotal():N0} VND";
                _cardPayment.Location = new Point(8, 122);
                _qrPayment.Location = new Point(8, 166);
                _content.Controls.AddRange([_cardPayment, _qrPayment]);
                AddField("Review destination and fare before paying", 225, 56);
                var demoLabel = new Label { Text = "Demo payment result", AutoSize = true, ForeColor = Muted, Location = new Point(0, 310) };
                _simulation.Location = new Point(0, 339);
                _simulation.Width = 300;
                _content.Controls.AddRange([demoLabel, _simulation]);
                SetPrimary("PAY");
                break;
            case Screen.Qr:
                _heading.Text = "Scan to pay";
                _subtitle.Text = "Demo QR expires in 02:00 • simulated gateway response";
                AddQrPanel();
                AddField("Scan the demo code to view this order's details", 288, 48);
                AddField("Choose Approved / Declined / Unknown below, then confirm", 344, 48);
                var qrOutcome = new Label { Text = "Simulated gateway result", AutoSize = true, ForeColor = Muted, Location = new Point(0, 418) };
                _content.Controls.Add(qrOutcome);
                _simulation.Location = new Point(230, 410);
                _simulation.Width = 300;
                _content.Controls.Add(_simulation);
                SetQrActions();
                break;
            case Screen.Result:
                var approved = _lastPurchase?.Status == OrderStatus.Paid;
                _heading.Text = approved ? "Ticket issued" : "Unsuccessful";
                _subtitle.Text = approved
                    ? _store is null
                        ? "Payment simulated • Offline demo, order was not saved"
                        : "Payment confirmed • Demo payment simulated"
                    : _lastPurchase?.Status == OrderStatus.Declined
                        ? "Payment declined • Demo payment simulated"
                        : "Payment status unknown • Reconciliation required";
                var order = _lastPurchase;
                var shortOrderId = order is null ? "—" : order.OrderId.ToString("N")[..8];
                AddField($"Order: {shortOrderId}", 114, 56);
                var ticketCode = order?.TicketCodes.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (approved && !string.IsNullOrWhiteSpace(ticketCode))
                {
                    AddTicketQrPanel(ticketCode);
                    AddField("Scan this QR to read your ticket code", 372, 56);
                }
                else
                {
                    AddField("No ticket issued", 190, 56);
                    AddField("You can return to the start screen", 266, 56);
                }
                SetPrimary("DONE");
                break;
        }
        LayoutScreen();
    }

    private void SetPrimary(string text)
    {
        _primary.Text = text;
        _footer.Controls.Add(_primary);
    }

    private void SetQrActions()
    {
        _primary.Text = "CONFIRM SIMULATED RESULT";
        _primary.Dock = DockStyle.Fill;
        _secondary.Text = "CANCEL";
        _secondary.Dock = DockStyle.Fill;
        _secondary.Visible = true;
        _secondary.BackColor = Color.White;
        _secondary.ForeColor = Navy;
        _secondary.FlatStyle = FlatStyle.Flat;
        _secondary.FlatAppearance.BorderColor = Border;
        _secondary.FlatAppearance.BorderSize = 1;

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        actions.Controls.Add(_primary, 0, 0);
        actions.Controls.Add(_secondary, 1, 0);
        _footer.Controls.Add(actions);
    }

    private void AddField(string text, int y, int height)
    {
        var box = new Panel { Location = new Point(0, y), Size = new Size(Math.Max(300, _content.ClientSize.Width), height), BackColor = Field };
        box.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1.5F);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 2, box.Height - 2);
        };
        var label = new Label
        {
            Text = text, Dock = DockStyle.Fill, Padding = new Padding(24, 0, 12, 0),
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink, Font = new Font("Segoe UI", 14F)
        };
        box.Controls.Add(label);
        _content.Controls.Add(box);
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
    }

    private void Place(Control control, int y, int height)
    {
        control.Location = new Point(0, y);
        control.Size = new Size(Math.Max(300, _content.ClientSize.Width), height);
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _content.Controls.Add(control);
    }

    private void AddQuantityRow()
    {
        var row = new Panel { Location = new Point(0, 190), Size = new Size(Math.Max(300, _content.ClientSize.Width), 56), BackColor = Field };
        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1.5F);
            e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 2, row.Height - 2);
        };
        var label = new Label { Text = "Quantity:", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", 14F), Location = new Point(24, 13) };
        _quantity.Location = new Point(135, 9);
        _quantity.Size = new Size(100, 36);
        row.Controls.Add(label);
        row.Controls.Add(_quantity);
        _content.Controls.Add(row);
    }

    private void AddQrPanel()
    {
        var box = new Panel { Location = new Point(0, 100), Size = new Size(Math.Max(300, _content.ClientSize.Width), 174), BackColor = Color.White };
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        box.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1.5F);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 2, box.Height - 2);
        };
        _qrPicture = new PictureBox
        {
            Size = new Size(154, 154), SizeMode = PictureBoxSizeMode.Zoom,
            Image = CreateQrImage(BuildQrPayload()), BackColor = Color.White
        };
        _qrPicture.Location = new Point((box.Width - _qrPicture.Width) / 2, 8);
        box.Controls.Add(_qrPicture);
        _content.Controls.Add(box);
    }

    private void AddTicketQrPanel(string ticketCode)
    {
        var box = new Panel { Location = new Point(0, 190), Size = new Size(Math.Max(300, _content.ClientSize.Width), 170), BackColor = Color.White };
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        box.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1.5F);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 2, box.Height - 2);
        };
        _qrPicture = new PictureBox
        {
            Size = new Size(154, 154), SizeMode = PictureBoxSizeMode.Zoom,
            Image = CreateQrImage(ticketCode), BackColor = Color.White
        };
        _qrPicture.Location = new Point((box.Width - _qrPicture.Width) / 2, 8);
        box.Controls.Add(_qrPicture);
        _content.Controls.Add(box);
    }

    private string BuildQrPayload()
    {
        _pendingOrderId ??= Guid.NewGuid();
        _qrExpiresAt = DateTime.Now.AddMinutes(2);
        var destination = _routeList[_destinations.SelectedIndex];
        return $"SMART-TICKETING-DEMO|ORDER={_pendingOrderId:N}|DESTINATION={destination.Name}|QUANTITY={(int)_quantity.Value}|AMOUNT={CurrentTotal()} VND|EXPIRES={_qrExpiresAt:O}";
    }

    private static Image CreateQrImage(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(7);
        using var stream = new MemoryStream(png);
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private void UpdateQrCountdown()
    {
        if (_screen != Screen.Qr)
            return;
        var seconds = Math.Max(0, (int)(_qrExpiresAt - DateTime.Now).TotalSeconds);
        _subtitle.Text = seconds == 0
            ? "Demo QR expired • return to payment and start again"
            : $"Demo QR expires in {seconds / 60:00}:{seconds % 60:00} • simulated gateway response";
        _primary.Enabled = seconds > 0;
    }

    private void LayoutScreen()
    {
        var width = Math.Max(300, _content.ClientSize.Width);
        foreach (Control control in _content.Controls)
        {
            if (control is Panel panel && panel.BackColor == Field)
                panel.Width = width;
            else if (control == _destinations)
                control.Width = width;
            else if (control.Controls.Contains(_quantity))
                control.Width = width;
            else if (_qrPicture is not null && control.Controls.Contains(_qrPicture))
            {
                control.Width = width;
                _qrPicture.Left = (width - _qrPicture.Width) / 2;
            }
            else if (control.Name == "routeTotal")
                control.Left = Math.Min(330, width - control.Width - 12);
        }
    }

    private void UpdateRouteTotal()
    {
        if (_screen != Screen.Routes || _routeList.Count == 0 || _destinations.SelectedIndex < 0)
            return;
        var total = CurrentTotal();
        var label = _content.Controls.Find("routeTotal", false).FirstOrDefault() as Label;
        if (label is not null)
            label.Text = $"Total: {total:N0} VND";
    }

    private long CurrentTotal()
    {
        if (_destinations.SelectedIndex < 0 || _destinations.SelectedIndex >= _routeList.Count)
            return 0;
        return checked(_routeList[_destinations.SelectedIndex].FareVnd * (long)_quantity.Value);
    }

    private async Task PrimaryActionAsync()
    {
        switch (_screen)
        {
            case Screen.Welcome:
                await LoadRoutesAsync();
                break;
            case Screen.Routes:
                if (_destinations.SelectedIndex < 0)
                {
                    _status.Text = "No routes are available. Check the database connection.";
                    return;
                }
                ShowScreen(Screen.Payment);
                break;
            case Screen.Payment:
                if (_qrPayment.Checked)
                {
                    if (_simulation.SelectedIndex != 0)
                    {
                        await CompletePurchaseAsync();
                        break;
                    }
                    _pendingOrderId = Guid.NewGuid();
                    ShowScreen(Screen.Qr);
                    _qrTimer.Start();
                    UpdateQrCountdown();
                    break;
                }
                await CompletePurchaseAsync();
                break;
            case Screen.Qr:
                _qrTimer.Stop();
                await CompletePurchaseAsync();
                break;
            case Screen.Result:
                _qrTimer.Stop();
                _pendingOrderId = null;
                _lastPurchase = null;
                ShowScreen(Screen.Welcome);
                break;
        }
    }

    private async Task LoadRoutesAsync()
    {
        if (_store is null)
        {
            _routeList =
            [
                new Destination(Guid.Parse("11111111-1111-1111-1111-111111111111"), "District 1 Central Station", 7000),
                new Destination(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Tan Son Nhat Airport", 15000),
                new Destination(Guid.Parse("33333333-3333-3333-3333-333333333333"), "University Campus", 9000)
            ];
            _destinations.DataSource = _routeList.Select(d => $"{d.Name}  —  {d.FareVnd:N0} VND").ToList();
            ShowScreen(Screen.Routes);
            UpdateRouteTotal();
            return;
        }
        _primary.Enabled = false;
        try
        {
            _routeList = (await _store.GetDestinationsAsync()).ToList();
            if (_routeList.Count == 0)
            {
                _status.Text = "No active routes found. Run schema.sql and seed.sql.";
                return;
            }
            _destinations.DataSource = _routeList.Select(d => $"{d.Name}  —  {d.FareVnd:N0} VND").ToList();
            ShowScreen(Screen.Routes);
            UpdateRouteTotal();
        }
        catch (Exception ex)
        {
            _status.Text = "Database error: " + ex.Message;
        }
        finally { _primary.Enabled = true; }
    }

    private async Task CompletePurchaseAsync()
    {
        if (_destinations.SelectedIndex < 0 || _destinations.SelectedIndex >= _routeList.Count)
        {
            _status.Text = "Select an available route before purchase.";
            return;
        }
        _primary.Enabled = false;
        try
        {
            await Task.Delay(450);
            var destination = _routeList[_destinations.SelectedIndex];
            var quantity = (int)_quantity.Value;
            var method = _qrPayment.Checked ? PaymentMethod.QrWallet : PaymentMethod.Card;
            var orderId = _pendingOrderId ?? Guid.NewGuid();
            var now = DateTime.UtcNow;
            var outcome = _simulation.SelectedIndex switch
            {
                1 => OrderStatus.Declined,
                2 => OrderStatus.ReconciliationRequired,
                _ => OrderStatus.Paid
            };
            var codes = outcome == OrderStatus.Paid
                ? string.Join(";", Enumerable.Range(1, quantity).Select(i => $"{destination.Id:N}-{orderId:N}-{i}"))
                : string.Empty;
            _lastPurchase = new PurchaseResult(orderId, destination.Id, destination.Name, quantity,
                checked(destination.FareVnd * quantity), method, outcome, codes, now);
            if (_store is not null)
                await _store.SavePurchaseAsync(_lastPurchase);
            _pendingOrderId = null;
            ShowScreen(Screen.Result);
        }
        catch (Exception ex)
        {
            _status.Text = "Purchase could not be saved: " + ex.Message;
        }
        finally { _primary.Enabled = true; }
    }
}
