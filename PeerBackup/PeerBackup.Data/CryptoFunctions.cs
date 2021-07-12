using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PeerBackup.Data
{
    // Thanks Eric J. from Stack Overflow for this nice cryptographically suitable random string generator that I tweaked and hopefully didn't break.
    public static class CryptoFunctions
    {
        internal static readonly char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890".ToCharArray();

        public static string GetNewNonce(int len)
        {
            byte[] data = new byte[4 * len];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;

                result.Append(chars[idx]);
            }

            return result.ToString();
        }

        public static string HashPassword(string username, string salt, string pass)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string value = string.Format("{0}{1}{2}", username, salt, pass);
                byte[] valueBytes = UTF8Encoding.UTF8.GetBytes(value);
                byte[] hashValue = sha.ComputeHash(valueBytes, 0, valueBytes.Length);
                return FormatBytes(hashValue);
            }
        }

        public static string FormatBytes(byte[] array)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in array)
            {
                sb.AppendFormat("{0:X2}", b);
            }
            return sb.ToString();
        }

    }
}
