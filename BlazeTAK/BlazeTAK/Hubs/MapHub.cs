using System.Collections.Concurrent;
using System.Security.Claims;
using BlazeTAK.Client.Models;
using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using BlazeTAK.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace BlazeTAK.Hubs;

//[Authorize(Roles = "Admin,user")]
[Authorize]
public class MapHub : Hub
{
    private static readonly ConcurrentDictionary<string, Guid?> _userGroupCache = new();
    private static readonly ConcurrentDictionary<string, string?> _userGroupNameCache = new();
    private static readonly ConcurrentDictionary<string, Guid?> _userUnitCache = new();

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserLocationService _userLocationService;
    private readonly ILogger<MapHub> _logger;
    
    public MapHub(
        ApplicationDbContext dbContext, 
        UserManager<ApplicationUser> userManager, 
        IUserLocationService userLocationService,
        ILogger<MapHub> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _userLocationService = userLocationService;
        _logger = logger;
    }
    
    private async Task<Guid?> GetGroupIdForCurrentUser()
    {
        try
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = Context.UserIdentifier ?? Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId) && _userGroupCache.TryGetValue(userId, out var cachedGroupId))
                {
                    return cachedGroupId;
                }

                var appUser = await _userManager.GetUserAsync(Context.User);

                if (appUser != null)
                {
                    var profile = await _dbContext.Profiles
                        .Include(p => p.Group)
                        .Include(p => p.Unit)
                        .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);

                    if (profile != null)
                    {
                        var groupId = profile.GroupId ?? profile.Unit?.GroupId;
                        var cacheKey = userId ?? appUser.Id;
                        _userGroupCache[cacheKey] = groupId;
                        return groupId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve GroupId for current user");
        }

        return null;
    }

    public async Task<string?> GetGroupName()
    {
        try
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = Context.UserIdentifier ?? Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId) && _userGroupNameCache.TryGetValue(userId, out var cachedGroupName) && cachedGroupName != null)
                {
                    return cachedGroupName;
                }

                var appUser = await _userManager.GetUserAsync(Context.User);

                if (appUser != null)
                {
                    var profile = await _dbContext.Profiles
                        .Include(p => p.Group)
                        .Include(p => p.Unit)
                        .ThenInclude(u => u.Group)
                        .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);

                    if (profile != null)
                    {
                        string? groupName = null;
                        if (profile.Group != null) 
                            groupName = profile.Group.Name;
                        else if (profile.Unit?.Group != null) 
                            groupName = profile.Unit.Group.Name;

                        var cacheKey = userId ?? appUser.Id;
                        _userGroupNameCache[cacheKey] = groupName;
                        return groupName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch group name");
        }

        return null;
    }

    private async Task<User?> GetCurrentUserProfileAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var appUser = await _userManager.GetUserAsync(Context.User);
        if (appUser == null)
        {
            return null;
        }

        return await _dbContext.Profiles
            .Include(p => p.Group)
            .Include(p => p.Unit)
                .ThenInclude(u => u!.Group)
            .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);
    }

    private async Task<Guid?> GetUnitIdForCurrentUser()
    {
        try
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = Context.UserIdentifier ?? Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId) && _userUnitCache.TryGetValue(userId, out var cachedUnitId))
                {
                    return cachedUnitId;
                }

                var profile = await GetCurrentUserProfileAsync();
                if (profile != null)
                {
                    var cacheKey = userId ?? profile.ApplicationUserId;
                    _userUnitCache[cacheKey] = profile.UnitId;
                    return profile.UnitId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve UnitId for current user");
        }

        return null;
    }

    public async Task JoinGroup()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated connection {ConnectionId} attempted to join group.", Context.ConnectionId);
            return;
        }

        var currentUserGroup = await GetGroupIdForCurrentUser();
        if (currentUserGroup != null && currentUserGroup != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, currentUserGroup.Value.ToString());
            _logger.LogInformation("Connection {ConnectionId} joined map group {GroupId}", Context.ConnectionId, currentUserGroup.Value);
        }

        var currentUserUnit = await GetUnitIdForCurrentUser();
        if (currentUserUnit != null && currentUserUnit != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"unit_{currentUserUnit.Value}");
            _logger.LogInformation("Connection {ConnectionId} joined map unit {UnitId}", Context.ConnectionId, currentUserUnit.Value);
        }
    }

    public async Task LeaveGroup()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var currentUserGroup = await GetGroupIdForCurrentUser();
        if (currentUserGroup != null && currentUserGroup != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, currentUserGroup.Value.ToString());
            _logger.LogInformation("Connection {ConnectionId} left map group {GroupId}", Context.ConnectionId, currentUserGroup.Value);
        }

        var currentUserUnit = await GetUnitIdForCurrentUser();
        if (currentUserUnit != null && currentUserUnit != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"unit_{currentUserUnit.Value}");
            _logger.LogInformation("Connection {ConnectionId} left map unit {UnitId}", Context.ConnectionId, currentUserUnit.Value);
        }
    }

    public async Task<UserLocationDto?> UpdateUserLocation(LocationUpdateDto dto)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        try
        {
            var profile = await GetCurrentUserProfileAsync();
            if (profile == null)
            {
                return null;
            }

            var (isSignificantUserMove, unitMarkersChanged, userLocation, unitMarkers) = 
                await _userLocationService.UpdateUserLocationAsync(profile.ID, dto, _dbContext);

            if (userLocation != null && profile.UnitId.HasValue && isSignificantUserMove)
            {
                // Position is shared ONLY between members of the same unit
                await Clients.Group($"unit_{profile.UnitId.Value}").SendAsync("UserLocationUpdated", userLocation);
            }

            if (unitMarkersChanged)
            {
                var groupId = profile.GroupId ?? profile.Unit?.GroupId;
                if (groupId.HasValue)
                {
                    var groupUnitMarkers = await _userLocationService.GetGroupLiveUnitMarkersAsync(groupId.Value, _dbContext);
                    await Clients.Group(groupId.Value.ToString()).SendAsync("UnitMarkersUpdated", groupUnitMarkers);
                }
            }

            return userLocation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user location");
            return null;
        }
    }

    public async Task<List<UserLocationDto>> GetUnitUserLocations()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return new List<UserLocationDto>();
        }

        try
        {
            var profile = await GetCurrentUserProfileAsync();
            if (profile?.UnitId != null)
            {
                return _userLocationService.GetUnitUserLocations(profile.UnitId.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unit user locations");
        }

        return new List<UserLocationDto>();
    }

    public async Task<List<LiveUnitMarkerDto>> GetLiveUnitMarkers()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return new List<LiveUnitMarkerDto>();
        }

        try
        {
            var groupId = await GetGroupIdForCurrentUser();
            if (groupId.HasValue)
            {
                return await _userLocationService.GetGroupLiveUnitMarkersAsync(groupId.Value, _dbContext);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching live unit markers");
        }

        return new List<LiveUnitMarkerDto>();
    }

    public async Task<List<MarkerDto>> GetMarkers()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated connection {ConnectionId} attempted to get markers.", Context.ConnectionId);
            return new List<MarkerDto>();
        }

        try
        {
            var appUser = await _userManager.GetUserAsync(Context.User);
            if (appUser == null)
            {
                return new List<MarkerDto>();
            }

            var profile = await _dbContext.Profiles
                .Include(p => p.Group)
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);

            var groupId = await GetGroupIdForCurrentUser() ?? profile?.GroupId ?? profile?.Unit?.GroupId;
            var unitId = profile?.UnitId;

            if (groupId == null && unitId == null)
            {
                return new List<MarkerDto>();
            }

            var query = _dbContext.PersistentMarkers
                .AsNoTracking()
                .Include(m => m.CreatedBy)
                .Include(m => m.Unit)
                .AsQueryable();

            if (groupId != null && groupId != Guid.Empty)
            {
                query = query.Where(m => m.Unit != null && (m.Unit.GroupId == groupId || (unitId != null && m.UnitId == unitId)));
            }
            else if (unitId != null && unitId != Guid.Empty)
            {
                query = query.Where(m => m.UnitId == unitId);
            }

            var markers = await query.ToListAsync();

            return markers.Select(m => new MarkerDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                UnitType = m.UnitType ?? "SFGPUCI----",
                Latitude = m.Location?.Y ?? 0,
                Longitude = m.Location?.X ?? 0,
                TimeStamp_Created = m.TimeStamp_Created,
                TimeStamp_Updated = m.TimeStamp_Updated,
                CreatedById = m.CreatedById,
                CreatedByName = m.CreatedBy?.Name,
                UnitId = m.UnitId,
                GroupId = null // Client never receives groupId
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting markers");
            return new List<MarkerDto>();
        }
    }

    public async Task<MarkerDto> AddPersistentMarker(MarkerDto dto)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Unauthorized: Authentication required to add markers.");
        }

        try
        {
            var appUser = await _userManager.GetUserAsync(Context.User);
            if (appUser == null)
            {
                throw new HubException("Unauthorized: User not found.");
            }

            var profile = await _dbContext.Profiles
                .Include(p => p.Group)
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);

            var markerId = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid();
            var unitId = profile?.UnitId ?? dto.UnitId;
            var createdById = profile?.ID ?? dto.CreatedById;

            var marker = new PersistentMarker
            {
                Id = markerId,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? "Tactical Marker" : dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                UnitType = string.IsNullOrWhiteSpace(dto.UnitType) ? "SFGPUCI----" : dto.UnitType.Trim(),
                Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
                TimeStamp_Created = DateTime.UtcNow,
                TimeStamp_Updated = DateTime.UtcNow,
                CreatedById = createdById,
                UnitId = unitId
            };

            _dbContext.PersistentMarkers.Add(marker);
            await _dbContext.SaveChangesAsync();

            var currentGroupId = await GetGroupIdForCurrentUser() ?? profile?.GroupId ?? profile?.Unit?.GroupId;

            var resultDto = new MarkerDto
            {
                Id = marker.Id,
                Name = marker.Name,
                Description = marker.Description,
                UnitType = marker.UnitType,
                Latitude = marker.Location.Y,
                Longitude = marker.Location.X,
                TimeStamp_Created = marker.TimeStamp_Created,
                TimeStamp_Updated = marker.TimeStamp_Updated,
                CreatedById = marker.CreatedById,
                CreatedByName = profile?.Name ?? dto.CreatedByName,
                UnitId = marker.UnitId,
                GroupId = null // Client never receives groupId
            };

            if (currentGroupId.HasValue && currentGroupId.Value != Guid.Empty)
            {
                await Clients.Group(currentGroupId.Value.ToString()).SendAsync("MarkerAdded", resultDto);
            }
            else
            {
                await Clients.Caller.SendAsync("MarkerAdded", resultDto);
            }

            return resultDto;
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving PersistentMarker");
            throw;
        }
    }

    public async Task<MarkerDto> UpdateMarker(MarkerDto dto)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Unauthorized: Authentication required to update markers.");
        }

        try
        {
            var marker = await _dbContext.PersistentMarkers.FindAsync(dto.Id);
            if (marker != null)
            {
                marker.Name = string.IsNullOrWhiteSpace(dto.Name) ? marker.Name : dto.Name.Trim();
                marker.Description = dto.Description?.Trim();
                marker.UnitType = string.IsNullOrWhiteSpace(dto.UnitType) ? marker.UnitType : dto.UnitType.Trim();
                marker.Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
                marker.TimeStamp_Updated = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                dto.TimeStamp_Updated = marker.TimeStamp_Updated;
            }
            else
            {
                dto.TimeStamp_Updated = DateTime.UtcNow;
            }

            dto.GroupId = null; // Client never receives groupId

            var currentGroupId = await GetGroupIdForCurrentUser();
            if (currentGroupId.HasValue && currentGroupId.Value != Guid.Empty)
            {
                await Clients.Group(currentGroupId.Value.ToString()).SendAsync("MarkerUpdated", dto);
            }
            else
            {
                await Clients.Caller.SendAsync("MarkerUpdated", dto);
            }

            return dto;
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating marker {MarkerId}", dto.Id);
            throw;
        }
    }

    public async Task MoveMarker(Guid markerId, double latitude, double longitude)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Unauthorized: Authentication required to move markers.");
        }

        try
        {
            var marker = await _dbContext.PersistentMarkers.FindAsync(markerId);
            DateTime updatedTime = DateTime.UtcNow;
            if (marker != null)
            {
                marker.Location = new Point(longitude, latitude) { SRID = 4326 };
                marker.TimeStamp_Updated = updatedTime;
                await _dbContext.SaveChangesAsync();
            }

            var currentGroupId = await GetGroupIdForCurrentUser();
            if (currentGroupId.HasValue && currentGroupId.Value != Guid.Empty)
            {
                await Clients.Group(currentGroupId.Value.ToString()).SendAsync("MarkerMoved", markerId, latitude, longitude, updatedTime);
            }
            else
            {
                await Clients.Caller.SendAsync("MarkerMoved", markerId, latitude, longitude, updatedTime);
            }
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving marker {MarkerId}", markerId);
            throw;
        }
    }

    public async Task DeleteMarker(Guid markerId)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Unauthorized: Authentication required to delete markers.");
        }

        try
        {
            var marker = await _dbContext.PersistentMarkers.FindAsync(markerId);
            if (marker != null)
            {
                _dbContext.PersistentMarkers.Remove(marker);
                await _dbContext.SaveChangesAsync();
            }

            var currentGroupId = await GetGroupIdForCurrentUser();
            if (currentGroupId.HasValue && currentGroupId.Value != Guid.Empty)
            {
                await Clients.Group(currentGroupId.Value.ToString()).SendAsync("MarkerDeleted", markerId);
            }
            else
            {
                await Clients.Caller.SendAsync("MarkerDeleted", markerId);
            }
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting marker {MarkerId}", markerId);
            throw;
        }
    }
}
