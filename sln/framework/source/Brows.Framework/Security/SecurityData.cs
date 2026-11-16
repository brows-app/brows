using Brows.Composition;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Brows.Security;

public static class SecurityData {
    private static readonly Encoding Encoding = Encoding.UTF8;

    private static async Task<T> Import<T>(CancellationToken token) where T : IExport {
        var service = Imports.Current.Find<T>() ?? throw new InvalidOperationException($"Missing import: {typeof(T)}");
        return service;
    }

    private static byte[] Encode(string s) {
        return Encoding.GetBytes(s);
    }

    private static string Decode(byte[] b) {
        return Encoding.GetString(b);
    }

    private static byte[] Bytes(SecureString s) {
        if (s is null) {
            return [];
        }
        var b = new byte[s.Length * 2];
        var p = Marshal.SecureStringToGlobalAllocUnicode(s);
        try {
            for (var i = 0; i < b.Length; i++) {
                b[i] = Marshal.ReadByte(p, i);
            }
        }
        finally {
            Marshal.ZeroFreeGlobalAllocUnicode(p);
        }
        return b;
    }

    private static async Task<byte[]> Entropy(CancellationToken token) {
        var service = await Import<IGetEntropy>(token);
        var entropy = default(SecureString);
        var worked = await service.Work(set: value => entropy = value, token);
        if (worked == false) {
            throw new InvalidOperationException("Entropy did not work.");
        }
        return Bytes(entropy);
    }

    private static async Task<byte[]> Protect(byte[] data, byte[] entropy, CancellationToken token) {
        var service = await Import<IProtectData>(token);
        var result = default(byte[]);
        var worked = await service.Work(data: data, entropy: entropy, set: data => result = data, token);
        if (worked == false) {
            throw new InvalidOperationException("Protect did not work.");
        }
        return result;
    }

    private static async Task<byte[]> Unprotect(byte[] data, byte[] entropy, CancellationToken token) {
        var service = await Import<IUnprotectData>(token);
        var result = default(byte[]);
        var worked = await service.Work(data, entropy, set: data => result = data, token);
        if (worked == false) {
            throw new InvalidOperationException("Unprotect did not work.");
        }
        return result;
    }

    public static Task<byte[]> Protect(byte[] data, SecureString entropy, CancellationToken token) {
        return Protect(data, Bytes(entropy), token);
    }

    public static Task<byte[]> Unprotect(byte[] data, SecureString entropy, CancellationToken token) {
        return Unprotect(data, Bytes(entropy), token);
    }

    public static async Task<byte[]> Protect(byte[] data, CancellationToken token) {
        var entropy = await Entropy(token);
        return await Protect(data, entropy, token);
    }

    public static async Task<byte[]> Unprotect(byte[] data, CancellationToken token) {
        var entropy = await Entropy(token);
        return await Unprotect(data, entropy, token);
    }

    public static async Task<string> Protect(string data, SecureString entropy, CancellationToken token) {
        var bytData = await Protect(Encode(data), entropy, token);
        return Convert.ToBase64String(bytData);
    }

    public static async Task<string> Unprotect(string data, SecureString entropy, CancellationToken token) {
        var bytData = Convert.FromBase64String(data);
        var newData = await Unprotect(bytData, entropy, token);
        return Decode(newData);
    }

    public static async Task<string> Protect(string data, CancellationToken token) {
        var bytData = await Protect(Encode(data), await Entropy(token), token);
        return Convert.ToBase64String(bytData);
    }

    public static async Task<string> Unprotect(string data, CancellationToken token) {
        var bytData = Convert.FromBase64String(data);
        var newData = await Unprotect(bytData, await Entropy(token), token);
        return Decode(newData);
    }

    /*
     * A secret is encoded as a length-prefixed UTF-8 username followed by
     * a length-prefixed UTF-16 password, so a stored secret can be decoded
     * without knowing which kind of secret it holds.
     */
    public static byte[] EncodeSecret(string username, SecureString password) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var user = Encode(username ?? "");
        var pass = Bytes(password);
        writer.Write(user.Length);
        writer.Write(user);
        writer.Write(pass.Length);
        writer.Write(pass);
        writer.Flush();
        return stream.ToArray();
    }

    public static (string Username, SecureString Password) DecodeSecret(byte[] data) {
        using var stream = new MemoryStream(data ?? []);
        using var reader = new BinaryReader(stream);
        var user = reader.ReadBytes(reader.ReadInt32());
        var pass = reader.ReadBytes(reader.ReadInt32());
        var password = new SecureString();
        foreach (var ch in Encoding.Unicode.GetString(pass)) {
            password.AppendChar(ch);
        }
        password.MakeReadOnly();
        return (Decode(user), password);
    }
}
