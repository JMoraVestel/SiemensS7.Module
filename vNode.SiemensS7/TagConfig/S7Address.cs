using S7.Net.Types;
using System;
using System.Text.RegularExpressions;

namespace vNode.SiemensS7.TagConfig
{
    /// <summary>
    /// Parsea y representa una dirección de memoria de un PLC Siemens S7.
    /// </summary>
    public class S7Address
    {
        /// <summary>
        /// Nombre del bloque de datos. Ejemplo: "DB1".
        /// </summary>
        public string DbName { get; private set; }

        /// <summary>
        /// Define las áreas de memoria disponibles en un PLC Siemens S7,
        /// según los códigos de tipo utilizados por el protocolo S7.
        /// </summary>
        public enum DataType
        {
            Input = 129,
            Output = 130,
            Memory = 131,
            DataBlock = 132,
            Counter = 28,
            Timer = 29
        }

        /// <summary>
        /// Tipo de dato asociado a la dirección.
        /// </summary>
        public string DataTypeValue { get; private set; }

        /// <summary>
        /// Desplazamiento (offset) dentro del bloque de datos.
        /// </summary>
        public int Offset { get; private set; }

        private S7Address() { }

        /// <summary>
        /// Parsea una cadena de dirección de un tag Siemens.
        /// Ejemplos: "DB1.DBW20", "DB10.DBX0.1".
        /// </summary>
        /// <param name="fullAddress">La dirección completa a parsear.</param>
        /// <returns>Una instancia de S7Address con la dirección descompuesta.</returns>
        /// <exception cref="ArgumentException">Si el formato de la dirección es inválido.</exception>
        public static S7Address Parse(string fullAddress)
        {
            if (string.IsNullOrWhiteSpace(fullAddress))
            {
                throw new ArgumentException("La dirección no puede ser nula o vacía.", nameof(fullAddress));
            }

            // Expresión regular para capturar las partes de la dirección.
            // Grupo 1: DbName (ej. DB1)
            // Grupo 2: DataType (ej. DBW, DBX)
            // Grupo 3: Offset (ej. 20, 0)
            // Grupo 4: Opcional, bit (ej. .1)
            var match = Regex.Match(fullAddress.Trim(), @"^(DB\d+)\.(DB[XWD])(\d+)(\.\d+)?$", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new ArgumentException($"El formato de la dirección '{fullAddress}' no es válido. Se esperaba un formato como 'DB1.DBW20'.");
            }

            var s7Addr = new S7Address
            {
                DbName = match.Groups[1].Value.ToUpper(),
                DataTypeValue = match.Groups[2].Value.ToUpper(),
                Offset = int.Parse(match.Groups[3].Value)
            };

            // Si hay un bit, se añade al tipo de dato para mantener la información.
            if (match.Groups[4].Success)
            {
                s7Addr.DataTypeValue += match.Groups[4].Value;
            }

            return s7Addr;
        }
    }
}
