using System.Collections.Concurrent;

namespace BlazeTAK.Services;

public record TrafficDataPoint(
    DateTime Timestamp,
    int Requests,
    int SuccessfulLogins,
    int FailedLoginAttempts,
    int ActiveUsers
);

public record TrafficEvent(
    DateTime Timestamp,
    string EventType,
    string Detail,
    string? User
);

public record TrafficStats(
    int TotalRequests24H,
    int TotalSuccessfulLogins24H,
    int TotalFailedLogins24H,
    int CurrentlyActiveUsers,
    IReadOnlyList<TrafficDataPoint> History,
    IReadOnlyList<TrafficEvent> RecentEvents
);

public interface ITrafficTracker
{
    void RecordRequest(string path, string method, int statusCode, string? userEmail = null);
    void RecordLoginAttempt(string email, bool success, string? detail = null);
    void RecordUserActivity(string userEmail);
    int GetActiveUsersCount(TimeSpan? window = null);
    TrafficStats GetTrafficStats(TimeSpan? duration = null, int bucketCount = 12);
}

public class TrafficTracker : ITrafficTracker
{
    private record RequestRecord(DateTime Timestamp, string Path, string Method, int StatusCode, string? UserEmail);
    private record LoginAttemptRecord(DateTime Timestamp, string Email, bool Success, string? Detail);

    private readonly ConcurrentQueue<RequestRecord> _requests = new();
    private readonly ConcurrentQueue<LoginAttemptRecord> _loginAttempts = new();
    private readonly ConcurrentDictionary<string, DateTime> _userLastActivity = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<TrafficEvent> _recentEvents = new();

    private const int MaxQueueSize = 5000;
    private const int MaxEventLogs = 50;

    public void RecordRequest(string path, string method, int statusCode, string? userEmail = null)
    {
        var now = DateTime.UtcNow;
        _requests.Enqueue(new RequestRecord(now, path, method, statusCode, userEmail));
        TrimQueue(_requests, MaxQueueSize);

        if (!string.IsNullOrEmpty(userEmail))
        {
            RecordUserActivity(userEmail);
        }
    }

    public void RecordLoginAttempt(string email, bool success, string? detail = null)
    {
        var now = DateTime.UtcNow;
        _loginAttempts.Enqueue(new LoginAttemptRecord(now, email, success, detail));
        TrimQueue(_loginAttempts, MaxQueueSize);

        var eventType = success ? "Login Success" : "Login Failed";
        var eventDetail = detail ?? (success ? "User signed in successfully." : "Invalid credentials or unauthorized.");
        _recentEvents.Enqueue(new TrafficEvent(now, eventType, eventDetail, email));
        TrimQueue(_recentEvents, MaxEventLogs);

        if (success)
        {
            RecordUserActivity(email);
        }
    }

    public void RecordUserActivity(string userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        _userLastActivity[userEmail] = DateTime.UtcNow;
    }

    public int GetActiveUsersCount(TimeSpan? window = null)
    {
        var threshold = DateTime.UtcNow - (window ?? TimeSpan.FromMinutes(15));
        return _userLastActivity.Count(kvp => kvp.Value >= threshold);
    }

    public TrafficStats GetTrafficStats(TimeSpan? duration = null, int bucketCount = 12)
    {
        var totalDuration = duration ?? TimeSpan.FromHours(24);
        var now = DateTime.UtcNow;
        var startTime = now - totalDuration;
        var bucketInterval = TimeSpan.FromTicks(totalDuration.Ticks / bucketCount);

        var requestsList = _requests.Where(r => r.Timestamp >= startTime).ToList();
        var loginsList = _loginAttempts.Where(l => l.Timestamp >= startTime).ToList();

        var history = new List<TrafficDataPoint>();

        for (int i = 0; i < bucketCount; i++)
        {
            var bStart = startTime + TimeSpan.FromTicks(bucketInterval.Ticks * i);
            var bEnd = bStart + bucketInterval;

            var reqCount = requestsList.Count(r => r.Timestamp >= bStart && r.Timestamp < bEnd);
            var successLogins = loginsList.Count(l => l.Timestamp >= bStart && l.Timestamp < bEnd && l.Success);
            var failedLogins = loginsList.Count(l => l.Timestamp >= bStart && l.Timestamp < bEnd && !l.Success);

            // Active users who had activity during or leading up to this bucket window
            var activeUsersInBucket = _userLastActivity
                .Count(kvp => kvp.Value >= bStart - TimeSpan.FromMinutes(15) && kvp.Value <= bEnd);

            // If no recorded activity in the bucket but there were successful logins or requests with users
            if (activeUsersInBucket == 0 && (successLogins > 0 || reqCount > 0))
            {
                var distinctUsersInBucket = requestsList
                    .Where(r => r.Timestamp >= bStart && r.Timestamp < bEnd && !string.IsNullOrEmpty(r.UserEmail))
                    .Select(r => r.UserEmail!)
                    .Union(loginsList.Where(l => l.Timestamp >= bStart && l.Timestamp < bEnd && l.Success).Select(l => l.Email))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                activeUsersInBucket = distinctUsersInBucket;
            }

            history.Add(new TrafficDataPoint(bEnd, reqCount, successLogins, failedLogins, activeUsersInBucket));
        }

        var activeNow = GetActiveUsersCount(TimeSpan.FromMinutes(15));
        var totalReq = requestsList.Count;
        var totalSuccess = loginsList.Count(l => l.Success);
        var totalFailed = loginsList.Count(l => !l.Success);

        var recentEvents = _recentEvents.Reverse().Take(15).ToList();

        return new TrafficStats(
            TotalRequests24H: totalReq,
            TotalSuccessfulLogins24H: totalSuccess,
            TotalFailedLogins24H: totalFailed,
            CurrentlyActiveUsers: activeNow,
            History: history,
            RecentEvents: recentEvents
        );
    }

    private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
    {
        while (queue.Count > maxSize && queue.TryDequeue(out _)) { }
    }
}
