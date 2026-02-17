# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Zebl.Api/Zebl.Api.csproj", "Zebl.Api/"]
COPY ["Zebl.Application/Zebl.Application.csproj", "Zebl.Application/"]
COPY ["Zebl.Infrastructure/Zebl.Infrastructure.csproj", "Zebl.Infrastructure/"]
RUN dotnet restore "Zebl.Api/Zebl.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Zebl.Api"
RUN dotnet build "Zebl.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
WORKDIR "/src/Zebl.Api"
RUN dotnet publish "Zebl.Api.csproj" -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Zebl.Api.dll"]
