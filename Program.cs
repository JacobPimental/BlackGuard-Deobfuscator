using System;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Collections.Generic;

namespace BlackGuardDeobfuscator
{
    class Program
    {
        static void Main(string[] args)
        {
            ModuleContext modCtx = ModuleDef.CreateModuleContext();
            ModuleDefMD module = ModuleDefMD.Load(@"C:\Users\Jacob\Desktop\blackguard-cleaned.exe", modCtx);
            Console.WriteLine(module.Assembly.Modules);

            var obfFuncs = getObfFuncs(module);
            var b64Funcs = getB64Funcs(module);

            foreach (var type in module.Types)
            {
                if (!type.HasMethods)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (!method.HasBody)
                        continue;
                    foreach (var inst in method.Body.Instructions)
                    {
                        if (inst.OpCode == OpCodes.Call && inst.Operand is MethodDef)
                        {
                            if (obfFuncs.ContainsKey((MethodDef)inst.Operand))
                            {
                                inst.OpCode = OpCodes.Ldstr;
                                inst.Operand = obfFuncs[(MethodDef)inst.Operand];
                            }
                        }
                    }
                    for (var i = 1; i < method.Body.Instructions.Count; i++)
                    {
                        var prevIns = method.Body.Instructions[i - 1];
                        var ins = method.Body.Instructions[i];
                        if (ins.OpCode == OpCodes.Call && ins.Operand is MethodDef)
                        {
                            if (b64Funcs.Contains((MethodDef)ins.Operand))
                            {
                                //Console.WriteLine(prevIns.Operand.ToString());
                                if (prevIns.Operand is string)
                                {
                                    try
                                    {
                                        string b64Dec = Encoding.ASCII.GetString(Convert.FromBase64String(prevIns.Operand.ToString()));
                                        ins.OpCode = OpCodes.Ldstr;
                                        ins.Operand = b64Dec;
                                        prevIns.OpCode = OpCodes.Nop;
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            module.Write(@"C:\Users\jacob\Desktop\blackguard-deob.exe");
        }

        static List<MethodDef> getB64Funcs(ModuleDefMD module)
        {
            List<MethodDef> b64Funcs = new List<MethodDef>();
            foreach (var type in module.Types)
            {
                if (!type.HasMethods)
                    continue;
                foreach(var method in type.Methods)
                {
                    if (!method.HasBody || method.Body.Instructions.Count > 5)
                        continue;
                    foreach(var ins in method.Body.Instructions)
                    {
                        if(ins.OpCode == OpCodes.Call && ins.Operand.ToString().Contains("FromBase64String"))
                        {
                            Console.WriteLine(method.FullName);
                            b64Funcs.Add(method);
                        }
                    }
                }
            }
            return b64Funcs;
        }

        static Dictionary<MethodDef, string> getObfFuncs(ModuleDefMD module)
        {
            foreach (var type in module.Types)
            {
                if (!type.HasMethods)
                    continue;

                byte[] kArr = getKey(type);
                if (kArr == null)
                {
                    continue;
                }
                else
                {
                    Console.WriteLine("Found it!");
                }
                MethodDef decryptMethod = getDecryptMethod(type);
                Console.WriteLine(decryptMethod);
                return decryptFuncs(type, kArr, decryptMethod);
            }
            return null;
        }

        static Instruction containsOpCode(System.Collections.Generic.IList<Instruction> instructions, OpCode opCode)
        {
            foreach(var ins in instructions)
            {
                if(ins.OpCode == opCode)
                {
                    return ins;
                }
            }
            return null;
        }

        static byte[] getKey(TypeDef type)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;

                Instruction newarr = containsOpCode(method.Body.Instructions, OpCodes.Newarr);
                Instruction xor = containsOpCode(method.Body.Instructions, OpCodes.Xor);

                if (newarr != null && xor != null)
                {
                    byte[] bArr = getArrayData(method.Body.Instructions);
                    for (int i = 0; i < bArr.Length; i++)
                    {
                        int d = bArr[i] ^ i ^ 170;
                        bArr[i] = (byte)d;
                    }
                    return bArr;
                }
            }
            return null;
        }

        static byte[] getArrayData(System.Collections.Generic.IList<Instruction> instructions)
        {
            bool foundArr = false;
            foreach(var ins in instructions)
            {
                if(ins.OpCode == OpCodes.Newarr)
                {
                    foundArr = true;
                }

                if(ins.OpCode == OpCodes.Ldtoken && foundArr )
                {
                    FieldDef field = (FieldDef)ins.Operand;
                    Console.WriteLine(field.Name);
                    Console.WriteLine(field.InitialValue.GetType());
                    return field.InitialValue;
                }
            }
            return null;
        }

        static MethodDef getDecryptMethod(TypeDef type)
        {
            foreach(var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;

                foreach(var ins in method.Body.Instructions)
                {
                    if(ins.OpCode == OpCodes.Callvirt)
                    {
                        if (ins.Operand.ToString().Contains("GetString"))
                        {
                            return method;
                        }
                    }
                }
            }
            return null;
        }

        static Dictionary<MethodDef, string> decryptFuncs(TypeDef type, byte[] kArr, MethodDef decryptMethod)
        {
            Dictionary<MethodDef, string> funcs = new Dictionary<MethodDef, string>();
            foreach(var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;

                for (var i = 2; i < method.Body.Instructions.Count; i++)
                {
                    Instruction prevIns = method.Body.Instructions[i - 1];
                    Instruction prevprevIns = method.Body.Instructions[i - 2];
                    Instruction ins = method.Body.Instructions[i];
                    if(ins.OpCode == OpCodes.Call && ins.Operand == decryptMethod)
                    {
                        funcs.Add(method, decryptString(prevprevIns.GetLdcI4Value(), prevIns.GetLdcI4Value(), kArr));
                    }
                }
            }
            return funcs;
        }

        static string decryptString(int x, int y, byte[] kArr)
        {
            return Encoding.UTF8.GetString(kArr, x, y);
        }
    }
}
