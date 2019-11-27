FROM dreamstream/librdkafka:1.2.1-1-bullseye-slim-dotnetcore3.0.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["Evaluation-Producer/Evaluation-Producer.csproj", "Evaluation-Producer/"]
COPY ["Dream-Stream-Producer/Dream-Stream-Producer.csproj", "Dream-Stream-Producer/"]
RUN dotnet restore -r linux-arm "Evaluation-Producer/Evaluation-Producer.csproj"
COPY . .
WORKDIR "/src/Evaluation-Producer"
RUN dotnet build "Evaluation-Producer.csproj" -r linux-arm -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Evaluation-Producer.csproj" -r linux-arm -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY libgrpc_csharp_ext.x86.so libgrpc_csharp_ext.x86.so
ENTRYPOINT ["dotnet", "Evaluation-Producer.dll"]