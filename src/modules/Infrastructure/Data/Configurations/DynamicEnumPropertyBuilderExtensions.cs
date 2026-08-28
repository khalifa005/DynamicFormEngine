using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Core.Enums;

namespace KH.Infrastructure.Data.Configurations;

internal static class DynamicEnumPropertyBuilderExtensions
{
    private const int StorageMaxLength = 50;

    public static PropertyBuilder<SessionStatusEnum> HasSessionStatusConversion(
        this PropertyBuilder<SessionStatusEnum> property) =>
        property
            .HasConversion(
                v => v.GetStorageCode(),
                v => SessionStatusEnum.GetByCode(v) ?? SessionStatusEnum.Active)
            .HasMaxLength(StorageMaxLength);

    public static PropertyBuilder<SessionOperationEnum> HasSessionOperationConversion(
        this PropertyBuilder<SessionOperationEnum> property) =>
        property
            .HasConversion(
                v => v.GetStorageCode(),
                v => SessionOperationEnum.GetByCode(v) ?? SessionOperationEnum.Create)
            .HasMaxLength(StorageMaxLength);

    public static PropertyBuilder<CallProviderEnum> HasCallProviderConversion(
        this PropertyBuilder<CallProviderEnum> property) =>
        property
            .HasConversion(
                v => v.GetStorageCode(),
                v => CallProviderEnum.GetByCode(v) ?? CallProviderEnum.Default)
            .HasMaxLength(StorageMaxLength);

    public static PropertyBuilder<CallInitiationChannelEnum> HasCallInitiationChannelConversion(
        this PropertyBuilder<CallInitiationChannelEnum> property) =>
        property
            .HasConversion(
                v => v.GetStorageCode(),
                v => CallInitiationChannelEnum.GetByCode(v) ?? CallInitiationChannelEnum.Default)
            .HasMaxLength(StorageMaxLength);

    public static PropertyBuilder<CallDispositionEnum?> HasCallDispositionConversion(
        this PropertyBuilder<CallDispositionEnum?> property) =>
        property
            .HasConversion(
                v => v == null ? null : v.GetStorageCode(),
                v => v == null ? null : CallDispositionEnum.GetByCode(v))
            .HasMaxLength(StorageMaxLength);

    public static PropertyBuilder<CallDirectionEnum?> HasCallDirectionConversion(
        this PropertyBuilder<CallDirectionEnum?> property) =>
        property
            .HasConversion(
                v => v == null ? null : v.GetStorageCode(),
                v => v == null ? null : CallDirectionEnum.GetByCode(v))
            .HasMaxLength(20);
}
