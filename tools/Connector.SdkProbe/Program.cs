using System.Reflection;
foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Microsoft.Azure.Workflows*.dll"))
{
    var assembly = Assembly.LoadFrom(file);
    Type[] types;
    try { types = assembly.GetExportedTypes(); }
    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).Cast<Type>().ToArray(); }
    foreach (var type in types.Where(t => t.Name.Contains("ServiceOperations") || t.Name.Contains("Dynamic") || t.Name is "ServiceOperationManifest" or "SwaggerSchema" or "ServiceOperationRequest" or "ServiceOperationResponse"))
    {
        Console.WriteLine("TYPE " + type.FullName);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName)) Console.WriteLine(" METHOD " + method);
        foreach (var property in type.GetProperties()) Console.WriteLine(" PROPERTY " + property.PropertyType + " " + property.Name);
    }
}
