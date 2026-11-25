namespace CDelta.Ir;

public interface IBodyProvider
{
    Body? Lower(FunctionId function);
}
