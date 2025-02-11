using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace BookWormsOnline.Models
{
    public class Member
    {
        [Key]
        public int Id { get; set; }

        public string? SessionId { get; set; } // ✅ Added SessionId (Nullable)
        [Required, StringLength(50)]
        public string FirstName { get; set; }

        [Required, StringLength(50)]
        public string LastName { get; set; }
        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; } // Hashed before saving

        [NotMapped]
        [Compare("PasswordHash", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } // Not mapped to DB

        [Required]
        public string EncryptedCreditCard { get; set; }

        [Required, Phone, StringLength(16, MinimumLength = 8)]
        public string MobileNo { get; set; }

        [Required, StringLength(255)]
        public string BillingAddress { get; set; }

        [Required, StringLength(255)]
        public string ShippingAddress { get; set; }

        [Required]
        public string PhotoPath { get; set; } = "/uploads/default.jpg"; // Default value


        public void EncodeEmail()
        {
            Email = System.Net.WebUtility.HtmlEncode(Email);
        }

        // Secure AES-256 Credit Card Encryption
        public void EncryptCreditCard(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32-byte key
            aes.IV = Encoding.UTF8.GetBytes("1234567890123456"); // 16-byte IV

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            EncryptedCreditCard = Convert.ToBase64String(encryptedBytes);
        }

        public string DecryptCreditCard()
        {
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32-byte key
            aes.IV = Encoding.UTF8.GetBytes("1234567890123456"); // 16-byte IV

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] encryptedBytes = Convert.FromBase64String(EncryptedCreditCard);
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        // 🔥 Track login attempts and lockout time
        public int FailedLoginAttempts { get; set; } = 0; // Default 0
        public DateTime? LockoutEndTime { get; set; } = null; // Nullable (only set if locked out)
    }
}
