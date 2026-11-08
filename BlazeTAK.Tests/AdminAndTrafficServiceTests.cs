using BlazeTAK.Services;
using Xunit;

namespace BlazeTAK.Tests;

public class AdminAndTrafficServiceTests
{
    [Fact]
    public void RegistrationService_DefaultState_IsEnabled()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"reg_test_{Guid.NewGuid():N}.conf");
        try
        {
            var service = new RegistrationService(configFilePath: tempFile);
            Assert.True(service.IsRegistrationEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void RegistrationService_CanToggleState()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"reg_test_{Guid.NewGuid():N}.conf");
        try
        {
            var service = new RegistrationService(configFilePath: tempFile);
            Assert.True(service.IsRegistrationEnabled);

            service.SetRegistrationEnabled(false);
            Assert.False(service.IsRegistrationEnabled);

            service.SetRegistrationEnabled(true);
            Assert.True(service.IsRegistrationEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void RegistrationService_PersistsStateToFile_AndLoadsOnStartup()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"reg_test_{Guid.NewGuid():N}.conf");
        try
        {
            // Initial instance disables registration
            var service1 = new RegistrationService(configFilePath: tempFile);
            service1.SetRegistrationEnabled(false);
            Assert.False(service1.IsRegistrationEnabled);

            // Verify file exists and has "false"
            Assert.True(File.Exists(tempFile));
            var fileContent = File.ReadAllText(tempFile).Trim();
            Assert.Equal("false", fileContent);

            // Simulating reboot / new startup by creating a new service instance reading the same file
            var service2 = new RegistrationService(configFilePath: tempFile);
            Assert.False(service2.IsRegistrationEnabled);

            // Re-enabling in service2
            service2.SetRegistrationEnabled(true);
            Assert.True(service2.IsRegistrationEnabled);
            Assert.Equal("true", File.ReadAllText(tempFile).Trim());

            // Simulating another reboot
            var service3 = new RegistrationService(configFilePath: tempFile);
            Assert.True(service3.IsRegistrationEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void RegistrationService_LoadsExistingFile_WithCustomContent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"reg_test_{Guid.NewGuid():N}.conf");
        try
        {
            File.WriteAllText(tempFile, "false\n");
            var service = new RegistrationService(configFilePath: tempFile);
            Assert.False(service.IsRegistrationEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TrafficTracker_RecordsRequestsAndCalculatesStats()
    {
        var tracker = new TrafficTracker();

        tracker.RecordRequest("/api/test", "GET", 200, "user1@example.com");
        tracker.RecordRequest("/hubs/map", "GET", 101, "user2@example.com");
        tracker.RecordRequest("/admin", "GET", 200, "admin@example.com");

        var stats = tracker.GetTrafficStats(TimeSpan.FromHours(1), bucketCount: 6);

        Assert.Equal(3, stats.TotalRequests24H);
        Assert.Equal(3, stats.CurrentlyActiveUsers);
        Assert.Equal(6, stats.History.Count);
    }

    [Fact]
    public void TrafficTracker_RecordsLoginAttempts_SuccessAndFailure()
    {
        var tracker = new TrafficTracker();

        tracker.RecordLoginAttempt("alpha@example.com", success: true, "Logged in via password");
        tracker.RecordLoginAttempt("attacker@example.com", success: false, "Invalid password");
        tracker.RecordLoginAttempt("attacker@example.com", success: false, "Invalid password");

        var stats = tracker.GetTrafficStats(TimeSpan.FromHours(24), bucketCount: 12);

        Assert.Equal(1, stats.TotalSuccessfulLogins24H);
        Assert.Equal(2, stats.TotalFailedLogins24H);
        Assert.Equal(3, stats.RecentEvents.Count);

        Assert.Contains(stats.RecentEvents, e => e.EventType == "Login Success" && e.User == "alpha@example.com");
        Assert.Contains(stats.RecentEvents, e => e.EventType == "Login Failed" && e.User == "attacker@example.com");
    }

    [Fact]
    public void TrafficTracker_ActiveUsersCount_RespectsTimeWindow()
    {
        var tracker = new TrafficTracker();

        tracker.RecordUserActivity("active1@example.com");
        tracker.RecordUserActivity("active2@example.com");

        Assert.Equal(2, tracker.GetActiveUsersCount(TimeSpan.FromMinutes(10)));
        Assert.Equal(0, tracker.GetActiveUsersCount(TimeSpan.FromMilliseconds(-1000)));
    }
}
