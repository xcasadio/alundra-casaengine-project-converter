// R5 discovery: reads the compiled Alundra.Tests.dll (a COPY of bin/Debug) and, for every top-level test type (nested and compiler-generated
// types rolled up into their outermost owner), counts the IL references into the Alundra assembly, per DLL top-level type:
//   call/callvirt/newobj/jmp (method), ldfld/stfld/ldsfld/stsfld/ldflda/ldsflda (field), ldtoken/castclass/isinst/box/unbox/newarr/... (type).
// Output (stdout, TSV): testType \t dllType \t calls \t news \t fields \t types
// Usage: IlScan <Alundra.Tests.dll>
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static class Program
{
    static readonly OperandType[] OneByte = new OperandType[256];
    static readonly OperandType[] TwoByte = new OperandType[256];
    static readonly bool[] OneByteKnown = new bool[256];
    static readonly bool[] TwoByteKnown = new bool[256];

    static void InitOpcodes()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not OpCode oc) continue;
            ushort v = (ushort)oc.Value;
            if (oc.Size == 1) { OneByte[v & 0xFF] = oc.OperandType; OneByteKnown[v & 0xFF] = true; }
            else { TwoByte[v & 0xFF] = oc.OperandType; TwoByteKnown[v & 0xFF] = true; }
        }
    }

    sealed class Counts { public int Calls, News, Fields, Types; }

    static int Main(string[] args)
    {
        InitOpcodes();
        var path = args[0];
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();

        // outermost owner of every TypeDef
        string FullName(TypeDefinition td) => td.Namespace.IsNil ? md.GetString(td.Name) : md.GetString(td.Namespace) + "." + md.GetString(td.Name);
        TypeDefinitionHandle Outer(TypeDefinitionHandle h)
        {
            var td = md.GetTypeDefinition(h);
            while (!td.GetDeclaringType().IsNil)
            {
                h = td.GetDeclaringType();
                td = md.GetTypeDefinition(h);
            }
            return h;
        }

        // DLL top-level type full name from a TypeReference handle (resolves nested through ResolutionScope); null if not the Alundra assembly
        string? DllTypeOfTypeRef(TypeReferenceHandle h)
        {
            var tr = md.GetTypeReference(h);
            var scope = tr.ResolutionScope;
            // climb nested type refs to the outermost
            while (scope.Kind == HandleKind.TypeReference)
            {
                var outer = md.GetTypeReference((TypeReferenceHandle)scope);
                tr = outer;
                scope = tr.ResolutionScope;
            }
            if (scope.Kind != HandleKind.AssemblyReference) return null;
            var an = md.GetAssemblyReference((AssemblyReferenceHandle)scope);
            if (md.GetString(an.Name) != "Alundra") return null;
            string ns = tr.Namespace.IsNil ? "" : md.GetString(tr.Namespace);
            string nm = md.GetString(tr.Name);
            return ns.Length == 0 ? nm : ns + "." + nm;
        }

        string? DllTypeOfTypeSpec(TypeSpecificationHandle h)
        {
            // generic instantiation: first byte(s) of the blob: 0x15 (GENERICINST) then 0x11/0x12 (CLASS/VALUETYPE) then TypeDefOrRefOrSpecEncoded
            var ts = md.GetTypeSpecification(h);
            var br = md.GetBlobReader(ts.Signature);
            var b = br.ReadByte();
            if (b == 0x15)
            {
                br.ReadByte();
                var th = br.ReadTypeHandle();
                if (th.Kind == HandleKind.TypeReference) return DllTypeOfTypeRef((TypeReferenceHandle)th);
            }
            else if (b == 0x1D || b == 0x14) { return null; } // arrays: element type not tracked
            return null;
        }

        string? DllTypeOfEntity(EntityHandle h)
        {
            switch (h.Kind)
            {
                case HandleKind.TypeReference: return DllTypeOfTypeRef((TypeReferenceHandle)h);
                case HandleKind.TypeSpecification: return DllTypeOfTypeSpec((TypeSpecificationHandle)h);
                case HandleKind.MemberReference:
                    return DllTypeOfEntity(md.GetMemberReference((MemberReferenceHandle)h).Parent);
                case HandleKind.MethodSpecification:
                    return DllTypeOfEntity(md.GetMethodSpecification((MethodSpecificationHandle)h).Method);
                default: return null; // TypeDefinition / MethodDefinition / FieldDefinition of the test assembly itself
            }
        }

        var table = new Dictionary<(string test, string dll), Counts>();
        void Add(string test, string dll, char kind)
        {
            if (!table.TryGetValue((test, dll), out var c)) table[(test, dll)] = c = new Counts();
            switch (kind) { case 'c': c.Calls++; break; case 'n': c.News++; break; case 'f': c.Fields++; break; default: c.Types++; break; }
        }

        foreach (var th in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(th);
            var owner = FullName(md.GetTypeDefinition(Outer(th)));
            foreach (var mh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                if (m.RelativeVirtualAddress == 0) continue;
                var body = pe.GetMethodBody(m.RelativeVirtualAddress);
                var il = body.GetILBytes();
                if (il == null) continue;
                int i = 0;
                while (i < il.Length)
                {
                    OperandType ot;
                    byte op = il[i++];
                    bool known;
                    if (op == 0xFE) { byte op2 = il[i++]; ot = TwoByte[op2]; known = TwoByteKnown[op2]; }
                    else { ot = OneByte[op]; known = OneByteKnown[op]; }
                    if (!known) { Console.Error.WriteLine($"unknown opcode at {i} in {md.GetString(m.Name)}"); break; }
                    switch (ot)
                    {
                        case OperandType.InlineNone: break;
                        case OperandType.ShortInlineBrTarget:
                        case OperandType.ShortInlineI:
                        case OperandType.ShortInlineVar: i += 1; break;
                        case OperandType.InlineVar: i += 2; break;
                        case OperandType.InlineI:
                        case OperandType.InlineBrTarget:
                        case OperandType.ShortInlineR:
                        case OperandType.InlineString:
                        case OperandType.InlineSig: i += 4; break;
                        case OperandType.InlineI8:
                        case OperandType.InlineR: i += 8; break;
                        case OperandType.InlineSwitch:
                            {
                                int n = BitConverter.ToInt32(il, i); i += 4 + 4 * n; break;
                            }
                        case OperandType.InlineMethod:
                        case OperandType.InlineField:
                        case OperandType.InlineType:
                        case OperandType.InlineTok:
                            {
                                int tok = BitConverter.ToInt32(il, i); i += 4;
                                var eh = MetadataTokens.EntityHandle(tok);
                                var dll = DllTypeOfEntity(eh);
                                if (dll != null)
                                {
                                    char kind = ot == OperandType.InlineField ? 'f' : (ot == OperandType.InlineMethod ? (op == 0x73 ? 'n' : 'c') : 't');
                                    Add(owner, dll, kind);
                                }
                                break;
                            }
                        default: Console.Error.WriteLine("unhandled operand " + ot); return 2;
                    }
                }
            }
        }

        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        foreach (var kv in table.OrderBy(k => k.Key.test, StringComparer.Ordinal).ThenBy(k => k.Key.dll, StringComparer.Ordinal))
            Console.WriteLine($"{kv.Key.test}\t{kv.Key.dll}\t{kv.Value.Calls}\t{kv.Value.News}\t{kv.Value.Fields}\t{kv.Value.Types}");
        return 0;
    }
}
