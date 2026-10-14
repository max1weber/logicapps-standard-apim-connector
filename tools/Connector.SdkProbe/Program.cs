using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Workflows.ServiceProviders.Abstractions;
var extra = new[] { typeof(ServiceOperationApi).GetProperty("Properties")!.PropertyType, typeof(ServiceOperation).GetProperty("Properties")!.PropertyType, typeof(ServiceProviderAuthenticationToken), typeof(ServiceOperationContext), typeof(ServiceOperationApiProperties), typeof(ConnectionParameters), typeof(Microsoft.Azure.Workflows.ServiceProviders.Abstractions.Entities.DynamicStateCustomDynamicList), typeof(Microsoft.Azure.Workflows.ServiceProviders.Abstractions.Entities.DynamicStateCustomDynamicProperties) };
var selected = new[] { typeof(IServiceOperationsProvider), typeof(ServiceOperationManifest), typeof(ServiceOperationRequest), typeof(ServiceOperationResponse), typeof(ServiceOperationApi), typeof(ServiceOperation), typeof(ServiceOperationManifest).GetProperty("Inputs")!.PropertyType }.Concat(extra).ToArray();
foreach (var type in selected)
{
    Console.WriteLine("TYPE " + type.FullName);
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName)) Console.WriteLine(" METHOD " + method + " ABSTRACT=" + method.IsAbstract);
    foreach (var ctor in type.GetConstructors()) Console.WriteLine(" CTOR " + ctor + " PARAMS=" + string.Join(",", ctor.GetParameters().Select(p => p.Name + "=" + p.DefaultValue)));
    foreach (var property in type.GetProperties()) Console.WriteLine(" PROPERTY " + property.PropertyType + " " + property.Name + " JSON=" + string.Join(",", property.CustomAttributes.Select(a => a.ToString())));
}
var engine = Directory.GetFiles(AppContext.BaseDirectory, "Microsoft.Azure.Workflows*.dll").Select(Assembly.LoadFrom).Select(a => a.GetType("Microsoft.Azure.Workflows.Data.Engines.BuiltInOperationDynamicRequestEngine")).First(t => t is not null)!;
foreach (var assembly in Directory.GetFiles(AppContext.BaseDirectory, "Microsoft.Azure.Workflows*.dll").Select(Assembly.LoadFrom))
{
    Type[] types; try { types = assembly.GetExportedTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).Cast<Type>().ToArray(); }
    foreach (var type in types.Where(t => t.FullName!.Contains("Controllers") || t.Name.Contains("ServiceProvider")))
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name.Contains("DynamicInvoke")))
    {
        Console.WriteLine("DISPATCH OWNER " + type.FullName + " " + method);
        Console.WriteLine("ATTR " + string.Join(",", method.CustomAttributes.Select(a => a.ToString())));
        var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        var body = machine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? method;
        if (body.GetMethodBody() is not null) Dump(body);
    }
}
var m = engine.GetMethod("InvokeDynamicRequest")!;
var state = m.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
Dump(state?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? m);
static void Dump(MethodInfo method)
{
    Console.WriteLine("DYNAMIC DISPATCH " + method);
    var ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
    var il = method.GetMethodBody()!.GetILAsByteArray()!;
    for (var i = 0; i < il.Length;)
    {
        ushort key = il[i++]; if (key == 0xfe) key = (ushort)(0xfe00 | il[i++]);
        var op = ops[key];
        var size = op.OperandType switch { OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1, OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8, OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il,i)*4, _ => 4 };
        if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType) { try { Console.WriteLine(" CALL " + method.Module.ResolveMember(BitConverter.ToInt32(il,i))); } catch { } }
        if (op.OperandType == OperandType.InlineString) Console.WriteLine(" STRING " + method.Module.ResolveString(BitConverter.ToInt32(il,i)));
        i += size;
    }
}
