using System;
using vNode.SiemensS7.TagConfig;
using Xunit;

namespace vNode.SiemensS7.Tests.TagConfig
{
    public class S7AddressTests
    {
        [Theory]
        [InlineData("DB1.DBW20", S7.Net.DataType.DataBlock, 1, 20, 0)]
        [InlineData("DB10.DBX0.1", S7.Net.DataType.DataBlock, 10, 0, 1)]
        [InlineData("DB2.DBW100", S7.Net.DataType.DataBlock, 2, 100, 0)]
        [InlineData("DB3.DBX5", S7.Net.DataType.DataBlock, 3, 5, 0)]
        public void Parse_ValidAddress_ReturnsExpectedValues(string address, S7.Net.DataType expectedDataType, int expectedDb, int expectedStartByte, int expectedBit)
        {
            // Se requiere un SiemensTagConfig para el tipo de dato, aquí se usa Word por defecto
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Word, StringSize = 10 };
            var (dataType, db, startByteAdr, count, bitAdr) = S7Address.Parse(address, config);

            Assert.Equal(expectedDataType, dataType);
            Assert.Equal(expectedDb, db);
            Assert.Equal(expectedStartByte, startByteAdr);
            Assert.Equal(expectedBit, bitAdr);
        }

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
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Word, StringSize = 10 };
            Assert.ThrowsAny<Exception>(() => S7Address.Parse(address, config));
        }

        [Fact]
        public void Parse_AddressWithBit_AppendsBitToDataType()
        {
            var config = new SiemensTagConfig { DataType = SiemensTagDataType.Bool, StringSize = 10 };
            var (dataType, db, startByteAdr, count, bitAdr) = S7Address.Parse("DB1.DBX0.7", config);

            Assert.Equal(S7.Net.DataType.DataBlock, dataType);
            Assert.Equal(1, db);
            Assert.Equal(0, startByteAdr);
            Assert.Equal(7, bitAdr);
        }
    }
}   