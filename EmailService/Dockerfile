# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 🔥 КРИТИЧНО: Копируем Directory.Packages.props ПЕРВЫМ
COPY ["Directory.Packages.props", "./"]

# Копируем файлы проектов
COPY ["EmailService/EmailService.csproj", "EmailService/"]
COPY ["Shared/Shared.csproj", "Shared/"]

RUN dotnet restore "EmailService/EmailService.csproj"

COPY . .
WORKDIR "/src/EmailService"
RUN dotnet build "EmailService.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "EmailService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EmailService.dll"]