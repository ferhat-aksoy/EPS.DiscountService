# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EPS.Discount.Server/EPS.Discount.Server.csproj", "EPS.Discount.Server/"]
COPY ["EPS.Discount.Application/EPS.Discount.Application.csproj", "EPS.Discount.Application/"]
COPY ["EPS.Discount.Core/EPS.Discount.Core.csproj", "EPS.Discount.Core/"]
COPY ["EPS.Discount.Data/EPS.Discount.Data.csproj", "EPS.Discount.Data/"]
COPY ["EPS.Discount.Proto/EPS.Discount.Proto.csproj", "EPS.Discount.Proto/"]
RUN dotnet restore "./EPS.Discount.Server/EPS.Discount.Server.csproj"
COPY . .
WORKDIR "/src/EPS.Discount.Server"
RUN dotnet build "./EPS.Discount.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EPS.Discount.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EPS.Discount.Server.dll"]