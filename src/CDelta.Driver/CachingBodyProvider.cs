using CDelta.Ir;

namespace CDelta.Driver;

internal sealed class CachingBodyProvider(IBodyProvider inner, ModuleBuilder builder) : IBodyProvider
{
    public Body? Lower(FunctionId function)
    {
        Body? body = inner.Lower(function);
        if (body is not null)
        {
            builder.SetBody(function, body);
        }

        return body;
    }
}
