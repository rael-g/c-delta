namespace CDelta.Ir;

public static class IrNames
{
    public static string Stage(ModuleStage stage) => stage switch
    {
        ModuleStage.Lowered => "lowered",
        ModuleStage.Instantiated => "instantiated",
        _ => "final",
    };

    public static string BinOp(BinOp op) => op switch
    {
        Ir.BinOp.Add => "add",
        Ir.BinOp.Sub => "sub",
        Ir.BinOp.Mul => "mul",
        Ir.BinOp.Div => "div",
        Ir.BinOp.Rem => "rem",
        Ir.BinOp.Shl => "shl",
        Ir.BinOp.Shr => "shr",
        Ir.BinOp.And => "and",
        Ir.BinOp.Or => "or",
        Ir.BinOp.Xor => "xor",
        Ir.BinOp.Eq => "eq",
        Ir.BinOp.Ne => "ne",
        Ir.BinOp.Lt => "lt",
        Ir.BinOp.Le => "le",
        Ir.BinOp.Gt => "gt",
        _ => "ge",
    };

    public static string UnOp(UnOp op) => op switch
    {
        Ir.UnOp.Neg => "neg",
        Ir.UnOp.Not => "not",
        _ => "bitnot",
    };

    public static string Mode(ArithMode mode) => mode switch
    {
        ArithMode.Default => "default",
        ArithMode.Trap => "trap",
        ArithMode.Wrap => "wrap",
        _ => "assume",
    };

    public static string Cast(CastKind kind) => kind switch
    {
        CastKind.IntInt => "int.int",
        CastKind.IntFloat => "int.float",
        CastKind.FloatInt => "float.int",
        CastKind.FloatFloat => "float.float",
        CastKind.IntEnum => "int.enum",
        CastKind.EnumInt => "enum.int",
        CastKind.PtrInt => "ptr.int",
        CastKind.IntPtr => "int.ptr",
        CastKind.PtrPtr => "ptr.ptr",
        CastKind.Bitcast => "bitcast",
        CastKind.ArraySlice => "array.slice",
        _ => "slice.ptr",
    };

    public static string Check(CheckKind kind) => kind switch
    {
        CheckKind.Bounds => "bounds",
        CheckKind.Null => "null",
        CheckKind.Optional => "optional",
        CheckKind.ResultValue => "result.value",
        CheckKind.ResultError => "result.error",
        CheckKind.Enum => "enum",
        CheckKind.Align => "align",
        CheckKind.DivZero => "divzero",
        _ => "overlap",
    };

    public static string Conv(CallConv conv) => conv switch
    {
        CallConv.Default => "default",
        CallConv.Cdecl => "cdecl",
        CallConv.Stdcall => "stdcall",
        CallConv.Fastcall => "fastcall",
        CallConv.Thiscall => "thiscall",
        _ => "vectorcall",
    };

    public static string ParamMode(ParamMode mode) => mode switch
    {
        Ir.ParamMode.Value => string.Empty,
        Ir.ParamMode.Ref => "ref ",
        Ir.ParamMode.RefReadonly => "ref.readonly ",
        _ => "out ",
    };

    public static bool HasMode(BinOp op) => op is Ir.BinOp.Add or Ir.BinOp.Sub or Ir.BinOp.Mul;

    public static bool HasMode(CastKind kind) => kind is CastKind.IntInt or CastKind.FloatInt or CastKind.IntEnum;
}
