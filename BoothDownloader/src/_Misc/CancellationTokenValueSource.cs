using System.CommandLine.Binding;

public class CancellationTokenValueSource : IValueDescriptor<CancellationToken>, IValueSource
{
    public bool TryGetValue(IValueDescriptor valueDescriptor, BindingContext bindingContext, out object? boundValue)
    {
        boundValue = (CancellationToken)bindingContext.GetService(typeof(CancellationToken))!;
        return true;
    }

    public object? GetDefaultValue() => throw new NotImplementedException();
    public string ValueName => throw new NotImplementedException();
    public Type ValueType => throw new NotImplementedException();
    public bool HasDefaultValue => throw new NotImplementedException();
}