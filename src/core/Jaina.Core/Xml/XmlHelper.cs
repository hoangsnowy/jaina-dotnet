using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Jaina.Core.Xml;

public static class XmlHelper
{
    public static string Serialize<T>(T obj, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var serializer = new XmlSerializer(typeof(T));
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, encoding);
        serializer.Serialize(writer, obj);
        return encoding.GetString(stream.ToArray());
    }

    public static T? Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T?)serializer.Deserialize(reader);
    }

    public static (bool IsValid, IReadOnlyList<string> Errors) ValidateXml(string xml, string xsd)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings();

        using var xsdReader = new StringReader(xsd);
        settings.Schemas.Add(null, XmlReader.Create(xsdReader));
        settings.ValidationType = ValidationType.Schema;
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        using var xmlReader = XmlReader.Create(new StringReader(xml), settings);
        while (xmlReader.Read()) { }

        return (errors.Count == 0, errors);
    }
}
