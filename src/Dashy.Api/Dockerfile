FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Dashy.Api/Dashy.Api.csproj", "Dashy.Api/"]
RUN dotnet restore "Dashy.Api/Dashy.Api.csproj"

COPY . .
WORKDIR "/src/Dashy.Api"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Data directory for SQLite volume mount
RUN mkdir -p /data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "Dashy.Api.dll"]
