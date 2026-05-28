using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

public class EncryptionHelper
{
    private static AsymmetricKeyParameter publicKeyPar;
    private static AsymmetricKeyParameter privateKeyPar;
    private static byte[] publicKeyDer;
    private static byte[] privateKeyDer;
    private static byte[] aesKey;

    // 取得 RSA 公鑰
    public static byte[] GetKey()
    {
        try
        {
            // 產生金鑰對
            RsaKeyPairGenerator rsaPair = new RsaKeyPairGenerator();
            rsaPair.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            AsymmetricCipherKeyPair keyPair = rsaPair.GenerateKeyPair();
            publicKeyPar = keyPair.Public;
            privateKeyPar = keyPair.Private;

            // 匯出公鑰 DER (PKCS#1 SubjectPublicKeyInfo)
            SubjectPublicKeyInfo spInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKeyPar);
            publicKeyDer = spInfo.ToAsn1Object().GetDerEncoded();
            // publicKeyDer 就是 DER 格式二進位資料，可以直接存檔

            // 匯出私鑰 DER (PKCS#8)
            PrivateKeyInfo pInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKeyPar);
            privateKeyDer = pInfo.ToAsn1Object().GetDerEncoded();
            // privateKeyDer 就是 DER 格式二進位資料，可以直接存檔

            return publicKeyDer;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("An unexpected error occurred during key generation.", ex);
        }
    }

    // RSA 解密
    public static byte[] Decrypt(byte[] dataToDecrypt)
    {
        if (privateKeyPar == null)
        {
            LogUtils.LogError("Key is null " + nameof(privateKeyPar));
            return null;
        }

        if (dataToDecrypt == null)
        {
            LogUtils.LogError("Data is null " + nameof(dataToDecrypt));
            return null;
        }

        try
        {
            IAsymmetricBlockCipher decEngine = new OaepEncoding(new RsaEngine(), new Sha256Digest());
            decEngine.Init(false, privateKeyPar); // false: 解密, 使用私鑰

            byte[] decryptedData = decEngine.ProcessBlock(dataToDecrypt, 0, dataToDecrypt.Length);
            return decryptedData;
        }
        catch (CryptographicException ex)
        {
            // 對內：記錄為潛在的安全事件
            LogUtils.LogWarning("解密失敗，可能是金鑰錯誤或資料被竄改。" + ex);

            // 對外：回傳通用的失敗信號，不洩漏任何細節
            return null;
        }
        catch (Exception ex)
        {
            LogUtils.LogError("解密時發生未預期的系統錯誤。" + ex);
            return null;
        }
    }

    // 儲存 AES
    public static void Save(byte[] data)
    {
        aesKey = data;
        //string hexStringWithDashes = BitConverter.ToString(aesKey);
        //string hexString = hexStringWithDashes.Replace("-", "").ToLower();
        //Debug.Log(hexString);
    }

    // 檢查是否需要加密
    public static bool CheckKey()
    {
        // 有 key 就要加密
        return aesKey != null;
    }

    // AES 加密
    public static byte[] AESEncrypt(byte[] plainBytes)
    {
        if (aesKey == null || (aesKey.Length != 16 && aesKey.Length != 24 && aesKey.Length != 32))
        {
            // 記錄詳細錯誤給開發者
            LogUtils.LogError("Encrypt fail: Invalid key length error.");
            //throw new ArgumentException("Invalid key length error", nameof(aesKey));
            return null;
        }

        if (plainBytes == null || plainBytes.Length == 0)
        {
            LogUtils.LogError("Encrypt fail：Plain is null or empty.");
            //throw new ArgumentNullException(nameof(plainBytes));
            return null;
        }

        if (plainBytes.Length < 12 + 16)
        {
            LogUtils.LogError("input too short.");
            //throw new ArgumentException("input too short");
            return null;
        }

        try
        {
            // 產生 12 bytes nonce
            byte[] nonce = new byte[12];
            new SecureRandom().NextBytes(nonce);

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(aesKey), 128, nonce, null);
            cipher.Init(true, parameters);

            byte[] output = new byte[cipher.GetOutputSize(plainBytes.Length)];
            int len = cipher.ProcessBytes(plainBytes, 0, plainBytes.Length, output, 0);
            len += cipher.DoFinal(output, len);

            // tag 是 output 最後 16 bytes
            byte[] tag = new byte[16];
            Array.Copy(output, output.Length - tag.Length, tag, 0, tag.Length);

            // ciphertext 是 output 前面部分
            byte[] ciphertext = new byte[output.Length - tag.Length];
            Array.Copy(output, 0, ciphertext, 0, ciphertext.Length);

            // 組合 nonce + ciphertext + tag
            byte[] result = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

            return result;
        }
        catch (CryptographicException ex)
        {
            // 對內：記錄詳細日誌
            // Log.Error("加密過程中發生密碼學錯誤。", ex);
            // 對外：拋出通用例外或回傳 null
            LogUtils.LogError("加密資料時發生內部錯誤。" + ex);
            return null;
        }

    }

    // AES 解密
    public static byte[] AESDecrypt(byte[] input)
    {
        if (aesKey == null || (aesKey.Length != 16 && aesKey.Length != 24 && aesKey.Length != 32))
        {
            // 記錄詳細錯誤給開發者
            LogUtils.LogError("Decrypt fail: Invalid key length error." + nameof(aesKey));
            return null;
        }

        if (input == null)
        {
            LogUtils.LogError("Decrypt fail：input is null or empty.");
            //throw new ArgumentNullException(nameof(plainBytes));
            return null;
        }

        if (input.Length < 12 + 16)
        {
            LogUtils.LogError("input too short.");
            //throw new ArgumentException("input too short");
            return null;
        }

        try
        {
            byte[] nonce = new byte[12];
            Array.Copy(input, 0, nonce, 0, 12);

            byte[] tag = new byte[16];
            Array.Copy(input, input.Length - 16, tag, 0, 16);

            int ciphertextLen = input.Length - 12 - 16;
            byte[] ciphertext = new byte[ciphertextLen];
            Array.Copy(input, 12, ciphertext, 0, ciphertextLen);

            // 組合 ciphertext + tag（BouncyCastle 需要這種格式）
            byte[] cipherAndTag = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, cipherAndTag, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, cipherAndTag, ciphertext.Length, tag.Length);

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(aesKey), 128, nonce, null);
            cipher.Init(false, parameters);

            byte[] plain = new byte[cipher.GetOutputSize(cipherAndTag.Length)];
            int len = cipher.ProcessBytes(cipherAndTag, 0, cipherAndTag.Length, plain, 0);
            len += cipher.DoFinal(plain, len);

            // 回傳明文
            if (len == plain.Length)
                return plain;
            byte[] result = new byte[len];
            Array.Copy(plain, 0, result, 0, len);
            return result;
        }
        catch (CryptographicException ex)
        {
            // 對內：記錄為潛在的安全事件
            LogUtils.LogWarning("解密失敗，可能是金鑰錯誤或資料被竄改。" + ex);

            // 對外：回傳通用的失敗信號，不洩漏任何細節
            return null;
        }
        catch (Exception ex)
        {
            LogUtils.LogError("解密時發生未預期的系統錯誤。" + ex);
            return null;
        }
    }
}