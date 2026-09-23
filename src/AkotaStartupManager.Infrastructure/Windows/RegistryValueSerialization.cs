using System.Globalization;
using AkotaStartupManager.Core.Models;
using Microsoft.Win32;

namespace AkotaStartupManager.Infrastructure.Windows;

/// <summary>
/// 按注册表值类型把原始数据编码进备份记录，并在恢复时还原。
/// 不能直接用 ToString()：byte[] 和 string[] 会变成 “System.Byte[]”“System.String[]”，
/// 恢复时 SetValue 必然抛异常，而此时原值已经从注册表删除，等于数据永久丢失。
/// </summary>
public static class RegistryValueSerialization
{
    public static void Capture(StartupBackupRecord backup, object? value, RegistryValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(backup);
        backup.OriginalRegistryValueKind = (int)kind;
        backup.OriginalValue = null;
        backup.OriginalMultiString = null;
        backup.OriginalBinaryBase64 = null;

        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                backup.OriginalValue = Require<string>(backup, value, kind);
                break;
            case RegistryValueKind.DWord:
                backup.OriginalValue = Require<int>(backup, value, kind).ToString(CultureInfo.InvariantCulture);
                break;
            case RegistryValueKind.QWord:
                backup.OriginalValue = Require<long>(backup, value, kind).ToString(CultureInfo.InvariantCulture);
                break;
            case RegistryValueKind.MultiString:
                backup.OriginalMultiString = Require<string[]>(backup, value, kind);
                break;
            case RegistryValueKind.Binary:
            case RegistryValueKind.None:
            case RegistryValueKind.Unknown:
                backup.OriginalBinaryBase64 = Convert.ToBase64String(Require<byte[]>(backup, value, kind));
                break;
            default:
                throw new InvalidOperationException($"注册表值类型 {kind} 不受支持，已保留原值未做备份。");
        }
    }

    public static object Decode(StartupBackupRecord backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (backup.OriginalRegistryValueKind is not { } rawKind)
        {
            throw new InvalidDataException($"备份“{backup.Name}”缺少注册表值类型，无法恢复。");
        }

        var kind = (RegistryValueKind)rawKind;
        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return backup.OriginalValue ?? string.Empty;
            case RegistryValueKind.DWord:
                return ParseNumber<int>(backup, kind);
            case RegistryValueKind.QWord:
                return ParseNumber<long>(backup, kind);
            case RegistryValueKind.MultiString:
                return backup.OriginalMultiString
                    ?? throw new InvalidDataException($"备份“{backup.Name}”由旧版本创建，未保存 REG_MULTI_SZ 原始数据，无法恢复。");
            case RegistryValueKind.Binary:
            case RegistryValueKind.None:
            case RegistryValueKind.Unknown:
                return backup.OriginalBinaryBase64 is { } encoded
                    ? Convert.FromBase64String(encoded)
                    : throw new InvalidDataException($"备份“{backup.Name}”由旧版本创建，未保存二进制原始数据，无法恢复。");
            default:
                throw new InvalidDataException($"备份“{backup.Name}”的注册表值类型 {kind} 不受支持，无法恢复。");
        }
    }

    /// <summary>
    /// 判断备份记录里的注册表数据是否足以还原，用于界面上标注可恢复性。
    /// </summary>
    public static bool CanDecode(StartupBackupRecord backup)
    {
        try
        {
            Decode(backup);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        {
            return false;
        }
    }

    private static T Require<T>(StartupBackupRecord backup, object? value, RegistryValueKind kind)
    {
        if (value is T typed)
        {
            return typed;
        }

        // 类型与声明不符时宁可放弃备份，也不能在没拿到完整数据的情况下删掉原值。
        throw new InvalidOperationException(
            $"注册表值“{backup.Name}”声明为 {kind}，实际数据格式为 {value?.GetType().Name ?? "null"}，无法安全备份，已保留原值。");
    }

    private static T ParseNumber<T>(StartupBackupRecord backup, RegistryValueKind kind) where T : struct, IParsable<T> =>
        T.TryParse(backup.OriginalValue, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"备份“{backup.Name}”的 {kind} 数据“{backup.OriginalValue}”无法解析，无法恢复。");
}
