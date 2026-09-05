# Build context is the REPOSITORY ROOT, not this folder:
#   docker build -f WebApi/Dockerfile -t carepath-api:local .
# Both base images are multi-arch manifests, so building on the Raspberry Pi
# resolves the arm64 variants automatically — no --platform flag required.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore inputs first so this layer is cached independently of source edits.
# Central Package Management means Directory.Packages.props is required for
# restore to resolve any version at all, and Directory.Build.props supplies
# the target framework.
COPY Directory.Build.props Directory.Packages.props ./
COPY Domain/Domain.csproj Domain/
COPY CarePath.Contracts/CarePath.Contracts.csproj CarePath.Contracts/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Infrastructure.Migrations.PostgreSql/Infrastructure.Migrations.PostgreSql.csproj Infrastructure.Migrations.PostgreSql/
COPY WebApi/WebApi.csproj WebApi/
RUN dotnet restore WebApi/WebApi.csproj

# Only these six projects reach the API. Test and client projects are excluded
# here and in .dockerignore so they neither slow the build nor enter the image.
COPY Domain/ Domain/
COPY CarePath.Contracts/ CarePath.Contracts/
COPY Application/ Application/
COPY Infrastructure/ Infrastructure/
COPY Infrastructure.Migrations.PostgreSql/ Infrastructure.Migrations.PostgreSql/
COPY WebApi/ WebApi/
RUN dotnet publish WebApi/WebApi.csproj -c Release --no-restore -o /app/publish

# ---------- runtime ----------
# 'chiseled' is the smallest supported production image: no shell, no package
# manager, and it already runs as the non-root 'app' user. The '-extra' variant
# adds ICU and tzdata, which .NET needs unless the app opts into
# InvariantGlobalization — avoided here so no project file has to change.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app/publish .

# Kestrel listens on 8080 in .NET container images: a non-privileged port, so
# the container never needs root. Map it to a host port with `docker run -p`.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# No secrets and no ASPNETCORE_ENVIRONMENT are baked in: both arrive at run
# time via --env-file so the same image is usable in any environment.
ENTRYPOINT ["dotnet", "CarePath.WebApi.dll"]
