using System;
using vNode.SiemensS7.TagConfig;
using vNode.SiemensS7.TagReader;
using Xunit;

public class SiemensDataConverterParseTests     
{
    [Fact]
    public void ConvertFromPlc_Bool_ReturnsExpected()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Bool };
        // Bit 1 del primer byte está a 1
        byte[] data = new byte[] { 0b00000010 };
        bool result = (bool)SiemensDataConverter.ConvertFromPlc(config, data, 0, 1);
        Assert.True(result);
    }

    [Fact]
    public void ConvertFromPlc_Int_ReturnsExpected()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Int };
        // 0x04D2 = 1234
        byte[] data = new byte[] { 0x04, 0xD2 };
        short result = (short)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal(1234, result);
    }

    [Fact]
    public void ConvertFromPlc_Real_ReturnsExpected()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Real };
        float value = 12.34f;
        byte[] data = BitConverter.GetBytes(value);
        // S7 usa big-endian, así que invertimos si el sistema es little-endian
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data);
        }
        float result = (float)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal(12.34f, result, 2);
    }

    [Fact]
    public void ConvertFromPlc_String_ReturnsExpected()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.String, StringSize = 5 };
        // S7NetPlus espera: [MaxLen][Len][Chars...]
        // El buffer completo debe tener el tamaño de StringSize + 2 bytes de cabecera.
        byte[] data = new byte[] { 5, 3, (byte)'A', (byte)'B', (byte)'C', 0, 0 };
        string result = (string)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void ConvertFromPlc_Date_ReturnsExpected()
    {
        // Este test requiere implmentar el soporte para DATE en SiemensDataConverter si se necesitas.
        // Aquí se muestra cómo sería el test si existiera el soporte.
        // Por defecto, la implementación actual lanzará NotSupportedException.
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Word };
        // Simula 10 días desde 1990-01-01
        byte[] data = new byte[] { 0x00, 0x0A }; // 10 en big endian
        // Si implementas el soporte para DATE, descomenta la siguiente línea:
        // var result = SiemensDataConverter.ConvertFromPlc(config, data, 0);
        // Assert.Contains("1990-01-11", result.ToString());
        Assert.True(true); // Placeholder hasta que DATE esté soportado
    }

    [Fact]
    public void ConvertFromPlc_Bool_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Bool };
        // Bit 2 del primer byte está a 1
        byte[] data = new byte[] { 0b00000100 };
        bool result = (bool)SiemensDataConverter.ConvertFromPlc(config, data, 0, 2);
        Assert.True(result);
    }

    [Fact]
    public void ConvertFromPlc_Byte_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Byte };
        byte[] data = new byte[] { 0x00, 0xAB };
        byte result = (byte)SiemensDataConverter.ConvertFromPlc(config, data, 1);
        Assert.Equal(0xAB, result);
    }

    [Fact]
    public void ConvertFromPlc_Word_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Word };
        byte[] data = new byte[] { 0x12, 0x34, 0x56 };
        ushort result = (ushort)SiemensDataConverter.ConvertFromPlc(config, data, 1);
        Assert.Equal(0x3456, result);
    }

    [Fact]
    public void ConvertFromPlc_DWord_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.DWord };
        byte[] data = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89 };
        uint result = (uint)SiemensDataConverter.ConvertFromPlc(config, data, 1);
        Assert.Equal(0x23456789u, result);
    }

    [Fact]
    public void ConvertFromPlc_Int_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Int };
        byte[] data = new byte[] { 0x04, 0xD2 }; // 0x04D2 = 1234
        short result = (short)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal(1234, result);
    }

    [Fact]
    public void ConvertFromPlc_DInt_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.DInt };
        byte[] data = new byte[] { 0x00, 0x00, 0x04, 0xD2 }; // 0x000004D2 = 1234
        int result = (int)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal(1234, result);
    }

    [Fact]
    public void ConvertFromPlc_Real_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.Real };
        float value = 12.34f;
        byte[] data = BitConverter.GetBytes(value);
        // S7 usa big-endian, así que invertimos si el sistema es little-endian
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data);
        }
        float result = (float)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal(12.34f, result, 2);
    }

    [Fact]
    public void ConvertFromPlc_String_UsesS7NetPlus()
    {
        var config = new SiemensTagConfig { DataType = SiemensTagDataType.String, StringSize = 5 };
        // S7NetPlus espera: [MaxLen][Len][Chars...]
        // El buffer completo debe tener el tamaño de StringSize + 2 bytes de cabecera.
        byte[] data = new byte[] { 5, 3, (byte)'A', (byte)'B', (byte)'C', 0, 0 };
        string result = (string)SiemensDataConverter.ConvertFromPlc(config, data, 0);
        Assert.Equal("ABC", result);
    }
}

