using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

internal static class HookHelper
{
    public static MethodInfo MethodOf(Delegate method) => method.Method;
    public static MethodInfo EzGetMethod(Type type, string name, Type[] parameters = null)
    {
        BindingFlags query = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        if (parameters == null) return type.GetMethod(name, query);
        return type.GetMethod(name, query, null, parameters, null);
    }
    public static MethodInfo EzGetMethod<T>(string name, Type[] parameters = null) => EzGetMethod(typeof(T), name, parameters);

    public sealed class DisposableHookCollection
    {
        private readonly List<ILHook> ilHooks = new List<ILHook>();
        private readonly List<Hook> hooks = new List<Hook>();
        public void Clear()
        {
            foreach (Hook hook in hooks)
            {
                hook.Dispose();
            }
            hooks.Clear();

            foreach (ILHook hook in ilHooks)
            {
                hook.Dispose();
            }
            ilHooks.Clear();
        }
        public void ILHook<T>(string methodName, ILContext.Manipulator to, Type[] parameters = null) => ilHooks.Add(new(EzGetMethod<T>(methodName, parameters), to));
        public void Hook<T>(string methodName, Delegate to, Type[] parameters = null) => hooks.Add(new(EzGetMethod<T>(methodName, parameters), to));
    }
}