using CDelta.Ir;

namespace CDelta.Core;

public sealed class TypeLayouts(Module module, Target target)
{
    private const int BitsPerByte = 8;
    private const int SliceLengthSize = 4;

    private readonly HashSet<int> _inProgress = [];

    public TypeSizes Of(TypeId type)
    {
        TypeNode node = module.Types[type.Index];
        switch (node.Kind)
        {
            case TypeKind.Ptr:
            case TypeKind.Fn:
                return new TypeSizes(target.PointerSize, target.PointerSize);
            case TypeKind.Slice:
                int pointerSize = target.PointerSize;
                return new TypeSizes(AlignUp(pointerSize + SliceLengthSize, pointerSize), pointerSize);
            case TypeKind.Array:
                TypeSizes element = Of(new TypeId(node.A));
                return new TypeSizes(element.Size * node.B, element.Alignment);
            case TypeKind.Optional:
                TypeSizes payload = Of(new TypeId(node.A));
                return new TypeSizes(AlignUp(payload.Size + 1, payload.Alignment), payload.Alignment);
            case TypeKind.Struct:
                return OfStruct(new StructId(node.A));
            case TypeKind.Enum:
                return Of(module.Enums[node.A].Underlying);
            default:
                return new TypeSizes(PrimitiveLayout.Size(node.Kind, target), PrimitiveLayout.Alignment(node.Kind, target));
        }
    }

    public TypeSizes OfStruct(StructId id)
    {
        StructDecl declaration = module.Structs[id.Index];
        if (declaration.Size != StructDecl.NotComputed)
        {
            return new TypeSizes(declaration.Size, declaration.Alignment);
        }

        if (!_inProgress.Add(id.Index))
        {
            throw new InvalidOperationException("a struct contains itself by value");
        }

        int[] offsets = new int[declaration.Fields.Count];
        TypeSizes result = Layout(declaration, offsets);
        for (int i = 0; i < offsets.Length; i++)
        {
            int index = declaration.Fields.Start + i;
            FieldDecl field = module.Fields[index];
            module.Fields[index] = field with
            {
                Offset = field.BitWidth == FieldDecl.NoBits ? offsets[i] : offsets[i] / BitsPerByte,
                BitOffset = field.BitWidth == FieldDecl.NoBits ? FieldDecl.NoBitOffset : offsets[i],
            };
        }

        module.Structs[id.Index] = declaration with { Size = result.Size, Alignment = result.Alignment };
        _inProgress.Remove(id.Index);
        return result;
    }

    public static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private TypeSizes Layout(StructDecl declaration, int[] offsets)
    {
        var fields = new List<(int Index, FieldDecl Field, TypeSizes Sizes)>();
        for (int i = 0; i < declaration.Fields.Count; i++)
        {
            FieldDecl field = module.Fields[declaration.Fields.Start + i];
            fields.Add((i, field, Of(field.Type)));
        }

        if (declaration.Layout == StructLayoutKind.Explicit)
        {
            return Finish(declaration, LayoutExplicit(fields, offsets));
        }

        if (declaration.Layout == StructLayoutKind.Auto && fields.TrueForAll(f => f.Field.BitWidth == FieldDecl.NoBits))
        {
            StableSortByDescendingAlignment(fields);
        }

        int pack = declaration.Layout == StructLayoutKind.Packed ? declaration.Pack : int.MaxValue;
        TypeSizes sequential = target.BitFields == BitFieldRules.Microsoft
            ? LayoutMicrosoft(fields, pack, offsets)
            : LayoutSysV(fields, pack, offsets);
        return Finish(declaration, sequential);
    }

    private static void StableSortByDescendingAlignment(List<(int Index, FieldDecl Field, TypeSizes Sizes)> fields)
    {
        for (int i = 1; i < fields.Count; i++)
        {
            (int Index, FieldDecl Field, TypeSizes Sizes) current = fields[i];
            int j = i - 1;
            while (j >= 0 && fields[j].Sizes.Alignment < current.Sizes.Alignment)
            {
                fields[j + 1] = fields[j];
                j--;
            }

            fields[j + 1] = current;
        }
    }

