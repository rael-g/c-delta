using CDelta.Diagnostics;
using CDelta.Ir;

namespace CDelta.Core;

public static class LayoutPass
{
    public static void Run(Module module, Target target) => Run(module, target, new DiagnosticBag());

    public static void Run(Module module, Target target, DiagnosticBag diagnostics)
    {
        var layouts = new TypeLayouts(module, target);
        for (int i = 0; i < module.Structs.Count; i++)
        {
            layouts.OfStruct(new StructId(i));
        }

        for (int i = 0; i < module.Unions.Count; i++)
        {
            layouts.OfUnion(new UnionId(i));
        }

        for (int f = 0; f < module.Functions.Count; f++)
        {
            Body? body = module.Bodies[f];
            if (body is not null)
            {
                CheckAddresses(module, layouts, body, diagnostics);
            }
        }
    }

    private static void CheckAddresses(Module module, TypeLayouts layouts, Body body, DiagnosticBag diagnostics)
    {
        foreach (Block block in body.Blocks)
        {
            foreach (StatementId id in block.Statements)
            {
                Statement statement = body.Statements[id.Index];
                if (statement.Rvalue.IsNone || body.Rvalues[statement.Rvalue.Index].Kind != RvalueKind.Addr)
                {
                    continue;
                }

                if (TakesMisalignedField(module, layouts, body, body.Rvalues[statement.Rvalue.Index].Place))
                {
                    string path = statement.Span.IsNone ? module.Name : module.Sources[statement.Span.File.Index].FullPath;
                    diagnostics.Add(DiagnosticCode.CD2008, new Location(path, statement.Span.Line, statement.Span.Column));
                }
            }
        }
    }

    private static bool TakesMisalignedField(Module module, TypeLayouts layouts, Body body, PlaceId id)
    {
        Place place = body.Places[id.Index];
        TypeId current = place.Base switch
        {
            PlaceBase.Local => body.Locals[place.Id].Type,
            PlaceBase.Global => module.Globals[place.Id].Type,
            _ => IrTyper.Pointee(module, body.Locals[place.Id].Type),
        };
        for (int i = 0; i < place.Projections.Count; i++)
        {
            Projection projection = body.Projections[place.Projections.Start + i];
            if (projection.Kind == ProjectionKind.Field && module.Types[current.Index].Kind == TypeKind.Struct)
            {
                StructDecl declaration = module.Structs[module.Types[current.Index].A];
                FieldDecl field = module.Fields[declaration.Fields.Start + projection.Value];
                if (declaration.Layout == StructLayoutKind.Explicit && field.Offset % layouts.Of(field.Type).Alignment != 0)
                {
                    return true;
                }
            }

            current = IrTyper.Project(module, current, projection);
        }

        return false;
    }
}
