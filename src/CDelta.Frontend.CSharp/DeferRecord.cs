using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace CDelta.Frontend.CSharp;

internal sealed record DeferRecord(ControlFlowGraph Cfg, SyntaxNode Scope, int BlockOrdinal, ControlFlowGraph Body);
