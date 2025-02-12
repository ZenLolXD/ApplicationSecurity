using System;
using System.Collections.Generic;
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
        public Member Member { get; set; } = new Member();

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? Photo { get; set; }

        public RegisterModel(ApplicationDbContext db, ILogger<RegisterModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        private bool IsValidAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;
            if (address.Length > 255 || address.Length < 5)
                return false;

            // Updated regex: hyphen is placed at the end so it's treated as a literal.
            string pattern = @"^[a-zA-Z0-9\s,.'()&/#-]+$";
            return Regex.IsMatch(address, pattern);
        }

        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
                return false;
            
            string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{12,}$";
            return Regex.IsMatch(password, pattern);
        }

        private bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;
            
            string pattern = @"^\d{8,16}$";
            return Regex.IsMatch(phoneNumber, pattern);
        }

        private bool IsValidJpgFile(IFormFile file)
        {
            if (file == null)
                return false;
            if (file.ContentType != "image/jpeg")
                return false;
            
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg")
                return false;
            if (file.Length > 2 * 1024 * 1024)
                return false; // Limit to 2MB
            
            return true;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Remove ModelState entries for properties that aren't directly supplied by the user.
            ModelState.Remove("Member.PasswordHash");
            ModelState.Remove("Member.OldPasswordHash1");
            ModelState.Remove("Member.OldPasswordHash2");
            // Remove any leftover key for Member.ConfirmPassword if it exists.
            ModelState.Remove("Member.ConfirmPassword");
            // Remove the PageModel's ConfirmPassword key (if present).
            ModelState.Remove("ConfirmPassword");

            var validationErrors = new List<string>();

            // Validate Passwords
            if (string.IsNullOrWhiteSpace(Password))
                validationErrors.Add("Password is required.");
            if (Password != ConfirmPassword)
                validationErrors.Add("Passwords do not match.");
            if (!IsValidPassword(Password))
                validationErrors.Add("Password must be at least 12 characters and include an uppercase letter, lowercase letter, number, and special character.");

            // Validate Email
            if (await _db.Members.AnyAsync(m => m.Email.ToLower() == Member.Email.ToLower()))
                validationErrors.Add("Email already exists.");

            // Validate Phone Number
            if (!IsValidPhoneNumber(Member.MobileNo))
                validationErrors.Add("Invalid phone number. Only 8-16 digits are allowed.");

            // Validate Billing & Shipping Addresses
            if (!IsValidAddress(Member.BillingAddress))
                validationErrors.Add("Invalid billing address. Use letters, numbers, and common punctuation only.");
            if (!IsValidAddress(Member.ShippingAddress))
                validationErrors.Add("Invalid shipping address. Use letters, numbers, and common punctuation only.");

            // Validate Credit Card Number (if provided)
            if (!string.IsNullOrWhiteSpace(Member.EncryptedCreditCard) && Member.EncryptedCreditCard.Length < 16)
                validationErrors.Add("Invalid credit card number.");

            // Validate Profile Picture (if provided)
            if (Photo != null && !IsValidJpgFile(Photo))
                validationErrors.Add("Invalid file. Only JPG images under 2MB are allowed.");

            // Prevent Password Reuse (if previous password hashes exist)
            if ((!string.IsNullOrEmpty(Member.OldPasswordHash1) && BCrypt.Net.BCrypt.Verify(Password, Member.OldPasswordHash1)) ||
                (!string.IsNullOrEmpty(Member.OldPasswordHash2) && BCrypt.Net.BCrypt.Verify(Password, Member.OldPasswordHash2)))
            {
                validationErrors.Add("You cannot reuse your last 2 passwords.");
            }

            // If there are any errors, add them to ModelState and return the page.
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                return Page();
            }

            // Sanitize Inputs to prevent XSS
            Member.FirstName = WebUtility.HtmlEncode(Member.FirstName);
            Member.LastName = WebUtility.HtmlEncode(Member.LastName);
            Member.Email = WebUtility.HtmlEncode(Member.Email);
            Member.BillingAddress = WebUtility.HtmlEncode(Member.BillingAddress);
            Member.ShippingAddress = WebUtility.HtmlEncode(Member.ShippingAddress);
            Member.MobileNo = WebUtility.HtmlEncode(Member.MobileNo);

            // Hash the Password
            Member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

            // Rotate Password History
            Member.OldPasswordHash2 = Member.OldPasswordHash1;
            Member.OldPasswordHash1 = Member.PasswordHash;

            // Encrypt Credit Card Data (if provided)
            Member.EncryptedCreditCard = EncryptCreditCard(Member.EncryptedCreditCard);

            // Handle Profile Picture Upload (if provided)
            if (Photo != null)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }
                string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                string filePath = Path.Combine(uploadsDir, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Photo.CopyToAsync(stream);
                }
                Member.PhotoPath = "/uploads/" + uniqueFileName;
            }

            // Save the new member to the database.
            _db.Members.Add(Member);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }

        private string EncryptCreditCard(string plainText)
        {
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");
                aes.IV = Encoding.UTF8.GetBytes("1234567890123456");

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credit card encryption failed.");
                return "Encryption Failed";
            }
        }
    }
}
