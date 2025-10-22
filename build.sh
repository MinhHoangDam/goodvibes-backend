#!/bin/bash
echo "Building .NET application..."
dotnet restore
dotnet build --configuration Release