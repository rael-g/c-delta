using System;

static class P
{
    static void Body() { }

    static void Run(Action action)
    {
        Defer(Body);
        Defer(action);
        Defer(() => Body());
    }
}
