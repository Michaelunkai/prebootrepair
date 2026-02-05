using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PreBootRepair.Models;
using PreBootRepair.Services;

namespace PreBootRepair
{
    public partial class MainWindow : Window
    {
        private readonly DiskService _diskService;
        private Models.DriveInfo? _selectedDrive;
        private const string LogsPath = @"C:\ProgramData\PreBootRepair\Logs";

        public MainWindow()
        {
            InitializeComponent();
            _diskService = new DiskService();
            
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check admin privileges
            if (!IsRunningAsAdmin())
            {
                AddLog("⚠️ Not running as Administrator - some features may be limited");
                StatusBarText.Text = "Limited mode - Run as Administrator for full functionality";
            }
            else
            {
                AddLog("✅ Running with Administrator privileges");
                StatusBarText.Text = "Ready - Administrator mode";
            }

            // Log system info
            var osVersion = Environment.OSVersion;
            string winVersion = osVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
            AddLog($"System: {winVersion} (Build {osVersion.Version.Build})");
            AddLog($"PreBootRepair v1.0.0 started at {DateTime.Now:HH:mm:ss}");

            // Load drives
            LoadDrives();

            // Check for existing schedule
            CheckScheduleStatus();

            // Show last scan results and check if repair just completed
            ShowLastResults();
            
            // Check if a repair was just performed (scheduled but now complete)
            var schedule = _diskService.GetScheduleInfo();
            var lastResult = _diskService.GetLastScanResult();
            if (lastResult != null && lastResult.Timestamp > DateTime.Now.AddHours(-1))
            {
                // Recent repair - show notification
                string resultMsg = lastResult.Success 
                    ? $"✅ Disk repair completed successfully!\n\nDrive: {lastResult.DriveLetter}\nErrors found: {lastResult.ErrorsFound}\nErrors fixed: {lastResult.ErrorsFixed}"
                    : $"⚠️ Disk repair completed with issues.\n\nDrive: {lastResult.DriveLetter}\nExit code: {lastResult.ExitCode}\n\nCheck logs for details.";
                
                MessageBox.Show(resultMsg, "Repair Results", MessageBoxButton.OK,
                    lastResult.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }

        private void LoadDrives()
        {
            DriveComboBox.Items.Clear();
            
            var drives = _diskService.GetAllDrives();
            foreach (var drive in drives)
            {
                var item = new ComboBoxItem
                {
                    Content = drive.DisplayName,
                    Tag = drive
                };
                DriveComboBox.Items.Add(item);
            }

            if (DriveComboBox.Items.Count > 0)
            {
                DriveComboBox.SelectedIndex = 0;
            }

            AddLog($"Found {drives.Count} fixed drive(s)");
        }

        private void CheckScheduleStatus()
        {
            var schedule = _diskService.GetScheduleInfo();
            
            if (schedule.IsScheduled)
            {
                ScheduleBanner.Visibility = Visibility.Visible;
                ScheduleStatusText.Text = $"Repair scheduled for {schedule.TargetDrive}";
                ScheduleDetailsText.Text = $"Mode: {schedule.Mode} | Scheduled: {schedule.ScheduledTime:g}";
                CancelButton.IsEnabled = true;
                RebootButton.IsEnabled = true;
                AddLog($"📌 Active repair schedule found for {schedule.TargetDrive}");
            }
            else
            {
                ScheduleBanner.Visibility = Visibility.Collapsed;
                CancelButton.IsEnabled = false;
                RebootButton.IsEnabled = false;
            }
        }

        private void ShowLastResults()
        {
            var result = _diskService.GetLastScanResult();
            if (result != null && result.Timestamp > DateTime.MinValue)
            {
                AddLog($"📋 Last scan: {result.Timestamp:g}");
                if (result.Success)
                {
                    AddLog($"   ✅ Completed successfully (Exit code: {result.ExitCode})");
                }
                else
                {
                    AddLog($"   ⚠️ Completed with issues (Exit code: {result.ExitCode})");
                }
                
                if (result.ErrorsFound > 0)
                {
                    AddLog($"   Found {result.ErrorsFound} errors, fixed {result.ErrorsFixed}");
                }
            }
        }

        private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DriveComboBox.SelectedItem is ComboBoxItem item && item.Tag is Models.DriveInfo drive)
            {
                _selectedDrive = drive;
                UpdateStatusDisplay(drive);
                AddLog($"Selected drive: {drive.DriveLetter}");
            }
        }

