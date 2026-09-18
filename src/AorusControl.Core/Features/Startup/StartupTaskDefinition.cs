using System.Globalization;
using System.Security;

namespace AorusControl.Core.Features.Startup;

/// <summary>
/// The autostart task, written out in full rather than left to <c>schtasks</c>'s switches.
///
/// <c>schtasks /Create /SC ONLOGON</c> looks like the simple way and quietly brings
/// Microsoft's defaults for a *maintenance* job along with it. Measured on this machine on
/// 2026-09-15, the task it had produced said:
///
/// <code>
/// &lt;DisallowStartIfOnBatteries&gt;true&lt;/DisallowStartIfOnBatteries&gt;
/// &lt;StopIfGoingOnBatteries&gt;true&lt;/StopIfGoingOnBatteries&gt;
/// </code>
///
/// Which means: on battery it never started at all, and unplugging the mains killed the
/// running app. Two more defaults do not appear in the file but apply anyway - a 72 hour
/// execution limit, after which Windows terminates a tray app that has done nothing wrong,
/// and priority 7, below normal, which slows the very startup this is supposed to make
/// prompt.
///
/// Every one of those is wrong for a program that watches the fans and is most needed
/// precisely when the mains cable is out. So the definition is ours, all of it, and the
/// <see cref="Version"/> marker in the description lets a later build recognise and replace
/// a task written by an earlier one.
/// </summary>
public static class StartupTaskDefinition
{
    /// <summary>
    /// Written into the task's description and checked before an existing task is trusted.
    /// Bump it whenever the definition below changes in a way that existing installations
    /// should pick up; <see cref="StartupManager.RepairAsync"/> then replaces them silently.
    /// </summary>
    public const string Version = "AORUS Control Autostart v3";

    public const string BackgroundStartArgument = "--background";

    /// <summary>
    /// Builds the task XML for one executable and one account.
    ///
    /// The element order is not a matter of taste: the Task Scheduler validates against a
    /// schema that fixes it, and a file in the wrong order is rejected outright. This order
    /// is the one Windows itself emits.
    /// </summary>
    public static string Build(string executablePath, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        string user = SecurityElement.Escape(userId)!;
        string command = SecurityElement.Escape(executablePath)!;
        string created = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Date>{created}</Date>
                <Description>{Version}</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <!-- If the app dies, Windows brings it back. Without this the task simply
                     records a failure and the machine spends the rest of the session with no
                     fan supervision and no lighting, until somebody notices. Three tries a
                     minute apart: enough for a one-off crash, not enough to loop over a fault
                     that will not go away. -->
                <RestartOnFailure>
                  <Count>3</Count>
                  <Interval>PT1M</Interval>
                </RestartOnFailure>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>{BackgroundStartArgument}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    /// <summary>
    /// Whether an exported task is one this build would write. The executable path matters as
    /// much as the version marker: an autostart left pointing at a path that no longer exists
    /// fails silently, which is the worst way for this to break.
    ///
    /// Both sides are stripped of everything outside printable ASCII first. schtasks hands
    /// its export back in the console code page, so a profile folder with an umlaut in it
    /// comes back altered - and a comparison that tripped over that would rewrite the task on
    /// every single start, for ever, without ever saying why.
    /// </summary>
    public static bool Matches(string? exportedXml, string executablePath)
    {
        if (exportedXml is null) return false;
        string exported = AsciiOnly(exportedXml);
        return exported.Contains(AsciiOnly(Version), StringComparison.Ordinal)
            && exported.Contains(AsciiOnly(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string AsciiOnly(string value) =>
        string.Concat(value.Where(character => character is >= ' ' and <= '~'));
}
