using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.TagReader.Interfaces
{
    /// <summary>  
    /// Define el contrato para un lector de tags Siemens, permitiendo la lectura y escritura de datos.  
    /// </summary>  
    public interface ISiemensTagReader
    {
        /// <summary>  
        /// Lee un lote de tags y devuelve los resultados en el formato esperado por el SDK.  
        /// </summary>  
        Dictionary<Guid, TagReadResult> ReadManyForSdk(Dictionary<Guid, SiemensTagWrapper> tagsToRead);

        /// <summary>  
        /// Escribe un único tag en el PLC.  
        /// </summary>  
        Task<bool> WriteTagAsync(SiemensTagWrapper tag, object value);

        /// <summary>  
        /// Escribe varios tags en el PLC agrupando por lotes.  
        /// </summary>  
        Task<bool> WriteTagsBatchAsync(IEnumerable<(SiemensTagWrapper Tag, object Value)> tagValuePairs);
    }
}
