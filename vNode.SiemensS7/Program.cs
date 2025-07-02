using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using vNode.SiemensS7;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;

class Program
{

    //TODO: Agrupar los datos según llegan en las tramas, el máximo es de 200 bytes.
    //Condición de agrupación: 1. ScanRate, 2.DataType <= 200 bytes.

    static void Main()
    {
        //1. Agrupamos datos por ScanRate



        const string resourceName = "vNode.SiemensS7.Types.s7_tag_definitions.json"; // Asegúrate del namespace + carpeta

        string json = ReadEmbeddedJson(resourceName);
        if (json == null)
        {
            Console.WriteLine($"❌ No se pudo cargar el recurso incrustado: {resourceName}");
            return;
        }

        var tags = JsonSerializer.Deserialize<List<S7TagConfig>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tags == null || tags.Count == 0)
        {
            Console.WriteLine("❌ No se encontraron tags válidos en el recurso JSON.");
            return;
        }

        var plc = SiemensFactory.CreateTcpConnection("192.168.3.200"); 
        var reader = new S7TagReader(plc);

        Console.WriteLine("📡 Leyendo tags desde el PLC...\n");

        var results = reader.ReadTags(tags);

        foreach (var kvp in results)
        {
            Console.WriteLine($"{kvp.Key} = {kvp.Value}");
        }

        plc.Disconnect();

        Console.WriteLine("\n✅ Lectura completada.");
    }

    static string ReadEmbeddedJson(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
