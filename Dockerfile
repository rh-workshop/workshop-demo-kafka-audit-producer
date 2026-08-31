# Build: SDK .NET 10 (UBI 9) compila y publica el host + la libreria (Paquete NuGet).
# Tag fijado en vez de :latest, para que el build no cambie de version sin avisar. Es la MISMA
# imagen que resuelve la tarea de pruebas del pipeline: si difirieran, el codigo podria pasar las
# pruebas con un SDK y construirse con otro.
FROM registry.access.redhat.com/ubi9/dotnet-100:9.8 AS build
USER 0
WORKDIR /src
# Los csproj se copian primero para que la capa de restore se reaproveche mientras solo cambie
# el codigo: antes cualquier edicion de un .cs invalidaba la descarga entera de NuGet.
# Directory.Build.props aporta el TargetFramework comun y global.json fija el SDK: sin ellos en
# esta capa, el restore fallaria o resolveria con otro SDK que el resto del build.
COPY Directory.Build.props global.json ./
COPY src/RedHat.Workshop.KafkaAudit.Producer/RedHat.Workshop.KafkaAudit.Producer.csproj src/RedHat.Workshop.KafkaAudit.Producer/
COPY src/RedHat.Workshop.KafkaAudit.Host/RedHat.Workshop.KafkaAudit.Host.csproj src/RedHat.Workshop.KafkaAudit.Host/
RUN dotnet restore src/RedHat.Workshop.KafkaAudit.Host
COPY . .
RUN dotnet publish src/RedHat.Workshop.KafkaAudit.Host -c Release --no-restore -o /app

# Runtime: .NET 10 runtime UBI 9 + libs nativas que necesita Confluent.Kafka (librdkafka)
FROM registry.access.redhat.com/ubi9/dotnet-100-runtime:9.8
USER 0
RUN microdnf install -y cyrus-sasl-lib openssl-libs libzstd zlib && microdnf clean all
USER 1001
COPY --from=build /app /app
WORKDIR /app
CMD ["dotnet", "RedHat.Workshop.KafkaAudit.Host.dll"]
