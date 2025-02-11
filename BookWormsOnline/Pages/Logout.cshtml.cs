using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using BookWormsOnline.Data;
using BookWormsOnline.Models;
using System;

namespace BookWormsOnline.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public LogoutModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // ✅ Retrieve user session details safely
            string? userIdString = HttpContext.Session.GetString("UserId");

            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int userId))
            {
                var user = await _db.Members.FindAsync(userId);
                if (user != null)
                {
                    // ✅ Reset SessionId in database to prevent reuse
                    user.SessionId = null;
                    
                    // ✅ Log the logout event
                    await _db.AuditLogs.AddAsync(new AuditLog
                    {
                        UserEmail = user.Email,
                        Action = "User logged out",
                        Timestamp = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                }
            }

            // ✅ Remove session variables
            HttpContext.Session.Remove("UserId");
            HttpContext.Session.Remove("UserEmail");
            HttpContext.Session.Remove("SessionId");

            // ✅ Clear entire session
            HttpContext.Session.Clear();

            // ✅ Redirect to login page
            return RedirectToPage("/Login");
        }
    }
}