        private void UpdateStatusDisplay(Models.DriveInfo drive)
        {
            string status = $"Drive: {drive.DriveLetter}\n";
            status += $"File System: {drive.FileSystem}\n";
            status += $"Space: {drive.FreeGB:F1} GB free of {drive.TotalGB:F1} GB ({100 - drive.UsedPercent:F0}% free)\n";
            status += $"Status: {(drive.IsDirty ? "⚠️ DIRTY (needs repair)" : "✅ Clean")}\n";
            status += $"SMART: {drive.SmartStatus}";
            
            if (drive.IsSystemDrive)
            {
                status += "\n[System Drive]";
            }

            StatusText.Text = status;
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrive == null)
            {
                MessageBox.Show("Please select a drive first.", "No Drive Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AnalyzeButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            StatusBarText.Text = $"Analyzing {_selectedDrive.DriveLetter}...";
            AddLog($"Analyzing {_selectedDrive.DriveLetter}...");

            try
            {
                var drive = await _diskService.AnalyzeDriveAsync(_selectedDrive.DriveLetter);
                if (drive != null)
                {
                    _selectedDrive = drive;
                    UpdateStatusDisplay(drive);
                    
                    AddLog(drive.IsDirty 
                        ? "⚠️ Drive needs repair" 
                        : "✅ Drive appears healthy");
                    AddLog($"SMART Status: {drive.SmartStatus}");
                }
                StatusBarText.Text = "Analysis complete";
            }
            catch (Exception ex)
            {
                AddLog($"❌ Analysis failed: {ex.Message}");
                StatusBarText.Text = "Analysis failed";
            }
            finally
            {
                AnalyzeButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
            }
        }

        private async void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                var result = MessageBox.Show(
                    "Administrator privileges are required to schedule disk repair.\n\n" +
                    "Would you like to restart the application with elevated privileges?",
                    "Elevation Required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    RestartAsAdmin();
                }
                return;
            }

