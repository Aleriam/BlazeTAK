using BlazeTAK.Data;
using BlazeTAK.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Services;

public class UserInfoService : IUserInfoService
{
    public async Task<User?> GetCurrentUserAsync(ApplicationUser appUser, ApplicationDbContext dbContext)
    {
        return await dbContext.Profiles
            .Include(p => p.Group)
            .Include(p => p.Unit)
            .ThenInclude(u => u!.Group)
            .FirstOrDefaultAsync(p => p.ApplicationUserId == appUser.Id);
    }
}
