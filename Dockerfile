# Build: SDK .NET 8 (UBI) compila y publica el host + la libreria (Paquete NuGet).
# Tag de rama (8.0) en vez de :latest, para que el build no cambie de major sin avisar.
FROM registry.access.redhat.com/ubi8/dotnet-80:8.0 AS build
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

# Runtime: .NET 8 runtime UBI + libs nativas que necesita Confluent.Kafka (librdkafka)
FROM registry.access.redhat.com/ubi8/dotnet-80-runtime:8.0
USER 0
RUN microdnf install -y cyrus-sasl-lib openssl-libs libzstd zlib && microdnf clean all
USER 1001
COPY --from=build /app /app
WORKDIR /app
CMD ["dotnet", "RedHat.Workshop.KafkaAudit.Host.dll"]
