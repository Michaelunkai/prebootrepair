using System;
using System.Security.Principal;

namespace NtfsRepairTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       NtfsRepairTool v1.0 - NTFS Filesystem Repair       ║");
            Console.WriteLine("║          Direct disk access for pre-boot repair          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // Check for admin
            if (!IsAdmin())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[!] This tool requires Administrator privileges.");
                Console.WriteLine("[!] Please run as Administrator.");
                Console.ResetColor();
                Environment.Exit(1);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Running with Administrator privileges");
            Console.ResetColor();

            // Parse arguments
            string? driveLetter = null;
            bool repair = false;
            bool force = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                
                if (arg == "/repair" || arg == "-repair" || arg == "--repair")
                {
                    repair = true;
                }
                else if (arg == "/force" || arg == "-force" || arg == "--force")
                {
                    force = true;
                }
                else if (arg == "/?" || arg == "-?" || arg == "--help" || arg == "-h")
                {
                    ShowHelp();
                    return;
                }
                else if (arg.Length >= 1 && char.IsLetter(arg[0]))
                {
                    driveLetter = arg.TrimEnd(':').ToUpper();
                }
            }

            // Interactive mode if no drive specified
            if (string.IsNullOrEmpty(driveLetter))
            {
                Console.WriteLine();
                Console.Write("Enter drive letter to analyze/repair (e.g., C): ");
                driveLetter = Console.ReadLine()?.Trim().TrimEnd(':').ToUpper();
                
                if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length != 1)
                {
                    Console.WriteLine("[!] Invalid drive letter");
                    Environment.Exit(1);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"[*] Target drive: {driveLetter}:");
            Console.WriteLine();

            // Warn about system drive
            string? systemDrive = Environment.GetEnvironmentVariable("SystemDrive")?.TrimEnd(':');
            if (driveLetter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[!] WARNING: You are targeting the system drive!");
                Console.WriteLine("[!] This tool is designed to run from WinPE, not from Windows.");
                Console.WriteLine("[!] Repairs on the active system drive may fail or be incomplete.");
                Console.ResetColor();
                
                if (!force)
                {
                    Console.WriteLine();
                    Console.Write("Continue anyway? (y/N): ");
                    if (Console.ReadLine()?.Trim().ToLower() != "y")
                    {
                        Console.WriteLine("Aborted.");
                        return;
                    }
                }
            }

            // Run analysis or repair
            using var ntfs = new NtfsRepair(driveLetter);
            
            if (repair)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[*] Opening volume for REPAIR mode...");
                Console.ResetColor();
                
                if (!ntfs.Open(forWriting: true))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[!] Failed to open volume for repair.");
                    Console.WriteLine("[!] Ensure the volume is not in use or boot from WinPE.");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                var result = ntfs.Repair();
                
                Console.WriteLine();
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] {result.Message}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[*] {result.Message}");
                }
                Console.ResetColor();

                foreach (var detail in result.Details)
                {
                    Console.WriteLine($"    {detail}");
                }
            }
            else
            {
                Console.WriteLine("[*] Opening volume for analysis...");
                
                if (!ntfs.Open(forWriting: false))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[!] Failed to open volume for reading.");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                var result = ntfs.Analyze();
                
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("                      ANALYSIS RESULTS");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();

                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  Status: {result.Message}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Status: {result.Message}");
                }
                Console.ResetColor();

                Console.WriteLine();
                Console.WriteLine("  Details:");
                foreach (var detail in result.Details)
                {
                    if (detail.StartsWith("CRITICAL"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (detail.StartsWith("WARNING"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    Console.WriteLine($"    {detail}");
                    Console.ResetColor();
                }

                if (result.ErrorsFound > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  To attempt repair, run with /repair flag:");
                    Console.WriteLine($"    NtfsRepairTool.exe {driveLetter}: /repair");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
        }

        static bool IsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: NtfsRepairTool.exe [drive] [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  drive       Drive letter to analyze/repair (e.g., C or C:)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  /repair     Attempt to repair detected issues");
            Console.WriteLine("  /force      Skip confirmation prompts");
            Console.WriteLine("  /help       Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  NtfsRepairTool.exe C           Analyze C: drive");
            Console.WriteLine("  NtfsRepairTool.exe D: /repair  Repair D: drive");
            Console.WriteLine();
            Console.WriteLine("Note: This tool is designed to run from WinPE for best results.");
            Console.WriteLine("      Running on an active system drive will have limited repair");
            Console.WriteLine("      capabilities due to Windows file locks.");
        }
    }
}
