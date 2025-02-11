using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using BookWormsOnline.Data;
using BookWormsOnline.Models;

namespace BookWormsOnline.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public Member CurrentUser { get; set; }

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnGetAsync()
        
        {
            string? userIdString = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToPage("Login");
            }

            string? sessionId = HttpContext.Session.GetString("SessionId");

            var user = await _db.Members.FindAsync(userId);
            if (user == null || user.SessionId != sessionId)
            {
                HttpContext.Session.Clear();
                return RedirectToPage("Login");
            }

            // ✅ Encode output before displaying
            user.Email = System.Net.WebUtility.HtmlEncode(user.Email);
            user.FirstName = System.Net.WebUtility.HtmlEncode(user.FirstName);
            user.LastName = System.Net.WebUtility.HtmlEncode(user.LastName);

            CurrentUser = user;
            return Page();
        }
    }
}
