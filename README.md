# Módulo de comunicaciones Siemens S7 para vNode Lite

El repositorio incluye el módulo de comunicaciones para **vNode Lite** capaz de interactuar con **PLC Siemens S7** mediante la biblioteca **S7NetPlus**.  
El código está desarrollado en **.NET 6** y en esta etapa **compila correctamente**, por lo que ya ofrece las funcionalidades básicas del canal.

---

## 📁 Estructura del módulo

La solución se divide en varias carpetas que agrupan las clases principales:

### `ChannelConfig`
Gestiona la configuración del canal y de los dispositivos:

- `SiemensChannelConfig` – lee y valida la configuración general (IP, Rack, Slot, intervalos de lectura y lista de tags).  
- `SiemensDeviceConfig` – representa cada PLC (ID del dispositivo, IP, Rack, Slot y estado habilitado).  
- `DevicesDictionaryConverter` – convierte la sección de dispositivos del JSON a un diccionario.  
- `InvalidChannelConfigException` – se usa para avisar de configuraciones incorrectas.  

### `TagConfig`
Describe cada tag y su dirección dentro del PLC:

- `SiemensTagConfig` – define dirección, tipo de dato, tamaño, `PollRate`, etc.  
- `SiemensTagWrapper` – encapsula la configuración junto al `TagModelBase` del SDK.  
- `S7Address` – parsea y valida las direcciones del estilo `DB1.DBW20`.  

### `TagReader`
Encargado de leer y escribir en el PLC:

- `SiemensTagReader` – realiza lecturas individuales o en lote, convierte valores y controla los resultados.  
- `SiemensDataConverter` – transforma los datos entre los tipos del PLC y .NET.  
- `TagReadResult` y `TagReadResultItem` – representan los resultados de la lectura.  

### `Scheduler`
Planifica las lecturas periódicas según el `PollRate` (Scan Rate):

- `SiemensScheduler` – agrupa los tags por tasa de sondeo y dispara el evento `ReadingDue`.  
- `TagReadBatchItem` – representa cada solicitud programada (dirección, tamaño, tiempo previsto de lectura, etc.).  

### `SiemensCommonLayer`
Abstrae la comunicación TCP con el PLC:

- `SiemensTcpStrategy` – administra la conexión, las lecturas y las escrituras mediante S7NetPlus.  

### `Diagnostics`
Registra estadísticas de lectura y escritura:

- `ChannelDiagnostics`, `DeviceDiagnostics` y `TagDiagnostics` – mantienen contadores de operaciones, fallos y tiempos medios.  
- `DevicePropertyChangedEventArgs` – notifica cambios en las propiedades de un dispositivo.  
- `SiemensControlTag` – describe los tags de control (habilitar, reiniciar, etc.).  

### Control y factoría
- `SiemensControl` – coordina varias instancias de canal y expone los tags de control.  
- `SiemensFactory` – crea canales nuevos y devuelve las configuraciones de esquema y diagnóstico.  

### Otras utilidades
- `PollRateHelper` – extrae el `PollRate` de la configuración de un tag.  
- En `Types/s7_tag_definitions.json` se incluye un ejemplo de definiciones de tags.  

---

## 🚀 Uso básico

El canal principal está implementado en `Siemens.cs`. Al instanciarlo, se pasa la configuración en JSON siguiendo el modelo de `SiemensChannelConfig`.  
Una vez configurado, es posible:

1. Registrar tags con `RegisterTag`.  
2. Iniciar el canal mediante `Start` para que el planificador comience a leer según el `PollRate`.  
3. Escribir valores en el PLC usando `SetTagValue`.  

---

## ✅ Estado actual

Todas las clases mencionadas se encuentran implementadas y el proyecto **compila sin errores**, ofreciendo las funciones básicas de **lectura/escritura** y **diagnóstico**.

---

## 📌 Próximos pasos

El siguiente hito consiste en comprobar el comportamiento del módulo interactuando con los procesos `Node.exe`, `Backend.exe` y el Frontend, asegurando que la comunicación funciona de forma integrada dentro de **vNode**.
