# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (Render uses PORT env variable)
EXPOSE $PORT

# Set environment variables (Render provides PORT)
ENV ASPNETCORE_URLS=http://+:$PORT

# Run the app
ENTRYPOINT ["dotnet", "TippspielWeb.dll"]
