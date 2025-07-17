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
- **SiemensTagWrapper**: Encapsula la configuración junto al modelo de tag.
- **S7Address**: Parseo y validación de direcciones tipo `DB1.DBW20`.

### TagReader
Encargado de leer y escribir en el PLC:

- **SiemensTagReader**: Lecturas individuales o en lote, conversión de valores y control de resultados.
- **SiemensDataConverter**: Transformación de datos entre tipos PLC y .NET.
- **TagReadResult/TagReadResultItem**: Representan los resultados de lectura.

### Scheduler
Planifica las lecturas periódicas según el `PollRate`:

- **SiemensScheduler**: Agrupa los tags por tasa de sondeo y dispara el evento `ReadingDue`.
- **TagReadBatchItem**: Representa cada solicitud programada (dirección, tamaño, tiempo previsto de lectura).

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

## 🚀 Uso básico

El canal principal está implementado en `Siemens.cs`.  
Para utilizarlo:

1. Configura el canal con un JSON siguiendo el modelo de `SiemensChannelConfig`.
2. Registra los tags con `RegisterTag`.
3. Inicia el canal con `Start` para que el planificador comience a leer según el `PollRate`.
4. Escribe valores en el PLC usando `SetTagValue`.

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

Todas las clases principales están implementadas y el proyecto **compila sin errores** en .NET 8, ofreciendo funciones completas de **lectura/escritura** y **diagnóstico**.  
Incluye pruebas unitarias para los componentes clave.

---

## 📌 Próximos hitos

- **Pruebas de estrés y rendimiento** en entornos reales.
- **Mejoras en la gestión de errores y diagnósticos**.
- **Documentación ampliada y en inglés** con ejemplos de uso avanzado.
- **Soporte para nuevas versiones de PLC Siemens y ampliación de tipos de tags**.