using BlazeTAK.Client.Models;
using BlazeTAK.Data;

namespace BlazeTAK.Services;

public interface IUserLocationService
{
    Task<(bool isSignificantUserMove, bool unitMarkersChanged, UserLocationDto? userLocation, List<LiveUnitMarkerDto> unitMarkers)> UpdateUserLocationAsync(
        Guid userId, 
        LocationUpdateDto location, 
        ApplicationDbContext dbContext);

    UserLocationDto? GetUserLocation(Guid userId);
    
    List<UserLocationDto> GetUnitUserLocations(Guid unitId);
    
    Task<List<LiveUnitMarkerDto>> GetGroupLiveUnitMarkersAsync(Guid groupId, ApplicationDbContext dbContext);
    
    Task<List<LiveUnitMarkerDto>> CalculateUnitMarkersForUnitAsync(Guid unitId, ApplicationDbContext dbContext);
    
    (bool removed, Guid? unitId, Guid? groupId) RemoveUserLocation(Guid userId);
}
