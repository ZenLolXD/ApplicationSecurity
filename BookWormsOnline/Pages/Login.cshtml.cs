using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BookWormsOnline.Data;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Net.Http;

namespace BookWormsOnline.Pages
{
    [ValidateAntiForgeryToken]
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string RecaptchaToken { get; set; }

        public string ErrorMessage { get; set; }

        private const int MaxFailedAttempts = 3; // Maximum allowed failed login attempts
        private const int LockoutMinutes = 5;      // Lockout duration in minutes

        public LoginModel(ApplicationDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<LoginModel> logger)
        {
            _db = db;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // ✅ Check for missing fields
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Email and Password are required.";
                return Page();
            }

            // ✅ Optionally validate reCAPTCHA if a token is provided
            if (!string.IsNullOrWhiteSpace(RecaptchaToken))
            {
                bool recaptchaValid = await ValidateRecaptcha(RecaptchaToken);
                if (!recaptchaValid)
                {
                    ErrorMessage = "reCAPTCHA validation failed. Please try again.";
                    return Page();
                }
            }

            // ✅ Sanitize and encode Email input
            Email = WebUtility.HtmlEncode(Email);

            // ✅ Validate Email format
            if (!Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ErrorMessage = "Invalid email format.";
                return Page();
            }

            // ✅ Retrieve user securely
            var user = await _db.Members.FirstOrDefaultAsync(m => m.Email == Email);
            if (user == null)
            {
                ErrorMessage = "Invalid email or password.";
                return Page();
            }

            // ✅ Check if the user is locked out
            if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value > DateTime.UtcNow)
            {
                ErrorMessage = $"Too many failed attempts. Try again at {user.LockoutEndTime.Value:HH:mm:ss}.";
                return Page();
            }

            // ✅ Verify the password
            if (!BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                // 🔥 Increment failed attempts
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    // 🔒 Lock the account for a specified duration
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    _logger.LogWarning("User {Email} locked out until {LockoutEndTime}", user.Email, user.LockoutEndTime);
                }
                await _db.SaveChangesAsync();
                ErrorMessage = "Invalid email or password.";
                return Page();
            }

            // ✅ Successful login: reset failed attempts and unlock the account
            user.FailedLoginAttempts = 0;
            user.LockoutEndTime = null;
            user.SessionId = Guid.NewGuid().ToString();
            await _db.SaveChangesAsync();

            // ✅ Store sanitized session variables
            HttpContext.Session.SetString("UserId", WebUtility.HtmlEncode(user.Id.ToString()));
            HttpContext.Session.SetString("UserEmail", WebUtility.HtmlEncode(user.Email));
            HttpContext.Session.SetString("SessionId", WebUtility.HtmlEncode(user.SessionId));

            return RedirectToPage("Index");
        }

        // ✅ Validates reCAPTCHA using Google's API
        private async Task<bool> ValidateRecaptcha(string token)
        {
            var secretKey = _config["GoogleReCaptcha:SecretKey"];
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetStringAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}"
            );

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var recaptchaResponse = JsonSerializer.Deserialize<RecaptchaResponse>(response, options);

            _logger.LogInformation("reCAPTCHA API Response: {Response}", response);

            if (recaptchaResponse == null || !recaptchaResponse.Success)
            {
                return false;
            }
            return recaptchaResponse.Score >= 0.5;
        }

        private class RecaptchaResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("score")]
            public float Score { get; set; }
        }
    }
}
