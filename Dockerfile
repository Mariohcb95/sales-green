# Dockerfile único e parametrizado para todos os serviços (multi-stage: SDK para compilar, runtime enxuto para rodar).
#   docker build --build-arg SERVICE_PATH=src/Services/Catalogo/SalesGreen.Catalogo.Api \
#                --build-arg ASSEMBLY=SalesGreen.Catalogo.Api -t salesgreen-catalogo .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE_PATH
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore "$SERVICE_PATH" \
 && dotnet publish "$SERVICE_PATH" -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG ASSEMBLY
ENV ASSEMBLY=${ASSEMBLY} \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
WORKDIR /app
COPY --from=build /app/publish .
# Usuário sem privilégios já existente nas imagens oficiais do .NET (não roda como root).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ASSEMBLY.dll\""]
