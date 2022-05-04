using System;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace BlackGuardDeobfuscator
{
    class Program
    {
        static void Main(string[] args)
        {
            ModuleContext modCtx = ModuleDef.CreateModuleContext();
            ModuleDefMD module = ModuleDefMD.Load(@"C:\Users\Jacob\Desktop\blackguard.exe", modCtx);
            Console.WriteLine(module.Assembly.Modules);
            int unkMethodCount = 0;
            foreach (var type in module.Types)
            {
                foreach (var attr in type.Fields)
                {
                    if(attr.FieldSig.Type.TypeName == "Byte[]")
                    {
                        Console.WriteLine("Name: " + attr.Name);
                        Console.WriteLine("Initial Value: " + BitConverter.ToString( attr.InitialValue));
                    }
                }
            }
        }
    }
}
