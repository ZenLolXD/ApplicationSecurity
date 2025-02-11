using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BookWormsOnline.Data;
using BookWormsOnline.Models;
using System.Net;

namespace BookWormsOnline.Pages
{
    [ValidateAntiForgeryToken]
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RegisterModel> _logger;

        [BindProperty]
        public Member Member { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        [BindProperty]
        public IFormFile Photo { get; set; }

        public RegisterModel(ApplicationDbContext db, ILogger<RegisterModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Validates that the password is at least 12 characters and includes uppercase, lowercase, digit, and special character.
        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;
            if (password.Length < 12)
                return false;
            
            string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{12,}$";
            return Regex.IsMatch(password, pattern);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Quick sanity check: if model binding already flagged an error, bail out.
                if (!ModelState.IsValid)
                    return Page();

                // ✅ Email Format Validation
                if (string.IsNullOrWhiteSpace(Member.Email) ||
                    !Regex.IsMatch(Member.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    ModelState.AddModelError("Member.Email", "Invalid email format.");
                    return Page();
                }

                // ✅ Phone Number Validation (Only digits, 8 to 15 characters)
                if (string.IsNullOrWhiteSpace(Member.MobileNo) ||
                    !Regex.IsMatch(Member.MobileNo, @"^\d{8,15}$"))
                {
                    ModelState.AddModelError("Member.MobileNo", "Invalid phone number. Only numbers are allowed.");
                    return Page();
                }

                // ✅ Address Length Validation (limit to 255 characters)
                if ((Member.BillingAddress != null && Member.BillingAddress.Length > 255) ||
                    (Member.ShippingAddress != null && Member.ShippingAddress.Length > 255))
                {
                    ModelState.AddModelError("Member.BillingAddress", "Address too long.");
                    return Page();
                }

                // ✅ Password Strength Validation
                if (!IsValidPassword(Password))
                {
                    ModelState.AddModelError("Password", "Password must be at least 12 characters and include an uppercase letter, lowercase letter, number, and special character.");
                    return Page();
                }

                // ✅ Ensure Passwords Match
                if (Password != ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                    return Page();
                }

                // ✅ Check if Email is Unique
                if (await _db.Members.AnyAsync(m => m.Email == Member.Email))
                {
                    _logger.LogWarning("Email already exists: {Email}", Member.Email);
                    ModelState.AddModelError("Member.Email", "Email already exists.");
                    return Page();
                }

                // ✅ Handle Photo Upload (if provided)
                if (Photo != null)
                {
                    string extension = Path.GetExtension(Photo.FileName).ToLower();
                    if (extension != ".jpg")
                    {
                        ModelState.AddModelError("Photo", "Only JPG files are allowed.");
                        return Page();
                    }
                    if (Photo.Length > 2 * 1024 * 1024) // 2MB
                    {
                        ModelState.AddModelError("Photo", "File size must be less than 2MB.");
                        return Page();
                    }
                    // Generate a unique filename to prevent overwrites
                    string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                    string filePath = Path.Combine("wwwroot", "uploads", uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Photo.CopyToAsync(stream);
                    }
                    Member.PhotoPath = "/uploads/" + uniqueFileName;
                }

                // ✅ Sanitize Inputs (Prevents XSS)
                Member.FirstName = WebUtility.HtmlEncode(Member.FirstName);
                Member.LastName = WebUtility.HtmlEncode(Member.LastName);
                Member.Email = WebUtility.HtmlEncode(Member.Email);
                Member.BillingAddress = WebUtility.HtmlEncode(Member.BillingAddress);
                Member.ShippingAddress = WebUtility.HtmlEncode(Member.ShippingAddress);

                // ✅ Hash the Password before saving
                Member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

                // ✅ Encrypt Credit Card Data Using AES-256
                Member.EncryptedCreditCard = EncryptCreditCard(Member.EncryptedCreditCard);
                _logger.LogInformation("Credit card encrypted.");

                // ✅ Save the New Member to the Database
                _db.Members.Add(Member);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Member successfully registered.");

                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed.");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return Page();
            }
        }

        // Encrypts the credit card data using AES-256.
        private string EncryptCreditCard(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                // 32-byte key for AES-256.
                aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
                // 16-byte IV.
                aes.IV = Encoding.UTF8.GetBytes("1234567890123456");

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }
    }
}
