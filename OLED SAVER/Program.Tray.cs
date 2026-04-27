using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using File = System.IO.File;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private static void SetupTrayIcon()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.RenderMode = ToolStripRenderMode.Professional;
            _contextMenu.Renderer = new DarkRenderer();
            _contextMenu.BackColor = Color.FromArgb(32, 32, 32);
            _contextMenu.ForeColor = Color.White;

            Icon icon;
            try
            {
                icon = LoadTrayIconFromResources();
            }
            catch
            {
                try
                {
                    icon = new Icon(Path.Combine(AppContext.BaseDirectory, "OLED SAVER.ico"));
                }
                catch
                {
                    icon = SystemIcons.Application;
                }
            }

            _trayIcon = new NotifyIcon
            {
                Text = "OLED Saver",
                Visible = true,
                Icon = icon
            };

            var enabledFeaturesMenuItem = new ToolStripMenuItem("Enabled Features")
            {
                ForeColor = Color.White
            };

            var taskbarHidingItem = new ToolStripMenuItem("Hide Taskbar")
            {
                Checked = _taskbarHidingEnabled,
                CheckOnClick = true,
                ForeColor = Color.White
            };
            taskbarHidingItem.Click += (s, e) =>
            {
                _taskbarHidingEnabled = taskbarHidingItem.Checked;

                if (!_taskbarHidingEnabled)
                {
                    ShowTaskbarAndDesktop();
                }

                UpdateBackgroundTimerProfile();
                SaveSettings();
            };

            var desktopIconsHidingItem = new ToolStripMenuItem("Hide Desktop Icons")
            {
                Checked = _desktopIconsHidingEnabled,
                CheckOnClick = true,
                ForeColor = Color.White
            };
            desktopIconsHidingItem.Click += (s, e) =>
            {
                _desktopIconsHidingEnabled = desktopIconsHidingItem.Checked;

                if (_desktopIconsHidingEnabled)
                {
                    StartDesktopMonitoring();
                    ScheduleDesktopIconRefresh();
                }
                else
                {
                    StopDesktopMonitoring();
                    ShowDesktopIconsIfNeeded();
                }

                UpdateBackgroundTimerProfile();
                SaveSettings();
            };

            var drawBlackOverlay = new ToolStripMenuItem("Draw Black Overlay")
            {
                Checked = _drawBlackOverlayEnabled,
                CheckOnClick = true,
                ForeColor = Color.White
            };
            drawBlackOverlay.Click += (s, e) =>
            {
                _drawBlackOverlayEnabled = drawBlackOverlay.Checked;
                if (!_drawBlackOverlayEnabled)
                {
                    _overlayUpdateCts?.Cancel();
                    _drawBlackOverlay = false;
                    HideBlackOverlays();
                    _windowThatTriggeredOverlay = IntPtr.Zero;
                    _overlayFaded = false;
                }

                UpdateBackgroundTimerProfile();
                SaveSettings();
            };

            var screenOffItem = new ToolStripMenuItem("Turn Off Display")
            {
                Checked = _screenOffEnabled,
                CheckOnClick = true,
                ForeColor = Color.White
            };
            screenOffItem.Click += (s, e) =>
            {
                _screenOffEnabled = screenOffItem.Checked;
                if (!_screenOffEnabled && _screenOff)
                {
                    TurnOnDisplay();
                }

                UpdateBackgroundTimerProfile();
                SaveSettings();
            };

            enabledFeaturesMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                taskbarHidingItem, desktopIconsHidingItem, drawBlackOverlay, screenOffItem
            });

            _contextMenu.Items.Add(enabledFeaturesMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            var timeoutMenuItem = new ToolStripMenuItem("Timeout Settings")
            {
                ForeColor = Color.White
            };

            var taskbarTimeoutItem = new ToolStripMenuItem("Taskbar Timeout...")
            {
                ForeColor = Color.White
            };
            taskbarTimeoutItem.Click += (s, e) => ShowTaskbarTimeoutDialog();
            timeoutMenuItem.DropDownItems.Add(taskbarTimeoutItem);

            var desktopIconsTimeoutItem = new ToolStripMenuItem("Desktop Icons Timeout...")
            {
                ForeColor = Color.White
            };
            desktopIconsTimeoutItem.Click += (s, e) => ShowDesktopIconsTimeoutDialog();
            timeoutMenuItem.DropDownItems.Add(desktopIconsTimeoutItem);

            var drawBlackOverlayTimeoutItem = new ToolStripMenuItem("Draw Black Overlay Windows Timeout...")
            {
                ForeColor = Color.White
            };
            drawBlackOverlayTimeoutItem.Click += (s, e) => ShowOverlayTimeoutDialog();
            timeoutMenuItem.DropDownItems.Add(drawBlackOverlayTimeoutItem);

            var displayOffTimeoutItem = new ToolStripMenuItem("Display Off Timeout...")
            {
                ForeColor = Color.White
            };
            displayOffTimeoutItem.Click += (s, e) => ShowDisplayOffTimeoutDialog();
            timeoutMenuItem.DropDownItems.Add(displayOffTimeoutItem);

            var activityThresholdItem = new ToolStripMenuItem("Taskbar Threshold...")
            {
                ForeColor = Color.White
            };
            activityThresholdItem.Click += (s, e) => ShowActivityThresholdDialog();
            timeoutMenuItem.DropDownItems.Add(activityThresholdItem);

            _contextMenu.Items.Add(timeoutMenuItem);

            var overlaySettingsMenuItem = new ToolStripMenuItem("Overlay Settings")
            {
                ForeColor = Color.White
            };

            var roundedCornersItem = new ToolStripMenuItem("Rounded Corners")
            {
                Checked = _overlayRoundedCorners,
                CheckOnClick = true,
                ForeColor = Color.White
            };
            roundedCornersItem.Click += (s, e) =>
            {
                _overlayRoundedCorners = roundedCornersItem.Checked;
                SaveSettings();
            };

            var opacitySettingsItem = new ToolStripMenuItem("Opacity Settings...")
            {
                ForeColor = Color.White
            };
            opacitySettingsItem.Click += (s, e) => ShowOverlayOpacityDialog();

            overlaySettingsMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                roundedCornersItem, opacitySettingsItem
            });

            _contextMenu.Items.Add(overlaySettingsMenuItem);

            var monitorsMenuItem = new ToolStripMenuItem("Monitor Settings")
            {
                ForeColor = Color.White
            };
            foreach (var setting in _monitorSettings.Values)
            {
                var monitorItem = new ToolStripMenuItem(setting.DisplayName)
                {
                    ForeColor = Color.White
                };

                var enabledItem = new ToolStripMenuItem("Enabled")
                {
                    Checked = setting.Enabled,
                    CheckOnClick = true,
                    Tag = setting,
                    ForeColor = Color.White
                };
                enabledItem.Click += (s, e) =>
                {
                    var menuItem = s as ToolStripMenuItem;
                    var monitorSetting = menuItem.Tag as MonitorSettings;
                    monitorSetting.Enabled = menuItem.Checked;
                    UpdateBackgroundTimerProfile();
                    SaveSettings();
                };

                var overlayTimeoutItem = new ToolStripMenuItem("Overlay Timeout...")
                {
                    ForeColor = Color.White
                };
                overlayTimeoutItem.Click += (s, e) => ShowTimeoutDialog(setting);

                monitorItem.DropDownItems.Add(enabledItem);
                monitorItem.DropDownItems.Add(overlayTimeoutItem);
                monitorsMenuItem.DropDownItems.Add(monitorItem);
            }

            _contextMenu.Items.Add(monitorsMenuItem);

            var exclusionsItem = new ToolStripMenuItem("Manage Exclusions...")
            {
                ForeColor = Color.White
            };

            _contextMenu.Items.Add(new ToolStripSeparator());

            exclusionsItem.Click += (s, e) => ShowExclusionsDialog();
            _contextMenu.Items.Add(exclusionsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var telegramItem = new ToolStripMenuItem("Telegram Channel")
            {
                ForeColor = Color.White
            };
            telegramItem.Click += (s, e) =>
            {
                try
                {
                    var psi = new ProcessStartInfo("https://t.me/arsenzaa")
                    {
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch
                {
                }
            };

            _contextMenu.Items.Add(telegramItem);

            var autoStartItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = IsAutoStartEnabled(),
                CheckOnClick = true,
                ForeColor = Color.White
            };

            autoStartItem.Click += (s, e) =>
            {
                EnableAutoStart(autoStartItem.Checked);
            };

            _contextMenu.Items.Add(autoStartItem);

            var exitItem = new ToolStripMenuItem("Exit")
            {
                ForeColor = Color.White
            };
            exitItem.Click += (s, e) => ExitApplication();
            _contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = _contextMenu;
            _trayIcon.DoubleClick += (s, e) => ShowSettingsDialog();

            if (_taskbarHidingEnabled)
                HideTaskbarAndDesktop();
        }

        private static void ShowSettingsDialog()
        {
            using (var form = new Form())
            {
                form.Text = "OLED Saver Settings";
                form.Size = new Size(420, 300);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var listView = new ListView
                {
                    Location = new Point(20, 20),
                    Size = new Size(370, 180),
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = true,
                    CheckBoxes = true
                };

                listView.Columns.Add("Monitor", 200);
                listView.Columns.Add("Overlay Timeout", 100);
                listView.Columns.Add("Status", 80);

                foreach (var setting in _monitorSettings.Values)
                {
                    var item = new ListViewItem(setting.DisplayName)
                    {
                        Checked = setting.Enabled,
                        Tag = setting
                    };
                    item.SubItems.Add(setting.TimeoutSeconds.ToString() + "s");
                    item.SubItems.Add(setting.Enabled ? "Enabled" : "Disabled");
                    listView.Items.Add(item);
                }

                var editOverlayButton = new Button
                {
                    Text = "Edit Overlay",
                    Location = new Point(20, 220),
                    Size = new Size(100, 23)
                };
                editOverlayButton.Click += (s, e) =>
                {
                    if (listView.SelectedItems.Count > 0)
                    {
                        var setting = listView.SelectedItems[0].Tag as MonitorSettings;
                        ShowTimeoutDialog(setting);
                        listView.SelectedItems[0].SubItems[1].Text = setting.TimeoutSeconds.ToString() + "s";
                    }
                };

                var okButton = new Button
                {
                    Text = "OK",
                    Location = new Point(240, 220),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(320, 220),
                    Size = new Size(75, 23),
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.AddRange(new Control[] { listView, editOverlayButton, okButton, cancelButton });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    foreach (ListViewItem item in listView.Items)
                    {
                        var setting = item.Tag as MonitorSettings;
                        setting.Enabled = item.Checked;
                    }

                    UpdateBackgroundTimerProfile();
                    SaveSettings();
                }
            }
        }

        public static void EnableAutoStart(bool enable)
        {
            try
            {
                if (enable)
                {
                    CreateShortcut();
                }
                else
                {
                    RemoveShortcut();
                }
            }
            catch
            {
            }
        }

        private static Icon LoadTrayIconFromResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(TrayIconResourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Resource '{TrayIconResourceName}' was not found.");
            }

            return new Icon(stream);
        }

        private static void CreateShortcut()
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "OLEDSaver Auto Start" });
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch
            {
            }
        }

        private static void RemoveShortcut()
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }

        public static bool IsAutoStartEnabled()
        {
            return File.Exists(ShortcutPath);
        }

        public static string GetStartupFolder()
        {
            return StartupFolderPath;
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
            public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 32);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 32);
            public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 32);
            public override Color MenuItemBorder => Color.FromArgb(70, 70, 70);
            public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
            public override Color MenuStripGradientBegin => Color.FromArgb(32, 32, 32);
            public override Color MenuStripGradientEnd => Color.FromArgb(32, 32, 32);
            public override Color ToolStripBorder => Color.FromArgb(60, 60, 60);
            public override Color ToolStripGradientBegin => Color.FromArgb(32, 32, 32);
            public override Color ToolStripGradientMiddle => Color.FromArgb(32, 32, 32);
            public override Color ToolStripGradientEnd => Color.FromArgb(32, 32, 32);
            public override Color SeparatorDark => Color.FromArgb(80, 80, 80);
            public override Color SeparatorLight => Color.FromArgb(80, 80, 80);
        }

        private class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColorTable())
            {
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Color.White;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(Point.Empty, e.Item.Bounds.Size);
                Color color = e.Item.Selected ? Color.FromArgb(60, 60, 60) : Color.FromArgb(32, 32, 32);
                using (SolidBrush brush = new SolidBrush(color))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.White;
                base.OnRenderArrow(e);
            }
        }
    }
}
