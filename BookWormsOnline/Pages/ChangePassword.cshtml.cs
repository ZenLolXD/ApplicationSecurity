using System;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using BookWormsOnline.Data;
using BookWormsOnline.Models;
using System.Net;

namespace BookWormsOnline.Pages
{
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public ChangePasswordModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string OldPassword { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmNewPassword { get; set; }

        public string ErrorMessage { get; set; }

        private const int MinPasswordAgeMinutes = 10;  // Minimum time before changing password again
        private const int MaxPasswordAgeDays = 90;     // Maximum allowed time before forcing a password change

        public async Task<IActionResult> OnPostAsync()
        {
            // ✅ Get logged-in user ID from session
            string? userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToPage("/Login");
            }

            // ✅ Retrieve user from database
            var user = await _db.Members.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                ErrorMessage = "User not found or password is invalid.";
                return Page();
            }

            // ✅ Enforce minimum password age (prevent frequent changes)
            TimeSpan minAgeSpan = TimeSpan.FromMinutes(MinPasswordAgeMinutes);
            if (DateTime.UtcNow - user.LastPasswordChange < minAgeSpan)
            {
                ErrorMessage = $"You must wait at least {MinPasswordAgeMinutes} minutes before changing your password again.";
                return Page();
            }

            // ✅ Enforce maximum password age (force password change)
            TimeSpan maxAgeSpan = TimeSpan.FromDays(MaxPasswordAgeDays);
            if (DateTime.UtcNow - user.LastPasswordChange > maxAgeSpan)
            {
                ErrorMessage = "Your password has expired. Please update your password.";
                return Page();
            }

            // ✅ Ensure password is in BCrypt format
            if (!user.PasswordHash.StartsWith("$2a$") && !user.PasswordHash.StartsWith("$2b$"))
            {
                ErrorMessage = "Your password format is invalid. Please reset your password.";
                return Page();
            }

            // ✅ Check if old password is correct
            if (!BCrypt.Net.BCrypt.Verify(OldPassword, user.PasswordHash))
            {
                ErrorMessage = "Incorrect old password.";
                return Page();
            }

            // ✅ Validate new password complexity
            if (!IsValidPassword(NewPassword))
            {
                ErrorMessage = "Password must be at least 12 characters and include an uppercase letter, lowercase letter, number, and special character.";
                return Page();
            }

            // ✅ Ensure new password matches confirmation
            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "New passwords do not match.";
                return Page();
            }

            // ✅ Prevent password reuse (last 2 passwords)
            if (BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash) ||
                (!string.IsNullOrEmpty(user.OldPasswordHash1) && BCrypt.Net.BCrypt.Verify(NewPassword, user.OldPasswordHash1)) ||
                (!string.IsNullOrEmpty(user.OldPasswordHash2) && BCrypt.Net.BCrypt.Verify(NewPassword, user.OldPasswordHash2)))
            {
                ErrorMessage = "You cannot reuse any of your last 2 passwords.";
                return Page();
            }

            // ✅ Rotate password history
            user.OldPasswordHash2 = user.OldPasswordHash1; // Move previous history down
            user.OldPasswordHash1 = user.PasswordHash; // Store current password in history
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword); // Store new password
            user.LastPasswordChange = DateTime.UtcNow; // ✅ Update password change timestamp

            await _db.SaveChangesAsync();

            return RedirectToPage("/Index"); // ✅ Redirect after successful password change
        }

        // ✅ Enforces password strength requirements
        private bool IsValidPassword(string password)
        {
            return password.Length >= 12 &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsLower) &&
                   password.Any(char.IsDigit) &&
                   password.Any(ch => !char.IsLetterOrDigit(ch));
        }
    }
}
