namespace System;

public delegate void Action();

public delegate void Action<in T>(T arg);
