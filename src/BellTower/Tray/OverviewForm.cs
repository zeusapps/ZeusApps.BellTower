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
    public event Action<string>? Navigate;
    public event Action<SessionIdentity>? SaveIdentity;
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
            Height = 105,
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
        bottom.Controls.AddRange([save, focus, status]);
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
        if (grid.SelectedRows.Count == 0 || grid.SelectedRows[0].Tag is not string key || key == editing || !rows.TryGetValue(key, out var value))
        {
            return;
        }

        editing = key;
        name.Text = value.Identity.ManualName ?? "";
        change.Text = value.Identity.Change ?? "";
        role.Text = value.Identity.Role ?? "";
    }

    private void Save()
    {
        if (editing is null || !rows.TryGetValue(editing, out var row))
        {
            return;
        }

        var identity = row.Identity with
        {
            ManualName = IdentityText.Optional(name.Text),
            Change = IdentityText.Optional(change.Text),
            Role = IdentityText.Optional(role.Text)
        };
        SaveIdentity?.Invoke(identity);
        status.Text = "Saved";
    }

}
