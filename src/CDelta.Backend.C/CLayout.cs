using CDelta.Ir;

namespace CDelta.Backend.C;

internal static class CLayout
{
    private const int ErrorCodeAlignment = 2;

    public static int AlignmentOf(Module module, TypeId type)
    {
        TypeNode node = module.Types[type.Index];
        Target target = module.Target!.Value;
        return node.Kind switch
        {
            TypeKind.Ptr or TypeKind.Fn or TypeKind.Slice => target.PointerSize,
            TypeKind.Array or TypeKind.Optional => AlignmentOf(module, new TypeId(node.A)),
            TypeKind.Result => Math.Max(AlignmentOf(module, new TypeId(node.A)), ErrorCodeAlignment),
            TypeKind.ErrorSet or TypeKind.AnyError => ErrorCodeAlignment,
            TypeKind.Struct => module.Structs[node.A].Alignment,
            _ => PrimitiveLayout.Alignment(node.Kind, target),
        };
    }
}
