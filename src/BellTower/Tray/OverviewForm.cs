using BellTower.Core;
using BellTower.Distribution;

namespace BellTower.Tray;

public sealed class OverviewForm : Form
{
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly TextBox name = new()
    {
        Width = 170
    };
    private readonly TextBox change = new()
    {
        Width = 210
    };
    // Native-name switches act on Codex only: Claude's native title write is unfinished (sync-native-session-titles).
    private Button[] nativeSwitches = [];
    private readonly Label status = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        MaximumSize = new Size(900, 0)
    };
    private readonly Dictionary<string, OverviewRow> rows = [];
    private string? editing;
    private string originalName = "";
    private string originalChange = "";
    private readonly TextBox checkout = new() { Width = 250 };
    private readonly TextBox color = new() { Width = 100 };
    private string originalCheckout = "";
    public event Action<string>? Navigate;
    public event Action<string, string?, string?>? SaveIdentity;
    public event Action<string, bool>? SetNativeSync;
    public event Action<string>? UseBellTowerName;
    public event Action<string, string?, string?, bool>? ConfigureIdentity;
    public OverviewForm()
    {
        Text = "Bell Tower - sessions" + (VersionDisplay.IsDev(Cli.ApplicationInfo.Display) ? " - " + Cli.ApplicationInfo.Display : "");
        Size = new Size(1050, 480);
        MinimumSize = new Size(800, 350);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        foreach (var column in new[]
        {
            "Session",
            "Agent",
            "Activity",
            "Health",
            "Attention",
            "Git",
            "Navigation"
        })
        {
            grid.Columns.Add(column, column);
        }

        Controls.Add(grid);
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 190,
            Padding = new Padding(10),
            WrapContents = true
        };
        bottom.Controls.AddRange([
            new Label { Text = "Name", AutoSize = true }, name,
            new Label { Text = "Change", AutoSize = true }, change
        ]);
        var save = new Button
        {
            Text = "Save",
            AutoSize = true
        };
        save.Click += (_, _) => Save();
        var focus = new Button
        {
            Text = "Focus session",
            AutoSize = true
        };
        focus.Click += (_, _) =>
        {
            if (editing is not null)
            {
                Navigate?.Invoke(editing);
            }
        };
        var enableNative = new Button { Text = "Use name in native chat", AutoSize = true };
        var pauseNative = new Button { Text = "Pause native names", AutoSize = true };
        var useBellTower = new Button { Text = "Use Bell Tower name", AutoSize = true };
        useBellTower.Click += (_, _) =>
        {
            if (editing is not null && rows.ContainsKey(editing))
            {
                UseBellTowerName?.Invoke(editing);
                status.Text = "Native title released; Bell Tower name in use";
            }
        };
        nativeSwitches = [enableNative, pauseNative];
        enableNative.Click += (_, _) => SetNative(true);
        pauseNative.Click += (_, _) => SetNative(false);
        var configure = new Button { Text = "Apply checkout/color", AutoSize = true };
        configure.Click += (_, _) => Configure(false, false);
        var clearCheckout = new Button { Text = "Clear checkout", AutoSize = true };
        clearCheckout.Click += (_, _) => { checkout.Clear(); Configure(false, false); };
        var random = new Button { Text = "Random color", AutoSize = true };
        random.Click += (_, _) => Configure(false, true);
        var reset = new Button { Text = "Reset pins", AutoSize = true };
        reset.Click += (_, _) => Configure(true, true);
        bottom.Controls.AddRange([new Label { Text = "Checkout", AutoSize = true }, checkout,
            new Label { Text = "Color", AutoSize = true }, color, configure, clearCheckout, random, reset]);
        bottom.Controls.AddRange([save, focus, useBellTower, enableNative, pauseNative, status]);
        Controls.Add(bottom);
        grid.SelectionChanged += (_, _) => SelectRow();
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is string key)
            {
                Navigate?.Invoke(key);
            }
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    public string StatusText => status.Text;
    public void SetStatus(string message) => status.Text = message;
    public void RefreshRows(IReadOnlyList<OverviewRow> values, DateTimeOffset now)
    {
        var keys = values.Select(v => v.Key).ToHashSet();
        foreach (var row in grid.Rows.Cast<DataGridViewRow>().ToArray())
        {
            if (row.Tag is string key && !keys.Contains(key))
            {
                grid.Rows.Remove(row);
            }
        }

        rows.Clear();
        foreach (var value in values)
        {
            rows[value.Key] = value;
            var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (string?)r.Tag == value.Key);
            if (row is null)
            {
                var index = grid.Rows.Add();
                row = grid.Rows[index];
                row.Tag = value.Key;
            }

            var attention = value.State.Reason is not null
                ? AlertPolicy.ReasonText(value.State.Reason) + (value.State.RequiresAttention(now) ? "" : " (pending/dismissed)")
                : value.State.UnverifiedSince is not null ? AlertPolicy.UnverifiedText + (value.State.PossiblyWaiting(now) ? " (unverified)" : " (pending/dismissed)") : "";
            row.SetValues(value.Identity.Name, value.State.Provider, value.State.Activity, value.State.AttachmentHealth, attention, value.Git.ToString(), value.Navigation);
            row.Cells[0].Style.BackColor = ColorTranslator.FromHtml(value.Identity.Palette.Accent);
            row.Cells[0].Style.ForeColor = ColorTranslator.FromHtml(value.Identity.Palette.Foreground);
        }

        SelectRow();
    }

    private void SelectRow()
    {
        if (grid.SelectedRows.Count == 0 || grid.SelectedRows[0].Tag is not string key || !rows.TryGetValue(key, out var value))
        {
            return;
        }

        foreach (var button in nativeSwitches)
        { button.Enabled = value.State.Provider == "codex"; }
        var changed = editing != key;
        if (changed || name.Text == originalName)
        {
            name.Text = value.Identity.ManualName ?? "";
            originalName = name.Text;
        }
        if (changed || change.Text == originalChange)
        {
            change.Text = value.Identity.PinnedChange ?? "";
            originalChange = change.Text;
        }
        if (changed || checkout.Text == originalCheckout)
        {
            checkout.Text = value.Identity.CheckoutPin ?? "";
            originalCheckout = checkout.Text;
        }
        if (changed || status.Text.Length == 0 || status.Text.Contains(" native name", StringComparison.Ordinal))
        {
            status.Text = NativeStatus(value);
        }
        editing = key;
    }

    public static string ProviderLabel(string provider) => provider == "codex" ? "Codex" : "Claude Code";

    // Same status and wording for both clients; the checkout provenance explains where the Git context comes from.
    public static string NativeStatus(OverviewRow row) => ProviderLabel(row.State.Provider) + " native name: " + NativeNamePolicy.StatusFor(row.State.Provider, row.Identity) +
        " | checkout (" + (row.Identity.CheckoutPin is null ? row.Identity.CheckoutSource : CheckoutPolicy.User) + "): " + row.Identity.Checkout(row.State.Directory);

    private void Save()
    {
        if (editing is null || !rows.ContainsKey(editing))
        {
            return;
        }

        SaveIdentity?.Invoke(editing, name.Text == originalName ? null : name.Text,
            change.Text == originalChange ? null : change.Text);
        originalName = name.Text;
        originalChange = change.Text;
        status.Text = "Saved";
    }

    private void SetNative(bool enabled)
    {
        if (editing is not null && rows.TryGetValue(editing, out var row))
        {
            SetNativeSync?.Invoke(editing, enabled);
            status.Text = ProviderLabel(row.State.Provider) + (enabled ? " native name update requested" : " native name updates paused");
        }
    }

    private void Configure(bool reset, bool random)
    {
        if (editing is null)
        { return; }
        try
        {
            ConfigureIdentity?.Invoke(editing, checkout.Text == originalCheckout ? null : checkout.Text,
                random ? "random" : string.IsNullOrWhiteSpace(color.Text) ? null : color.Text.Trim(), reset);
            originalCheckout = checkout.Text;
            color.Clear();
            status.Text = reset ? "Pins reset" : "Saved";
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { status.Text = error.Message; }
    }

}
