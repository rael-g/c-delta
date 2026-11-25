using CDelta.Ir;

namespace CDelta.Core.Tests;

internal sealed class DictionaryBodyProvider(Module module) : IBodyProvider
{
    public Body? Lower(FunctionId function) => module.Bodies[function.Index];
}
