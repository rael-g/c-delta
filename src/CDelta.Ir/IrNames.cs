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
        Ir.BinOp.Ge => "ge",
        Ir.BinOp.FAdd => "fadd",
        Ir.BinOp.FSub => "fsub",
        Ir.BinOp.FMul => "fmul",
        Ir.BinOp.FDiv => "fdiv",
        Ir.BinOp.FRem => "frem",
        Ir.BinOp.FEq => "feq",
        Ir.BinOp.FNe => "fne",
        Ir.BinOp.FLt => "flt",
        Ir.BinOp.FLe => "fle",
        Ir.BinOp.FGt => "fgt",
        _ => "fge",
    };

    public static string UnOp(UnOp op) => op switch
    {
        Ir.UnOp.Neg => "neg",
        Ir.UnOp.Not => "not",
        Ir.UnOp.BitNot => "bitnot",
        _ => "fneg",
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
        CastKind.SlicePtr => "slice.ptr",
        CastKind.EnumError => "enum.error",
        CastKind.ErrorEnum => "error.enum",
        _ => "error.error",
    };

    public static string AllocaInit(AllocaInit init) => init switch
    {
        Ir.AllocaInit.Zero => "zero",
        Ir.AllocaInit.Undef => "undef",
        _ => "fill",
    };

    public static AllocaInit? ParseAllocaInit(string word) => word switch
    {
        "zero" => Ir.AllocaInit.Zero,
        "undef" => Ir.AllocaInit.Undef,
        "fill" => Ir.AllocaInit.Fill,
        _ => null,
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

    public static bool HasMode(CastKind kind) => kind is CastKind.IntInt or CastKind.FloatInt;

    public static BinOp? ParseBinOp(string word)
    {
        foreach (BinOp op in Enum.GetValues<BinOp>())
        {
            if (BinOp(op) == word)
            {
                return op;
            }
        }

        return null;
    }

    public static UnOp? ParseUnOp(string word)
    {
        foreach (UnOp op in Enum.GetValues<UnOp>())
        {
            if (UnOp(op) == word)
            {
                return op;
            }
        }

        return null;
    }

    public static ArithMode ParseMode(string word) => word switch
    {
        "trap" => ArithMode.Trap,
        "wrap" => ArithMode.Wrap,
        "assume" => ArithMode.Assume,
        _ => ArithMode.Default,
    };

    public static CastKind? ParseCast(string word)
    {
        foreach (CastKind kind in new[] { CastKind.IntInt, CastKind.IntFloat, CastKind.FloatInt, CastKind.FloatFloat, CastKind.IntEnum, CastKind.EnumInt, CastKind.PtrInt, CastKind.IntPtr, CastKind.PtrPtr, CastKind.Bitcast, CastKind.SlicePtr, CastKind.EnumError, CastKind.ErrorEnum, CastKind.ErrorError })
        {
            if (Cast(kind) == word)
            {
                return kind;
            }
        }

        return null;
    }

    public static CheckKind? ParseCheck(string word)
    {
        foreach (CheckKind kind in new[] { CheckKind.Bounds, CheckKind.Null, CheckKind.Optional, CheckKind.ResultValue, CheckKind.ResultError, CheckKind.Enum, CheckKind.Align, CheckKind.DivZero, CheckKind.Overlap })
        {
            if (Check(kind) == word)
            {
                return kind;
            }
        }

        return null;
    }

    public static CallConv ParseConv(string word) => word switch
    {
        "cdecl" => CallConv.Cdecl,
        "stdcall" => CallConv.Stdcall,
        "fastcall" => CallConv.Fastcall,
        "thiscall" => CallConv.Thiscall,
        "vectorcall" => CallConv.Vectorcall,
        _ => CallConv.Default,
    };
}
