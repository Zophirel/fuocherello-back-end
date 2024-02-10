using Fuocherello.Models;
namespace Fuocherello.Services.EmailService
{
    public interface IEmailService
    {
        bool SendEmail(EmailDTO request);
    }
}