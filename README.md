# vNode.SiemensS7

Módulo para **vNode Lite** que permite integrar los PLC Siemens usando la librería [S7NetPlus](https://github.com/S7NetPlus/s7netplus). 

## Requisitos
- .NET 6 SDK.
- IP, rack y slot de la PLC Siemens.

## Primer objetivo: PollRate
Antes de organizar los datos por tipo, necesitamos conocer la tasa de **PollRate** (*Scan Rate*) de cada tag. 
Para ello se siguen las siguientes condicioes:
1. Tasa de Scan Rate.
2. Agrupa los tags por `DataType` sin superar los 200 bytes en cada grupo.

## Estructura principal
- `ChannelConfig/SiemensChannelConfig.cs` define cómo se conectas al PLC y qué tags vas se van leer.
- `SiemensCommonLayer/S7TcpStrategy.cs` implementa la conexión TCP con S7NetPlus.
- `TagReader/S7TagReader.cs` lee los tags y convierte los valores.
- `TagConfig/` agrupa las clases que describen cada tag (`SiemensTagConfig`, `SiemensTagWrapper`, etc.).
- `Types/s7_tag_definitions.json` es un ejemplo de tags que para probar. (mock, no se usuará así)

## Compilación
Desde la raíz del repositorio ejecuta:

```bash
dotnet build vNode.SiemensS7.sln
```

## Ejecución de ejemplo
`Program.cs` carga el archivo `s7_tag_definitions.json` y lee los tags que tengas configurados:

```bash
dotnet run --project vNode.SiemensS7
```

Hay que modificar la IP del PLC en `Program.cs` o en la configuración que use `SiemensFactory.CreateTcpConnection` antes de ejecutar, por ahora está hardcodeada.

## Configuración de Tags
Cada tag se define mediante estas propiedades:

- `DeviceId`: identificador del dispositivo.
- `DbNumber`: número de datablock.
- `StartByte`: byte de inicio dentro del DB.
- `BitNumber`: (opcional) número de bit para tipos `Bool`.
- `StringSize`: longitud para datos `String`.
- `ArraySize`: tamaño de array cuando aplique.
- `PollRate`: intervalo de lectura en milisegundos.
- `DataType`: tipo de dato (Bool, Int16, Real, etc.).

## Estado actual
El código está en desarrollo y todavía faltan pequeñas tareas de análisis, control de canales y pruebas automatizadas.


