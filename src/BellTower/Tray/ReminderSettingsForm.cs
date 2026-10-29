using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tray;

internal sealed class ReminderSettingsForm : Form
{
    private readonly CheckBox enabled = new() { Text = "Enable attention reminders", AutoSize = true };
    private readonly NumericUpDown initial = Number(1, 1440);
    private readonly NumericUpDown idle = Number(1, 1440);
    private readonly NumericUpDown repeat = Number(1, 1440);
    private readonly NumericUpDown maximum = Number(1, 10);
    private readonly NumericUpDown escalation = Number(1, 10);
    private readonly ConfigStore config;

    public ReminderSettingsForm(ConfigStore config)
    {
        this.config = config;
        Text = "Attention reminders";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var settings = config.Read().Reminders;
        enabled.Checked = settings.Enabled;
        initial.Value = settings.InitialDelayMinutes;
        idle.Value = settings.IdleThresholdMinutes;
        repeat.Value = settings.RepeatIntervalMinutes;
        maximum.Value = settings.MaxPerEpisode;
        escalation.Value = settings.EscalationAfterReminder;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 7,
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new(SizeType.AutoSize));
        layout.ColumnStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(enabled, 0, 0);
        layout.SetColumnSpan(enabled, 2);
        AddRow(layout, 1, "First reminder after (minutes)", initial);
        AddRow(layout, 2, "Require keyboard/mouse inactivity (minutes)", idle);
        AddRow(layout, 3, "Repeat interval (minutes)", repeat);
        AddRow(layout, 4, "Maximum reminders per episode", maximum);
        AddRow(layout, 5, "Escalation after reminder number", escalation);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static NumericUpDown Number(int minimum, int maximum) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Width = 72,
        TextAlign = HorizontalAlignment.Right
    };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 12, 0) }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void Save()
    {
        var max = decimal.ToInt32(maximum.Value);
        var escalationAt = decimal.ToInt32(escalation.Value);
        if (escalationAt > max)
        {
            MessageBox.Show(this, "The escalation reminder number cannot exceed the per-episode reminder limit.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.None;
            return;
        }

        var edited = new ReminderSettings
        {
            Enabled = enabled.Checked,
            InitialDelayMinutes = decimal.ToInt32(initial.Value),
            IdleThresholdMinutes = decimal.ToInt32(idle.Value),
            RepeatIntervalMinutes = decimal.ToInt32(repeat.Value),
            MaxPerEpisode = max,
            EscalationAfterReminder = escalationAt
        };
        config.Update(value => ApplySettings(value, edited));
    }

    internal static void ApplySettings(AppConfig value, ReminderSettings edited)
    {
        value.Reminders ??= new();
        value.Reminders.Enabled = edited.Enabled;
        value.Reminders.InitialDelayMinutes = edited.InitialDelayMinutes;
        value.Reminders.IdleThresholdMinutes = edited.IdleThresholdMinutes;
        value.Reminders.RepeatIntervalMinutes = edited.RepeatIntervalMinutes;
        value.Reminders.MaxPerEpisode = edited.MaxPerEpisode;
        value.Reminders.EscalationAfterReminder = edited.EscalationAfterReminder;
    }
}
