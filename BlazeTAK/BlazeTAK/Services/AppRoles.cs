namespace BlazeTAK.Services;

//names of ASP.NET Identity roles used across the app
// - admin: full control (appointed via Admin:Email seed config)
// - groupleader: may send messages on behalf of a whole group (appointed in Groups.razor)
// - user: default role of every registered account
public static class AppRoles
{
    public const string Admin = "admin";
    public const string GroupLeader = "groupleader";
    public const string User = "user";

    public static readonly string[] All = [Admin, GroupLeader, User];
}
