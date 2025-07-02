namespace vNode.SiemensS7.TagConfig
{
    public class S7Address
    {
        public string DbName { get; set; }   // Ej: DB1
        public string DataType { get; set; } // Ej: DBW
        public int Offset { get; set; }      // Ej: 2

        public static S7Address Parse(string fullAddress)
        {
            // Ejemplo entrada: DB1.DBW2
            var parts = fullAddress.Split(new[] { '.', 'D', 'B', 'W' }, System.StringSplitOptions.RemoveEmptyEntries);
            return new S7Address
            {
                DbName = "DB" + parts[0],
                DataType = "DBW", // Mejorar con regex para DBX, DBD, etc.
                Offset = int.Parse(parts[1])
            };
        }
    }
}
