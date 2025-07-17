using System;
using vNode.SiemensS7.TagConfig;
using Xunit;

namespace vNode.SiemensS7.Tests.TagConfig
{
    public class S7AddressTests
    {
        /// <summary>
        /// Verifica que el método Parse de S7Address interpreta correctamente direcciones válidas.
        /// Comprueba que los valores de DbName, DataTypeValue y Offset sean los esperados para cada caso.
        /// </summary>
        [Theory]
        [InlineData("DB1.DBW20", "DB1", "DBW", 20)]
        [InlineData("DB10.DBX0.1", "DB10", "DBX.1", 0)]
        [InlineData("DB2.DBW100", "DB2", "DBW", 100)]
        [InlineData("DB3.DBX5", "DB3", "DBX", 5)]
        public void Parse_ValidAddress_ReturnsExpectedValues(string address, string expectedDbName, string expectedDataType, int expectedOffset)
        {
            var result = S7Address.Parse(address);

            Assert.Equal(expectedDbName, result.DbName);
            Assert.StartsWith(expectedDataType, result.DataTypeValue);
            Assert.Equal(expectedOffset, result.Offset);
        }

        /// <summary>
        /// Verifica que el método Parse de S7Address lanza una excepción ArgumentException
        /// cuando se le pasa una dirección inválida o nula.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("INVALID")]
        [InlineData("DB.DBW")]
        [InlineData("DB1.DBW")]
        [InlineData("DB1.DBWXYZ")]
        [InlineData("DB1.DBW-1")]
        public void Parse_InvalidAddress_ThrowsArgumentException(string address)
        {
            Assert.Throws<ArgumentException>(() => S7Address.Parse(address));
        }

        /// <summary>
        /// Verifica que el método Parse de S7Address interpreta correctamente direcciones con bit,
        /// añadiendo el bit al DataTypeValue y extrayendo el DbName y Offset correctamente.
        /// </summary>
        [Fact]
        public void Parse_AddressWithBit_AppendsBitToDataType()
        {
            var result = S7Address.Parse("DB1.DBX0.7");
            Assert.Equal("DB1", result.DbName);
            Assert.Equal("DBX.7", result.DataTypeValue);
            Assert.Equal(0, result.Offset);
        }
    }
}   