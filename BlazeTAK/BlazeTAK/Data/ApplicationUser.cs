using BlazeTAK.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace BlazeTAK.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    // Domain profile of this login account (1:1). Created automatically by ApplicationDbContext on registration.
    public User? Profile { get; set; }
}
