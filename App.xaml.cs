using System;
using System.Linq;
using System.Windows;
using PreBootRepair.Models;
using PreBootRepair.Services;

namespace PreBootRepair
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle command-line arguments
            var args = e.Args;
            
            if (args.Contains("/help", StringComparer.OrdinalIgnoreCase) || 
                args.Contains("-help", StringComparer.OrdinalIgnoreCase) ||
                args.Contains("/?") || args.Contains("-?"))
            {
                ShowHelp();
                Shutdown(0);
                return;
            }

            bool silentSchedule = args.Contains("/schedule", StringComparer.OrdinalIgnoreCase) ||
                                  args.Contains("-schedule", StringComparer.OrdinalIgnoreCase);
            
            bool rebootNow = args.Contains("/reboot", StringComparer.OrdinalIgnoreCase) ||
                             args.Contains("-reboot", StringComparer.OrdinalIgnoreCase);

            // Find drive letter
            string targetDrive = "C:";
            foreach (var arg in args)
            {
                if (arg.Length == 2 && arg[1] == ':' && char.IsLetter(arg[0]))
                {
                    targetDrive = arg.ToUpper();
                    break;
                }
            }

            // Determine repair mode
            RepairMode mode = RepairMode.Standard;
            if (args.Contains("/basic", StringComparer.OrdinalIgnoreCase))
                mode = RepairMode.Basic;
            else if (args.Contains("/advanced", StringComparer.OrdinalIgnoreCase))
                mode = RepairMode.Advanced;

            // Silent mode
            if (silentSchedule)
            {
                await RunSilentMode(targetDrive, mode, rebootNow);
                return;
            }

            // Normal GUI mode - startup continues with MainWindow
        }

        private async System.Threading.Tasks.Task RunSilentMode(string drive, RepairMode mode, bool reboot)
        {
            var service = new DiskService();
            bool success = false;

            try
            {
                success = mode switch
                {
                    RepairMode.Basic => await service.ScheduleBasicRepairAsync(drive),
                    RepairMode.Standard => await service.ScheduleStandardRepairAsync(drive),
                    RepairMode.Advanced => await service.ScheduleAdvancedRepairAsync(drive),
                    _ => false
                };

                if (success)
                {
                    Console.WriteLine($"Successfully scheduled {mode} repair for {drive}");
                    
                    if (reboot)
                    {
                        Console.WriteLine("Initiating reboot in 10 seconds...");
                        await service.RebootAsync(10);
                    }
                    
                    Shutdown(0);
                }
                else
                {
                    Console.WriteLine("Failed to schedule repair");
                    MessageBox.Show($"Failed to schedule repair for {drive}.\n\nMake sure you're running as Administrator.",
                        "PreBootRepair", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "PreBootRepair", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void ShowHelp()
        {
            string help = @"PreBootRepair - Disk Repair Utility

Usage: PreBootRepair.exe [options] [drive]

Options:
  /schedule    Schedule disk repair silently (requires admin)
  /reboot      Reboot immediately after scheduling
  /basic       Use basic repair mode (dirty bit only)
  /standard    Use standard repair mode (CHKDSK /R)
  /advanced    Use advanced mode (CHKDSK + SFC + DISM)
  /help        Show this help message

Examples:
  PreBootRepair.exe                    Open GUI
  PreBootRepair.exe /schedule C:       Schedule repair for C:
  PreBootRepair.exe /schedule /reboot  Schedule and reboot now
  PreBootRepair.exe /schedule D: /advanced /reboot

For more information, see the README.md file.";

            MessageBox.Show(help, "PreBootRepair Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
