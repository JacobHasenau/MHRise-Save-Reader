using System.Security.Cryptography;

namespace MHRise_Save_Reader;

internal sealed class SaveFileReader
{
    public byte[] ReadSaveData(string saveFilePath, ulong? steamId = null, int? curveIndex = null)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
        {
            throw new ArgumentException("The save file path cannot be empty.", nameof(saveFilePath));
        }

        var fullPath = Path.GetFullPath(saveFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The specified save file does not exist.", fullPath);
        }

        var saveBytes = File.ReadAllBytes(fullPath);
        if (saveBytes.Length == 0)
        {
            throw new InvalidDataException("The save file is empty.");
        }

        if (!CitrusDecryptor.LooksLikeCitrusSave(saveBytes))
        {
            return saveBytes;
        }

        if (!steamId.HasValue)
        {
            throw new SaveFileRequiresSteamIdException();
        }

        return CitrusDecryptor.Decrypt(saveBytes, steamId.Value, curveIndex);
    }
}

internal sealed class SaveFileRequiresSteamIdException : InvalidOperationException
{
    public SaveFileRequiresSteamIdException()
        : base("This save file requires a SteamID64 to decrypt.")
    {
    }
}

internal static class CitrusDecryptor
{
    private const int KeyIvSize = 0x20;
    private const int EncryptedKeysSize = 0x200;
    private const int EncryptedDataSize = 0x40000;
    private const int HashSize = 0x20;
    private const int BlockSize = KeyIvSize + EncryptedKeysSize + EncryptedDataSize + HashSize;
    private const int TrailerSize = 0x1000;
    private const int ChecksumDataSize = EncryptedDataSize - HashSize;

    public static bool LooksLikeCitrusSave(byte[] saveBytes)
    {
        return saveBytes.Length > TrailerSize && (saveBytes.Length - TrailerSize) % BlockSize == 0;
    }

    public static byte[] Decrypt(byte[] encryptedSave, ulong steamId, int? curveIndex)
    {
        if (!LooksLikeCitrusSave(encryptedSave))
        {
            throw new InvalidDataException("The save file does not look like a Citrus-encrypted save.");
        }

        var encryptedLength = encryptedSave.Length - TrailerSize;
        var blockCount = encryptedLength / BlockSize;
        var curve = ResolveCurve(encryptedSave.AsSpan(0, BlockSize), steamId, curveIndex);
        var decrypted = new byte[blockCount * EncryptedDataSize];

        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var block = encryptedSave.AsSpan(blockIndex * BlockSize, BlockSize);
            var outerKey = block[..16].ToArray();
            var outerIv = block.Slice(16, 16).ToArray();
            var encryptedKeys = block.Slice(KeyIvSize, EncryptedKeysSize).ToArray();
            AesDecryptInPlace(encryptedKeys, outerKey, outerIv);

            var (dataKey, dataIv) = DecryptInnerKeys(encryptedKeys, steamId, curve);
            var encryptedData = block.Slice(KeyIvSize + EncryptedKeysSize, EncryptedDataSize).ToArray();
            AesDecryptInPlace(encryptedData, dataKey, dataIv);

            var expectedChecksum = block.Slice(KeyIvSize + EncryptedKeysSize + EncryptedDataSize, HashSize);
            var actualChecksum = CalculateChecksum(outerKey, outerIv, encryptedKeys, encryptedData);
            if (!actualChecksum.AsSpan().SequenceEqual(expectedChecksum))
            {
                throw new InvalidDataException("The save checksum did not match after decryption. Verify the SteamID64 and curve index.");
            }

            Buffer.BlockCopy(encryptedData, 0, decrypted, blockIndex * EncryptedDataSize, EncryptedDataSize);
        }

