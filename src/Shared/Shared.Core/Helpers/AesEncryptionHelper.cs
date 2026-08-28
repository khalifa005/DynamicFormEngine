using System.Security.Cryptography;
using System.Text;

namespace Shared.Core.Helpers;

public static class AesEncryptionHelper
{
    private const string KeyText = "aGY%Eabk$RxF$vJva1ha7q8x7AXaEA8P";
    private const string IvText = "u?LlJ8P20Q0s*3yA";

    public static string Encrypt(string plainText)
    {
        byte[] encrypted;
        string encryptedEncoded;

        byte[] key = Encoding.UTF8.GetBytes(KeyText);
        byte[] iv = Encoding.UTF8.GetBytes(IvText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }

                    encrypted = ms.ToArray();
                    encryptedEncoded = Convert.ToBase64String(encrypted);
                }
            }
        }

        return encryptedEncoded;
    }

    public static string Decrypt(string plainText)
    {
        string plaintext = string.Empty;
        byte[] key = Encoding.UTF8.GetBytes(KeyText);
        byte[] iv = Encoding.UTF8.GetBytes(IvText);
        plainText = plainText.Replace(' ', '+');
        byte[] cipherText = Convert.FromBase64String(plainText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream(cipherText))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader reader = new StreamReader(cs))
                    {
                        plaintext = reader.ReadToEnd();
                    }
                }
            }
        }

        return plaintext;
    }
}
