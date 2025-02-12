using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using BookWormsOnline.Data;
using BookWormsOnline.Models;

namespace BookWormsOnline.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ResetPasswordModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        [Required]
        public string Token { get; set; }

        [BindProperty]
        [Required, DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [BindProperty]
        [Required, DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public string Message { get; set; }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("Login");
            }

            var user = await _db.Members.FirstOrDefaultAsync(m => m.PasswordResetToken == token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                Message = "Invalid or expired token.";
                return Page();
            }

            Token = token; // Keep token in BindProperty for post-back
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _db.Members.FirstOrDefaultAsync(m => m.PasswordResetToken == Token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                Message = "Invalid or expired token.";
                return Page();
            }

            // Hash new password and update user
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpiry = null;

            await _db.SaveChangesAsync();

            Message = "Password reset successfully! Please log in.";
            return RedirectToPage("Login");
        }
    }
}
