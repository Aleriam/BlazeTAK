using System.Collections.Concurrent;
using BlazeTAK.Client.Models;
using BlazeTAK.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Services;

public class UserLocationService : IUserLocationService
{
    private readonly ConcurrentDictionary<Guid, UserLocationDto> _userLocations = new();
    private readonly ConcurrentDictionary<Guid, (double Latitude, double Longitude)> _lastBroadcastedUserLocations = new();
    private readonly ConcurrentDictionary<string, (double Latitude, double Longitude, int MemberCount)> _lastBroadcastedUnitMarkers = new();
    private readonly ILogger<UserLocationService> _logger;

    public UserLocationService(ILogger<UserLocationService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool isSignificantUserMove, bool unitMarkersChanged, UserLocationDto? userLocation, List<LiveUnitMarkerDto> unitMarkers)> UpdateUserLocationAsync(
        Guid userId, 
        LocationUpdateDto location, 
        ApplicationDbContext dbContext)
    {
        var profile = await dbContext.Profiles
            .AsNoTracking()
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p => p.ID == userId);

        if (profile == null)
        {
            return (false, false, null, new List<LiveUnitMarkerDto>());
        }

        var isLeader = profile.Unit?.LeaderId == profile.ID;
        var existing = _userLocations.TryGetValue(userId, out var prevLoc) ? prevLoc : null;

        // Determine heading: use provided heading, or compute from movement if user moved
        double? heading = location.Heading;
        if (!heading.HasValue && existing != null)
        {
            var movedDistance = CalculateDistanceInMeters(existing.Latitude, existing.Longitude, location.Latitude, location.Longitude);
            if (movedDistance > 1.0)
            {
                heading = CalculateBearing(existing.Latitude, existing.Longitude, location.Latitude, location.Longitude);
            }
            else
            {
                heading = existing.Heading;
            }
        }

        var userLoc = new UserLocationDto
        {
            UserId = profile.ID,
            UnitId = profile.UnitId,
            GroupId = profile.GroupId ?? profile.Unit?.GroupId,
            Name = profile.Name,
            CallSign = profile.CallSign,
            CallSignNumber = profile.CallSignNumber,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Heading = heading,
            Speed = location.Speed,
            Accuracy = location.Accuracy,
            IsLeader = isLeader,
            Timestamp = DateTime.UtcNow
        };

        _userLocations[userId] = userLoc;

        // Check if user moved by more than 10 meters since last broadcast
        bool isSignificantUserMove = false;
        if (!_lastBroadcastedUserLocations.TryGetValue(userId, out var lastBroadcastLoc))
        {
            isSignificantUserMove = true;
            _lastBroadcastedUserLocations[userId] = (userLoc.Latitude, userLoc.Longitude);
        }
        else
        {
            var dist = CalculateDistanceInMeters(lastBroadcastLoc.Latitude, lastBroadcastLoc.Longitude, userLoc.Latitude, userLoc.Longitude);
            if (dist >= 10.0)
            {
                isSignificantUserMove = true;
                _lastBroadcastedUserLocations[userId] = (userLoc.Latitude, userLoc.Longitude);
            }
        }

        // Calculate unit markers if user belongs to a unit
        var unitMarkers = new List<LiveUnitMarkerDto>();
        bool unitMarkersChanged = false;

        if (profile.UnitId.HasValue)
        {
            unitMarkers = await CalculateUnitMarkersForUnitAsync(profile.UnitId.Value, dbContext);

            // Check if any unit marker moved by more than 30 meters or cluster membership changed
            foreach (var marker in unitMarkers)
            {
                if (!_lastBroadcastedUnitMarkers.TryGetValue(marker.Id, out var lastUnitMarker))
                {
                    unitMarkersChanged = true;
                    _lastBroadcastedUnitMarkers[marker.Id] = (marker.Latitude, marker.Longitude, marker.MemberCount);
                }
                else
                {
                    var unitDist = CalculateDistanceInMeters(lastUnitMarker.Latitude, lastUnitMarker.Longitude, marker.Latitude, marker.Longitude);
                    if (unitDist >= 30.0 || lastUnitMarker.MemberCount != marker.MemberCount)
                    {
                        unitMarkersChanged = true;
                        _lastBroadcastedUnitMarkers[marker.Id] = (marker.Latitude, marker.Longitude, marker.MemberCount);
                    }
                }
            }
        }

