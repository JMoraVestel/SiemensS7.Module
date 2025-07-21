using S7.Net;
using System;
using System.Text.RegularExpressions;

namespace vNode.SiemensS7.TagConfig
{
    // Objeto de resultado para los tests y validaciones
    public class S7ParsedAddress
    {
        public string DbName { get; set; }
        public string DataTypeValue { get; set; }
        public int Offset { get; set; }
    }

    public static class S7Address
    {
        // Regex para DB1.DBW20, DB10.DBX0.1, DB2.DBW100, DB3.DBX5
        private static readonly Regex DbRegex = new Regex(
            @"^(DB(?<db>\d+))\.(DB(?<type>[XBWDS])(?<byte>\d+))(?:\.(?<bit>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static (DataType dataType, int db, int startByteAdr, int count, int bitAdr) Parse(string address, SiemensTagConfig config)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("La dirección no puede ser nula o vacía.", nameof(address));

            var match = DbRegex.Match(address.Trim());
            if (!match.Success)
                throw new ArgumentException($"Formato de dirección S7 inválido: {address}");

            int db = int.Parse(match.Groups["db"].Value);
            string type = match.Groups["type"].Value.ToUpper();
            int startByteAdr = int.Parse(match.Groups["byte"].Value);
            int bitAdr = match.Groups["bit"].Success ? int.Parse(match.Groups["bit"].Value) : (config.BitNumber ?? 0);

            DataType dataType = DataType.DataBlock;
            int count = config.GetSize();

            return (dataType, db, startByteAdr, count, bitAdr);
        }
    }
}