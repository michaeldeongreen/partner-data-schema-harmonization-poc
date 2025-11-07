#!/bin/bash

# Script to create folder structure in Data Lake Storage Gen2 containers
# This script should be run after the infrastructure deployment

# Get the storage account name from azd environment
STORAGE_ACCOUNT_NAME=$(azd env get-values | grep STORAGE_ACCOUNT_NAME | cut -d'=' -f2 | tr -d '"')

if [ -z "$STORAGE_ACCOUNT_NAME" ]; then
    echo "Error: STORAGE_ACCOUNT_NAME not found in azd environment"
    exit 1
fi

echo "Creating folder structure in storage account: $STORAGE_ACCOUNT_NAME"

# Function to create a directory in Data Lake Storage Gen2
create_directory() {
    local container=$1
    local path=$2
    
    echo "Creating directory: $container/$path"
    az storage fs directory create \
        --name "$path" \
        --file-system "$container" \
        --account-name "$STORAGE_ACCOUNT_NAME" \
        --auth-mode login
}

# Create bronze container folders
create_directory "bronze" "tenantA"
create_directory "bronze" "tenantA/raw"

# Create silver container folders
create_directory "silver" "tenantA"
create_directory "silver" "tenantA/canonical"

# Create gold container folders
create_directory "gold" "tenantA"
create_directory "gold" "tenantA/enriched"

echo "Folder structure creation completed!"
echo ""
echo "Created structure:"
echo "├── bronze/"
echo "│   └── tenantA/"
echo "│       └── raw/"
echo "├── silver/"
echo "│   └── tenantA/"
echo "│       └── canonical/"
echo "└── gold/"
echo "    └── tenantA/"
echo "        └── enriched/"