        return (isSignificantUserMove, unitMarkersChanged, userLoc, unitMarkers);
    }

    public UserLocationDto? GetUserLocation(Guid userId)
    {
        return _userLocations.TryGetValue(userId, out var loc) ? loc : null;
    }

    public List<UserLocationDto> GetUnitUserLocations(Guid unitId)
    {
        return _userLocations.Values
            .Where(l => l.UnitId == unitId)
            .ToList();
    }

    public async Task<List<LiveUnitMarkerDto>> GetGroupLiveUnitMarkersAsync(Guid groupId, ApplicationDbContext dbContext)
    {
        var unitIds = await dbContext.Units
            .AsNoTracking()
            .Where(u => u.GroupId == groupId)
            .Select(u => u.ID)
            .ToListAsync();

        var result = new List<LiveUnitMarkerDto>();
        foreach (var unitId in unitIds)
        {
            var markers = await CalculateUnitMarkersForUnitAsync(unitId, dbContext);
            result.AddRange(markers);
        }

        return result;
    }

    public async Task<List<LiveUnitMarkerDto>> CalculateUnitMarkersForUnitAsync(Guid unitId, ApplicationDbContext dbContext)
    {
        var unit = await dbContext.Units
            .AsNoTracking()
            .Include(u => u.Users)
            .FirstOrDefaultAsync(u => u.ID == unitId);

        if (unit == null)
        {
            return new List<LiveUnitMarkerDto>();
        }

        var activeMemberLocations = _userLocations.Values
            .Where(l => l.UnitId == unitId)
            .ToList();

        if (activeMemberLocations.Count == 0)
        {
            return new List<LiveUnitMarkerDto>();
        }

        var markerRadius = unit.MarkerRaduis > 0 ? unit.MarkerRaduis : 50;
        var unitType = string.IsNullOrWhiteSpace(unit.UnitType) ? "SFGPUCI----" : unit.UnitType.Trim();
        var result = new List<LiveUnitMarkerDto>();

        var unassigned = new HashSet<Guid>(activeMemberLocations.Select(m => m.UserId));
        var locationMap = activeMemberLocations.ToDictionary(m => m.UserId, m => m);

        // 1. Leader marker: if leader has active location
        if (unit.LeaderId.HasValue && unassigned.Contains(unit.LeaderId.Value))
        {
            var leaderLoc = locationMap[unit.LeaderId.Value];
            unassigned.Remove(unit.LeaderId.Value);

            var inRangeMemberIds = new List<Guid> { leaderLoc.UserId };
            var inRangeMemberNames = new List<string> { leaderLoc.Name };

            foreach (var otherId in unassigned.ToList())
            {
                var otherLoc = locationMap[otherId];
                var dist = CalculateDistanceInMeters(leaderLoc.Latitude, leaderLoc.Longitude, otherLoc.Latitude, otherLoc.Longitude);
                if (dist <= markerRadius)
                {
                    inRangeMemberIds.Add(otherId);
                    inRangeMemberNames.Add(otherLoc.Name);
                    unassigned.Remove(otherId);
                }
            }

            result.Add(new LiveUnitMarkerDto
            {
                Id = $"{unit.ID}_leader",
                UnitId = unit.ID,
                UnitName = unit.Name,
                CallSign = unit.CallSign,
                CallSignNumber = unit.CallSignNumber,
                UnitType = unitType,
                Latitude = leaderLoc.Latitude,
                Longitude = leaderLoc.Longitude,
                MemberCount = inRangeMemberIds.Count,
                MarkerRadius = markerRadius,
                IsLeaderMarker = true,
                MemberIds = inRangeMemberIds,
                MemberNames = inRangeMemberNames,
                Timestamp = DateTime.UtcNow
            });
        }

        // 2. Members who left leader radius (or all members if no active leader)
        int subClusterIndex = 1;
        while (unassigned.Count > 0)
        {
            var anchorId = unassigned.First();
            var anchorLoc = locationMap[anchorId];
            unassigned.Remove(anchorId);

            var clusterMemberIds = new List<Guid> { anchorId };
            var clusterMemberNames = new List<string> { anchorLoc.Name };

            double totalLat = anchorLoc.Latitude;
            double totalLng = anchorLoc.Longitude;

            foreach (var otherId in unassigned.ToList())
            {
                var otherLoc = locationMap[otherId];
                var dist = CalculateDistanceInMeters(anchorLoc.Latitude, anchorLoc.Longitude, otherLoc.Latitude, otherLoc.Longitude);
                if (dist <= markerRadius)
                {
                    clusterMemberIds.Add(otherId);
                    clusterMemberNames.Add(otherLoc.Name);
                    totalLat += otherLoc.Latitude;
                    totalLng += otherLoc.Longitude;
                    unassigned.Remove(otherId);
                }
            }

            var avgLat = totalLat / clusterMemberIds.Count;
            var avgLng = totalLng / clusterMemberIds.Count;

            result.Add(new LiveUnitMarkerDto
            {
                Id = $"{unit.ID}_cluster_{subClusterIndex++}_{anchorId}",
                UnitId = unit.ID,
                UnitName = unit.Name,
                CallSign = unit.CallSign,
                CallSignNumber = unit.CallSignNumber,
                UnitType = unitType,
                Latitude = avgLat,
                Longitude = avgLng,
                MemberCount = clusterMemberIds.Count,
                MarkerRadius = markerRadius,
                IsLeaderMarker = false,
                MemberIds = clusterMemberIds,
                MemberNames = clusterMemberNames,
                Timestamp = DateTime.UtcNow
            });
        }

        return result;
    }

    public (bool removed, Guid? unitId, Guid? groupId) RemoveUserLocation(Guid userId)
    {
        if (_userLocations.TryRemove(userId, out var loc))
        {
            _lastBroadcastedUserLocations.TryRemove(userId, out _);
            return (true, loc.UnitId, loc.GroupId);
        }

        return (false, null, null);
    }

    public static double CalculateDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = ToRadians(lon2 - lon1);
        var y = Math.Sin(dLon) * Math.Cos(ToRadians(lat2));
        var x = Math.Cos(ToRadians(lat1)) * Math.Sin(ToRadians(lat2)) -
                Math.Sin(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Cos(dLon);
        var brng = Math.Atan2(y, x);
        return (ToDegrees(brng) + 360.0) % 360.0;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
