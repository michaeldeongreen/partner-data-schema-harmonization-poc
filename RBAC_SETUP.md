# Azure RBAC Setup for Cognitive Services OpenAI

This guide will help you set up RBAC authentication for your Azure AI Foundry resource.

## Prerequisites

1. You're logged into Azure CLI (✅ Already done)
2. You have access to the Cognitive Services resource
3. You have permissions to assign roles (or ask an admin to do it)

## Step 1: Identify Your Resource

Your Azure AI endpoint: `https://<your-cognitive-service-name>.cognitiveservices.azure.com/`

Resource name: `<your-cognitive-service-name>`

> **Note**: Replace `<your-cognitive-service-name>` with your actual Azure Cognitive Services resource name

## Step 2: Find Your Resource Details

Run these commands to get the exact resource information:

```bash
# Get subscription ID
az account show --query id -o tsv

# Find your Cognitive Services resource
az cognitiveservices account show --name <your-cognitive-service-name> --resource-group <your-resource-group> --query id -o tsv
```

If you don't know the resource group, find it with:
```bash
az cognitiveservices account list --query "[?name=='<your-cognitive-service-name>'].{Name:name, ResourceGroup:resourceGroup, Location:location}" -o table
```

## Step 3: Assign RBAC Role

Once you have the resource group, run this command:

```bash
# Get your user object ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Assign the Cognitive Services OpenAI User role
az role assignment create \
  --assignee $USER_ID \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.CognitiveServices/accounts/<your-cognitive-service-name>"
```

Replace `<RESOURCE_GROUP>` with your actual resource group name.

## Alternative: Using Azure Portal

1. Go to the Azure Portal
2. Navigate to your Cognitive Services resource `<your-cognitive-service-name>`
3. Click on "Access control (IAM)"
4. Click "+ Add" → "Add role assignment"
5. Select "Cognitive Services OpenAI User" role
6. Select "User, group, or service principal"
7. Search for and select your user account
8. Click "Review + assign"

## Step 4: Verify the Assignment

```bash
# Check your role assignments on the resource
az role assignment list --assignee $(az ad signed-in-user show --query id -o tsv) --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.CognitiveServices/accounts/<your-cognitive-service-name>" -o table
```

## Available Roles for Cognitive Services OpenAI

- **Cognitive Services OpenAI User**: Can use the API to generate completions, embeddings, etc.
- **Cognitive Services OpenAI Contributor**: Can use the API and manage deployments
- **Cognitive Services Contributor**: Full management access

For development purposes, "Cognitive Services OpenAI User" is sufficient.

## Troubleshooting

### Error: "Insufficient privileges"
You don't have permission to assign roles. Contact your Azure administrator.

### Error: "Resource not found"
Double-check the resource group and resource name.

### Error: Still getting 403 after assignment
- Wait a few minutes for the assignment to propagate
- Try refreshing your Azure CLI login: `az login`
- Verify the assignment was successful using the verification command above

## Testing

After setting up RBAC, restart your application and test the connection:

```bash
cd src/SchemaHarmonizer
dotnet run
```

Navigate to http://localhost:5161 and click "Test AI Connection"