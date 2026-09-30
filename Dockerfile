FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/PayableFlow.Api/PayableFlow.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /app/data && chown app /app/data
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/app/data/payableflow.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayableFlow.Api.dll"]
