using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qlvb.Infrastructure.Security;

/// <summary>Mật khẩu sai, mã khôi phục sai hoặc đang bị tạm khóa do nhập sai nhiều lần.</summary>
public sealed class AuthException(string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>
/// Quản lý khóa dữ liệu (DEK 256 bit, ngẫu nhiên). DEK không bao giờ ghi ra đĩa ở dạng rõ:
/// nó được "bọc" bằng AES-256-GCM với khóa dẫn xuất từ mật khẩu (PBKDF2-HMAC-SHA256) và,
/// riêng biệt, với khóa dẫn xuất từ mã khôi phục. Đổi mật khẩu chỉ bọc lại DEK, không mã hóa lại dữ liệu.
/// Không dùng mã hóa tự chế: chỉ dùng các thuật toán chuẩn trong System.Security.Cryptography.
/// </summary>
public sealed class KeyStore
{
    public const int KeySize = 32;
    public const int DefaultIterations = 600_000;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    private const int FreeAttempts = 5;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);
    // Bảng chữ cho mã khôi phục: bỏ các ký tự dễ nhầm (0/O, 1/I/L).
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int RecoveryLength = 20;

    private readonly string _path;
    private readonly int _iterations;
    private readonly Func<DateTime> _now;

    public KeyStore(string path, int iterations = DefaultIterations, Func<DateTime>? now = null)
    {
        _path = path;
        _iterations = iterations;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public bool Exists => File.Exists(_path);
    public string FilePath => _path;

    // ---------------------------------------------------------------- tạo mới
    /// <summary>Khởi tạo lần đầu: sinh DEK, bọc bằng mật khẩu và mã khôi phục. Trả về (DEK, mã khôi phục).</summary>
    public (byte[] Key, string RecoveryCode) Create(string password)
    {
        if (Exists) throw new InvalidOperationException("Tệp khóa đã tồn tại.");
        CheckPasswordPolicy(password);
        var dek = RandomNumberGenerator.GetBytes(KeySize);
        var code = NewRecoveryCode();
        var file = new KeyFile
        {
            Iterations = _iterations,
            Password = Wrap(dek, password, "password"),
            Recovery = Wrap(dek, NormalizeRecovery(code), "recovery"),
            CreatedUtc = _now(),
            PasswordChangedUtc = _now(),
        };
        Save(file);
        return (dek, code);
    }

    // ---------------------------------------------------------------- mở khóa
    public byte[] Unlock(string password)
    {
        var f = Load();
        CheckLockout(f);
        var dek = TryUnwrap(f.Password, password, "password", f.Iterations);
        if (dek == null)
        {
            RegisterFailure(f);
            throw Failure(f, "Mật khẩu không đúng.");
        }
        if (f.FailedAttempts != 0 || f.LockedUntilUtc != null)
        {
            f.FailedAttempts = 0;
            f.LockedUntilUtc = null;
            Save(f);
        }
        return dek;
    }

    /// <summary>Kiểm tra mật khẩu (dùng khi mở khóa màn hình, xác nhận thao tác quan trọng).</summary>
    public bool Verify(string password)
    {
        try
        {
            var k = Unlock(password);
            CryptographicOperations.ZeroMemory(k);
            return true;
        }
        catch (AuthException) { return false; }
    }

    /// <summary>Đổi mật khẩu: bọc lại DEK bằng mật khẩu mới.</summary>
    public void ChangePassword(string oldPassword, string newPassword)
    {
        CheckPasswordPolicy(newPassword);
        if (oldPassword == newPassword) throw new AuthException("Mật khẩu mới phải khác mật khẩu cũ.");
        var dek = Unlock(oldPassword);
        try
        {
            var f = Load();
            f.Password = Wrap(dek, newPassword, "password");
            f.Iterations = _iterations;
            f.PasswordChangedUtc = _now();
            Save(f);
        }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }

    /// <summary>Quên mật khẩu: dùng mã khôi phục đặt mật khẩu mới. Trả về DEK và MÃ KHÔI PHỤC MỚI (mã cũ hết hiệu lực).</summary>
    public (byte[] Key, string NewRecoveryCode) ResetWithRecoveryCode(string recoveryCode, string newPassword)
    {
        CheckPasswordPolicy(newPassword);
        var f = Load();
        CheckLockout(f);
        var dek = TryUnwrap(f.Recovery, NormalizeRecovery(recoveryCode), "recovery", f.Iterations);
        if (dek == null)
        {
            RegisterFailure(f);
            throw Failure(f, "Mã khôi phục không đúng.");
        }
        var code = NewRecoveryCode();
        f.Password = Wrap(dek, newPassword, "password");
        f.Recovery = Wrap(dek, NormalizeRecovery(code), "recovery");
        f.Iterations = _iterations;
        f.FailedAttempts = 0;
        f.LockedUntilUtc = null;
        f.PasswordChangedUtc = _now();
        Save(f);
        return (dek, code);
    }

    /// <summary>Tạo mã khôi phục mới (cần mật khẩu hiện tại). Mã cũ hết hiệu lực.</summary>
    public string RegenerateRecoveryCode(string password)
    {
        var dek = Unlock(password);
        try
        {
            var f = Load();
            var code = NewRecoveryCode();
            f.Recovery = Wrap(dek, NormalizeRecovery(code), "recovery");
            Save(f);
            return code;
        }
        finally { CryptographicOperations.ZeroMemory(dek); }
    }

    /// <summary>Kiểm tra tệp khóa (có thể là tệp khóa trong bản sao lưu) mở được bằng mật khẩu, không ghi bộ đếm.</summary>
    public static byte[]? TryUnlockFile(string keyFileJson, string password)
    {
        var f = Parse(keyFileJson);
        return TryUnwrap(f.Password!, password, "password", f.Iterations);
    }

    public string ReadRaw() => File.ReadAllText(_path);

    /// <summary>Ghi đè tệp khóa (dùng khi khôi phục từ bản sao lưu).</summary>
    public void ReplaceRaw(string json)
    {
        _ = Parse(json);
        WriteAtomic(_path, json);
    }

    // ---------------------------------------------------------------- chính sách
    public static void CheckPasswordPolicy(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new AuthException($"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.");
        if (password.Length > MaxPasswordLength)
            throw new AuthException($"Mật khẩu tối đa {MaxPasswordLength} ký tự.");
        if (!password.Any(char.IsLetter) || !password.Any(c => !char.IsLetter(c)))
            throw new AuthException("Mật khẩu phải có cả chữ cái và chữ số (hoặc ký tự đặc biệt).");
        if (password.Trim() != password)
            throw new AuthException("Mật khẩu không được bắt đầu hoặc kết thúc bằng dấu cách.");
    }

    public static string NewRecoveryCode()
    {
        var chars = new char[RecoveryLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
        var s = new string(chars);
        return string.Join("-", Enumerable.Range(0, RecoveryLength / 5).Select(i => s.Substring(i * 5, 5)));
    }

    public static string NormalizeRecovery(string code) =>
        new string((code ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    // ---------------------------------------------------------------- nội bộ
    private void CheckLockout(KeyFile f)
    {
        if (f.LockedUntilUtc is { } until && until > _now())
            throw new AuthException($"Nhập sai nhiều lần. Vui lòng thử lại sau {Format(until - _now())}.", until - _now());
    }

    private void RegisterFailure(KeyFile f)
    {
        f.FailedAttempts++;
        if (f.FailedAttempts >= FreeAttempts)
        {
            var exp = Math.Min(f.FailedAttempts - FreeAttempts, 10);
            var delay = TimeSpan.FromSeconds(30 * Math.Pow(2, exp));
            if (delay > MaxDelay) delay = MaxDelay;
            f.LockedUntilUtc = _now() + delay;
        }
        Save(f);
    }

    private AuthException Failure(KeyFile f, string msg)
    {
        if (f.LockedUntilUtc is { } until && until > _now())
            return new AuthException($"{msg} Đã nhập sai {f.FailedAttempts} lần, tạm khóa {Format(until - _now())}.", until - _now());
        var left = FreeAttempts - f.FailedAttempts;
        return new AuthException(left > 0 ? $"{msg} Còn {left} lần thử trước khi bị tạm khóa." : msg);
    }

    private static string Format(TimeSpan t) =>
        t.TotalSeconds < 60 ? $"{Math.Ceiling(t.TotalSeconds)} giây" : $"{Math.Ceiling(t.TotalMinutes)} phút";

    private WrappedKey Wrap(byte[] dek, string secret, string purpose)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var kek = Derive(secret, salt, _iterations);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ct = new byte[dek.Length];
            var tag = new byte[16];
            using var gcm = new AesGcm(kek, 16);
            gcm.Encrypt(nonce, dek, ct, tag, Aad(purpose));
            return new WrappedKey { Salt = salt, Nonce = nonce, Tag = tag, Data = ct };
        }
        finally { CryptographicOperations.ZeroMemory(kek); }
    }

    private static byte[]? TryUnwrap(WrappedKey? w, string secret, string purpose, int iterations)
    {
        if (w == null || string.IsNullOrEmpty(secret)) return null;
        var kek = Derive(secret, w.Salt, iterations);
        try
        {
            var dek = new byte[w.Data.Length];
            using var gcm = new AesGcm(kek, 16);
            gcm.Decrypt(w.Nonce, w.Data, w.Tag, dek, Aad(purpose));
            return dek;
        }
        catch (CryptographicException) { return null; }
        finally { CryptographicOperations.ZeroMemory(kek); }
    }

    private static byte[] Aad(string purpose) => Encoding.ASCII.GetBytes("QLVB-DEK-v1|" + purpose);

    private static byte[] Derive(string secret, byte[] salt, int iterations)
    {
        var pw = Encoding.UTF8.GetBytes(secret.Normalize(NormalizationForm.FormC));
        try { return Rfc2898DeriveBytes.Pbkdf2(pw, salt, iterations, HashAlgorithmName.SHA256, KeySize); }
        finally { CryptographicOperations.ZeroMemory(pw); }
    }

    private KeyFile Load()
    {
        if (!Exists) throw new FileNotFoundException("Không tìm thấy tệp khóa dữ liệu.");
        return Parse(File.ReadAllText(_path));
    }

    /// <summary>Đọc tệp khóa; nội dung hỏng thì báo lỗi dễ hiểu thay vì lỗi kỹ thuật của bộ đọc JSON.</summary>
    internal static KeyFile Parse(string json)
    {
        const string msg = "Tệp khóa dữ liệu (qlvb.key) bị hỏng hoặc không đúng định dạng. Hãy khôi phục từ bản sao lưu gần nhất.";
        KeyFile? f;
        try { f = JsonSerializer.Deserialize(json, KeyFileJson.Default.KeyFile); }
        catch (JsonException ex) { throw new InvalidDataException(msg, ex); }
        if (f?.Password == null || f.Recovery == null) throw new InvalidDataException(msg);
        return f;
    }

    private void Save(KeyFile f) => WriteAtomic(_path, JsonSerializer.Serialize(f, KeyFileJson.Default.KeyFile));

    internal static void WriteAtomic(string path, string content)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, Path.GetFileName(path) + ".tmp");
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            w.Write(content);
            w.Flush();
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}

public sealed class WrappedKey
{
    public byte[] Salt { get; set; } = [];
    public byte[] Nonce { get; set; } = [];
    public byte[] Tag { get; set; } = [];
    public byte[] Data { get; set; } = [];
}

public sealed class KeyFile
{
    public int Format { get; set; } = 1;
    public string Kdf { get; set; } = "PBKDF2-HMAC-SHA256";
    public int Iterations { get; set; }
    public string Cipher { get; set; } = "AES-256-GCM";
    public WrappedKey? Password { get; set; }
    public WrappedKey? Recovery { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime PasswordChangedUtc { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(KeyFile))]
internal sealed partial class KeyFileJson : JsonSerializerContext;
