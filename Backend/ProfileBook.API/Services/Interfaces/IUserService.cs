using ProfileBook.API.DTOs;

namespace ProfileBook.API.Services.Interfaces
{
    public interface IUserService
    {
        string Register(RegisterDto dto);
        object Login(LoginDto dto);
        object ForgotPassword(ForgotPasswordDto dto);
        object ResetPassword(ResetPasswordDto dto);
        List<object> GetUsers();
        string DeleteUser(int id);
    }
}
