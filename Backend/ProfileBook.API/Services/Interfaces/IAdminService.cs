using ProfileBook.API.DTOs;

namespace ProfileBook.API.Services.Interfaces
{
    public interface IAdminService
    {
        object GetDashboardStats();
        List<object> GetUsers();
        string DeleteUser(int id);
        object UpdateUser(int id, UpdateUserDto dto);
        object CreateAdmin(CreateAdminDto dto);
        object CreateUser(CreateAdminDto dto);
        object ExtendCredentials(int userId, int additionalMinutes);
        List<object> GetExpiringCredentials(int daysAhead = 7);
        List<object> GetExpiredCredentials();
        object DeactivateExpiredCredentials();
        object SetMainAdmin(int userId);
        object GetAdminProfile(int userId);
        bool IsMainAdmin(int userId);
        bool IsMainAdmin(string email);
    }
}
