using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

//Alright, so the Db class has a Get() function. This function returns the singleton Db for the whole game.
//If that singleton hasn't been created, it first it creates it and initializes it.
//Here's the thing:
//
// **** Some classes' static initializers call Db.Get() (sometimes indirectly)! ***
// ...and...
// **** Patching a class with Harmony causes static initializers to be called! ****
//
//Patches are normally applied *far before* the Db is initalized. This means that if we try to patch a class
//normally, and that class (indirectly or not) calls Db.Get(), the Db will be initialized earlier than normal,
//which confuses the crud out of the game.
//
//So we hook this function into Db.Initialize. This allows us to patch classes after the Db is ready.
//
//Just call `Register(typeof(TargetType), typeof(PatchType), "TargetMethod", new Type[] { typeof(Parameter1Type) })` at
//more or less any point. This class will apply your patch when the Db is initialized.
[HarmonyPatch(typeof(Db), "Initialize")]
public static class PostDbPatcher
{
    public static IEnumerable<PatchProcessor> patches { get { return appliedPatches; } }

    //Registers a patch. It will be applied after the game's Db is initialized. If the Db is already initialized, the patch will be applied immediately.
    public static void Register(Type patchClass, Type targetType, string targetMethodName, Type[] targetMethodParameters = null)
    {
        var target = new HarmonyMethod(targetType, targetMethodName, targetMethodParameters);
        //For some reason the constructor doesn't set these???
        //target.declaringType = targetType;
        //target.methodName = targetMethodName;
        //target.argumentTypes = targetMethodParameters;
        var patch = new PatchInfo(patchClass, target);

        if (delayedPatches != null) //Db not ready yet.
            delayedPatches.Add(patch);
        else //Db ready, apply immediately. 
            ApplyPatch(patch);
    }

    //Called by Harmony when patches are normally applied; we just capture the instance here.
    private static bool Prepare(HarmonyInstance harmony)
    {
        PostDbPatcher.harmony = harmony; //capture the harmony instance
        return true;
    }
    //Called after the Db is initialized.
    private static void Postfix()
    {
        if (delayedPatches == null) return; //already called

        Debug.Log(string.Format("PostDbPatcher: Doing {0} late patches...", delayedPatches.Count));
        foreach (var patch in delayedPatches)
            ApplyPatch(patch);
        delayedPatches.Clear();

        delayedPatches = null; //mark us as finished, working in immediate mode now
    }

    //Applies the patch.
    private static void ApplyPatch(PatchInfo patch)
    {
        try
        {
            PatchProcessor proc = new PatchProcessor(harmony, patch.patchClass, patch.target);
            proc.Patch();
            appliedPatches.Add(proc);
        }
        catch (Exception ex)
        {
            Debug.Log(string.Format("PostDbPatcher: Failed to apply patch for {0}, targetting {1}.{2}({3}): {4}", patch.patchClass.FullName, patch.target.declaringType, patch.target.methodName ?? ".ctor", ArgumentsToString(patch.target.argumentTypes), ex.ToString()));
        }
    }
    
    //Just for debugging.
    private static string ArgumentsToString(Type[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
            return "";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < arguments.Length; i++)
        {
            if (i != 0) sb.Append(", ");
            sb.Append(arguments[i].Name);
        }
        return sb.ToString();
    }

    class PatchInfo
    {
        public Type patchClass;
        public HarmonyMethod target;

        public PatchInfo(Type patchClass, HarmonyMethod target)
        {
            this.patchClass = patchClass;
            this.target = target;
        }
    }

    static HarmonyInstance harmony;
    static List<PatchInfo> delayedPatches = new List<PatchInfo>();
    static List<PatchProcessor> appliedPatches = new List<PatchProcessor>();
}
