# Changelog

All notable changes to PreBootRepair will be documented in this file.

## [1.0.0] - 2024-02-06

### Added
- Initial release
- Modern dark theme WPF interface
- Three repair modes:
  - **Basic**: Sets dirty bit for quick autochk scan
  - **Standard**: Schedules CHKDSK /R via BootExecute registry
  - **Advanced**: CHKDSK + post-boot SFC/DISM scheduled task
- Drive analysis with SMART status detection
- Volume dirty bit detection using FSCTL_IS_VOLUME_DIRTY
- Schedule management (create, cancel, status)
- Automatic reboot option with countdown
- Post-repair notification popup
- Activity log with timestamps
- Command-line interface:
  - `/schedule` - Silent scheduling
  - `/reboot` - Auto-reboot after schedule
  - `/basic`, `/standard`, `/advanced` - Mode selection
  - `/help` - Show help
- Keyboard shortcuts:
  - F5 - Analyze drive
  - Ctrl+S - Schedule repair
  - Ctrl+R - Refresh drives
  - Ctrl+L - View logs
  - F1 - Help
- Menu bar with File, Tools, Help options
- Status bar with real-time feedback
- Logging to C:\ProgramData\PreBootRepair\Logs
- Windows 10/11 version detection
- Administrator privilege handling with UAC elevation
- Single-file self-contained executable (no .NET install needed)

### Technical
- Built with .NET 8.0 and WPF
- Uses Windows native BootExecute mechanism
- P/Invoke for volume dirty bit detection
- Registry manipulation for schedule persistence
- Scheduled tasks for advanced repair mode

### Documentation
- README.md with full feature documentation
- QUICKSTART.txt for quick reference
- MIT License

---

## Future Plans
- [ ] Repair history viewer
- [ ] Scheduled recurring repairs
- [ ] Email/notification on completion
- [ ] Bad sector mapping visualization
- [ ] Multiple drive batch repair
