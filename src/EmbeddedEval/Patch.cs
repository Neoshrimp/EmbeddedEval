using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

namespace TrueEval
{

    public class Patch
    {
        static private readonly Harmony harmony = new Harmony("deeznuts");

        public Patch()
        {
            harmony.PatchAll();
        }

        // eval is just used to locate injection points
        // argument is an expression or statement to be evaluated
        public static void Eval(string code) {}

        static MethodInfo evalSignature = AccessTools.Method(typeof(Patch), nameof(Patch.Eval));

        // harmony patches could be dynamically generate depending on eval usage
        [HarmonyPatch(typeof(Target), nameof(Target.SomeMethod))]
        // harmony (not harmonyX) outputs debug to desktop by default
        [HarmonyDebug]
        class Target_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeInstruction prevCi = null;
                foreach (var ci in instructions)
                {
                    if (ci.Is(OpCodes.Call, evalSignature))
                    {
                        Console.WriteLine("injected");
                        // this call consumes Eval code argument
                        yield return new CodeInstruction(OpCodes.Pop);

                        var newIL = CodeToIL((string)prevCi.operand, generator);

                        foreach (var newCi in newIL)
                        {
                            yield return newCi;
                        }
                    }
                    else
                    {
                        yield return ci;
                    }
                    prevCi = ci;
                    
                }
            }

        }

        static int assemblyCount = 0;

        // using this approach each eval still need to be legal c# code
        public static IEnumerable<CodeInstruction> CodeToIL(string code, ILGenerator generator)
        {

            
            string source = @$" 
using System;
public class DummyClass 
{{ 
    unsafe public static void Dummy() 
    {{ 
        {code}
    }} 
}} 
";

            var tree = CSharpSyntaxTree.ParseText(source);
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

            var consoleLib = MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location);
            var runtimeLib = MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"));

            var compilation = CSharpCompilation.Create($"eval{assemblyCount}",
                syntaxTrees: new[] { tree }, references: new[] { mscorlib, consoleLib , runtimeLib},
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                optimizationLevel: OptimizationLevel.Debug              
                )
                );


            var ms = new MemoryStream();


            var emitResult = compilation.Emit(ms);
            Console.WriteLine($"Compilation successful? {emitResult.Success}");


            var context = AssemblyLoadContext.Default;
            ms.Position = 0;
            var compiledAssembly = context.LoadFromStream(ms);

            var type = compiledAssembly.GetType("DummyClass");
            var method = type.GetMethod("Dummy");



            var filteredIL = PatchProcessor.GetOriginalInstructions(method, generator)
                    .Where(ci => ci.opcode != OpCodes.Ret);

            Console.WriteLine(compilation.AssemblyName);
            filteredIL.Do(il => Console.WriteLine(il));

            assemblyCount++;
            

            return filteredIL;

            
        }



    }
}
