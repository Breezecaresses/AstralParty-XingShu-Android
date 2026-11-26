using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

class ModImgLoaderPatcher
{
    static void Main(string[] args)
    {
        try
        {
            Patch(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static void Patch(string[] args)
    {
        if (args.Length < 4)
        {
            throw new ArgumentException("Usage: ModImgLoaderPatcher <inputDll> <hooksDll> <outputDll> <repoRoot>");
        }

        string inputDll = Path.GetFullPath(args[0]);
        string hooksDll = Path.GetFullPath(args[1]);
        string outputDll = Path.GetFullPath(args[2]);
        string repoRoot = Path.GetFullPath(args[3]);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(inputDll));
        resolver.AddSearchDirectory(Path.GetDirectoryName(hooksDll));
        foreach (string dir in new[] { "updated_20261019_verified", "unity", "framework", "thirdparty", "../build/bin/Release" })
        {
            string full = Path.GetFullPath(Path.Combine(repoRoot, "XingShu-Dev", "references", dir));
            if (Directory.Exists(full))
            {
                resolver.AddSearchDirectory(full);
            }
        }

        var readerParams = new ReaderParameters { AssemblyResolver = resolver };
        using (var game = AssemblyDefinition.ReadAssembly(inputDll, readerParams))
        using (var hooks = AssemblyDefinition.ReadAssembly(hooksDll, readerParams))
        {
            var module = game.MainModule;
            var loaderType = module.Types.Single(t => t.FullName == "UI.TextureLoader");
            var loadExternal = loaderType.Methods.Single(m => m.Name == "LoadExternal" && m.HasBody && m.Parameters.Count == 0);
            var onLoadCompleted = loaderType.Methods.Single(m => m.Name == "OnLoadCompleted" && m.Parameters.Count == 1);

            var gLoaderType = module.Types.Single(t => t.FullName == "FairyGUI.GLoader");
            var getUrl = gLoaderType.Methods.Single(m => m.Name == "get_url" && m.Parameters.Count == 0);

            var nTextureType = module.Types.Single(t => t.FullName == "FairyGUI.NTexture");

            var modLoaderType = hooks.MainModule.Types.Single(t => t.FullName == "XingShuRuntime.Utilities.ModImgTextureLoader");
            var tryResolveForLoader = modLoaderType.Methods.FirstOrDefault(m =>
                m.Name == "TryResolveForLoader"
                && m.IsStatic
                && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "FairyGUI.GLoader"
                && m.Parameters[1].ParameterType.FullName == "System.String");
            var tryResolveSingle = modLoaderType.Methods.Single(m =>
                m.Name == "TryResolve" && m.IsStatic && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
            var useLoaderOverload = tryResolveForLoader != null;
            var modLoaderRef = module.ImportReference(modLoaderType);
            var tryResolveRef = new MethodReference(
                useLoaderOverload ? "TryResolveForLoader" : "TryResolve",
                nTextureType,
                modLoaderRef)
            {
                HasThis = false
            };
            if (useLoaderOverload)
            {
                tryResolveRef.Parameters.Add(new ParameterDefinition(gLoaderType));
            }
            tryResolveRef.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

            bool alreadyPatched = loadExternal.Body.Instructions.Any(i =>
                i.Operand is MethodReference mr && (
                    mr.FullName.Contains("ModImgTextureLoader::TryResolveForLoader")
                    || (!useLoaderOverload && mr.FullName.Contains("ModImgTextureLoader::TryResolve"))));

            if (!alreadyPatched)
            {
                loadExternal.Body.SimplifyMacros();
                loadExternal.Body.InitLocals = true;

                var modTexVar = new VariableDefinition(nTextureType);
                loadExternal.Body.Variables.Add(modTexVar);

                var firstOrig = loadExternal.Body.Instructions[0];
                var il = loadExternal.Body.GetILProcessor();

                var prefix = useLoaderOverload
                    ? new[]
                    {
                        il.Create(OpCodes.Ldarg_0),
                        il.Create(OpCodes.Ldarg_0),
                        il.Create(OpCodes.Call, getUrl),
                        il.Create(OpCodes.Call, tryResolveRef),
                        il.Create(OpCodes.Stloc, modTexVar),
                        il.Create(OpCodes.Ldloc, modTexVar),
                        il.Create(OpCodes.Brfalse, firstOrig),
                        il.Create(OpCodes.Ldarg_0),
                        il.Create(OpCodes.Ldloc, modTexVar),
                        il.Create(OpCodes.Call, onLoadCompleted),
                        il.Create(OpCodes.Ret),
                    }
                    : new[]
                    {
                        il.Create(OpCodes.Ldarg_0),
                        il.Create(OpCodes.Call, getUrl),
                        il.Create(OpCodes.Call, tryResolveRef),
                        il.Create(OpCodes.Stloc, modTexVar),
                        il.Create(OpCodes.Ldloc, modTexVar),
                        il.Create(OpCodes.Brfalse, firstOrig),
                        il.Create(OpCodes.Ldarg_0),
                        il.Create(OpCodes.Ldloc, modTexVar),
                        il.Create(OpCodes.Call, onLoadCompleted),
                        il.Create(OpCodes.Ret),
                    };

                for (int idx = 0; idx < prefix.Length; idx++)
                {
                    il.InsertBefore(firstOrig, prefix[idx]);
                }

                loadExternal.Body.OptimizeMacros();
            }

            game.Write(outputDll);
        }

        // Verify written assembly cleanly
        using (var verified = AssemblyDefinition.ReadAssembly(outputDll, readerParams))
        {
            var vt = verified.MainModule.Types.Single(t => t.FullName == "UI.TextureLoader");
            var vm = vt.Methods.Single(m => m.Name == "LoadExternal" && m.HasBody);
            bool hasTryResolve = vm.Body.Instructions.Any(i =>
                i.Operand is MethodReference mr && mr.FullName.Contains("ModImgTextureLoader::TryResolve"));
            bool hasComplete = vm.Body.Instructions.Any(i =>
                i.Operand is MethodReference mr && mr.FullName.Contains("UI.TextureLoader::OnLoadCompleted"));
            if (!hasTryResolve || !hasComplete)
            {
                throw new InvalidOperationException("Verified output DLL is missing TextureLoader modimg hook instructions");
            }
            if (verified.MainModule.GetTypeReferences().Any(tr =>
                (tr.FullName == "FairyGUI.GLoader" || tr.FullName == "FairyGUI.NTexture")
                && string.Equals(tr.Scope.Name, "AstralParty.Runtime", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Verified output DLL introduced external TypeReference to own FairyGUI types");
            }
        }

        Console.WriteLine("PASS: UI.TextureLoader.LoadExternal patched & verified -> ModImgTextureLoader::TryResolveForLoader");
    }
}
