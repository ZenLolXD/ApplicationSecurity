using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using BookWormsOnline.Data;
using BookWormsOnline.Models;

namespace BookWormsOnline.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public ForgotPasswordModel(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [BindProperty]
        [Required, EmailAddress]
        public string Email { get; set; }

        public string Message { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _db.Members.FirstOrDefaultAsync(m => m.Email == Email);
            if (user == null)
            {
                Message = "If the email exists, a reset link has been sent.";
                return Page();
            }

            // Generate token
            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15); // Token valid for 15 minutes
            await _db.SaveChangesAsync();

            // Send Email
            bool emailSent = SendResetEmail(user.Email, user.PasswordResetToken);
            if (!emailSent)
            {
                Message = "Error sending reset email.";
                return Page();
            }

            Message = "Check your email for the reset link.";
            return Page();
        }

        private bool SendResetEmail(string email, string token)
        {
            try
            {
                string resetLink = $"{Request.Scheme}://{Request.Host}/ResetPassword?token={token}";

                var smtpClient = new SmtpClient(_config["SMTP:Host"])
                {
                    Port = int.Parse(_config["SMTP:Port"]),
                    Credentials = new NetworkCredential(_config["SMTP:User"], _config["SMTP:Password"]),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_config["SMTP:User"]),
                    Subject = "Password Reset Request",
                    Body = $"Click <a href='{resetLink}'>here</a> to reset your password.",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);
                smtpClient.Send(mailMessage);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