            if (_selectedDrive == null)
            {
                MessageBox.Show("Please select a drive first.", "No Drive Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RepairMode mode = (RepairMode)ModeComboBox.SelectedIndex;
            string modeText = mode switch
            {
                RepairMode.Basic => "Basic (dirty bit)",
                RepairMode.Standard => "Standard (CHKDSK)",
                RepairMode.Advanced => "Advanced (CHKDSK + SFC/DISM)",
                _ => "Unknown"
            };

            ScheduleButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            StatusBarText.Text = $"Scheduling {modeText} repair...";
            AddLog($"Scheduling {modeText} repair for {_selectedDrive.DriveLetter}...");

            try
            {
                bool success = mode switch
                {
                    RepairMode.Basic => await _diskService.ScheduleBasicRepairAsync(_selectedDrive.DriveLetter),
                    RepairMode.Standard => await _diskService.ScheduleStandardRepairAsync(_selectedDrive.DriveLetter),
                    RepairMode.Advanced => await _diskService.ScheduleAdvancedRepairAsync(_selectedDrive.DriveLetter),
                    _ => false
                };

                if (success)
                {
                    AddLog($"✅ {modeText} repair scheduled successfully");
                    StatusBarText.Text = "Repair scheduled - reboot to apply";
                    CheckScheduleStatus();

                    var result = MessageBox.Show(
                        "Disk repair has been scheduled.\n\n" +
                        "The repair will run automatically on next reboot, before Windows loads.\n\n" +
                        "Would you like to reboot now?",
                        "Repair Scheduled", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        RebootButton_Click(sender, e);
                    }
                }
                else
                {
                    AddLog("❌ Failed to schedule repair");
                    StatusBarText.Text = "Failed to schedule repair";
                    MessageBox.Show("Failed to schedule disk repair. Check the log for details.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ Error: {ex.Message}");
                StatusBarText.Text = "Error scheduling repair";
                MessageBox.Show($"Error scheduling repair: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ScheduleButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel the scheduled repair?",
                "Cancel Repair", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            CancelButton.IsEnabled = false;
            StatusBarText.Text = "Cancelling scheduled repair...";
            AddLog("Cancelling scheduled repair...");

            try
            {
                if (await _diskService.CancelScheduledRepairAsync())
                {
                    AddLog("✅ Scheduled repair cancelled");
                    StatusBarText.Text = "Repair cancelled";
                    CheckScheduleStatus();
                }
                else
                {
                    AddLog("❌ Failed to cancel repair");
                    StatusBarText.Text = "Failed to cancel repair";
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ Error: {ex.Message}");
                StatusBarText.Text = "Error cancelling repair";
            }
            finally
            {
                CancelButton.IsEnabled = true;
            }
        }

        private async void RebootButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "The computer will restart in 10 seconds.\n\n" +
                "Save all your work before continuing.\n\n" +
                "The disk repair will run before Windows loads and may take several minutes.",
                "Confirm Reboot", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK)
                return;

            AddLog("🔄 Initiating system reboot...");
            StatusBarText.Text = "Initiating reboot...";

            if (await _diskService.RebootAsync(10))
            {
                AddLog("System will restart in 10 seconds");
                StatusBarText.Text = "Rebooting in 10 seconds...";
            }
            else
            {
                AddLog("❌ Failed to initiate reboot");
                StatusBarText.Text = "Failed to initiate reboot";
                MessageBox.Show("Failed to initiate reboot.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Menu handlers
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ViewLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(LogsPath))
                {
                    Process.Start("explorer.exe", LogsPath);
                }
                else
                {
                    MessageBox.Show("Logs folder does not exist yet.", "No Logs", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open logs folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LogListBox.Items.Clear();
            AddLog("Log cleared");
        }

        private void RefreshDrivesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LoadDrives();
            AddLog("Drive list refreshed");
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "PreBootRepair v1.0.0\n\n" +
                "A free, open-source Windows utility that schedules disk repairs to run before Windows boots.\n\n" +
                "Features:\n" +
                "• Schedule CHKDSK at boot via BootExecute\n" +
                "• Basic, Standard, and Advanced repair modes\n" +
                "• Dark theme modern UI\n" +
                "• Command-line support\n\n" +
                "Built with .NET 8 and WPF\n" +
                "License: MIT\n\n" +
                "© 2024 OpenClaw",
                "About PreBootRepair", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CommandLineHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string help = @"PreBootRepair Command Line Usage:

  PreBootRepair.exe [options] [drive]

Options:
  /schedule    Schedule disk repair silently
  /reboot      Reboot immediately after scheduling
  /basic       Use basic repair mode (dirty bit)
  /standard    Use standard repair mode (CHKDSK /R)
  /advanced    Use advanced mode (CHKDSK + SFC + DISM)
  /help        Show help message

Examples:
  PreBootRepair.exe
      Open the GUI application

  PreBootRepair.exe /schedule C:
      Schedule standard repair for C: drive

  PreBootRepair.exe /schedule D: /advanced /reboot
      Schedule advanced repair for D: and reboot now";

            MessageBox.Show(help, "Command Line Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddLog(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogListBox.Items.Add(entry);
            LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        }

        private static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(psi);
                Application.Current.Shutdown();
            }
            catch
            {
                // User cancelled UAC prompt
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // F5 - Analyze drive
            if (e.Key == Key.F5)
            {
                if (AnalyzeButton.IsEnabled)
                    AnalyzeButton_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+S - Schedule repair
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (ScheduleButton.IsEnabled)
                    ScheduleButton_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+R - Refresh drives
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                RefreshDrivesMenuItem_Click(sender, e);
                e.Handled = true;
            }
            // Ctrl+L - View logs
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ViewLogsMenuItem_Click(sender, e);
                e.Handled = true;
            }
            // F1 - Help
            else if (e.Key == Key.F1)
            {
                AboutMenuItem_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
