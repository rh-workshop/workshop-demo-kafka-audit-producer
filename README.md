# workshop-demo-kafka-audit-producer

Cliente de logs de auditoría para aplicaciones .NET. Cifra el evento y lo publica
en Kafka, para que el pipeline lo enmascare y lo entregue al destino final.

## Es una librería, no un servicio

El entregable principal es el **paquete NuGet** `RedHat.Workshop.KafkaAudit.Producer`: lo
añaden como dependencia los microservicios que emiten auditoría, y se ejecuta
dentro de ellos, en su propio namespace. No es un pod de la plataforma.

`RedHat.Workshop.KafkaAudit.Host` es un ejecutable de ejemplo que demuestra la librería y
sirve para validar el flujo de extremo a extremo.

## Qué hace

1. Serializa el evento a **OTLP** (Protobuf, esquema de OpenTelemetry).
2. Lo comprime con GZip: hay que hacerlo **antes** de cifrar, porque el dato
   cifrado tiene entropía alta y ya no comprime.
3. Lo cifra con **AES-256-GCM**, con la llave derivada por HKDF-SHA256 y el
   tópico autenticado como AAD: un mensaje copiado a otro tópico deja de descifrar.
4. Lo publica por mTLS con confirmación (`Acks.All`): en auditoría no se puede
   publicar y olvidarse.

## Construir

```bash
dotnet test
dotnet pack -c Release
```

El paquete apunta a **`net8.0`**, que es lo que fija a qué runtime se compila y por
tanto qué aplicaciones pueden consumirlo. El `global.json` declara `8.0.100` como SDK
**mínimo** con `rollForward: latestMajor`, de modo que un SDK más reciente también
sirve para construirlo: la imagen de CI trae el 10 y con `latestMinor` la compilación
fallaba con «A compatible .NET SDK was not found», porque esa política no cruza de una
versión mayor a otra.

Fijar el SDK exacto obligaría a que la imagen de CI y cada puesto de trabajo tuvieran
esa versión concreta instalada; lo que de verdad hay que fijar es el destino, y eso lo
hace el `TargetFramework`.

## Contrato con el pipeline Java

El formato del payload cifrado, la derivación de llave y los nombres de los
atributos OTLP deben coincidir **byte a byte** con
[`workshop-demo-kafka-audit-pipeline`](https://github.com/rh-workshop/workshop-demo-kafka-audit-pipeline),
que es quien descifra. Ambos lados congelan el mismo vector de interoperabilidad
en sus pruebas: si uno cambia y el otro no, los mensajes acaban en la cola de
descarte.
