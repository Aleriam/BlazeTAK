using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using BlazeTAK.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace BlazeTAK.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserInfoService _userInfoService;

    public ChatHub(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserLocationService userLocationService,
        IUserInfoService userInfoService,
        ILogger<MapHub> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _userInfoService = userInfoService;
    }
    
    
    
    private async Task<User?> GetCurrentUserProfileAsync()
    {
        var appUser = await _userManager.GetUserAsync(Context.User);
        if (appUser == null)
            return null;
        return await _userInfoService.GetCurrentUserAsync(appUser, _dbContext);
    }
    
}