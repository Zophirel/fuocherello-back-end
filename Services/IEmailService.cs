using final.Models;
namespace final.Services.EmailService
{
    public interface IEmailService
    {
        bool SendEmail(EmailDTO request);
    }
}