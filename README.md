# Módulo de comunicaciones Siemens S7 para vNode Lite

Este repositorio implementa el módulo de comunicaciones para **vNode Lite**, permitiendo la integración y gestión de **PLC Siemens S7** mediante la biblioteca **S7NetPlus** y tecnologías modernas de .NET.  
El código está desarrollado en **.NET 8** y ofrece funcionalidades completas de canal, incluyendo lectura, escritura, diagnóstico y agrupamiento eficiente de tags.

---

## 📁 Arquitectura y Estructura del Módulo

La solución está organizada en varios componentes principales, cada uno en su carpeta correspondiente:

### ChannelConfig
Gestiona la configuración de canales y dispositivos Siemens:

- **SiemensChannelConfig**: Lee y valida la configuración general (IP, Rack, Slot, intervalos de lectura y lista de tags).
- **SiemensDeviceConfig**: Representa cada PLC (ID, IP, Rack, Slot, estado).
- **DevicesDictionaryConverter**: Convierte la sección de dispositivos del JSON a un diccionario.
- **InvalidChannelConfigException**: Notifica configuraciones incorrectas.

### TagConfig
Define y valida cada tag y su dirección dentro del PLC:

- **SiemensTagConfig**: Dirección, tipo de dato, tamaño, `PollRate`, etc.
- **SiemensTagWrapper**: Encapsula la configuración junto al modelo de tag y realiza validaciones.
- **S7Address**: Parseo y validación de direcciones tipo `DB1.DBW20` y obtención de parámetros para lectura/escritura.

### TagReader
Encargado de leer y escribir en el PLC:

- **SiemensTagReader**: Lecturas individuales o en lote, conversión de valores y control de resultados.
- **SiemensDataConverter**: Transformación de datos entre tipos PLC y .NET, tanto para lectura como para escritura.
- **TagReadResult/TagReadResultItem**: Representan los resultados de lectura, incluyendo éxito o error.

### Scheduler
Planifica las lecturas periódicas según el `PollRate`:

- **SiemensScheduler**: Agrupa los tags por tasa de sondeo y tipo de dato, y dispara el evento `ReadingDue`.
- **TagReadBatchItem**: Representa cada solicitud programada (dirección, tamaño, tiempo previsto de lectura).

#### 🟢 **Agrupamiento y envío de tramas**
El envío de tramas se realiza agrupando los tags que están listos para ser leídos en **lotes** (batches) según los siguientes criterios:
- **ScanRate (PollRate)**: Todos los tags con la misma frecuencia de lectura se agrupan juntos.
- **DataType**: Los tags se agrupan por tipo de dato (por ejemplo, todos los `Int` juntos).
- **Tamaño máximo de trama**: Cada lote se limita a un tamaño máximo de **200 bytes**. Si al añadir un tag el lote supera este límite y ya contiene al menos un tag, se inicia un nuevo lote.

Este mecanismo garantiza que las lecturas sean eficientes, agrupando los datos de forma óptima para minimizar el número de operaciones y evitar sobrecargar el PLC.

### SiemensCommonLayer
Abstrae la comunicación TCP con el PLC:

- **SiemensTcpStrategy**: Administra la conexión, lecturas y escrituras mediante S7NetPlus.

### Diagnostics
Registra estadísticas de lectura y escritura:

- **ChannelDiagnostics, DeviceDiagnostics, TagDiagnostics**: Contadores de operaciones, fallos y tiempos medios.
- **DevicePropertyChangedEventArgs**: Notifica cambios en propiedades de dispositivos.
- **SiemensControlTag**: Tags de control (habilitar, reiniciar, etc.).

### Control y factoría
- **SiemensControl**: Coordina instancias de canal y expone los tags de control.
- **SiemensFactory**: Crea canales nuevos y devuelve configuraciones de esquema y diagnóstico.

### Otras utilidades
- **PollRateHelper**: Extrae el `PollRate` de la configuración de un tag.
- **Types/s7_tag_definitions.json**: Ejemplo de definiciones de tags.

---

## 🚀 Flujo de trabajo y funcionamiento

1. **Carga y validación de configuración**  
   - El canal y los tags se configuran mediante JSON y se deserializan en objetos de configuración.
   - Se valida la configuración de cada tag (tipo, dirección, rango de bit, etc.) usando `SiemensTagWrapper` y `S7Address`.

2. **Lectura de tags**  
   - Se agrupan los tags por `PollRate` y tipo de dato.
   - Se leen en lotes de hasta 200 bytes usando `SiemensTagReader`.
   - Los datos crudos (`byte[]`) se convierten a tipos .NET mediante `SiemensDataConverter`.

3. **Escritura de tags**  
   - Los valores .NET se convierten al formato PLC antes de escribir.
   - Se soporta escritura individual y por lotes, respetando el tamaño máximo de trama.

4. **Conversión de datos**  
   - `SiemensDataConverter` transforma los datos entre los tipos PLC y .NET, manejando correctamente booleanos, enteros, reales, strings, etc.

5. **Diagnóstico y logging**  
   - Se registran estadísticas de operaciones, errores y cambios de estado para diagnóstico y trazabilidad.

6. **Serialización y deserialización**  
   - Se utilizan `System.Text.Json` y `Newtonsoft.Json` para la gestión de configuraciones y datos.
   - Existen tests unitarios que validan la correcta deserialización de canales y tags desde JSON.

---

## 🛠️ Librerías y tecnologías utilizadas

- **.NET 8**: Plataforma principal.
- **S7NetPlus**: Comunicación con PLC Siemens S7.
- **Newtonsoft.Json** y **System.Text.Json**: Serialización/deserialización de configuraciones y datos.
- **Moq**: Mocking para pruebas unitarias.
- **xUnit**: Framework de testing.
- **S7.Net.Types**: Utilidades para direcciones y tipos Siemens S7.

---

## ✅ Estado actual

Todas las clases principales están implementadas y el proyecto **compila sin errores** en .NET 8, ofreciendo funciones completas de **lectura/escritura**, **diagnóstico** y **agrupamiento eficiente de datos**.  
Incluye pruebas unitarias para los componentes clave, incluyendo la conversión de datos y la deserialización de configuraciones.

---

## 📦 Ejemplo de JSON de configuración de canal Siemens S7

El frontend debe enviar un JSON con la configuración del canal y los tags.

### Canal

{
  "nodeName": "ChannelProduction1",
  "ipAddress": "192.168.1.100",
  "cpuType": "S7300",
  "rack": 0,
  "slot": 2,
  "pollingIntervalMs": 1000
}

### Tags

[
  {
    "tagId": "a1b2c3d4-e5f6-7890-1234-56789abcdef0",
    "name": "Started",
    "address": "DB101.DBX0.0",
    "dataType": "Bool",
    "pollRate": 500,
    "bitNumber": 0,
    "stringSize": 0,
    "arraySize": 0,
    "isReadOnly": false,
    "deviceId": "plc1"
  },
  {
    "tagId": "b2c3d4e5-f6a1-8901-2345-6789abcdef01",
    "name": "Pressure",
    "address": "DB101.DBW2",
    "dataType": "Word",
    "pollRate": 1000,
    "bitNumber": 1,
    "stringSize": 0,
    "arraySize": 0,
    "isReadOnly": false,
    "deviceId": "plc1"
  }
]

---

## 📌 Próximos hitos

- **Pruebas de estrés y rendimiento** en entornos reales.
- **Mejoras en la gestión de errores y diagnósticos**.
- **Documentación ampliada y en inglés** con ejemplos de uso avanzado.
- **Soporte para nuevas versiones de PLC Siemens y ampliación de tipos de tags**.

---