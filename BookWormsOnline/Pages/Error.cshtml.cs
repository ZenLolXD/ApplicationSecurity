using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace BookWormsOnline.Pages
{
    public class ErrorModel : PageModel
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }

        public void OnGet(int? statusCode = null)
        {
            if (statusCode.HasValue)
            {
                StatusCode = statusCode.Value;

                switch (statusCode)
                {
                    case 404:
                        Message = "The page you are looking for does not exist.";
                        break;
                    case 403:
                        Message = "You do not have permission to access this page.";
                        break;
                    case 500:
                        Message = "Something went wrong on our side. Please try again later.";
                        break;
                    default:
                        Message = "An unexpected error occurred.";
                        break;
                }
            }
            else
            {
                StatusCode = 500;
                Message = "An unexpected error occurred.";
            }
        }
    }
}
