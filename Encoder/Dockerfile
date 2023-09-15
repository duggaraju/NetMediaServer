#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y xz-utils
ADD https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz /
RUN tar xf /ffmpeg-master-latest-linux64-gpl.tar.xz -C /usr/local --strip=1

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Encoder/Packager.csproj", "Encoder/"]
RUN dotnet restore "Encoder/Packager.csproj"
COPY . .
WORKDIR "/src/Encoder"
RUN dotnet build "Packager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Packager.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Packager.dll"]