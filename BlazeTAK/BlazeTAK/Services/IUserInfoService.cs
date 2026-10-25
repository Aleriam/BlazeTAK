using BlazeTAK.Data;
using BlazeTAK.Data.Models;

namespace BlazeTAK.Services;

public interface IUserInfoService
{
    Task<User?> GetCurrentUserAsync(ApplicationUser appUser, ApplicationDbContext dbContext);
}
