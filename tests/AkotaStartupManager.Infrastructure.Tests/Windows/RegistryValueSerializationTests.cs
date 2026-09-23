using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Infrastructure.Windows;
using Microsoft.Win32;

namespace AkotaStartupManager.Infrastructure.Tests.Windows;

public sealed class RegistryValueSerializationTests
{
    [Fact]
    public void String_RoundTrips()
    {
        const string raw = @"""C:\Program Files\App\app.exe"" --silent";
        var backup = Capture(raw, RegistryValueKind.String);

        Assert.Equal(raw, RegistryValueSerialization.Decode(backup));
    }

    [Fact]
    public void ExpandString_KeepsUnExpandedText()
    {
        const string raw = @"%SystemRoot%\System32\app.exe /background";
        var backup = Capture(raw, RegistryValueKind.ExpandString);

        Assert.Equal(RegistryValueKind.ExpandString, (RegistryValueKind)backup.OriginalRegistryValueKind!);
        Assert.Equal(raw, RegistryValueSerialization.Decode(backup));
    }

    [Fact]
    public void DWord_RoundTrips()
    {
        var backup = Capture(42, RegistryValueKind.DWord);

        Assert.Equal(42, RegistryValueSerialization.Decode(backup));
    }

    [Fact]
    public void QWord_RoundTrips()
    {
        var backup = Capture(9_000_000_000L, RegistryValueKind.QWord);

        Assert.Equal(9_000_000_000L, RegistryValueSerialization.Decode(backup));
    }

    [Fact]
    public void MultiString_RoundTrips()
    {
        var backup = Capture(new[] { "one", "two" }, RegistryValueKind.MultiString);

        Assert.Equal(new[] { "one", "two" }, Assert.IsType<string[]>(RegistryValueSerialization.Decode(backup)));
    }

    [Fact]
    public void Binary_RoundTrips()
    {
        var backup = Capture(new byte[] { 1, 2, 3, 4 }, RegistryValueKind.Binary);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.IsType<byte[]>(RegistryValueSerialization.Decode(backup)));
    }

    /// <summary>
    /// 旧实现用 value.ToString() 保存数据，byte[] 会变成 "System.Byte[]"，
    /// 恢复时 SetValue 必然抛异常，而原值此时已经从注册表删除。
    /// </summary>
    [Fact]
    public void Binary_IsNotStoredAsTypeName()
    {
        var backup = Capture(new byte[] { 1 }, RegistryValueKind.Binary);

        Assert.Null(backup.OriginalValue);
        Assert.Equal(new byte[] { 1 }, Convert.FromBase64String(backup.OriginalBinaryBase64!));
    }

    [Fact]
    public void MultiString_IsNotStoredAsTypeName()
    {
        var backup = Capture(new[] { "one" }, RegistryValueKind.MultiString);

        Assert.Null(backup.OriginalValue);
        Assert.Equal(new[] { "one" }, backup.OriginalMultiString);
    }

    [Fact]
    public void Capture_ThrowsWhenValueDoesNotMatchKind()
    {
        var backup = new StartupBackupRecord { Name = "Probe" };

        var exception = Assert.Throws<InvalidOperationException>(
            () => RegistryValueSerialization.Capture(backup, "plain text", RegistryValueKind.MultiString));

        Assert.Contains("已保留原值", exception.Message);
    }

    [Fact]
    public void Decode_ThrowsForLegacyMultiStringRecord()
    {
        var backup = new StartupBackupRecord
        {
            Name = "Legacy",
            OriginalValue = "System.String[]",
            OriginalRegistryValueKind = (int)RegistryValueKind.MultiString
        };

        Assert.Throws<InvalidDataException>(() => RegistryValueSerialization.Decode(backup));
        Assert.False(RegistryValueSerialization.CanDecode(backup));
    }

    [Fact]
    public void Decode_ThrowsWhenValueKindIsMissing()
    {
        var backup = new StartupBackupRecord { Name = "Legacy", OriginalValue = "app.exe" };

        Assert.Throws<InvalidDataException>(() => RegistryValueSerialization.Decode(backup));
    }

    private static StartupBackupRecord Capture(object value, RegistryValueKind kind)
    {
        var backup = new StartupBackupRecord { Name = "Probe" };
        RegistryValueSerialization.Capture(backup, value, kind);
        return backup;
    }
}
