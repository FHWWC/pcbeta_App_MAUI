using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// Service for encrypting and decrypting passwords using MAUI SecureStorage.
/// Passwords are encrypted locally and stored in secure storage.
/// </summary>
public class PasswordSecurityService
{
    private readonly string _encryptionKey = "PCBetaMAUI_SecureKey_v1"; // Fixed key for consistent encryption/decryption
    
    /// <summary>
    /// Encrypts a password and saves it to MAUI SecureStorage
    /// </summary>
    public async Task SavePasswordAsync(string username, string password)
    {
        try
        {
            var encrypted = EncryptPassword(password);
            await SecureStorage.SetAsync($"password_{username}", encrypted);
            await SecureStorage.SetAsync("last_username", username);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving password: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Retrieves and decrypts the saved password for a username
    /// </summary>
    public async Task<string?> GetPasswordAsync(string username)
    {
        try
        {
            var encrypted = await SecureStorage.GetAsync($"password_{username}");
            if (encrypted == null)
                return null;

            return DecryptPassword(encrypted);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving password: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the saved password for a username
    /// </summary>
    public async Task ClearPasswordAsync(string username)
    {
        try
        {
            SecureStorage.Remove($"password_{username}");
            SecureStorage.Remove("last_username");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing password: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a password is saved for the given username
    /// </summary>
    public async Task<bool> HasSavedPasswordAsync(string username)
    {
        try
        {
            var encrypted = await SecureStorage.GetAsync($"password_{username}");
            return !string.IsNullOrEmpty(encrypted);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the last used username
    /// </summary>
    public async Task<string?> GetLastUsernameAsync()
    {
        try
        {
            return await SecureStorage.GetAsync("last_username");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the security question and answer for a username
    /// </summary>
    public async Task SaveSecurityAnswerAsync(string username, string questionId, string answer)
    {
        try
        {
            var encryptedAnswer = EncryptPassword(answer);
            await SecureStorage.SetAsync($"question_{username}", questionId);
            await SecureStorage.SetAsync($"answer_{username}", encryptedAnswer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving security answer: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the saved security question ID for a username
    /// </summary>
    public async Task<string?> GetSecurityQuestionIdAsync(string username)
    {
        try
        {
            return await SecureStorage.GetAsync($"question_{username}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving security question: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the saved and decrypted security answer for a username
    /// </summary>
    public async Task<string?> GetSecurityAnswerAsync(string username)
    {
        try
        {
            var encrypted = await SecureStorage.GetAsync($"answer_{username}");
            if (encrypted == null)
                return null;

            return DecryptPassword(encrypted);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving security answer: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the saved security question and answer for a username
    /// </summary>
    public async Task ClearSecurityAnswerAsync(string username)
    {
        try
        {
            SecureStorage.Remove($"question_{username}");
            SecureStorage.Remove($"answer_{username}");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing security answer: {ex.Message}");
        }
    }

    /// <summary>
    /// Encrypts password using AES encryption
    /// </summary>
    private string EncryptPassword(string password)
    {
        try
        {
            using (var aes = Aes.Create())
            {
                // Derive key and IV from the encryption key
                var key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
                aes.Key = key;
                aes.IV = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(16).Substring(0, 16));

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(password);
                        }
                        var encrypted = ms.ToArray();
                        return Convert.ToBase64String(encrypted);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Encryption error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Decrypts password using AES decryption
    /// </summary>
    private string DecryptPassword(string encryptedPassword)
    {
        try
        {
            using (var aes = Aes.Create())
            {
                // Derive key and IV from the encryption key (must match encryption)
                var key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
                aes.Key = key;
                aes.IV = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(16).Substring(0, 16));

                var encrypted = Convert.FromBase64String(encryptedPassword);
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                using (var ms = new MemoryStream(encrypted))
                {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (var sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Decryption error: {ex.Message}");
            throw;
        }
    }
}
