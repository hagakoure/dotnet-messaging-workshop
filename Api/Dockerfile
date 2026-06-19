# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "./"]

# Копируем файлы проектов
COPY ["Api/Api.csproj", "Api/"]
COPY ["Shared/Shared.csproj", "Shared/"]

# Restore теперь найдёт версии в Directory.Packages.props
RUN dotnet restore "Api/Api.csproj"

# Копируем остальной код
COPY . .

WORKDIR "/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]