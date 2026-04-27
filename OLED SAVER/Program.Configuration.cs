using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using File = System.IO.File;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private static void ShowOverlayOpacityDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Overlay Opacity Settings";
                form.Size = new Size(350, 200);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var opacityLabel = new Label
                {
                    Text = "Normal Opacity (0.1 - 1.0):",
                    Location = new Point(20, 20),
                    Size = new Size(150, 20)
                };

                var opacityTextBox = new TextBox
                {
                    Text = _overlayOpacity.ToString("F1"),
                    Location = new Point(180, 20),
                    Size = new Size(100, 20)
                };

                var fadedOpacityLabel = new Label
                {
                    Text = "Faded Opacity (0.1 - 1.0):",
                    Location = new Point(20, 60),
                    Size = new Size(150, 20)
                };

                var fadedOpacityTextBox = new TextBox
                {
                    Text = _overlayFadedOpacity.ToString("F1"),
                    Location = new Point(180, 60),
                    Size = new Size(100, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(120, 120),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(200, 120),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.AddRange(new Control[]
                {
                    opacityLabel, opacityTextBox, fadedOpacityLabel, fadedOpacityTextBox, okButton, cancelButton
                });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (double.TryParse(opacityTextBox.Text, out double opacity) && opacity >= 0.1 && opacity <= 1.0)
                    {
                        _overlayOpacity = opacity;
                    }

                    if (double.TryParse(fadedOpacityTextBox.Text, out double fadedOpacity) && fadedOpacity >= 0.1 && fadedOpacity <= 1.0)
                    {
                        _overlayFadedOpacity = fadedOpacity;
                    }

                    SaveSettings();
                }
            }
        }

        private static void ShowActivityThresholdDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Taskbar Threshold Settings";
                form.Size = new Size(355, 195);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Taskbar Threshold (mouse movement distance):",
                    Location = new Point(20, 20),
                    Size = new Size(280, 20)
                };

                var numericUpDown = new NumericUpDown
                {
                    Location = new Point(20, 50),
                    Size = new Size(100, 23),
                    Minimum = 10,
                    Maximum = 1000,
                    Value = _activityThreshold
                };

                var descLabel = new Label
                {
                    Text = "Distance from bottom edge of screen to trigger taskbar\nLower values = closer to edge, Higher values = further from edge",
                    Location = new Point(20, 80),
                    Size = new Size(280, 40),
                    ForeColor = Color.Gray
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(180, 130),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(260, 130),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.AddRange(new Control[] { label, numericUpDown, descLabel, okButton, cancelButton });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    _activityThreshold = (int)numericUpDown.Value;
                    SaveSettings();
                }
            }
        }

        private static void ShowTimeoutDialog(MonitorSettings setting)
        {
            using (var form = new Form())
            {
                form.Text = $"Overlay Timeout Settings - {setting.DisplayName}";
                form.Size = new Size(300, 150);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Overlay Timeout (seconds):",
                    Location = new Point(20, 20),
                    Size = new Size(140, 20)
                };

                var textBox = new TextBox
                {
                    Text = setting.TimeoutSeconds.ToString(),
                    Location = new Point(170, 18),
                    Size = new Size(100, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(120, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(200, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(textBox.Text, out int timeout) && timeout > 0)
                    {
                        setting.TimeoutSeconds = timeout;
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid timeout value (positive integer).", "Invalid Input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static void ShowTaskbarTimeoutDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Taskbar Timeout Settings";
                form.Size = new Size(300, 150);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Taskbar Timeout (seconds):",
                    Location = new Point(20, 20),
                    Size = new Size(160, 20)
                };

                var textBox = new TextBox
                {
                    Text = _taskbarTimeoutSeconds.ToString(),
                    Location = new Point(190, 18),
                    Size = new Size(70, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(120, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                form.Controls.AddRange(new Control[] { label, textBox, okButton });
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(textBox.Text, out int timeout) && timeout > 0)
                    {
                        _taskbarTimeoutSeconds = timeout;
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid timeout value (positive integer).", "Invalid Input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static void ShowOverlayTimeoutDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Draw Black Overlay Timeout Settings";
                form.Size = new Size(300, 150);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Draw Black Overlay Timeout (seconds):",
                    Location = new Point(20, 20),
                    Size = new Size(200, 20)
                };

                var textBox = new TextBox
                {
                    Text = _drawBlackOverlayEnabledTimeoutSeconds.ToString(),
                    Location = new Point(220, 18),
                    Size = new Size(50, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(100, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                form.Controls.AddRange(new Control[] { label, textBox, okButton });
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(textBox.Text, out int timeout) && timeout > 0)
                    {
                        _drawBlackOverlayEnabledTimeoutSeconds = timeout;
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid timeout value (positive integer).", "Invalid Input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static void ShowDisplayOffTimeoutDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Display Off Timeout Settings";
                form.Size = new Size(300, 150);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Display Off Timeout (seconds):",
                    Location = new Point(20, 20),
                    Size = new Size(180, 20)
                };

                var textBox = new TextBox
                {
                    Text = _displayOffTimeoutSeconds.ToString(),
                    Location = new Point(200, 18),
                    Size = new Size(70, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(120, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                form.Controls.AddRange(new Control[] { label, textBox, okButton });
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(textBox.Text, out int timeout) && timeout > 0)
                    {
                        _displayOffTimeoutSeconds = timeout;
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Enter a valid timeout (positive integer).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static void ShowDesktopIconsTimeoutDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Desktop Icons Timeout Settings";
                form.Size = new Size(300, 150);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = "Desktop Icons Timeout (seconds):",
                    Location = new Point(20, 20),
                    Size = new Size(200, 20)
                };

                var textBox = new TextBox
                {
                    Text = _desktopIconsTimeoutSeconds.ToString(),
                    Location = new Point(220, 18),
                    Size = new Size(50, 20)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(100, 60),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                form.Controls.AddRange(new Control[] { label, textBox, okButton });
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(textBox.Text, out int timeout) && timeout > 0)
                    {
                        _desktopIconsTimeoutSeconds = timeout;
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid timeout value (positive integer).", "Invalid Input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private static List<string> GetAllActiveProcessNamesWithWindows()
        {
            var processesWithWindows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);

                    try
                    {
                        var proc = Process.GetProcessById((int)pid);
                        if (!string.IsNullOrEmpty(proc.ProcessName))
                            processesWithWindows.Add(proc.ProcessName);
                    }
                    catch
                    {
                    }
                }
                return true;
            }, IntPtr.Zero);

            return processesWithWindows.ToList();
        }

        private static void ShowExclusionsDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Manage Excluded Processes";
                form.Size = new Size(520, 520);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = Color.FromArgb(32, 32, 32);
                form.ForeColor = Color.White;

                const int margin = 20;
                const int spacing = 10;
                const int buttonHeight = 30;
                const int textBoxHeight = 23;
                const int listBoxWidth = 200;
                const int listBoxHeight = 280;

                var excludedLabel = new Label
                {
                    Text = "Excluded Processes:",
                    Location = new Point(margin, margin),
                    Size = new Size(listBoxWidth, 20),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(32, 32, 32)
                };

                var activeLabel = new Label
                {
                    Text = "Active Processes:",
                    Location = new Point(margin * 2 + listBoxWidth + spacing, margin),
                    Size = new Size(listBoxWidth, 20),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(32, 32, 32)
                };

                var excludedListBox = new ListBox
                {
                    Location = new Point(margin, margin + 25),
                    Size = new Size(listBoxWidth, listBoxHeight),
                    SelectionMode = SelectionMode.One,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(24, 24, 24),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                foreach (var excluded in _excludedWindowTitles)
                {
                    excludedListBox.Items.Add(excluded);
                }

                var activeProcessesListBox = new ListBox
                {
                    Location = new Point(margin * 2 + listBoxWidth + spacing, margin + 25),
                    Size = new Size(listBoxWidth, listBoxHeight),
                    SelectionMode = SelectionMode.One,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(24, 24, 24),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                int buttonsY = margin + 25 + listBoxHeight + spacing;

                var addButton = new Button
                {
                    Text = "Add",
                    Location = new Point(margin * 2 + listBoxWidth + spacing, buttonsY),
                    Size = new Size(80, buttonHeight),
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                addButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);

                var removeButton = new Button
                {
                    Text = "Remove",
                    Location = new Point(margin, buttonsY),
                    Size = new Size(80, buttonHeight),
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                removeButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);

                int manualY = buttonsY + buttonHeight + spacing;

                var manualLabel = new Label
                {
                    Text = "Manual add:",
                    Location = new Point(margin, manualY),
                    Size = new Size(80, 20),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(32, 32, 32)
                };

                var manualInput = new TextBox
                {
                    Location = new Point(margin, manualY + 25),
                    Size = new Size(listBoxWidth - 80, textBoxHeight),
                    PlaceholderText = "Process name...",
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(24, 24, 24),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                var addManualButton = new Button
                {
                    Text = "Add",
                    Location = new Point(margin + listBoxWidth - 75, manualY + 25),
                    Size = new Size(75, buttonHeight),
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                addManualButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);

                int bottomY = form.ClientSize.Height - buttonHeight - margin;

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(form.ClientSize.Width - 160, bottomY),
                    Size = new Size(75, buttonHeight),
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                okButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(form.ClientSize.Width - 80, bottomY),
                    Size = new Size(75, buttonHeight),
                    DialogResult = DialogResult.Cancel,
                    Font = new Font("Segoe UI", 9F),
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                cancelButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);

                addButton.Click += (s, e) =>
                {
                    if (activeProcessesListBox.SelectedItem != null)
                    {
                        var selectedProcess = activeProcessesListBox.SelectedItem.ToString();
                        if (!excludedListBox.Items.Contains(selectedProcess))
                        {
                            excludedListBox.Items.Add(selectedProcess);
                        }
                    }
                };

                removeButton.Click += (s, e) =>
                {
                    if (excludedListBox.SelectedItem != null)
                    {
                        excludedListBox.Items.Remove(excludedListBox.SelectedItem);
                    }
                };

                addManualButton.Click += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(manualInput.Text))
                    {
                        var processName = manualInput.Text.Trim();
                        if (!excludedListBox.Items.Contains(processName))
                        {
                            excludedListBox.Items.Add(processName);
                            manualInput.Clear();
                        }
                    }
                };

                manualInput.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        addManualButton.PerformClick();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                };

                void UpdateActiveProcesses()
                {
                    try
                    {
                        var activeProcesses = GetAllActiveProcessNamesWithWindows();
                        var currentItems = activeProcessesListBox.Items.Cast<string>().ToHashSet();
                        var newItems = new HashSet<string>(activeProcesses);

                        if (!currentItems.SetEquals(newItems))
                        {
                            var selectedItem = activeProcessesListBox.SelectedItem?.ToString();
                            activeProcessesListBox.Items.Clear();

                            foreach (var process in activeProcesses.OrderBy(p => p))
                            {
                                activeProcessesListBox.Items.Add(process);
                            }

                            if (selectedItem != null && activeProcessesListBox.Items.Contains(selectedItem))
                            {
                                activeProcessesListBox.SelectedItem = selectedItem;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                var updateTimer = new Timer { Interval = 1000 };
                updateTimer.Tick += (s, e) => UpdateActiveProcesses();
                updateTimer.Start();

                UpdateActiveProcesses();

                form.Controls.AddRange(new Control[]
                {
                    excludedLabel,
                    activeLabel,
                    excludedListBox,
                    activeProcessesListBox,
                    addButton,
                    removeButton,
                    manualLabel,
                    manualInput,
                    addManualButton,
                    okButton,
                    cancelButton
                });

                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                var result = form.ShowDialog();
                updateTimer.Stop();
                updateTimer.Dispose();

                if (result == DialogResult.OK)
                {
                    _excludedWindowTitles.Clear();
                    foreach (var item in excludedListBox.Items)
                    {
                        _excludedWindowTitles.Add(item.ToString());
                    }
                    InvalidateVideoDetectionCache();
                    SaveSettings();
                }
            }
        }

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OLEDSaver",
            "settings.json"
        );

        public class AppSettings
        {
            public Dictionary<string, MonitorSettings> MonitorSettings { get; set; } = new();

            public int TaskbarThreshold { get; set; } = 150;
            public bool TaskbarHidingEnabled { get; set; }
            public bool DesktopIconsHidingEnabled { get; set; }
            public bool DrawBlackOverlayEnabled { get; set; }
            public bool ScreenOffEnabled { get; set; }
            public int TaskbarTimeoutSeconds { get; set; }
            public int DesktopIconsTimeoutSeconds { get; set; }
            public int OverlayWindowsTimeoutSeconds { get; set; }
            public int DisplayOffTimeoutSeconds { get; set; }
            public List<string> ExcludedWindowTitles { get; set; } = new List<string>();
            public bool OverlayRoundedCorners { get; set; } = true;
            public double OverlayOpacity { get; set; } = 0.93;
            public double OverlayFadedOpacity { get; set; } = 0.6;
        }

        private static void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    TaskbarThreshold = _activityThreshold,
                    TaskbarHidingEnabled = _taskbarHidingEnabled,
                    DesktopIconsHidingEnabled = _desktopIconsHidingEnabled,
                    DrawBlackOverlayEnabled = _drawBlackOverlayEnabled,
                    ScreenOffEnabled = _screenOffEnabled,
                    TaskbarTimeoutSeconds = _taskbarTimeoutSeconds,
                    DesktopIconsTimeoutSeconds = _desktopIconsTimeoutSeconds,
                    OverlayWindowsTimeoutSeconds = _drawBlackOverlayEnabledTimeoutSeconds,
                    DisplayOffTimeoutSeconds = _displayOffTimeoutSeconds,
                    MonitorSettings = _monitorSettings,
                    ExcludedWindowTitles = _excludedWindowTitles.ToList(),
                    OverlayRoundedCorners = _overlayRoundedCorners,
                    OverlayOpacity = _overlayOpacity,
                    OverlayFadedOpacity = _overlayFadedOpacity
                };

                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return;

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    _taskbarHidingEnabled = settings.TaskbarHidingEnabled;
                    _desktopIconsHidingEnabled = settings.DesktopIconsHidingEnabled;
                    _drawBlackOverlayEnabled = settings.DrawBlackOverlayEnabled;
                    _screenOffEnabled = settings.ScreenOffEnabled;
                    _taskbarTimeoutSeconds = settings.TaskbarTimeoutSeconds;
                    _desktopIconsTimeoutSeconds = settings.DesktopIconsTimeoutSeconds;
                    _drawBlackOverlayEnabledTimeoutSeconds = settings.OverlayWindowsTimeoutSeconds;
                    _displayOffTimeoutSeconds = settings.DisplayOffTimeoutSeconds;
                    _activityThreshold = settings.TaskbarThreshold;
                    _overlayRoundedCorners = settings.OverlayRoundedCorners;
                    _overlayOpacity = settings.OverlayOpacity;
                    _overlayFadedOpacity = settings.OverlayFadedOpacity;

                    if (settings.ExcludedWindowTitles != null)
                        _excludedWindowTitles = settings.ExcludedWindowTitles.ToList();

                    if (settings.MonitorSettings != null)
                    {
                        foreach (var kvp in settings.MonitorSettings)
                        {
                            if (_monitorSettings.ContainsKey(kvp.Key))
                            {
                                _monitorSettings[kvp.Key].Enabled = kvp.Value.Enabled;
                                _monitorSettings[kvp.Key].TimeoutSeconds = kvp.Value.TimeoutSeconds;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
