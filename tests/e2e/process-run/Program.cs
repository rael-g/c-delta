using Std;

return Process.Run(Work);

static Result<int> Work()
{
    return Check(Process.Args().Length).Try() + 41;
}

static Result<int, StepError> Check(int count)
{
    if (count > 1)
    {
        return StepError.Rejected;
    }

    return 1;
}

enum StepError { Rejected }
