﻿using EasyCrypto.Validation;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EasyCrypto
{
    /// <summary>
    /// AES encryption helper class
    /// </summary>
    public class AesEncryption
    {
        /// <summary>
        /// Encrypts string and returns string. Salt and IV will be embeded to encrypted string.
        /// Can later be decrypted with <see cref="DecryptWithPassword(string, string)"/>
        /// IV and salt are generated by <see cref="CryptoRandom"/> which is using System.Security.Cryptography.Rfc2898DeriveBytes.
        /// IV size is 16 bytes (128 bits) and key size will be 32 bytes (256 bits).
        /// </summary>
        /// <param name="dataToEncrypt">String to encrypt</param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <returns>Encrypted string</returns>
        public static string EncryptWithPassword(string dataToEncrypt, string password)
        {
            byte[] data = Encoding.UTF8.GetBytes(dataToEncrypt);
            byte[] result = EncryptWithPassword(data, password);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts string with embeded salt and IV that are encrypted with <see cref="EncryptWithPassword(string, string)"/>
        /// </summary>
        /// <param name="dataToDecrypt">string to decrypt</param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <returns>Decrypted string</returns>
        public static string DecryptWithPassword(string dataToDecrypt, string password)
        {
            byte[] data = Convert.FromBase64String(dataToDecrypt);
            byte[] result = DecryptWithPassword(data, password);
            return Encoding.UTF8.GetString(result);
        }

        /// <summary>
        /// Encrypts byte array and returns byte array. Salt and IV will be embeded in result.
        /// Can be later decrypted using <see cref="DecryptWithPassword(byte[], string)"/>
        /// IV and salt are generated by <see cref="CryptoRandom"/> which is using System.Security.Cryptography.Rfc2898DeriveBytes.
        /// IV size is 16 bytes (128 bits) and key size will be 32 bytes (256 bits).
        /// </summary>
        /// <param name="dataToEncrypt">Bytes to encrypt</param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <returns>Encrypted bytes</returns>
        public static byte[] EncryptWithPassword(byte[] dataToEncrypt, string password)
            => HandleByteToStream(dataToEncrypt, (inStream, outStream) => EncryptWithPassword(inStream, password, outStream));

        /// <summary>
        /// Decrypts byte array with embeded salt and IV that is encrypted with <see cref="EncryptWithPassword(byte[], string)"/>
        /// </summary>
        /// <param name="dataToDecrypt"></param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <returns>Decrypted bytes</returns>
        public static byte[] DecryptWithPassword(byte[] dataToDecrypt, string password)
            => HandleByteToStream(dataToDecrypt, (inStream, outStream) => DecryptWithPassword(inStream, password, outStream));

        /// <summary>
        /// Reads data from stream (parameter dataToEncrypt) and writes encrypted data to stream (parameter destination).
        /// Can be later decrypted using <see cref="DecryptWithPassword(Stream, string, Stream)"/>.
        /// Salt and IV will be embeded to resulting stream.
        /// IV and salt are generated by <see cref="CryptoRandom"/> which is using System.Security.Cryptography.Rfc2898DeriveBytes.
        /// IV size is 16 bytes (128 bits) and key size will be 32 bytes (256 bits).
        /// </summary>
        /// <param name="dataToEncrypt">Stream containing data to encrypt</param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <param name="destination">Stream to which to write encrypted data</param>
        /// <param name="keySize">Optional key size in bytes. Valid values are 16, 24 and 32. Default is 32.</param>
        public static void EncryptWithPassword(Stream dataToEncrypt, string password, Stream destination, int keySize = 32)
        {
            if (!(new[] { 16, 24, 32 }).Contains(keySize)) throw new ArgumentException($"{nameof(keySize)} must be 16, 24 or 32 bytes");
            byte[] salt;
            var ph = new PasswordHasher((uint)keySize);
            byte[] key = ph.HashPasswordAndGenerateSalt(password, out salt);
            destination.Write(BitConverter.GetBytes(salt.Length), 0, sizeof(int));
            destination.Write(salt, 0, salt.Length);
            EncryptAndEmbedIv(dataToEncrypt, key, destination);
        }

        /// <summary>
        /// Reads data from stream (parameter dataToEncrypt) and writes decrypted data to stream (parameter destination).
        /// Result can be lated decrypted with <see cref="DecryptWithPassword(Stream, string, Stream)"/>
        /// IV and salt will are read from input data (parameter dataToDecrypt).
        /// </summary>
        /// <param name="dataToDecrypt">Stream containing data to decrypt</param>
        /// <param name="password">Password that is used for generating key for encryption/decryption</param>
        /// <param name="destination">Stream to which to write decrypted data</param>
        public static void DecryptWithPassword(Stream dataToDecrypt, string password, Stream destination)
        {
            byte[] sizeOfSaltBytes = new byte[sizeof(int)];
            dataToDecrypt.Read(sizeOfSaltBytes, 0, sizeOfSaltBytes.Length);
            int sizeOfSalt = BitConverter.ToInt32(sizeOfSaltBytes, 0);
            byte[] salt = new byte[sizeOfSalt];
            dataToDecrypt.Read(salt, 0, salt.Length);
            var ph = new PasswordHasher((uint)sizeOfSalt);
            byte[] key = ph.HashPassword(password, salt);
            DecryptWithEmbededIv(dataToDecrypt, key, destination);
        }

        /// <summary>
        /// Encrypts bytes and embeds IV. Can be decrypted with <see cref="DecryptWithEmbededIv(byte[], byte[])"/>
        /// IV is generated by <see cref="CryptoRandom"/> which is using System.Security.Cryptography.Rfc2898DeriveBytes.
        /// IV size is 16 bytes (128 bits) and key size will be 32 bytes (256 bits).
        /// </summary>
        /// <param name="dataToEncrypt">Bytes to encrypt</param>
        /// <param name="key">Key that will be used for encryption/decryption</param>
        /// <returns>Byte array, encrypted data with embeded IV</returns>
        public static byte[] EncryptAndEmbedIv(byte[] dataToEncrypt, byte[] key) 
            => HandleByteToStream(dataToEncrypt, (inStream, outStream) => EncryptAndEmbedIv(inStream, key, outStream));

        /// <summary>
        /// Decrypts bytes with embeded IV encrypted with <see cref="EncryptAndEmbedIv(byte[], byte[])"/>
        /// </summary>
        /// <param name="dataToDecrypt">Bytes, data with embeded IV, to decrypt</param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <returns>Byte array, encrypted data</returns>
        public static byte[] DecryptWithEmbededIv(byte[] dataToDecrypt, byte[] key) 
            => HandleByteToStream(dataToDecrypt, (inStream, outStream) => DecryptWithEmbededIv(inStream, key, outStream));

        /// <summary>
        /// Encrypts and embeds IV into result. Data is read from stream and encrypted data is wrote to stream.
        /// Can be decrypted with <see cref="DecryptWithEmbededIv(Stream, byte[], Stream)"/>
        /// IV is generated by <see cref="CryptoRandom"/> which is using System.Security.Cryptography.Rfc2898DeriveBytes.
        /// IV size is 16 bytes (128 bits).
        /// </summary>
        /// <param name="dataToEncrypt">Stream containing data to encrypt</param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="destination"></param>
        public static void EncryptAndEmbedIv(Stream dataToEncrypt, byte[] key, Stream destination)
        {
            byte[] iv = CryptoRandom.NextBytesStatic(16);
            destination.Write(iv, 0, iv.Length);
            Encrypt(dataToEncrypt, key, iv, destination);
        }

        /// <summary>
        /// Decrypts data with embeded IV, that is encrypted with <see cref="EncryptAndEmbedIv(Stream, byte[], Stream)"/>, into result. 
        /// Data is read from stream and decrypted data is wrote to stream.
        /// </summary>
        /// <param name="dataToDecrypt">Stream containing data to decrypt.</param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="destination">Stream to which decrypted data will be wrote.</param>
        public static void DecryptWithEmbededIv(Stream dataToDecrypt, byte[] key, Stream destination)
        {
            byte[] iv = new byte[16];
            dataToDecrypt.Read(iv, 0, iv.Length);
            Decrypt(dataToDecrypt, key, iv, destination);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataToEncrypt"></param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="iv">Initialization vector, must be 16 bytes</param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] dataToEncrypt, byte[] key, byte[] iv) 
            => HandleByteToStream(dataToEncrypt, (inStream, outStream) => Encrypt(inStream, key, iv, outStream));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataToDecrypt"></param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="iv">Initialization vector, must be 16 bytes</param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] dataToDecrypt, byte[] key, byte[] iv) 
            => HandleByteToStream(dataToDecrypt, (inStream, outStream) => Decrypt(inStream, key, iv, outStream));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataToEncrypt">Stream with data to decrypt.</param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="iv">Initialization vector, must be 16 bytes</param>
        /// <param name="destination">Stream to which encrypted data will be wrote.</param>
        public static void Encrypt(Stream dataToEncrypt, byte[] key, byte[] iv, Stream destination)
            => Encrypt(false, dataToEncrypt, key, iv, destination);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataToDecrypt">Stream with data to encrypt.</param>
        /// <param name="key">Key that will be used for encryption/decryption, must be 16, 24 or 32 bytes long.</param>
        /// <param name="iv">Initialization vector, must be 16 bytes</param>
        /// <param name="destination">Stream to which decrypted data will be wrote.</param>
        public static void Decrypt(Stream dataToDecrypt, byte[] key, byte[] iv, Stream destination)
            => Decrypt(false, dataToDecrypt, key, iv, destination);

        internal static void Encrypt(bool skipValidations, Stream dataToEncrypt, byte[] key, byte[] iv, Stream destination)
        {
            if (iv == null || iv.Length != 16) throw new ArgumentException($"{nameof(iv)} must be 16 bytes in length");
            if (key == null || !(new[] { 16, 24, 32 }).Contains(key.Length)) throw new ArgumentException($"{nameof(key)} must 16, 24 or 32 bytes in length");

            using (var aes = new AesManaged())
            {
                aes.IV = iv;
                aes.Key = key;
                aes.Padding = PaddingMode.ISO10126;
                using (var encryptor = aes.CreateEncryptor())
                {
                    CryptoStream cs = new CryptoStream(destination, encryptor, CryptoStreamMode.Write);
                    int bufferSize = aes.BlockSize;
                    byte[] buffer = new byte[bufferSize];
                    int read = 0;
                    while ((read = dataToEncrypt.Read(buffer, 0, bufferSize)) > 0)
                    {
                        cs.Write(buffer, 0, read);
                        cs.Flush();
                    }
                    cs.FlushFinalBlock();
                }
            }
        }

        internal static void Decrypt(bool skipValidations, Stream dataToDecrypt, byte[] key, byte[] iv, Stream destination)
        {
            //if (!skipValidations)
            //{
            //    KeyCheckValueValidator.ValidateKeyCheckValue(key, dataToDecrypt);
            //}

            if (iv == null || iv.Length != 16) throw new ArgumentException($"{nameof(iv)} must be 16 bytes in length");
            if (key == null || !(new[] { 16, 24, 32 }).Contains(key.Length)) throw new ArgumentException($"{nameof(key)} must 16, 24 or 32 bytes in length");

            using (var aes = new AesManaged())
            {
                aes.IV = iv;
                aes.Key = key;
                aes.Padding = PaddingMode.ISO10126;
                using (var decryptor = aes.CreateDecryptor())
                {
                    CryptoStream cs = new CryptoStream(destination, decryptor, CryptoStreamMode.Write);
                    int bufferSize = aes.BlockSize;
                    byte[] buffer = new byte[bufferSize];
                    int read = 0;
                    while ((read = dataToDecrypt.Read(buffer, 0, bufferSize)) > 0)
                    {
                        cs.Write(buffer, 0, read);
                        cs.Flush();
                    }
                    cs.FlushFinalBlock();
                }
            }
        }

        internal static byte[] HandleByteToStream(byte[] data, Action<Stream, Stream> action)
        {
            byte[] result;
            using (Stream inStream = new MemoryStream())
            using (Stream outStream = new MemoryStream())
            {
                inStream.Write(data, 0, data.Length);
                inStream.Flush();
                inStream.Position = 0;
                action(inStream, outStream);
                outStream.Position = 0;
                result = new byte[outStream.Length];
                outStream.Read(result, 0, result.Length);
            }
            return result;
        }

    }
}
