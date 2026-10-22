using BellTower.Core;

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
    private readonly TextBox role = new()
    {
        Width = 90
    };
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
    private string originalRole = "";
    public event Action<string>? Navigate;
    public event Action<string, string?, string?, string?>? SaveIdentity;
    public event Action<string, bool>? SetNativeSync;
    public OverviewForm()
    {
        Text = "Bell Tower - sessions";
        Size = new Size(1050, 480);
        MinimumSize = new Size(800, 350);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        foreach (var column in new[]
        {
            "Session",
            "Agent",
            "Activity",
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
            Height = 145,
            Padding = new Padding(10),
            WrapContents = true
        };
        bottom.Controls.AddRange([
            new Label { Text = "Name", AutoSize = true }, name,
            new Label { Text = "Change", AutoSize = true }, change,
            new Label { Text = "Role", AutoSize = true }, role
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
        var enableNative = new Button { Text = "Use name in Codex", AutoSize = true };
        var pauseNative = new Button { Text = "Pause Codex names", AutoSize = true };
        enableNative.Click += (_, _) => SetNative(true);
        pauseNative.Click += (_, _) => SetNative(false);
        bottom.Controls.AddRange([save, focus, enableNative, pauseNative, status]);
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
                ? AlertPolicy.ReasonText(value.State.Reason) + (value.State.RequiresAttention(now) ? "" : " (pending/dismissed)") : "";
            row.SetValues(value.Identity.Name, value.State.Provider, value.State.Activity, attention, value.Git.ToString(), value.Navigation);
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

        var changed = editing != key;
        if (changed || name.Text == originalName)
        {
            name.Text = value.Identity.ManualName ?? "";
            originalName = name.Text;
        }
        if (changed || change.Text == originalChange)
        {
            change.Text = value.Identity.Change ?? "";
            originalChange = change.Text;
        }
        if (changed || role.Text == originalRole)
        {
            role.Text = value.Identity.Role ?? "";
            originalRole = role.Text;
        }
        if (value.State.Provider == "codex")
        {
            status.Text = "Codex names: " + value.Identity.NativeSyncStatus;
        }
        else if (changed)
        {
            status.Text = "";
        }
        editing = key;
    }

    private void Save()
    {
        if (editing is null || !rows.ContainsKey(editing))
        {
            return;
        }

        SaveIdentity?.Invoke(editing, name.Text == originalName ? null : name.Text,
            change.Text == originalChange ? null : change.Text, role.Text == originalRole ? null : role.Text);
        originalName = name.Text;
        originalChange = change.Text;
        originalRole = role.Text;
        status.Text = "Saved";
    }

    private void SetNative(bool enabled)
    {
        if (editing is not null && rows.TryGetValue(editing, out var row) && row.State.Provider == "codex")
        {
            SetNativeSync?.Invoke(editing, enabled);
            status.Text = enabled ? "Codex name update requested" : "Codex name updates paused";
        }
    }

}