    private static TypeSizes Finish(StructDecl declaration, TypeSizes raw)
    {
        int alignment = Math.Max(raw.Alignment, 1);
        int size = Math.Max(raw.Size, declaration.MinSize);
        return new TypeSizes(AlignUp(size, alignment), alignment);
    }

    private static TypeSizes LayoutExplicit(List<(int Index, FieldDecl Field, TypeSizes Sizes)> fields, int[] offsets)
    {
        int end = 0;
        int alignment = 1;
        foreach ((int index, FieldDecl field, TypeSizes sizes) in fields)
        {
            offsets[index] = field.Offset;
            end = Math.Max(end, field.Offset + sizes.Size);
            alignment = Math.Max(alignment, sizes.Alignment);
        }

        return new TypeSizes(end, alignment);
    }

    private static TypeSizes LayoutSysV(List<(int Index, FieldDecl Field, TypeSizes Sizes)> fields, int pack, int[] offsets)
    {
        long bit = 0;
        int alignment = 1;
        foreach ((int index, FieldDecl field, TypeSizes sizes) in fields)
        {
            int fieldAlignment = Math.Min(sizes.Alignment, pack);
            alignment = Math.Max(alignment, fieldAlignment);
            if (field.BitWidth == FieldDecl.NoBits)
            {
                int offset = AlignUp((int)((bit + BitsPerByte - 1) / BitsPerByte), fieldAlignment);
                offsets[index] = offset;
                bit = (long)(offset + sizes.Size) * BitsPerByte;
                continue;
            }

            long unit = (long)fieldAlignment * BitsPerByte;
            long storage = (long)sizes.Size * BitsPerByte;
            bool packed = fieldAlignment < sizes.Alignment;
            if (!packed && bit % unit + field.BitWidth > storage)
            {
                bit = (bit + unit - 1) / unit * unit;
            }

            offsets[index] = (int)bit;
            bit += field.BitWidth;
        }

        return new TypeSizes((int)((bit + BitsPerByte - 1) / BitsPerByte), alignment);
    }

    private static TypeSizes LayoutMicrosoft(List<(int Index, FieldDecl Field, TypeSizes Sizes)> fields, int pack, int[] offsets)
    {
        int end = 0;
        int alignment = 1;
        bool inUnit = false;
        int unitOffset = 0;
        int unitSize = 0;
        int bitsUsed = 0;
        foreach ((int index, FieldDecl field, TypeSizes sizes) in fields)
        {
            int fieldAlignment = Math.Min(sizes.Alignment, pack);
            alignment = Math.Max(alignment, fieldAlignment);
            if (field.BitWidth == FieldDecl.NoBits)
            {
                if (inUnit)
                {
                    end = unitOffset + unitSize;
                    inUnit = false;
                }

                int offset = AlignUp(end, fieldAlignment);
                offsets[index] = offset;
                end = offset + sizes.Size;
                continue;
            }

            int storage = sizes.Size * BitsPerByte;
            if (inUnit && unitSize == sizes.Size && bitsUsed + field.BitWidth <= storage)
            {
                offsets[index] = unitOffset * BitsPerByte + bitsUsed;
                bitsUsed += field.BitWidth;
                continue;
            }

            if (inUnit)
            {
                end = unitOffset + unitSize;
            }

            unitOffset = AlignUp(end, fieldAlignment);
            unitSize = sizes.Size;
            bitsUsed = field.BitWidth;
            inUnit = true;
            offsets[index] = unitOffset * BitsPerByte;
        }

        if (inUnit)
        {
            end = unitOffset + unitSize;
        }

        return new TypeSizes(end, alignment);
    }
}