        return decrypted;
    }

    private static CitrusCurve ResolveCurve(ReadOnlySpan<byte> firstBlock, ulong steamId, int? curveIndex)
    {
        if (curveIndex.HasValue)
        {
            if (curveIndex.Value < 0 || curveIndex.Value >= CitrusCurves.All.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(curveIndex), $"Curve index must be between 0 and {CitrusCurves.All.Length - 1}.");
            }

            return CitrusCurves.All[curveIndex.Value];
        }

        var outerKey = firstBlock[..16].ToArray();
        var outerIv = firstBlock.Slice(16, 16).ToArray();
        var encryptedKeys = firstBlock.Slice(KeyIvSize, EncryptedKeysSize).ToArray();
        AesDecryptInPlace(encryptedKeys, outerKey, outerIv);
        var encryptedData = firstBlock.Slice(KeyIvSize + EncryptedKeysSize, EncryptedDataSize).ToArray();
        var expectedChecksum = firstBlock.Slice(KeyIvSize + EncryptedKeysSize + EncryptedDataSize, HashSize);

        foreach (var curve in CitrusCurves.All)
        {
            try
            {
                var (dataKey, dataIv) = DecryptInnerKeys(encryptedKeys, steamId, curve);
                var candidateData = encryptedData.ToArray();
                AesDecryptInPlace(candidateData, dataKey, dataIv);

                var actualChecksum = CalculateChecksum(outerKey, outerIv, encryptedKeys, candidateData);
                if (actualChecksum.AsSpan().SequenceEqual(expectedChecksum))
                {
                    return curve;
                }
            }
            catch
            {
                // Ignore bad curves and keep searching.
            }
        }

        throw new InvalidDataException("Unable to determine the Citrus curve index for this save file.");
    }

    private static (byte[] Key, byte[] Iv) DecryptInnerKeys(byte[] encryptedKeys, ulong steamId, CitrusCurve curve)
    {
        var key = new byte[16];
        var iv = new byte[16];

        ReadHalf(encryptedKeys.AsSpan(0, 128), steamId, curve).CopyTo(key, 0);
        ReadHalf(encryptedKeys.AsSpan(128, 128), steamId, curve).CopyTo(key, 8);
        ReadHalf(encryptedKeys.AsSpan(256, 128), steamId, curve).CopyTo(iv, 0);
        ReadHalf(encryptedKeys.AsSpan(384, 128), steamId, curve).CopyTo(iv, 8);

        return (key, iv);
    }

    private static byte[] ReadHalf(ReadOnlySpan<byte> segment, ulong steamId, CitrusCurve curve)
    {
        var c1 = new EcPoint(ReadUnsignedLittleEndian(segment[..32]), ReadUnsignedLittleEndian(segment.Slice(32, 32)));
        var c2 = new EcPoint(ReadUnsignedLittleEndian(segment.Slice(64, 32)), ReadUnsignedLittleEndian(segment.Slice(96, 32)));
        var decryptedPoint = DecryptEcElGamal(c1, c2, steamId, curve) ?? throw new InvalidDataException("Unable to decrypt an inner Citrus key segment.");
        var value = decryptedPoint.X / 100;
        return ToFixedLittleEndianBytes(value, 8);
    }

    private static EcPoint? DecryptEcElGamal(EcPoint c1, EcPoint c2, ulong steamId, CitrusCurve curve)
    {
        var d = new System.Numerics.BigInteger(BitConverter.GetBytes(~steamId), isUnsigned: true, isBigEndian: false);
        var sharedSecret = ScalarMultiply(d, c1, curve);
        if (!sharedSecret.HasValue)
        {
            return null;
        }

        var negative = new EcPoint(sharedSecret.Value.X, Mod(curve.P - sharedSecret.Value.Y, curve.P));
        return PointAdd(c2, negative, curve);
    }

    private static void AesDecryptInPlace(byte[] buffer, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(buffer, 0, buffer.Length);
        Buffer.BlockCopy(plaintext, 0, buffer, 0, plaintext.Length);

        var patch = new byte[16];
        for (var index = 0; index < patch.Length; index++)
        {
            patch[index] = (byte)(key[index] ^ iv[index]);
        }

        for (var offset = 16; offset < buffer.Length; offset += 16)
        {
            for (var index = 0; index < 16; index++)
            {
                buffer[offset + index] ^= patch[index];
            }
        }
    }

    private static byte[] CalculateChecksum(byte[] outerKey, byte[] outerIv, byte[] decryptedKeyBlob, byte[] plaintextBlock)
    {
        var buffer = new byte[outerKey.Length + outerIv.Length + decryptedKeyBlob.Length + ChecksumDataSize];
        var offset = 0;

        Buffer.BlockCopy(outerKey, 0, buffer, offset, outerKey.Length);
        offset += outerKey.Length;
        Buffer.BlockCopy(outerIv, 0, buffer, offset, outerIv.Length);
        offset += outerIv.Length;
        Buffer.BlockCopy(decryptedKeyBlob, 0, buffer, offset, decryptedKeyBlob.Length);
        offset += decryptedKeyBlob.Length;
        Buffer.BlockCopy(plaintextBlock, 0, buffer, offset, ChecksumDataSize);

        return SHA3_256.HashData(buffer);
    }

    private static EcPoint? PointAdd(EcPoint? left, EcPoint? right, CitrusCurve curve)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        var p = curve.P;
        var x1 = left.Value.X;
        var y1 = left.Value.Y;
        var x2 = right.Value.X;
        var y2 = right.Value.Y;

        if (x1 == x2 && Mod(y1 + y2, p) == 0)
        {
            return null;
        }

        System.Numerics.BigInteger slope;
        if (x1 == x2 && y1 == y2)
        {
            var numerator = Mod(3 * x1 * x1 + curve.A, p);
            var denominator = ModInverse(Mod(2 * y1, p), p);
            slope = Mod(numerator * denominator, p);
        }
        else
        {
            var numerator = Mod(y2 - y1, p);
            var denominator = ModInverse(Mod(x2 - x1, p), p);
            slope = Mod(numerator * denominator, p);
        }

        var x3 = Mod(slope * slope - x1 - x2, p);
        var y3 = Mod(slope * (x1 - x3) - y1, p);
        return new EcPoint(x3, y3);
    }

    private static EcPoint? ScalarMultiply(System.Numerics.BigInteger scalar, EcPoint point, CitrusCurve curve)
    {
        EcPoint? result = null;
        EcPoint? addend = point;

        while (scalar > 0)
        {
            if (!scalar.IsEven)
            {
                result = PointAdd(result, addend, curve);
            }

            addend = PointAdd(addend, addend, curve);
            scalar >>= 1;
        }

        return result;
    }

    private static System.Numerics.BigInteger ModInverse(System.Numerics.BigInteger value, System.Numerics.BigInteger modulus)
    {
        var t = System.Numerics.BigInteger.Zero;
        var newT = System.Numerics.BigInteger.One;
        var r = modulus;
        var newR = Mod(value, modulus);

        while (newR != 0)
        {
            var quotient = r / newR;
            (t, newT) = (newT, t - quotient * newT);
            (r, newR) = (newR, r - quotient * newR);
        }

        if (r != 1)
        {
            throw new InvalidOperationException("A modular inverse does not exist for the provided value.");
        }

        return Mod(t, modulus);
    }

    private static System.Numerics.BigInteger Mod(System.Numerics.BigInteger value, System.Numerics.BigInteger modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static System.Numerics.BigInteger ReadUnsignedLittleEndian(ReadOnlySpan<byte> bytes)
        => new(bytes, isUnsigned: true, isBigEndian: false);

    private static byte[] ToFixedLittleEndianBytes(System.Numerics.BigInteger value, int byteCount)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Resize(ref bytes, byteCount);
        return bytes;
    }

    private readonly record struct EcPoint(System.Numerics.BigInteger X, System.Numerics.BigInteger Y);
}

internal sealed record CitrusCurve(int Index, System.Numerics.BigInteger P, System.Numerics.BigInteger A, System.Numerics.BigInteger B, System.Numerics.BigInteger Gx, System.Numerics.BigInteger Gy);
