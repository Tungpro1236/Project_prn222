using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Project_Prn222_Group3.Infrastructure;

// HTML number inputs submit a dot decimal separator regardless of the Windows display locale.
// In vi-VN the default decimal binder interprets that dot as a grouping separator (123.45 -> 12345).
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        if (string.IsNullOrWhiteSpace(value.FirstValue) && context.ModelMetadata.IsNullableValueType)
            context.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(value.FirstValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            context.Result = ModelBindingResult.Success(amount);
        else context.ModelState.TryAddModelError(context.ModelName, "Nhập số hợp lệ, dùng dấu chấm cho phần thập phân.");
        return Task.CompletedTask;
    }
}

public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
        => (Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType) == typeof(decimal)
            ? new InvariantDecimalModelBinder() : null;
}
