using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;
using System.Runtime.Serialization;

public class VersionIgnoringSerializationBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        // Игнорируем версию: загружаем assembly по имени и ищем тип
        var assmName = assemblyName.Split(',')[0].Trim(); // Только имя assembly
        var assm = Assembly.Load(assmName);
        return assm.GetType(typeName) ?? typeof(string); // Fallback на string, если не найден
    }

    public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        // Сохраняем без версии
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }
}