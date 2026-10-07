using BlazeTAK.Client.Models;
using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using BlazeTAK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazeTAK.Tests;

public class UserLocationServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UpdateUserLocationAsync_MovementUnder10Meters_IsNotSignificant()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserLocationService(NullLogger<UserLocationService>.Instance);

        var userId = Guid.NewGuid();
        var user = new User
        {
            ID = userId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "John Doe",
            CallSign = "VIPER",
            CallSignNumber = 1
        };
        dbContext.Profiles.Add(user);
        await dbContext.SaveChangesAsync();

        // Initial location report (First report is always significant)
        var loc1 = new LocationUpdateDto { Latitude = 47.50000, Longitude = 14.50000 };
        var (isSignificant1, _, userLoc1, _) = await service.UpdateUserLocationAsync(userId, loc1, dbContext);

        Assert.True(isSignificant1);
        Assert.NotNull(userLoc1);

        // Move by 5 meters (approx 0.000045 degrees lat)
        var loc2 = new LocationUpdateDto { Latitude = 47.50004, Longitude = 14.50000 };
        var (isSignificant2, _, _, _) = await service.UpdateUserLocationAsync(userId, loc2, dbContext);

        Assert.False(isSignificant2);

        // Move by 15 meters (> 10m threshold)
        var loc3 = new LocationUpdateDto { Latitude = 47.50020, Longitude = 14.50000 };
        var (isSignificant3, _, _, _) = await service.UpdateUserLocationAsync(userId, loc3, dbContext);

        Assert.True(isSignificant3);
    }

    [Fact]
    public async Task CalculateUnitMarkers_LeaderAndMembersInRange_GroupedUnderOneMarker()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserLocationService(NullLogger<UserLocationService>.Instance);

        var leaderId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var unit = new Unit
        {
            ID = unitId,
            Name = "Alpha Squad",
            CallSign = "ALPHA",
            CallSignNumber = 1,
            MarkerRaduis = 50,
            UnitType = "SFGPUCI----",
            LeaderId = leaderId
        };

        var leader = new User
        {
            ID = leaderId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Captain Alpha",
            CallSign = "ALPHA",
            CallSignNumber = 1,
            UnitId = unitId
        };

        var member = new User
        {
            ID = memberId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Private Bravo",
            CallSign = "ALPHA",
            CallSignNumber = 2,
            UnitId = unitId
        };

        dbContext.Units.Add(unit);
        dbContext.Profiles.AddRange(leader, member);
        await dbContext.SaveChangesAsync();

        // Leader at 47.50000, 14.50000
        await service.UpdateUserLocationAsync(leaderId, new LocationUpdateDto { Latitude = 47.50000, Longitude = 14.50000 }, dbContext);

        // Member at 20 meters from leader (within 50m radius)
        await service.UpdateUserLocationAsync(memberId, new LocationUpdateDto { Latitude = 47.50015, Longitude = 14.50000 }, dbContext);

        var markers = await service.CalculateUnitMarkersForUnitAsync(unitId, dbContext);

        // Should be exactly 1 unit marker representing the whole squad
        Assert.Single(markers);
        var marker = markers[0];
        Assert.True(marker.IsLeaderMarker);
        Assert.Equal(2, marker.MemberCount);
        Assert.Equal("SFGPUCI----", marker.UnitType);
        Assert.Equal(50, marker.MarkerRadius);
        Assert.Contains(leaderId, marker.MemberIds);
        Assert.Contains(memberId, marker.MemberIds);
    }

    [Fact]
    public async Task CalculateUnitMarkers_MemberLeavesRadius_GetsSeparateUnitMarker()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserLocationService(NullLogger<UserLocationService>.Instance);

        var leaderId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var unit = new Unit
        {
            ID = unitId,
            Name = "Bravo Squad",
            CallSign = "BRAVO",
            CallSignNumber = 1,
            MarkerRaduis = 50,
            UnitType = "SFGPUCA----",
            LeaderId = leaderId
        };

        var leader = new User
        {
            ID = leaderId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Sgt Bravo",
            CallSign = "BRAVO",
            CallSignNumber = 1,
            UnitId = unitId
        };

        var member = new User
        {
            ID = memberId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Scout Charlie",
            CallSign = "BRAVO",
            CallSignNumber = 2,
            UnitId = unitId
        };

        dbContext.Units.Add(unit);
        dbContext.Profiles.AddRange(leader, member);
        await dbContext.SaveChangesAsync();

        // Leader at 47.50000, 14.50000
        await service.UpdateUserLocationAsync(leaderId, new LocationUpdateDto { Latitude = 47.50000, Longitude = 14.50000 }, dbContext);

        // Member at 200 meters from leader (outside 50m radius)
        await service.UpdateUserLocationAsync(memberId, new LocationUpdateDto { Latitude = 47.50200, Longitude = 14.50000 }, dbContext);

        var markers = await service.CalculateUnitMarkersForUnitAsync(unitId, dbContext);

        // Should have 2 markers: 1 leader marker and 1 separated subgroup marker
        Assert.Equal(2, markers.Count);

        var leaderMarker = markers.First(m => m.IsLeaderMarker);
        Assert.Equal(1, leaderMarker.MemberCount);
        Assert.Contains(leaderId, leaderMarker.MemberIds);

        var separatedMarker = markers.First(m => !m.IsLeaderMarker);
        Assert.Equal(1, separatedMarker.MemberCount);
        Assert.Contains(memberId, separatedMarker.MemberIds);
        Assert.Equal("SFGPUCA----", separatedMarker.UnitType);
    }

    [Fact]
    public async Task UpdateUserLocationAsync_UnitMarkerMovesOver30Meters_TriggersUnitMarkersChanged()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserLocationService(NullLogger<UserLocationService>.Instance);

        var leaderId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var unit = new Unit
        {
            ID = unitId,
            Name = "Delta Unit",
            CallSign = "DELTA",
            CallSignNumber = 1,
            MarkerRaduis = 50,
            UnitType = "SFGPUCI----",
            LeaderId = leaderId
        };

        var leader = new User
        {
            ID = leaderId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Delta Lead",
            CallSign = "DELTA",
            CallSignNumber = 1,
            UnitId = unitId
        };

        dbContext.Units.Add(unit);
        dbContext.Profiles.Add(leader);
        await dbContext.SaveChangesAsync();

        // Initial position
        var (_, unitChanged1, _, _) = await service.UpdateUserLocationAsync(leaderId, new LocationUpdateDto { Latitude = 47.50000, Longitude = 14.50000 }, dbContext);
        Assert.True(unitChanged1);

        // Leader moves 15 meters (>10m for user, but <30m for unit)
        var (userChanged2, unitChanged2, _, _) = await service.UpdateUserLocationAsync(leaderId, new LocationUpdateDto { Latitude = 47.50015, Longitude = 14.50000 }, dbContext);
        Assert.True(userChanged2);
        Assert.False(unitChanged2);

        // Leader moves another 35 meters (>30m threshold for unit)
        var (userChanged3, unitChanged3, _, _) = await service.UpdateUserLocationAsync(leaderId, new LocationUpdateDto { Latitude = 47.50060, Longitude = 14.50000 }, dbContext);
        Assert.True(userChanged3);
        Assert.True(unitChanged3);
    }

    [Fact]
    public void DistanceAndBearingCalculations_AreAccurate()
    {
        // 1 degree lat distance at equator is approx 111.195 km
        var dist = UserLocationService.CalculateDistanceInMeters(0.0, 0.0, 1.0, 0.0);
        Assert.InRange(dist, 111000, 112000);

        // Moving directly North from (0,0) to (1,0) should have bearing ~ 0 / 360 deg
        var bearingNorth = UserLocationService.CalculateBearing(0.0, 0.0, 1.0, 0.0);
        Assert.InRange(bearingNorth, 0, 0.01);

        // Moving directly East from (0,0) to (0,1) should have bearing ~ 90 deg
        var bearingEast = UserLocationService.CalculateBearing(0.0, 0.0, 0.0, 1.0);
        Assert.InRange(bearingEast, 89.9, 90.1);
    }

    [Fact]
    public async Task GetUserLocation_And_RemoveUserLocation_WorksProperly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserLocationService(NullLogger<UserLocationService>.Instance);

        var userId = Guid.NewGuid();
        var user = new User
        {
            ID = userId,
            ApplicationUserId = Guid.NewGuid().ToString(),
            Name = "Ghost",
            CallSign = "GHOST",
            CallSignNumber = 1
        };
        dbContext.Profiles.Add(user);
        await dbContext.SaveChangesAsync();

        var loc = new LocationUpdateDto { Latitude = 47.50000, Longitude = 14.50000, Heading = 180.0, Speed = 5.0, Accuracy = 2.0 };
        await service.UpdateUserLocationAsync(userId, loc, dbContext);

        var retrieved = service.GetUserLocation(userId);
        Assert.NotNull(retrieved);
        Assert.Equal(47.50000, retrieved.Latitude);
        Assert.Equal(14.50000, retrieved.Longitude);
        Assert.Equal(180.0, retrieved.Heading);

        var (removed, _, _) = service.RemoveUserLocation(userId);
        Assert.True(removed);
        Assert.Null(service.GetUserLocation(userId));
    }
}
